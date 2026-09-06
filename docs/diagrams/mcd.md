# MCD — DataShare

4 entités : `USER`, `REFRESH_TOKEN`, `FILE`, `TAG`. Base PostgreSQL.

```mermaid
erDiagram
    USER o|--o{ FILE : uploads
    USER ||--o{ REFRESH_TOKEN : owns
    FILE ||--o{ TAG : has

    USER {
        uuid id PK
        string email UK "unique, format validé"
        string password_hash "BCrypt"
        timestamp created_at
    }

    REFRESH_TOKEN {
        uuid id PK
        uuid user_id FK
        string token_hash
        timestamp expires_at
        boolean revoked "default false"
        timestamp created_at
    }

    FILE {
        uuid id PK
        uuid user_id FK "nullable — un fichier peut exister sans propriétaire (upload anonyme, US07)"
        string original_filename
        string content_type "MIME type détecté serveur (signature/magic bytes), jamais le Content-Type déclaré par le client — affiché avant téléchargement (US02)"
        string storage_path
        bigint size_bytes "max 1 Go (contrôle applicatif)"
        string download_token UK "identifiant non prédictible"
        string password_hash "nullable, min 6 caractères en clair avant hash"
        timestamp expires_at "défaut +7 jours, max +7 jours"
        timestamp created_at
    }

    TAG {
        uuid id PK
        uuid file_id FK
        string label "texte libre, max 30 caractères, pas de doublon par fichier"
    }
```

## Pourquoi `FILE.user_id` est nullable

Un fichier peut exister **sans** propriétaire : c'est l'upload anonyme (US07). Ce n'est pas une fonctionnalité qu'on construit forcément en v1 (elle est classée "avancée/optionnelle" dans la spec), mais le MCD représente le **domaine métier réel**, pas le périmètre de la première livraison. La distinction à garder en tête :

- **MCD (ce fichier)** = ce que les données représentent conceptuellement, selon la spec complète (US01 à US10). Un fichier anonyme existe dans ce domaine, donc `user_id` doit pouvoir être vide.
- **API v1 / code** = ce qu'on construit en premier. Si seul US01 (upload authentifié) est implémenté, le code applicatif n'écrira jamais de fichier avec `user_id` vide — mais ça n'a aucune raison de changer le MCD, qui reste correct et prêt si US07 est ajoutée plus tard sans migration de schéma.

Bref : ne pas confondre "ce qu'on construit maintenant" (scope de livraison) et "ce que les données peuvent légitimement représenter" (modèle conceptuel).

## Pourquoi des UUID plutôt que des ID auto-incrémentés ?

Réponse courte, à pouvoir justifier en soutenance si l'évaluateur challenge ce choix :

- **`FILE.id` et `USER.id` sont exposés via l'API** (`DELETE /api/files/{id}`, historique, réponse d'inscription). Avec un entier auto-incrémenté (1, 2, 3...), n'importe quel utilisateur authentifié pourrait tester `/api/files/1`, `/api/files/2`, etc. et déduire des informations (nombre total de fichiers, volume d'utilisateurs, existence d'une ressource) — même si le contrôle d'autorisation bloque ensuite l'accès. C'est une recommandation de sécurité standard pour toute API publique (mitigation des attaques par énumération / IDOR — *Insecure Direct Object Reference*), indépendante de ce projet précis.
- **`REFRESH_TOKEN.id` et `TAG.id` ne sont jamais exposés directement** (le refresh token expose sa valeur, pas son id de ligne ; les tags sortent en tableau de strings). Un `bigserial` y aurait été tout aussi valable, avec un léger gain de stockage/perf. UUID partout a été choisi pour la **cohérence du schéma** (un seul type d'identifiant dans tout le code EF Core) — pas parce que c'était strictement nécessaire pour ces deux tables.
- **Contrepartie connue** : un UUID (v4, aléatoire — ce que génère `Guid.NewGuid()` par défaut) est plus lourd (16 octets vs 4/8) et fragmente un peu plus l'index B-tree PostgreSQL à l'écriture qu'un entier séquentiel. Négligeable à l'échelle de ce projet (prototype MVP), mais pas gratuit en très gros volume — c'est le compromis à connaître, pas un choix sans inconvénient.
