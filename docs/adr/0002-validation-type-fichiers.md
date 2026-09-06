# ADR 0002 — Validation du type réel des fichiers uploadés

Statut : décidé le 2026-09-03
Contexte : la spec (US01, section "Contrôles de saisie") demande une politique de fichiers interdits ("à définir selon la politique de sécurité, ex. .exe, .bat, etc."), et les "meilleures pratiques à suivre" listent explicitement la validation des données.

## Décision

Ne jamais faire confiance à l'extension du fichier ni au `Content-Type` déclaré par le client (les deux sont trivialement falsifiables — un `.exe` renommé en `.pdf` avec un `Content-Type: application/pdf` forgé passerait sans contrôle). Valider le **type réel** du fichier côté serveur via sa signature binaire (magic bytes), avant tout stockage.

- Nouveau composant `FileTypeValidationService` dans la couche Services, appelé par `FileService` en tout premier lors de l'upload (US01), avant écriture sur le disque.
- Rejet (400) si :
  - le type détecté ne correspond à aucun type autorisé (allowlist), ou
  - le type détecté est incohérent avec l'extension déclarée dans le nom de fichier.
- Le `content_type` stocké en base (`FILE.content_type`, voir `docs/diagrams/mcd.md`) et renvoyé par l'API (US02, métadonnées avant téléchargement) est celui **détecté par le serveur**, jamais celui envoyé par le client.
- Implémentation .NET envisagée : bibliothèque de détection par signature (ex. `MimeDetective`) plutôt qu'une simple lecture des premiers octets à la main, pour couvrir un catalogue de signatures large sans le maintenir soi-même.

## Hors périmètre (justifié)

Pas de protection anti "zip bomb" (détection de taux de compression anormal, décompression récursive limitée, etc.). Cette classe d'attaque ne s'applique que lorsqu'un service **décompresse** le contenu d'une archive. DataShare stocke et sert chaque fichier tel quel, en blob opaque — aucune décompression n'a lieu côté serveur, donc le risque ne s'applique pas ici. Un `.zip` est traité comme n'importe quel autre type de fichier autorisé : sa signature est vérifiée en tant que zip, son contenu interne n'est jamais inspecté ni extrait.
