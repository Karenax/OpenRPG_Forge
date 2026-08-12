# File active — consolidation OpenRPG Forge

Dernière mise à jour : 12 août 2026.

Les 717 fonctions Aurora et les 187 lots sont clos. Cette file décrit le résultat de la consolidation
et sépare les tâches réalisables dans le dépôt des décisions externes de release.

## C1 — Reproductibilité et dépôt

- [x] Corriger les échecs CI liés au parallélisme et à `.artifacts`.
- [x] Ajouter les régressions, régénérer le graphe et passer le portail intégré.
- [ ] Publier le lot puis obtenir CI et graphe verts sur GitHub Actions.

## C2 — Contexte unique

- [x] Faire de `PROJECT_CONTEXT.md` le contexte canonique du produit et de son but final.
- [x] Supprimer les contextes, prompts et journaux historiques contradictoires.
- [x] Conserver le dossier Aurora immuable comme source d'exigences et de provenance.
- [x] Mettre à jour les références documentaires et contrôler les liens.

## C3 — Distribution OpenRPG Forge

- [x] Produire Creator, Runtime et archives portables Windows x64 en `0.1.0-dev`.
- [x] Produire un installateur et un désinstallateur autonomes.
- [x] Fournir manifestes, SHA-256, licences agrégées, crédits, notices et SBOM CycloneDX 1.6.
- [x] Valider installation, lancement, mise à jour et désinstallation sur un profil propre.
- [x] Documenter Authenticode et l'état volontairement `NotSigned` du candidat local.

## C4 — Parcours Creator complet

- [x] Couvrir module vierge et zones intérieure/extérieure sans édition JSON.
- [x] Couvrir import gouverné, placement et configuration de modèles 3D.
- [x] Couvrir créature, objet, porte, trigger, rencontre et faction.
- [x] Couvrir dialogue, quête, journal, pouvoir et script.
- [x] Couvrir diagnostic, correctif, undo/redo, playtest et sauvegarde/reprise.
- [x] Couvrir export `.orpmod`, validation et rechargement depuis la distribution.
- [x] Couvrir Assistant/MCP par plan, dry-run, approbation, test et revert.

## C5 — Interface consolidée

- [x] Stabiliser les neuf espaces et le profil Débutant à quatre espaces.
- [x] Intégrer les outils spécialisés dans une fenêtre active, embarquée et redimensionnable.
- [x] Replier UUID, chemins, schémas et provenance derrière les détails techniques.
- [x] Vérifier navigation, focus, contrastes, échelles et fenêtres responsives par les smokes UI.
- [x] Revoir interactivement le Creator en fenêtre normale et maximisée, puis lancer un playtest 3D.

## C6 — Qualité 3D et démonstration

- [x] Remplacer les primitives dominantes de la Crypte par un kit artistique CC0 gouverné.
- [x] Recomposer Forgotten Forest avec des arbres stylisés, des échelles cohérentes et une lumière lisible.
- [x] Écarter les cubes auxiliaires Blender lors du rendu des assets multi-maillages.
- [x] Vérifier collisions, navigation, LOD/budgets et profils de qualité par les gates runtime.
- [x] Valider caméra, mouvement, combat, dialogue, quête et sauvegarde en Forward+.
- [x] Capturer des preuves reproductibles de la carte entière et du playtest rapproché.

## C7 — Preuves et maintenabilité

- [x] Réparer les chemins de preuve périmés et ajouter une preuve dédiée au parcours produit.
- [x] Créer la matrice et la procédure d'acceptation de bout en bout.
- [x] Rejouer l'installateur sur un profil utilisateur temporaire propre.
- [x] Extraire la politique de fenêtres, le décorateur de Crypte et le mapping de quêtes MCP.
- [x] Conserver le portail intégré vert après chaque extraction.

## Décisions externes avant release publique

- [ ] Choisir et ajouter la licence du code du dépôt.
- [ ] Fournir un certificat Authenticode et signer les exécutables.
- [ ] Rejouer la revue physique multi-écrans et un fournisseur IA réel.
- [ ] Autoriser explicitement un tag et une GitHub Release.

AV8–AV10 restent postérieures à ce candidat : Vault complet, SQLite, KTX2, surveillance temps réel,
curation de masse et finition artistique exhaustive. Elles ne rouvrent pas la consolidation actuelle.
