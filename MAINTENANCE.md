# Maintenance — DataShare

Document vivant, enrichi au fil des étapes du projet (pas rédigé d'un bloc à la fin) — voir `CLAUDE.md`.

## Base de données

### Purge des refresh tokens révoqués/expirés

**Constat** : par design (voir `SECURITY.md`, rotation + détection de réutilisation), un refresh token n'est **jamais supprimé** en base — seulement marqué `Revoked = true`. La table `RefreshTokens` grossit donc indéfiniment au fil des connexions/déconnexions/rafraîchissements, y compris pour des lignes devenues totalement inertes (session expirée normalement, copie perdue par l'utilisateur — PC reformaté, etc.).

**Pas un problème fonctionnel** — ces lignes ne sont jamais réutilisables une fois révoquées ou expirées — mais un problème d'hygiène de base à moyen/long terme (volume de la table, temps de sauvegarde, lookup légèrement plus lent avec le temps si aucun index adapté).

**À faire** (pas encore implémenté, noté ici pour ne pas l'oublier) : un job périodique qui supprime les lignes `RefreshTokens` où `Revoked == true` OU `ExpiresAt < maintenant`, au-delà d'une fenêtre de rétention raisonnable (ex. 30 jours après révocation/expiration — pas immédiatement, pour garder une marge d'investigation en cas d'incident de sécurité).

**Options d'implémentation à évaluer le moment venu** :
- Un `BackgroundService` ASP.NET Core natif (`IHostedService`), qui tourne dans le process de l'API elle-même à intervalle régulier — le plus simple à mettre en place, pas d'infra supplémentaire.
- Un job planifié externe (cron sur le serveur, ou tâche planifiée CI/CD) qui exécute une requête SQL de purge directement contre PostgreSQL — découplé du cycle de vie de l'API, plus robuste si l'API redémarre souvent.
- Une contrainte PostgreSQL native (ex. partitionnement par date + `DROP` de partition) — probablement disproportionné pour le volume de ce prototype, à ne considérer que si le volume réel le justifie un jour.

Pas bloquant pour l'Étape 3 (l'authentification fonctionne très bien sans ce nettoyage) — à traiter à l'Étape 5/6 (qualité/maintenance) ou plus tard si le temps manque.
