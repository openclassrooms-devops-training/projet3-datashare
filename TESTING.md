# Tests — DataShare

Document vivant, enrichi au fil des étapes du projet (pas rédigé d'un bloc à la fin) — voir `CLAUDE.md`.

## Critères d'acceptation

Pour qu'une User Story soit considérée comme testée :
- Le chemin nominal (happy path) est couvert.
- **Chaque branche d'erreur métier** (pas juste "une erreur générique") est testée séparément — ex. côté backend, une exception typée par cas de rejet ; côté frontend, un message distinct par code d'erreur reçu.
- Les tests tournent en isolation (mock des dépendances externes — service HTTP, `DbContext` via EF Core InMemory) : pas d'appel réseau réel, pas de dépendance à un ordre d'exécution entre tests.
- Un gap de couverture volontaire (branche non testée) est **documenté en commentaire** dans le fichier de test, avec la raison — jamais silencieux.

## Tests unitaires

### Backend (MSTest + Moq + EF Core InMemory)

Exécution :
```bash
cd backend
dotnet test tests/DataShare.UnitTests
```

| US | Fichiers | Couverture |
|---|---|---|
| US03/US04 (auth) | `AuthServiceTests.cs`, `AuthControllerTests.cs` | 100% sauf `RefreshTokenAsync` (86% branches) — cas "vol de refresh token détecté" non testable en InMemory (`ExecuteUpdateAsync` non supporté), documenté dans le fichier de test, **couvert en intégration** (voir plus bas) |
| US01 (upload) | `FileServiceTests.cs`, `FilesControllerTests.cs` | `FilesController` 100% ; `FileService.UploadAsync` 100% lignes / 90,6% branches — la branche `status = "expired"` est inatteignable à l'upload (une expiration fraîchement calculée est toujours dans le futur), documenté dans le fichier de test |

36 tests au total, 0 échec.

### Frontend (Jest)

Exécution :
```bash
cd frontend
npm test
```

| US | Fichiers | Couverture |
|---|---|---|
| US03/US04 (auth) | `login.component.spec.ts`, `register.component.spec.ts` | 100% (Login), voir fichiers |
| US01 (upload) | `upload.component.spec.ts` | 100% lignes / 90% branches — le reste (10%) est du code mort par construction : le fallback `?? ''` sur un `FormControl` `nonNullable: true` (jamais `null` en pratique) et le fallback `?? '...'` après `mapFileErrorCode` (qui renvoie toujours une string via son `default`, jamais `undefined`) |

## Tests end-to-end (Cypress)

Exécution (nécessite backend + postgres + frontend démarrés) :
```bash
docker compose up -d
cd backend && dotnet run --project src/DataShare.Api &
cd frontend && ng serve &
cd frontend && npx cypress run
```

| Scénario | Fichier | Statut |
|---|---|---|
| Inscription → connexion → upload d'un fichier | `cypress/e2e/register-login-upload.cy.ts` | ✅ passant |
| Connexion avec mauvais mot de passe | `cypress/e2e/error-scenarios.cy.ts` | ✅ passant |
| Upload : type de fichier non supporté | `cypress/e2e/error-scenarios.cy.ts` | ✅ passant |
| Upload : mot de passe fichier trop court | `cypress/e2e/error-scenarios.cy.ts` | ✅ passant |

4 tests, tous exécutés réellement contre le backend/frontend en marche, 0 échec. Objectif spec (2-3 scénarios critiques minimum) dépassé.

**Piège rencontré à deux reprises, à connaître** : après un clic de soumission, `cy.contains(...)`/`cy.url().should(...)` peuvent timeout (4s par défaut) si la requête réseau met du temps à répondre (cold start, ou simplement le temps de l'aller-retour). Systématiquement intercepter la requête (`cy.intercept(...).as('x')` + `cy.wait('@x')`) avant de vérifier ce qui en découle (navigation, message affiché) — plus robuste, et donne le vrai code HTTP en cas d'échec au lieu d'un timeout muet.

**Piège d'origine avec `localStorage`** : pour poser un token directement (bypass du formulaire, plus rapide pour les tests qui ne portent pas sur le login lui-même), il faut `cy.visit(...)` sur l'origine du frontend **avant** d'écrire dans `window.localStorage` — le storage est isolé par origine, l'écrire avant la première visite l'envoie au mauvais endroit.

## Tests d'intégration backend

`WebApplicationFactory` (`CustomWebApplicationFactory.cs`) démarre l'API réelle (vrai routing, vrai `[Authorize]`, vrais middlewares) contre une **vraie base Postgres dédiée** (`datashare_integration_test`, distincte de la base de dev) — rien n'est mocké, contrairement aux tests unitaires. Setup de la base : voir `backend/README.md`.

Exécution :
```bash
cd backend
dotnet test tests/DataShare.IntegrationTests
```

| Fichier | Ce qui est vérifié |
|---|---|
| `AuthIntegrationTests.cs` | Register → login → refresh (parcours complet réel) ; **révocation en cascade des refresh tokens** (le cas non testable en unitaire avec EF Core InMemory — couvert ici pour de vrai) ; email déjà utilisé → 400 |
| `FilesIntegrationTests.cs` | Upload avec token valide → 201, fichier réellement écrit sur disque + ligne réellement persistée en base ; upload sans token → 401 (vérifie que `[Authorize]` est réellement appliqué par le pipeline, pas juste supposé) ; type de fichier non supporté → 400, avec la vraie `FileTypeValidationService` (pas un mock) |

6 tests, 0 échec.

## Couverture de code

Objectif indicatif : 70% (spec OpenClassrooms). Capture d'écran du rapport global à ajouter une fois les tests d'intégration et E2E en place — pour l'instant seule la couverture par fichier a été vérifiée au fil de l'écriture des tests (cf. tableau ci-dessus), pas de rapport HTML global généré.
