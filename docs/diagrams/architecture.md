# Schéma d'architecture — DataShare

Client Angular → API ASP.NET Core (middleware JWT, contrôleurs, services) → PostgreSQL (métadonnées) + système de fichiers local (contenu des fichiers). Un service d'arrière-plan purge quotidiennement les fichiers expirés.

```mermaid
flowchart TB
    subgraph Client["Client"]
        SPA["Angular SPA\n(Standalone Components)"]
    end

    subgraph API["Backend — ASP.NET Core Web API"]
        direction TB
        MW["JWT Auth Middleware"]

        subgraph Controllers["Controllers"]
            AuthC["AuthController\n/api/auth/*"]
            FilesC["FilesController\n/api/files/*"]
        end

        subgraph Services["Services"]
            AuthS["AuthService\n(login, register,\nissue/refresh JWT)"]
            FileS["FileService\n(upload, download,\nlist, delete)"]
            TypeS["FileTypeValidationService\n(signature/magic bytes,\nindépendant de l'extension\net du Content-Type client)"]
            StorageS["IFileStorageService\n(abstraction, impl. locale)"]
            CleanupS["FileExpirationCleanupService\n(IHostedService, purge quotidienne)"]
        end
    end

    subgraph Data["Persistance"]
        PG[("PostgreSQL\nvia EF Core")]
        FS[("Système de fichiers local\n(dossier de stockage)")]
    end

    SPA -->|"HTTPS + JWT (Bearer)"| MW
    MW --> AuthC
    MW --> FilesC

    AuthC --> AuthS
    FilesC --> FileS

    FileS -->|"1. vérifie la signature\navant tout stockage"| TypeS
    TypeS -->|"rejette si type non autorisé\nou incohérent avec l'extension"| FileS

    AuthS -->|"users, refresh_tokens"| PG
    FileS -->|"files, tags (métadonnées,\ncontent_type détecté serveur)"| PG
    FileS --> StorageS
    StorageS -->|"lecture/écriture fichiers"| FS

    CleanupS -->|"purge expirés (cron quotidien)"| PG
    CleanupS -->|"suppression physique"| FS

    classDef client fill:#fde8d8,stroke:#e58a5c,color:#1a1a1a
    classDef backend fill:#eef1fb,stroke:#6b7bd6,color:#1a1a1a
    classDef data fill:#e7f5ec,stroke:#4caf7d,color:#1a1a1a

    class SPA client
    class MW,AuthC,FilesC,AuthS,FileS,TypeS,StorageS,CleanupS backend
    class PG,FS data
```
