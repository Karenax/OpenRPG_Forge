# File active — consolidation OpenRPG Forge

Dernière mise à jour : 21 août 2026.

Les 717 fonctions Aurora et les 187 lots sont clos. Cette file décrit le résultat de la consolidation
et sépare les tâches réalisables dans le dépôt des décisions externes de release.

## C1 — Reproductibilité et dépôt

- [x] Corriger les échecs CI liés au parallélisme et à `.artifacts`.
- [x] Ajouter les régressions, régénérer le graphe et passer le portail intégré.
- [x] Publier le lot consolidé sur `origin/main`.
- [ ] Obtenir CI et graphe verts sur GitHub Actions : jobs refusés avant démarrage par le compte
  GitHub pour paiement échoué ou limite de dépenses insuffisante.

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
- [x] Garantir zéro fenêtre d'outil au démarrage et un seul outil secondaire actif, réutilisable,
  déplaçable, maximisable et refermable.
- [x] Replier UUID, chemins, schémas et provenance derrière les détails techniques.
- [x] Simplifier l'édition des plaçables autour du viewport : choisir, placer, sélectionner et transformer.
- [x] Vérifier navigation, focus, contrastes, échelles et fenêtres responsives par les smokes UI.
- [x] Revoir interactivement le Creator en fenêtre normale et maximisée, puis lancer un playtest 3D.

## C6 — Qualité 3D et démonstration

- [x] Remplacer les primitives dominantes de la Crypte par un kit artistique CC0 gouverné.
- [x] Recomposer Forgotten Forest avec des arbres stylisés, des échelles cohérentes et une lumière lisible.
- [x] Écarter les cubes auxiliaires Blender lors du rendu des assets multi-maillages.
- [x] Vérifier collisions, navigation, LOD/budgets et profils de qualité par les gates runtime.
- [x] Valider caméra, mouvement, combat, dialogue, quête et sauvegarde en Forward+.
- [x] Démarrer dans l’auberge et valider déplacement rapide, mini-carte et atlas des six zones sur `M`.
- [x] Sécuriser toute la géométrie jouable de l’auberge : sol continu, quatre murs infranchissables,
  porte activée uniquement par interaction, alignement visuel murs/sol et pieds/sol, puis récupération
  après une chute forcée sous une carte intérieure ou extérieure (régression Forward+ automatisée).
- [x] Supprimer le rebond de caméra pendant la marche et remplacer le bouton central de sortie par un
  clic gauche sur la porte 3D, avec validation complète de la transition auberge → Clairval.
- [x] Recomposer le HUD runtime sur une grille Full HD compacte et le revoir visuellement en 1080p/QHD.
- [x] Capturer des preuves reproductibles de la carte entière et du playtest rapproché.

## C7 — Preuves et maintenabilité

- [x] Réparer les chemins de preuve périmés et ajouter une preuve dédiée au parcours produit.
- [x] Créer la matrice et la procédure d'acceptation de bout en bout.
- [x] Rejouer l'installateur sur un profil utilisateur temporaire propre.
- [x] Extraire la politique de fenêtres, le décorateur de Crypte et le mapping de quêtes MCP.
- [x] Conserver le portail intégré vert après chaque extraction.

## C8 — Édition manuelle et non destructive

- [x] Remplacer les principales collections à séparateurs des éditeurs Dialogue, Quête, Blueprint,
  contenu et Objet par un composant liste + inspecteur avec ajout, duplication, suppression et ordre.
- [x] Préserver les champs, localisations et entrées non représentés lors des modifications rapides
  des fondations Objet, du gameplay Objet, des profils avancés Objet et du Terrain.
- [x] Ajouter création/suppression manuelle des nœuds de dialogue, états et transitions de quête.
- [x] Ajouter la suppression transactionnelle des zones, blueprints, dialogues et quêtes avec
  dry-run, blocage des dépendances, protections lecture seule/zone d’entrée et undo/redo.
- [x] Vérifier interactivement le démarrage sans fenêtres parasites, le formulaire structuré vide,
  l’ajout d’une ligne, ses champs nommés et le repli explicite Modifier/Replier.
- [x] Intégrer ce lot dans la phase consolidée, le valider avec les autres changements locaux et le
  publier uniquement après autorisation explicite.

## C9 — Lanceur des jeux créés avec OpenRPG Forge

- [x] Ajouter un profil canonique v1 : identité, exécutable, accent, image, liens, réglages,
  capacité multijoueur et port local.
- [x] Ajouter dans le Creator un formulaire direct et l'action **Créer le lanceur du jeu**.
- [x] Construire un lanceur WPF RPG cinématique avec Accueil, Configuration et Multijoueur, décor
  gouverné, brumes et braises animées, parallaxe, transitions et mode de mouvements réduits.
