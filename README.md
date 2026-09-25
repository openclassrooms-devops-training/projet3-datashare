# DataShare — projet3-datashare

> Plateforme de transfert sécurisé de fichiers (prototype MVP). Projet 3 du parcours OpenClassrooms Expert DevOps.

## Contexte

Projet pédagogique OpenClassrooms (path 2461, projet 4089) : concevoir et développer, en tant que référent technique senior fictif chez « DataShare », le prototype d'une plateforme de partage de fichiers façon WeTransfer, avec pilotage de l'architecture, implémentation supervisée (dont un usage actif d'un copilote IA, relu et documenté — voir `docs/documentation-technique.md`, section 8), tests, sécurité, performance et documentation.

Le brief complet, les spécifications, les maquettes Figma et le suivi de la mission sont hors de ce repo (matériel de formation non officialisé) : `C:\workspace\.mafal\projet3-datashare\entrants\`.

## Stack

- **Backend** : .NET 8 / ASP.NET Core Web API, EF Core, PostgreSQL
- **Frontend** : Angular 17+
- **Tests** : MSTest + Coverlet (backend), Jest + Cypress (frontend)
- **CI/CD** : GitHub Actions (lint, tests, couverture, SonarQube)

Détail complet et rationale des choix : [`docs/adr/0001-stack-technique.md`](docs/adr/0001-stack-technique.md).

## Démarrage

### Backend
```bash
cd backend
dotnet restore
dotnet run
```

### Frontend
```bash
cd frontend
npm install
ng serve
```

## Tests

```bash
# Backend
cd backend && dotnet test --collect:"XPlat Code Coverage"

# Frontend
cd frontend && npm test              # unitaires (Jest)
cd frontend && npx cypress run       # e2e
```

## Suivi qualité et maintenance

Voir `TESTING.md`, `SECURITY.md`, `PERF.md`, `MAINTENANCE.md` (à la racine, alimentés au fil du projet).

## Workflow Git

- `main` : production, protégée (PR + 1 review)
- `develop` : intégration, protégée (PR)
- `feature/etape<N>-description` : une branche par étape de la mission

Détail des conventions : [`CLAUDE.md`](CLAUDE.md).

## Licence

Projet pédagogique — usage personnel dans le cadre de la formation OpenClassrooms.
