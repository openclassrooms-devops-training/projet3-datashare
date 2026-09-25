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

## Upload de fichiers (US01)

### Validation du type réel du fichier (magic bytes)

**Décision** : ne jamais faire confiance à l'extension du fichier ni au `Content-Type` déclaré par le client (les deux sont trivialement falsifiables — un `.exe` renommé en `.pdf` avec un `Content-Type: application/pdf` forgé passerait sans contrôle). Le type réel est détecté côté serveur via sa signature binaire (`FileTypeValidationService`, bibliothèque `Mime-Detective`), comparé à une allowlist, et croisé avec l'extension déclarée pour détecter une incohérence. Décision complète et justification détaillée : `docs/adr/0002-validation-type-fichiers.md`.

**Pourquoi documenté ici aussi** : c'est la seule ligne de défense contre l'upload d'un exécutable ou d'un script malveillant déguisé en document — sans elle, n'importe qui pourrait déposer un fichier dangereux et le faire télécharger à un tiers via le lien de partage.

### L'identité de l'uploadeur vient du token, jamais du body client

**Décision** : l'id de l'utilisateur propriétaire d'un fichier est lu depuis la claim `sub` du JWT (côté serveur, dans `FilesController`), jamais depuis un champ du formulaire envoyé par le client.

**Pourquoi ça a failli être fait autrement** : une première version de `FileRequest`/`FileService` acceptait un champ `User`/`UserId` directement dans le body `multipart/form-data` — repéré et corrigé en revue avant merge. Si ça avait été livré tel quel, n'importe quel utilisateur authentifié aurait pu uploader un fichier en se faisant passer pour un autre (usurpation d'appartenance), simplement en changeant ce champ dans la requête. Ne jamais faire confiance à une identité fournie par le client quand elle est déjà disponible de façon fiable via le token d'authentification.

### Nom de fichier sur disque : le token de téléchargement, jamais le nom original

**Décision** : le fichier est écrit sur le disque sous le nom `{downloadToken}{extension}` — le nom original du client (`OriginalFilename`) n'est stocké qu'en base, jamais réutilisé comme chemin de fichier réel.

**Pourquoi** : un nom de fichier fourni par l'utilisateur est une donnée non fiable comme n'importe quelle autre entrée client — l'utiliser directement dans un `Path.Combine` exposerait à une traversée de chemin (ex. un nom contenant `../`) si jamais un contrôle en amont venait à manquer, et évite aussi les collisions entre deux uploads du même nom de fichier.

### Le mot de passe optionnel d'un fichier est un contrôle d'accès, pas un chiffrement

**Décision** : le mot de passe optionnel posé sur un fichier à l'upload est hashé avec BCrypt (`PasswordHash`, même mécanisme que le mot de passe de compte utilisateur) et vérifié via `BCrypt.Verify(...)` au moment du téléchargement (US02) — un échec de vérification bloque le téléchargement (exception dédiée → 401), mais **le contenu du fichier lui-même n'est jamais chiffré**. Il est stocké sur disque tel quel, en clair, dès l'upload.

**Pourquoi ce n'est pas un oubli** : BCrypt est un hash à sens unique — il ne peut produire qu'une réponse vrai/faux à "ce mot de passe correspond-il ?", jamais servir de clé pour transformer des octets. Le mot de passe protège donc uniquement le **point d'entrée API** (`GET`/`POST /api/files/download/{token}`), exactement comme un login protège l'accès à un compte. Il ne protège pas la **confidentialité du fichier au repos** : quiconque a un accès direct au système de fichiers du serveur (ou à une sauvegarde non chiffrée) lit le fichier sans avoir besoin du mot de passe.

**Pourquoi c'est un compromis acceptable pour ce MVP** : un vrai chiffrement du contenu (clé dérivée du mot de passe via une KDF, chiffrement au upload/déchiffrement au download) est une fonctionnalité sensiblement plus complexe, non demandée par le brief (qui exclut explicitement les "fonctionnalités avancées" du prototype). Le choix actuel reste cohérent avec le niveau de risque visé pour une démonstration à des investisseurs, tant qu'il est documenté comme tel plutôt que présenté comme une protection de confidentialité complète.

**À reconsidérer si** : le produit évoluait vers un usage réel avec des données sensibles — le chiffrement du contenu deviendrait alors nécessaire, pas juste souhaitable.

## Historique, liste et suppression de fichiers (US02/US05/US06)

### La suppression vérifie l'appartenance côté serveur, jamais côté client

**Décision** : `DELETE /api/files/{id}` vérifie que le fichier appartient bien à l'utilisateur authentifié (comparaison `UserId` du fichier vs claim `sub` du JWT) **avant** toute suppression. En cas de non-correspondance, renvoie **403** (jamais 404) et ne supprime rien.

**Pourquoi documenté comme faille évitée, pas comme acquis d'emblée** : une première version écrite pendant le développement de cette US supprimait directement le fichier par son id, sans vérifier le propriétaire — repérée et corrigée en relecture avant merge. Si elle avait été livrée telle quelle, n'importe quel utilisateur authentifié aurait pu supprimer le fichier de n'importe qui d'autre simplement en devinant/énumérant des UUID de fichiers (IDOR — *Insecure Direct Object Reference*). Même principe déjà appliqué à l'upload (US01, voir plus haut) : ne jamais faire confiance à une identité que le serveur peut déjà vérifier lui-même — ici appliqué une seconde fois, sur une action destructive cette fois, donc avec un impact plus élevé si elle avait été manquée.

**Pourquoi 403 et pas 404** : renvoyer 404 ("fichier introuvable") aurait masqué la vraie raison du refus, mais surtout aurait été trompeur — le fichier existe bel et bien, ce n'est pas une question d'existence mais de propriété. 403 reflète correctement la sémantique HTTP (requête comprise, refusée pour cause d'autorisation), cohérent avec le reste de l'API.

### Le lien de téléchargement ne révèle jamais si un token a existé mais est expiré

**Décision** : `GET`/`POST /api/files/download/{token}` renvoie exactement le même message générique ("lien invalide ou expiré") pour un token qui n'a jamais existé **et** pour un token expiré — jamais de distinction observable entre les deux cas.

**Pourquoi** : même logique que l'énumération de comptes déjà évitée côté authentification — un message différent par cas permettrait à un tiers de déduire qu'un lien a réellement existé (donc qu'un fichier a été partagé à cette adresse), même sans jamais accéder à son contenu.
