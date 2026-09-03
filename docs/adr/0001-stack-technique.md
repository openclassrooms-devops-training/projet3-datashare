# ADR 0001 — Stack technique et organisation du projet DataShare

Statut : décidé le 2026-09-03
Contexte : Projet 3 OpenClassrooms (path 2461, projet 4089) — voir `C:\workspace\.mafal\projet3-datashare\entrants\` pour le brief complet (hors repo, non commité).

## Décisions

### Back-end : .NET Core (C#)
Option autorisée par les spécifications (parmi Spring Boot, .NET Core, NestJS, PHP Symfony/Laravel — voir les spécifications, section "Contraintes techniques").

- Tests unitaires : **MSTest** + **Coverlet** (`dotnet test --collect:"XPlat Code Coverage"`) pour la couverture.
- Conteneurisé avec Docker.

### Front-end : Angular
Option autorisée (parmi Angular, React, VueJS).

- Tests unitaires : **Jest** (via `jest-preset-angular`), pour cohérence avec `projet2-modif-appli/frontend` (qui utilise déjà Jest, pas Karma).
- Tests e2e : **Cypress** — c'est l'outil explicitement suggéré par la spec ("Cypress ou équivalent") et celui déjà utilisé dans projet2 ; cohérence retenue plutôt que Playwright (défaut du template `github-template`, mais on s'écarte de la suggestion spec + de l'existant projet2).

### Base de données : PostgreSQL
Option autorisée (parmi PostgreSQL, MongoDB). Choisi pour un modèle relationnel classique (utilisateurs, fichiers, tags, liens de téléchargement) avec des relations bien définies — cohérent avec la production attendue d'un MCD à l'Étape 1.

### Stockage des fichiers : système de fichiers local
Option autorisée (parmi local, AWS S3). Choisi pour rester simple sur le périmètre MVP (4 semaines) sans complexité de credentials/coûts cloud.

### Authentification
JWT, conformément aux spécifications (US03/US04, "Authentification JWT" dans les meilleures pratiques). Piste OpenID Connect/Google envisagée puis écartée : la spec attend explicitement un compte email + mot de passe hashé (US03) et une connexion email + mot de passe (US04) — remplacer ça par de l'auth déléguée Google s'écarterait du brief.

Même logique globale que `projet2-modif-appli` (Spring Boot, `AuthenticationManager` + `BCryptPasswordEncoder` + JWT HMAC signé) transposée en .NET Core, **avec en plus un système de refresh token "à l'ancienne"** que projet2 n'avait pas (projet2 ne gérait qu'un unique JWT d'accès, sans renouvellement) :
- Access token JWT court (ex. 15 min).
- Refresh token plus long, stocké côté serveur (ou httpOnly cookie), révocable.
- Endpoint dédié pour échanger un refresh token valide contre un nouvel access token, sans repasser par login/mot de passe.

## Organisation Git

- Modèle Gitflow-like : branches `main` et `develop` protégées (pas de push direct, PR obligatoire), 1 review requise sur `main`, via GitHub Rulesets.
- Repo public sous l'organisation `openclassrooms-devops-training` (comme `spring-hr-association`, `snapface`) — nécessaire pour que les Rulesets fonctionnent sans plan GitHub payant (l'org est sur le plan Free ; les Rulesets ne sont disponibles sur repo privé qu'avec un plan Team/Enterprise).
- Bootstrap à partir du template personnel `MafLabs38/github-template`, sous-templates `templates/dotnet/` (back) et `templates/angular/` (front) : CI GitHub Actions (build, tests, couverture, SonarQube), `.gitignore`, structure de `CLAUDE.md`.
- Adaptation par rapport au template : règle "English only" du template passée en **français** (projet de formation), architecture backend adaptée de MVVM (desktop) vers Web API (Controllers/Services/Repositories), Playwright e2e remplacé par Cypress, xUnit remplacé par MSTest, pas de clé JIRA (branches nommées par étape de mission).
- Conventional commits (déjà une consigne du brief OC).
- Les 6 étapes de la mission (voir `.mafal\projet3-datashare\entrants\mission\`) servent de fil rouge : une branche/un groupe de commits par étape, avec `TESTING.md`/`SECURITY.md`/`PERF.md`/`MAINTENANCE.md` ouverts et enrichis au fil de l'eau plutôt que rédigés d'un bloc à l'Étape 6.

## Diagrammes

Tous les diagrammes (architecture, MCD, séquences des flux API) sont produits en **Mermaid** (`.mmd`), commités dans `docs/diagrams/` :
- `erDiagram` pour le MCD
- `flowchart`/architecture pour le schéma des briques techniques
- `sequenceDiagram` pour les flux API critiques (upload, téléchargement, auth)

Avantage : versionnés comme du code (diffables en PR, rendus nativement par GitHub), et réutilisables tels quels dans le support de présentation (soutenance) via la tooling de présentation existante (`C:\workspace\presentation`), qui sait déjà rendre du Mermaid.

## Prochaines étapes

Étape 1 de la mission : schéma d'architecture, MCD (PostgreSQL), contrat d'interface — en Mermaid + OpenAPI, dans `docs/diagrams/` et `docs/api/`.
