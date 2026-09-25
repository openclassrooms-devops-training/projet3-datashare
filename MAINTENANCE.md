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

## Interface

### Avatar utilisateur : initiales plutôt que photo + prénom/nom

**Constat** : la maquette Figma "Mon espace" (variante mobile, iPhone 16 - 5/6) montre un avatar avec photo de profil et prénom/nom ("Claire Marie") dans le header. Notre modèle de données (`User`) ne capture qu'un email à l'inscription — pas de prénom/nom, pas de photo de profil. On utilise donc les 2 premières lettres de l'email en majuscules comme avatar de substitution (voir l'historique de `HeaderComponent`, US01), au lieu de reproduire fidèlement ce détail de la maquette.

**Pourquoi pas corrigé** : aucune US du brief ne demande de prénom/nom ni de photo de profil — ça relève des "fonctionnalités avancées" explicitement exclues du prototype (voir `00-mission-brief.md`). Pas de valeur à l'ajouter sans qu'une US le demande.

**Autre écart lié, note pour memoire** : la maquette mobile a aussi un agencement distinct (menu hamburger, onglets en pilule type "Switch Component", bouton "..." par fichier au lieu de Supprimer/Accéder explicites) qu'on n'a pas reproduit — l'écran "Mon espace" actuel est responsive (la sidebar passe en colonne sur petit écran) mais pas une refonte mobile dédiée. Idem : pas demandé par une US, périmètre volontairement limité pour ce prototype.

**À faire si le produit évolue** : ajouter `FirstName`/`LastName` (et une URL de photo) à l'entité `User`, un écran de complétion de profil, et une variante mobile dédiée de l'interface plutôt qu'un simple responsive.

## Dépendances

### Mise à jour automatisée — Dependabot

**Mécanisme** : `.github/dependabot.yml` configure Dependabot (natif GitHub) pour surveiller **3 écosystèmes** séparément et ouvrir une Pull Request automatiquement dès qu'une nouvelle version est disponible :
- `github-actions` (racine du repo) : les versions `@vX` utilisées dans les workflows CI (`.github/workflows/*.yml`)
- `nuget` (`/backend`) : les packages référencés dans les `.csproj`
- `npm` (`/frontend`) : les dépendances de `package.json`

**Fréquence** : vérification **hebdomadaire**, indépendamment pour chacun des trois écosystèmes.

**Procédure de traitement des PR ouvertes** : Dependabot ouvre la PR mais ne merge jamais rien tout seul — chaque PR passe par la **même CI** que n'importe quelle PR humaine (lint, build, tests unitaires/intégration/E2E) avant merge. Une mise à jour qui casse quelque chose se voit donc en CI, pas en production. En pratique :
- Un bump **mineur/patch** avec CI verte est généralement sûr à merger directement.
- Un bump **majeur** (ex. `v4` → `v6`) mérite une lecture rapide du changelog avant merge — un changement de version majeure peut casser une API utilisée, même si la CI de ce projet ne le détecte pas forcément (dépend de la couverture de test réelle sur la partie concernée).
- Preuve que le mécanisme tourne réellement : les PR #1 à #5 du repo (`chore(deps): bump actions/...`) ont été ouvertes automatiquement par Dependabot, jamais écrites à la main.

**Risque non couvert par ce fichier** : Dependabot gère séparément les **alertes de sécurité** (vulnérabilité connue/CVE sur une dépendance déjà utilisée) — ces PR-là peuvent arriver en dehors du planning hebdomadaire, dès qu'une faille est publiée, indépendamment de `dependabot.yml`. À surveiller via l'onglet "Security" du repo GitHub.
