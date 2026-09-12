# projet3-datashare

> DataShare — Plateforme de transfert sécurisé de fichiers (prototype MVP). Projet 3 du parcours OpenClassrooms Expert DevOps (path 2461, projet 4089).

Brief complet et entrants du projet : `C:\workspace\.mafal\projet3-datashare\entrants\` (hors de ce repo, jamais commité).
Décisions d'architecture : `docs/adr/`.

## Stack technique

### Backend (`backend/`)
- **Framework** : .NET 8, ASP.NET Core Web API (C# 12)
- **ORM** : Entity Framework Core 8
- **Base de données** : PostgreSQL
- **Stockage fichiers** : système de fichiers local
- **Auth** : JWT (access token court + refresh token), voir `docs/adr/0001-stack-technique.md`
- **Tests** : MSTest + Coverlet (couverture)
- **Qualité** : SonarQube, .NET Analyzers

### Frontend (`frontend/`)
- **Framework** : Angular 17+ (Standalone Components)
- **Tests unitaires** : Jest (`jest-preset-angular`)
- **Tests e2e** : Cypress
- **Qualité** : SonarQube, ESLint

### Commandes utiles

```bash
# Backend
cd backend
dotnet run
dotnet test --collect:"XPlat Code Coverage"

# Frontend
cd frontend
ng serve
npm test              # Jest
npx cypress run       # e2e
ng build --configuration=production
```

## Architecture

### Backend — ASP.NET Core Web API

Architecture en couches classique, pas de MVVM (ce n'est pas une app desktop) :

```
backend/
├── src/
│   ├── Controllers/     # Endpoints REST (DTOs en entrée/sortie uniquement)
│   ├── Services/        # Logique métier (interfaces + implémentations)
│   ├── Models/          # Entités EF Core
│   ├── DTOs/            # Objets de transfert (requêtes/réponses API)
│   ├── Middleware/       # Gestion d'erreurs, auth JWT
│   ├── AppDbContext.cs
│   └── Program.cs
└── tests/
    ├── UnitTests/         # MSTest
    └── IntegrationTests/  # MSTest
