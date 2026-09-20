// Test de charge k6 sur l'endpoint critique POST /api/files (US01, upload).
//
// Execution locale (loopback - latence reseau quasi nulle, mesure surtout le temps
// de traitement serveur) :
//   k6 run perf/upload-load-test.js
//   (backend + postgres doivent tourner - dotnet run + docker compose up -d)
//
// Execution depuis une autre machine du reseau local (latence reseau reelle, plus
// representatif) - copier le dossier perf/ (ou cloner le repo) sur l'autre machine,
// installer k6 dessus, puis :
//   BASE_URL=http://<ip-lan-du-poste-backend>:5074 k6 run perf/upload-load-test.js
//
// Via un proxy HTTP (latence supplementaire, plus proche d'un vrai trajet internet) -
// k6 respecte nativement les variables d'environnement standard :
//   HTTP_PROXY=http://user:pass@proxyhost:port BASE_URL=http://<ip-lan>:5074 k6 run perf/upload-load-test.js
// Attention : de nombreux proxys publics bloquent le routage vers des IP privees
// (192.168.x.x) par securite - a verifier aupres du fournisseur du proxy avant de
// s'appuyer sur ce resultat. Le proxy SOCKS5 n'est pas garanti nativement supporte
// par k6 (contrairement au proxy HTTP) - a verifier au cas par cas.
//
// Fichier de test : sample-2mb.pdf (2 Mo, signature PDF valide verifiee manuellement)
// - plus representatif d'un usage reel qu'un fichier de quelques octets, qui terminait
// l'upload trop vite pour solliciter reellement l'ecriture disque/la detection de type.

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter } from 'k6/metrics';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5074';
const fileContent = open('./sample-2mb.pdf', 'b');

const uploadErrors = new Counter('upload_errors');

export const options = {
  stages: [
    { duration: '10s', target: 5 },  // montee en charge : 0 -> 5 utilisateurs virtuels
    { duration: '20s', target: 5 },  // palier : 5 utilisateurs simultanes pendant 20s
    { duration: '5s', target: 0 },   // descente
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'], // 95% des requetes doivent repondre en moins de 500ms
    upload_errors: ['count==0'],       // aucun upload ne doit echouer
  },
};

// setup() s'execute une seule fois, avant la montee en charge - pas repete par utilisateur virtuel.
export function setup() {
  const email = `k6-${Date.now()}@example.com`;
  const password = 'Password1!';

  http.post(
    `${BASE_URL}/api/auth/register`,
    JSON.stringify({ email, password }),
    { headers: { 'Content-Type': 'application/json' } }
  );

  const loginRes = http.post(
    `${BASE_URL}/api/auth/login`,
    JSON.stringify({ email, password }),
    { headers: { 'Content-Type': 'application/json' } }
  );

  return { token: loginRes.json('accessToken') };
}

// default : ce que chaque utilisateur virtuel repete en boucle pendant le test.
export default function (data) {
  const payload = {
    file: http.file(fileContent, 'sample-2mb.pdf', 'application/pdf'),
    expiresInDays: '7',
  };

  const res = http.post(`${BASE_URL}/api/files`, payload, {
    headers: { Authorization: `Bearer ${data.token}` },
  });

  const ok = check(res, {
    'status is 201': (r) => r.status === 201,
  });

  if (!ok) {
    uploadErrors.add(1);
  }

  sleep(1);
}
