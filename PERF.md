# Performance — DataShare

Document vivant, enrichi au fil des étapes du projet (pas rédigé d'un bloc à la fin) — voir `CLAUDE.md`.

## Suivi des métriques

### Temps de réponse — `UseSerilogRequestLogging()`

**Décision** : chaque requête HTTP est chronométrée de bout en bout par le middleware Serilog (`Program.cs`), qui écrit une ligne de log structurée par requête (méthode, route, code de statut, durée en ms), plutôt que de s'appuyer sur le logging par défaut d'ASP.NET Core (plus verbeux, moins facilement exploitable).

**Pourquoi** : ça donne une visibilité continue sur les temps de réponse réels de l'application, en complément du test de charge ciblé fait avec k6 (endpoint précis, charge contrôlée). L'un couvre l'usage réel dans la durée, l'autre un scénario de stress ponctuel — les deux sont nécessaires, aucun ne remplace l'autre.

## Test de charge — `POST /api/files` (upload, US01)

**Outil** : [k6](https://k6.io/) (Grafana Labs), installé en local (`winget install k6`).

**Script** : `perf/upload-load-test.js`. Exécution :
```bash
# Backend + postgres doivent tourner (docker compose up -d, dotnet run)
k6 run perf/upload-load-test.js
```

**Scénario** : montée progressive à 5 utilisateurs virtuels simultanés (10s), palier de 20s, puis descente (5s) — chaque utilisateur uploade un fichier PDF de 2 Mo (`sample-2mb.pdf`, signature valide vérifiée manuellement) en boucle, avec 1s de pause entre deux itérations.

**Limite connue et assumée — test en local (loopback)** : k6 et le backend tournent sur la **même machine**, donc la latence réseau est quasi nulle. Ce test mesure le temps de traitement serveur (validation, écriture disque, écriture base) sous charge concurrente, **pas** un temps de réponse représentatif d'un vrai client distant (qui ajouterait un aller-retour réseau réel). Un premier essai avec un fichier de 14 octets donnait des chiffres artificiellement bas (p95=84ms) — remplacé par un fichier de 2 Mo, plus représentatif d'un usage réel, ce qui a fait apparaître un temps de traitement bien plus honnête (voir résultat ci-dessous).

**Seuils définis** : p95 < 500ms, 0% d'échec.

**Résultat obtenu** (exécuté en local, poste de dev, 2026-09-19, fichier 2 Mo) :

```
✓ 'p(95)<500' p(95)=335.16ms
✓ 'count==0' count=0 (upload_errors)

checks_succeeded...: 100.00% (117/117)
http_req_duration..: avg=227.34ms  min=161.8ms  med=198.86ms  max=688.3ms  p(90)=293.33ms  p(95)=335.16ms
http_req_failed....: 0.00%
iterations.........: 117 (3.1/s)
data_sent..........: 246 MB (6.5 MB/s)
```

**Interprétation** : les deux seuils restent respectés, mais avec beaucoup moins de marge que le premier essai (335ms de p95 contre un seuil à 500ms, au lieu de 84ms) — la taille du fichier a un impact direct et significatif sur le temps de traitement, logique puisque l'écriture disque et la lecture du flux pour la détection de signature sont proportionnelles à la taille. Aucune erreur sur 117 uploads malgré 246 Mo transférés en ~35s.

**Prochaine étape, plus rigoureuse — test réseau réel** : ce script est portable (`BASE_URL`) et prêt à être relancé depuis une autre machine du réseau local, pointée sur l'IP LAN de ce poste (`192.168.1.19` au moment de la rédaction) :
```bash
BASE_URL=http://192.168.1.19:5074 k6 run perf/upload-load-test.js
```
Optionnellement via un proxy HTTP (latence supplémentaire, plus proche d'un vrai trajet internet) :
```bash
HTTP_PROXY=http://user:pass@proxyhost:port BASE_URL=http://192.168.1.19:5074 k6 run perf/upload-load-test.js
```
k6 respecte nativement `HTTP_PROXY`/`HTTPS_PROXY`. **Attention** : beaucoup de proxys publics bloquent le routage vers des IP privées (`192.168.x.x`) par sécurité — à vérifier auprès du fournisseur avant de s'y fier. Le support SOCKS5 n'est pas garanti nativement par k6 (contrairement au HTTP) — à vérifier au cas par cas si cette voie est utilisée. Résultat de ce test à ajouter ici une fois exécuté (nécessite une deuxième machine, non disponible dans cet environnement).

## Budget de performance front

Pas encore mesuré (taille du bundle Angular, temps de premier rendu). À compléter — cf. `ng build` (déjà exécuté au fil du développement, taille du bundle initial ~1,5 Mo, jamais analysée spécifiquement sous l'angle performance).