```

- Pas de logique métier dans les contrôleurs — ils délèguent aux services.
- Pas de couche Repositories séparée — les services injectent `AppDbContext` directement (choix assumé : pour la taille de ce projet, une couche repository par-dessus EF Core n'apporte pas grand-chose ; `DbContext`/`DbSet<T>` fait déjà office de repository + unit-of-work). Testabilité assurée via le provider EF Core InMemory plutôt que par mock de repository.
- Injection de dépendances via le conteneur ASP.NET Core natif.
- Requêtes paramétrées uniquement (EF Core LINQ), jamais de concaténation SQL.
- Nullable reference types activés (`<Nullable>enable</Nullable>`).

### Frontend — Angular

```
frontend/src/
├── app/
│   ├── core/           # Transversal, organise par type technique (pas par feature)
│   │   ├── guards/
│   │   ├── interceptors/
│   │   ├── models/
│   │   └── services/
│   ├── components/      # Un dossier par ecran
│   │   ├── login/
│   │   └── register/
│   └── app.component.ts
├── assets/
└── environments/
```

- Pas de couche `features/`/`shared/` intermédiaire — trop pour la taille de ce projet (choix assumé : structure plate, `components/<ecran>/`, plus lisible/explicite pour ce périmètre). `shared/` pourra être ajouté plus tard, seulement le jour où un composant réutilisable entre plusieurs écrans apparaît réellement — pas par anticipation.
- Smart/Dumb components : les Smart gèrent la donnée, les Dumb sont purement présentationnels (`@Input`/`@Output`).
- `OnPush` change detection partout.
- Routes lazy-loadées (`loadComponent`).
- Standalone components (pas de NgModules).

## Conventions Git

### Branches protégées (GitHub Rulesets)

Les branches `main` et `develop` sont **protégées**. **Aucun push direct**, même pour l'admin — tout passe par Pull Request.

- `main` : PR requise, 1 review minimum (l'admin peut force-merger via `bypass_mode: pull_request`, mais ne peut jamais push directement)
- `develop` : PR requise, 0 review minimum

### Fil rouge : les étapes de la mission

Le projet suit les 6 étapes du brief OpenClassrooms (voir `.mafal\projet3-datashare\entrants\mission\`). **Une branche = une PR = une étape**, dans cet ordre, sans anticiper sur la suivante — le mentor doit pouvoir lire l'historique des PR comme la progression naturelle des étapes du brief.

Avant de commencer le travail d'une étape, relire son fichier dans `entrants/mission/` et comparer précisément son "Résultat attendu" à ce qu'on s'apprête à faire. Piège déjà rencontré sur ce projet : en démarrant l'Étape 2 ("initialisation des applications"), il a été tentant d'ajouter tout de suite JWT, BCrypt et les entités EF Core (`User`, etc.) — mais ça appartient explicitement à l'Étape 3 ("Implémentez votre première User Story", résultat attendu = "un système d'authentification fonctionnel"). Ne pas construire par anticipation ce qu'une étape ultérieure demande explicitement, même si c'est tentant ou "logique" techniquement — ça brouille la lisibilité de la PR pour le mentor et ça duplique le travail entre deux étapes.

### Workflow

```
1. git checkout develop && git checkout -b feature/etape<N>-description
2. Développer + commiter (Conventional Commits)
3. git push origin feature/etape<N>-description
4. gh pr create --base develop
5. CI passe → merge dans develop
6. Quand develop est stable → PR develop → main
```

### Nommage des branches

| Préfixe | Usage | Exemple |
|---------|-------|---------|
| `feature/` | Nouvelle fonctionnalité | `feature/etape1-architecture-mcd` |
| `fix/` | Correction de bug | `fix/etape4-upload-token` |
| `chore/` | Maintenance technique | `chore/update-deps` |
| `docs/` | Documentation uniquement | `docs/etape6-readme` |

### Conventional Commits

Format : `type(scope): description`

`feat` · `fix` · `refactor` · `test` · `docs` · `chore` · `style`

### Interdit

- Push direct sur `main` ou `develop` → rejeté par GitHub
- Force push sur branches protégées → rejeté
- Merge sans PR → rejeté

## Suivi qualité et maintenance

Répartis en 4 fichiers à la racine (exigence de la spec OC) : `TESTING.md`, `SECURITY.md`, `PERF.md`, `MAINTENANCE.md`. À ouvrir et enrichir au fil des étapes, pas rédiger d'un bloc à la fin.

- Couverture de code : objectif indicatif 70 % (spec OC), avec capture d'écran du rapport dans TESTING.md.
- Scan de sécurité basique (`npm audit` côté front, équivalent .NET côté back), documenté dans SECURITY.md.
- Test de performance sur un endpoint critique (upload/download) avec k6, documenté dans PERF.md.

## Utilisation de l'IA dans le développement

Contrainte du brief : l'IA générative ne doit être utilisée que pour développer **une seule User Story** du projet ; le reste est codé manuellement. Pour cette US : tâches assignées explicitement, code relu, commits séparés (ex. `feat(ai): ...` puis `fix: ... (revue humaine)`), et une section dédiée dans la doc technique expliquant l'usage fait de l'IA. Voir `.mafal\projet3-datashare\entrants\ia-et-developpement.md` et `mission\04-etape-4-fonctionnalites.md`.

**Conséquence concrète pour Claude Code sur ce repo** : tant que l'US "IA" n'est pas explicitement désignée par l'utilisateur (normalement à l'Étape 4, parmi "les autres fonctionnalités"), **ne pas écrire de code métier implémentant une User Story** (entités, services, contrôleurs, composants Angular liés à une US) — scaffolding/infra/CI/docs/diagrammes restent hors de cette contrainte, ce n'est pas du code de User Story. Pour toute US hors du quota IA, produire à la place un guide d'implémentation (méthode, ordre suggéré, pièges à éviter, sans code) dans `.mafal\projet3-datashare\consignes\etape<N>-<sujet>.md`, et laisser l'utilisateur coder à la main. Voir `.mafal\projet3-datashare\consignes\etape3-authentification.md` pour le format de référence.

## Sécurité

- **Jamais** de secrets/tokens dans le code commité.
- `.env` non commité pour le développement local ; **GitHub Secrets** pour la CI.
- Mots de passe hashés (BCrypt côté .NET), jamais stockés en clair.
- Validation des entrées côté client ET serveur.
