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
| US03/US04 (auth) | `AuthServiceTests.cs`, `AuthControllerTests.cs` | 100% sauf `RefreshTokenAsync` (86% branches) — cas "vol de refresh token détecté" non testable en InMemory (`ExecuteUpdateAsync` non supporté), documenté dans le fichier de test, à couvrir en intégration |
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
| Inscription → connexion → upload d'un fichier | `cypress/e2e/register-login-upload.cy.ts` | ✅ passant, exécuté réellement contre le backend/frontend en marche |

Objectif spec : 2-3 scénarios critiques minimum — celui-ci est le premier (le plus englobant, couvre 3 US d'un coup). D'autres scénarios (échec de connexion, fichier trop volumineux, etc.) restent à ajouter.

**Piège rencontré, à connaître** : `cy.url().should('include', ...)` juste après un clic de soumission peut timeout (4s par défaut) si la première requête réseau met du temps à répondre (cold start). Préférer intercepter la requête (`cy.intercept(...).as('x')` + `cy.wait('@x')`) avant de vérifier la navigation qui en découle — plus robuste, et donne le vrai code HTTP en cas d'échec au lieu d'un timeout muet.

## Tests d'intégration backend

`DataShare.IntegrationTests` est scaffoldé (projet créé à l'Étape 2) mais vide. Prévu après les tests E2E — utilisera `WebApplicationFactory` + une vraie base, pour valider le pipeline HTTP réel (routing, `[Authorize]`, sérialisation) sans mocker les services, contrairement aux tests unitaires. C'est aussi là que le cas non couvert en unitaire (révocation en cascade des refresh tokens) pourra être testé pour de vrai.

## Couverture de code

Objectif indicatif : 70% (spec OpenClassrooms). Capture d'écran du rapport global à ajouter une fois les tests d'intégration et E2E en place — pour l'instant seule la couverture par fichier a été vérifiée au fil de l'écriture des tests (cf. tableau ci-dessus), pas de rapport HTML global généré.