- [x] Appliquer au runtime résolution, fenêtre, qualité, VSync, volumes et réduction des mouvements.
- [x] Démarrer et valider une session locale autoritaire depuis le lanceur.
- [x] Produire atomiquement le dossier du jeu et vérifier chaque fichier par taille et SHA-256.
- [x] Fournir le CLI, les scripts et le modèle embarqué dans la distribution Creator.
- [x] Vérifier visuellement les trois écrans WPF sans contrôle Windows natif visible et rejouer le
  smoke sur le runtime exporté.
- [ ] Brancher un transport réseau inter-processus avant d'activer **Rejoindre par adresse**.

## C10 — Interface de combat dynamique du runtime

- [x] Projeter les résultats autoritaires de combat depuis Domain/Application sans dépendance Godot
  ni recalcul de dégâts, soins, ressources, recharge ou décès.
- [x] Ajouter barres animées, cible, plaques ennemies, nombres flottants, recharge réelle, réactions
  des capacités, toasts et pings temporaires de mini-carte.
- [x] Ajouter impacts particulaires, accent écran, impulsion caméra, micro hit-stop et routage SFX,
  avec cue déclarée ou repli procédural déterministe.
- [x] Borner les pools et valider 600 frames réchauffées sous 64 Kio d'allocations et 1 ms p95.
- [x] Couvrir contraste élevé, modes de vision des couleurs, texte ×1,2/×2 et mouvements réduits.
- [x] Capturer 30 preuves Forward+ en 1920×1080 et 2560×1440, puis revoir interactivement attaque,
  atlas, roue d'actions et transition Auberge du Cerf d'Ambre → Clairval.
- [x] Conserver `forest-strike` à sa recharge réelle de zéro seconde et laisser le contenu JSON ainsi
  que l'équilibrage hors du lot.

## C11 — Qualification et publication de la phase

- [x] Synchroniser `PROJECT_CONTEXT.md`, `STATUS.md` et cette file avec l'état réellement vérifié.
- [x] Exclure du commit les plans Hermes et sidecars Godot régénérables, sans supprimer les snapshots
  content-addressés qui documentent les révisions de zones.
- [x] Isoler les restaurations RID des distributions pour préserver les verrous NuGet canoniques.
- [x] Régénérer le graphe Roslyn et obtenir zéro diagnostic sur le contrôle complet.
- [x] Rejouer les tests ciblés, les preuves visuelles Forward+ et le portail intégré.
- [x] Publier la phase sur `origin/main` avec preuve de worktree propre et divergence nulle.

## C12 — Ruleset RPG systémique et Crypte des Cendres

- [x] Établir un ruleset clean-room canonique couvrant action directe, groupe, exploration libre,
  progression, magie systémique, furtivité, quêtes, factions, IA, persistance et monde comme arme.
- [x] Ajouter un manifeste versionné propre à la Crypte avec cinq interactions environnementales :
  huile, eau, brasero, pilier et pointes.
- [x] Ajouter les commandes isolées au module : poussée, feu, givre, force et concentration tactique.
- [x] Brancher les dégâts environnementaux sur le chemin de combat autoritaire existant afin de
  conserver phases de boss, événements, butin, progression et transitions de quête.
- [x] Préserver le parcours historique de la Crypte et maintenir le combat direct comme solution.
- [x] Ajouter la documentation du module et un test de contrat statique ; 8 tests Python passent.
- [ ] Exécuter `scripts/Verify.ps1`, la compilation complète .NET/Godot et le contrôle du graphe sur
  l’environnement Windows canonique.
- [ ] Rejouer la Crypte en Forward+ et vérifier visuellement les cinq dispositifs, les contrôles,
  la concentration tactique, les collisions et les états de quête.
- [ ] Généraliser masse, stabilité, surfaces, substances, supports et causalité dans Domain/Application
  au lieu de conserver ces comportements dans un directeur spécifique au module.
- [ ] Exposer les propriétés systémiques dans le Creator sans JSON, avec validation, dry-run,
  diff sémantique, undo/redo et outils de débogage.
- [ ] Ajouter compagnons, témoins, réputation multidimensionnelle et solutions systémiques aux futurs
  modules après qualification de la première tranche verticale.

## Décisions externes avant release publique

- [ ] Choisir et ajouter la licence du code du dépôt.
- [ ] Fournir un certificat Authenticode et signer les exécutables.
- [ ] Rejouer la revue physique multi-écrans et un fournisseur IA réel.
- [ ] Autoriser explicitement un tag et une GitHub Release.
- [ ] Corriger **Billing & plans** puis relancer les deux workflows du commit final.

AV8–AV10 restent postérieures à ce candidat : Vault complet, SQLite, KTX2, surveillance temps réel,
curation de masse et finition artistique exhaustive. Elles ne rouvrent pas la consolidation actuelle.
