# Backend — DataShare API

.NET 8 / ASP.NET Core Web API.

## Setup initial (une seule fois)

La chaîne de connexion locale n'est **jamais** commitée (voir `docs/documentation-technique.md`, section sécurité) : elle passe par [dotnet user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), stocké hors du repo.

```bash
cd src/DataShare.Api
dotnet user-secrets set "ConnectionStrings:DataShare" "Host=localhost;Port=5432;Database=datashare;Username=datashare;Password=datashare_dev_only"
```

(Les identifiants doivent correspondre à ceux de `docker-compose.yml` à la racine.)

**Base dédiée aux tests d'intégration** (`DataShare.IntegrationTests`, distincte de la base de dev, jamais partagée) — à créer une seule fois :

```bash
docker exec -i datashare-postgres psql -U datashare -d datashare -c "CREATE DATABASE datashare_integration_test;"
```

Les tests d'intégration (`CustomWebApplicationFactory`) recréent le schéma à chaque run (`EnsureDeletedAsync` + `MigrateAsync`) — seule la base elle-même doit exister au préalable.

## Commandes utiles

```bash
# Démarrer PostgreSQL en local (depuis la racine du repo)
docker compose up -d

# Lancer l'API
dotnet run --project src/DataShare.Api

# Tests unitaires
dotnet test tests/DataShare.UnitTests
dotnet test tests/DataShare.UnitTests --collect:"XPlat Code Coverage"

# Tests d'intégration (necessite la base dediee, voir ci-dessus)
dotnet test tests/DataShare.IntegrationTests
```

Swagger UI disponible en développement sur `/swagger`, health check sur `/health`.

Voir `docs/diagrams/mcd.md` pour le MCD cible (`USER`, `REFRESH_TOKEN`, `FILE`, `TAG`).

Voir les conventions dans le [`CLAUDE.md`](../CLAUDE.md) racine.
