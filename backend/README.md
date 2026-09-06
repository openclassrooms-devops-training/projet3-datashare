# Backend — DataShare API

.NET 8 / ASP.NET Core Web API.

## Setup initial (une seule fois)

La chaîne de connexion locale n'est **jamais** commitée (voir `docs/documentation-technique.md`, section sécurité) : elle passe par [dotnet user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), stocké hors du repo.

```bash
cd src/DataShare.Api
dotnet user-secrets set "ConnectionStrings:DataShare" "Host=localhost;Port=5432;Database=datashare;Username=datashare;Password=datashare_dev_only"
```

(Les identifiants doivent correspondre à ceux de `docker-compose.yml` à la racine.)

## Commandes utiles

```bash
# Démarrer PostgreSQL en local (depuis la racine du repo)
docker compose up -d

# Lancer l'API
dotnet run --project src/DataShare.Api

# Tests
dotnet test
dotnet test --collect:"XPlat Code Coverage"
```

Swagger UI disponible en développement sur `/swagger`, health check sur `/health`.

Aucune entité métier pour l'instant (`AppDbContext` vide) — elles arrivent au fil des étapes, avec l'implémentation des User Stories correspondantes (voir `docs/diagrams/mcd.md` pour le MCD cible).

Voir les conventions dans le [`CLAUDE.md`](../CLAUDE.md) racine.
