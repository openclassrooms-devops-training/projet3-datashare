# DataShare — Documentation technique

Document livrable attendu par la spec du projet (voir `.mafal\projet3-datashare\entrants\mission\00-mission-brief.md`, liste des livrables). Assemblé au fil des étapes ; converti en PDF avant dépôt final.

## Sommaire

1. Architecture de l'application → voir [`docs/diagrams/architecture.md`](diagrams/architecture.md)
2. **Choix technologiques justifiés** (cette section)
3. Modèle de données → voir [`docs/diagrams/mcd.md`](diagrams/mcd.md)
4. Documentation des endpoints (API) → voir [`docs/api/openapi.yaml`](api/openapi.yaml)
5. Sécurité et gestion des accès — à compléter (Étape 5)
6. Qualité, tests et maintenance — à compléter (Étape 5, `TESTING.md`/`SECURITY.md`/`PERF.md`/`MAINTENANCE.md`)
7. Processus d'installation et d'exécution — à compléter (Étape 6, README)
8. Utilisation de l'IA dans le développement — à compléter (Étape 4)

## Choix technologiques justifiés

| Élément | Technologie choisie | Alternatives (imposées par la spec) | Justification |
|---|---|---|---|
| Langage / framework back-end | C# / .NET 8, ASP.NET Core Web API | Java/Spring Boot, TypeScript/NestJS, PHP (Symfony/Laravel) | Stack déjà maîtrisée par le développeur — réduit le risque d'implémentation sur un planning contraint de 4 semaines. ASP.NET Core figure parmi les frameworks web les plus performants du marché (benchmarks TechEmpower), avec un typage fort et Entity Framework Core comme ORM mature pour l'accès aux données. |
| Framework front-end | Angular 17+ (Standalone Components) | React, VueJS | Framework "complet" (routing, formulaires, client HTTP, injection de dépendances intégrés nativement), plus structurant que React/Vue qui nécessitent d'assembler ces briques soi-même — pertinent pour un objectif de montée en compétence sur des projets de grande taille. Déjà utilisé dans `projet2-modif-appli`, ce qui capitalise sur une compétence déjà en cours d'acquisition plutôt que d'en disperser l'effort sur un nouveau framework. |
| Base de données | PostgreSQL | MongoDB | SGBD relationnel open-source le plus utilisé en entreprise. Le domaine métier (utilisateurs, fichiers, tags, tokens de rafraîchissement) est naturellement relationnel, avec des relations et cardinalités bien définies (voir MCD) — un SGBD NoSQL document-orienté n'apporterait aucun bénéfice ici et compliquerait la modélisation sans contrepartie. |
| Stockage des fichiers | Système de fichiers local | Stockage AWS S3 | Plus simple à mettre en œuvre pour un prototype MVP à livrer en 4 semaines : aucun compte ni identifiants cloud à gérer, aucun coût. L'accès au stockage est isolé derrière une abstraction (`IFileStorageService`, voir architecture) qui permettrait de basculer vers S3 plus tard sans réécrire la logique métier. |
| Authentification | JWT — access token court + refresh token | Sessions serveur classiques, authentification déléguée (OpenID Connect / Google) | Conforme à la spec (US03/US04, "Authentification JWT" dans les bonnes pratiques attendues). Le refresh token est ajouté au-delà du strict minimum demandé, pour permettre la révocation d'une session sans attendre l'expiration de l'access token — bonne pratique de sécurité. L'authentification déléguée (Google) a été envisagée puis écartée : la spec attend explicitement une création de compte et une connexion par email/mot de passe propres à l'application. |
| Tests back-end | MSTest + Coverlet | xUnit + FluentAssertions + Moq | MSTest est le framework de test officiel de l'écosystème .NET, nativement intégré à Visual Studio ; Coverlet fournit la couverture de code via `dotnet test --collect:"XPlat Code Coverage"`. |
| Tests unitaires front-end | Jest (`jest-preset-angular`) | Karma/Jasmine (outillage historique d'Angular CLI) | Cohérence avec `projet2-modif-appli`, qui utilise déjà Jest — évite d'introduire un deuxième outil de test à maîtriser pour un même type de besoin. |
| Tests end-to-end | Cypress | Playwright | Explicitement suggéré par la spec ("Cypress ou équivalent") et déjà utilisé dans `projet2-modif-appli` — cohérence retenue plutôt que d'introduire un nouvel outil. |
| Test de performance | k6 | Apache JMeter, Artillery | Explicitement suggéré par la spec ("k6 ou autre outil de performance"). Scripts de test écrits en JavaScript, léger (binaire unique, pas de JVM contrairement à JMeter), bien adapté à un test ciblé sur un seul endpoint critique (upload/download) plutôt qu'à une suite de charge complexe — voir `PERF.md` (Étape 5). |
| Intégration continue | GitHub Actions | GitLab CI | Le code est hébergé sur GitHub (organisation dédiée à la formation) ; Actions s'intègre nativement, sans outil ni compte supplémentaire à configurer. |
| Qualité de code | SonarQube, ESLint (front), .NET Analyzers (back) | — | Détection automatisée des bugs, vulnérabilités et code smells à chaque build. |
| Gestion de version | Git / GitHub, branches `main` et `develop` protégées (GitHub Rulesets), Conventional Commits | Ancienne API GitHub "Branch Protection" | Conforme à la spec ("historique Git propre", norme conventional commit). Les Rulesets sont préférés à l'ancienne API de protection de branche car ils permettent un contournement admin exclusivement via Pull Request (`bypass_mode: pull_request`) — jamais de push direct, même pour un compte administrateur. |
| Environnement de développement | VS Code | Visual Studio, Rider, WebStorm | Un seul IDE pour le back-end (C#) et le front-end (Angular/TypeScript) grâce aux extensions officielles — évite de jongler entre deux environnements différents. |

### Points de vigilance assumés

- Le stockage local et l'auto-incrémentation évitée au profit d'UUID (voir [`docs/diagrams/mcd.md`](diagrams/mcd.md)) sont des compromis documentés, pas des choix « par défaut » : chacun a une alternative connue et une raison explicite de ne pas l'avoir retenue à ce stade.
- **Gestion des secrets locaux** : le mot de passe PostgreSQL dans `docker-compose.yml` (conteneur local, sans donnée réelle, non exposé) reste en clair — pratique standard et à risque quasi nul pour du développement local. En revanche, la chaîne de connexion utilisée par l'API .NET **n'est jamais commitée** : elle passe par `dotnet user-secrets` (voir `backend/README.md`), le mécanisme officiel .NET conçu pour ça, stocké hors du repo. Les deux ne sont pas traités pareil, volontairement.
- Détail complet du raisonnement et de l'historique de la décision : [`docs/adr/0001-stack-technique.md`](adr/0001-stack-technique.md) et [`docs/adr/0002-validation-type-fichiers.md`](adr/0002-validation-type-fichiers.md).

### Portée du front-end : web uniquement pour le MVP

Les maquettes Figma fournies (voir mail de Lisa) couvrent à la fois des écrans mobile (iPhone) et desktop/web. **Le MVP cible uniquement le web** — l'application Angular est développée et validée pour un usage navigateur desktop ; aucune application mobile native ni PWA installable n'est prévue pour ce périmètre. Les écrans mobile de la maquette servent de référence pour un éventuel responsive design ultérieur, pas d'objectif de livraison pour le MVP.
