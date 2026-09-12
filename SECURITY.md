# Sécurité — DataShare

Document vivant, enrichi au fil des étapes du projet (pas rédigé d'un bloc à la fin) — voir `CLAUDE.md`.

## Authentification (US03/US04)

### Rotation des refresh tokens + détection de réutilisation

**Décision** : chaque refresh token n'est utilisable qu'**une seule fois**. À chaque appel réussi à `POST /api/auth/refresh`, le token présenté est immédiatement révoqué et remplacé par un nouveau (rotation). Si un refresh token **déjà révoqué** est présenté à nouveau, ce n'est pas traité comme une simple erreur — c'est interprété comme un **signal de vol**, et déclenche la révocation de **tous** les refresh tokens de l'utilisateur concerné (pas seulement celui-ci), forçant une reconnexion complète.

**Pourquoi** : un refresh token volé (XSS, interception, fuite de logs...) reste un risque même avec un stockage soigné côté client. La rotation seule limite déjà la fenêtre d'exploitation (un token volé ne fonctionne qu'une fois), mais ne détecte pas le vol lui-même. La détection de réutilisation comble ce trou : si l'attaquant utilise sa copie volée *avant* l'utilisateur légitime, celui-ci se voit refuser son prochain refresh (le token qu'il a en main a déjà été consommé) — un signal qu'il faut alors traiter comme une compromission, pas comme un bug, et couper court à toute la chaîne de tokens plutôt que de laisser l'attaquant continuer avec le nouveau token émis.

**Algorithme** (`AuthService.RefreshAsync`) :

```
1. hash = SHA256(refreshToken reçu)
2. ligne = chercher RefreshToken où TokenHash == hash

3. si ligne introuvable                → 401
4. si ligne.Revoked == true            → révoquer tous les refresh tokens de ligne.UserId, 401
5. si ligne.ExpiresAt < maintenant     → 401 (expiration normale, pas un vol)
6. sinon :
     - marquer ligne.Revoked = true
     - générer un nouvel access token + un nouveau refresh token
     - persister le nouveau refresh token (hashé), lié au même UserId
     - renvoyer les deux nouveaux tokens
```

**Pourquoi SHA-256 et pas BCrypt pour le hash du refresh token** (contrairement au mot de passe) : le refresh token est déjà à haute entropie (généré aléatoirement, pas un secret humain faible), donc pas besoin du ralentissement volontaire de BCrypt. Plus important : BCrypt génère un sel différent à chaque hash, rendant impossible une recherche indexée en base (`WHERE TokenHash = @hash`) — il faudrait comparer le token reçu contre chaque ligne une par une. SHA-256 est déterministe (même entrée → même hash), ce qui permet un lookup direct.

**Ce que ça ne couvre pas** (limite connue) : si l'attaquant utilise le token volé *avant* l'utilisateur légitime, c'est ce dernier qui déclenche la détection (son propre refresh échoue) — l'attaquant, lui, a déjà obtenu un token valide entre-temps. La révocation de toute la chaîne limite les dégâts après coup, mais ne bloque pas la première utilisation frauduleuse. Une parade plus complète existerait (ex. liaison à des métadonnées client comme l'IP/user-agent), non retenue ici pour ce prototype.

### Stockage des tokens côté client : `localStorage`, pas cookie `httpOnly`

**Décision** : les tokens (access + refresh) sont stockés en `localStorage` côté Angular, et attachés manuellement via un intercepteur HTTP (`Authorization: Bearer <token>`) — pas de cookie `httpOnly`.

**Alternative considérée et écartée** : un cookie `httpOnly` + `Secure` + `SameSite` est le pattern recommandé par l'OWASP pour une SPA, car il élimine le vol de token par XSS (le JS ne peut pas lire un cookie `httpOnly`). Non retenu pour ce prototype pour trois raisons concrètes :
1. Implique de poser le cookie côté serveur (`Response.Cookies.Append(...)` dans `AuthController`) plutôt que de renvoyer le token dans le body JSON — change le contrat `TokenResponse` déjà stabilisé et testé dans `docs/api/openapi.yaml`.
2. Réintroduit une exposition au CSRF (le navigateur attache le cookie automatiquement, y compris depuis un site tiers) qui n'existe pas avec un header `Authorization` manuel — nécessiterait une protection anti-CSRF dédiée (`SameSite` + éventuellement un token synchronizer), donc plus de surface à sécuriser, pas moins.
3. Ajoute de la complexité CORS (`AllowCredentials()`, origine exacte obligatoire, `withCredentials: true` côté Angular) pour un gain de sécurité qui, sur ce prototype à un seul frontend/backend connus, reste marginal.

**Comment le risque XSS est limité autrement, en compensation** : Angular échappe automatiquement tout contenu inséré via l'interpolation standard (`{{ }}`) ou le binding de propriété — le risque XSS ne s'active que via un usage explicite de `[innerHTML]`/`bypassSecurityTrustHtml`, qu'on évite pour tout contenu fourni par un utilisateur (voir le vault de cours pour le détail du mécanisme). Tant que cette discipline est respectée, la surface XSS réelle du projet reste faible.

**À reconsidérer si** : le projet évoluait vers plusieurs clients (appli mobile, autre frontend) ou une exigence de sécurité plus stricte — le pattern cookie `httpOnly` + BFF deviendrait alors plus justifié.
