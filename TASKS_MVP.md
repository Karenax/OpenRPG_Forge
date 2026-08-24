# File active — consolidation OpenRPG Forge

Dernière mise à jour : 24 août 2026.

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

## C5 — Interface consolidée historique

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

## C12 — Migration vers Forge Studio autonome

- [x] Accepter l'ADR 0045, fixer les huit rubriques et trente-six pages, puis cartographier les
  66 classes `Window` historiques vers une destination unique.
- [x] Créer `Forge.Studio.Core`, son catalogue testable et les tests de couverture de la matrice.
- [x] Créer l'application Godot .NET autonome sans `EditorPlugin`, avec Module Hub et une seule
  fenêtre produit.
- [x] Ouvrir Forgotten Forest, restaurer workspace/page/zone, afficher la zone active dans une vue
  3D permanente, charger les modèles gouvernés et partager la sélection avec l'inspecteur.
- [x] Fournir la première page spécialisée Dialogues en lecture réelle ligne par ligne et désactiver
  explicitement toute action qui n'est pas encore transactionnelle.
- [x] Fournir `OpenRPG Forge Studio.cmd` et `scripts/Open-Studio.ps1`, sans argument `--editor`.
- [x] Valider la fondation par build sans avertissement, smoke Godot 8/36/1 fenêtre et captures
  Forward+ à 1920×1080 et 2560×1440.
- [x] Rendre la coque compacte en QHD et livrer la première retouche Monde : sélection 3D, gizmo
  X/Y/Z, clic droit, transformation/tags/propriétés, ajout filtré, duplication exacte, suppression
  confirmée, dry-run, undo/redo et sauvegarde atomique.
- [x] Qualifier UX0-A sur cinq IDs : Explorateur hiérarchique, filtres d’affichage, recherche
  globale, références inverses et aide contextuelle, avec smoke, inspection Forward+ FHD/QHD et
  preuve spécifique dans la matrice Studio.
- [x] Réaliser UX0-B sur cinq IDs : disposition multi-panneaux, onglets multi-zones, raccourcis
  configurables, profils d’éditeur et parité plusieurs onglets de zone.
- [x] Réaliser UX0-C sur cinq IDs : vue 3D principale, sélection/inspection, caméra, gizmos et
  undo/redo complets dans un parcours continu.
- [x] Réaliser UX0-D sur cinq IDs : palette contextuelle, palettes Standard/Custom, placement
  répété, retour visuel invalide et copier/couper/coller/dupliquer.
- [x] Réaliser UX0-E sur cinq IDs : assistant de création de projet, métadonnées localisées,
  identité technique stable, première zone obligatoire et point de départ du joueur.
- [x] Réaliser UX0-F sur cinq IDs : chargement du module, connexion client, objets acquis/perdus,
  utilisation d’objet, mort et respawn par cartes d’événements transactionnelles.
- [x] Réaliser UX0-G sur quatre IDs : équipement/niveau, repos, heartbeat et événement utilisateur
  global par cartes nommées, en réutilisant les politiques runtime canoniques.
- [x] Réaliser UX0-H sur trois IDs : abort de cutscene, temps/calendrier du monde et échelle de
  progression, sans déplacer la séquence de démarrage hors de la page Cinématiques.
- [x] Réaliser UX0-I sur trois IDs : variables de projet, scripts mis en cache, description et
  instructions joueur, sans exposer leurs clés ni perdre les structures avancées.
- [x] Réaliser UX0-J sur deux IDs : renommage sûr et messages/erreurs/statut unifiés dans la coque,
  sans absorber les pages métier des vagues suivantes.
- [x] Réaliser UX1-A sur deux IDs : assistant de création de zone avec gabarits vide/jouable et
  document indépendant, prévisualisé, annulable et persisté sans exposer d’identifiant technique.
- [x] Réaliser UX1-B sur deux IDs : redimensionnement avec pertes explicites et copie complète ou
  gabarit, prévisualisés, annulables, remappés et persistés depuis la fenêtre unique du Studio.
- [x] Réaliser UX1-C sur `ORF-AREA-028` : écran de chargement humain avec médias importés, astuces,
  progression réelle, mouvements réduits, fallback accessible, undo/redo et persistance exacte.
- [x] Réaliser UX1-D sur `ORF-AREA-035` : calques imbriqués directement manipulables, états hérités,
  responsables, affectation et sélection d’objets, export partiel, undo/redo et persistance exacte.
- [x] Réaliser UX1-E sur `ORF-PLC-001` : blueprint de décor humain avec ressource importée,
  collision, rôle, interaction, transform, scripts, sélection 3D, undo/redo et persistance exacte.
- [x] Réaliser UX1-F sur `ORF-PLC-002` : décor statique direct avec regroupement, instanciation,
  occlusion, fusion sûre, collision optionnelle, sélection 3D, undo/redo et persistance exacte.
- [x] Réaliser UX1-G sur `ORF-PLC-003` : objet utilisable direct avec actions ajoutables, portée,
  focus, autorité, conditions, `OnUsed`, sélection 3D, undo/redo et persistance exacte.
- [x] Réaliser UX1-H sur `ORF-PLC-004` : conteneur direct avec capacité, masse, propriétaire,
  accès, persistance, réplication, inventaire, butin, interface, sélection 3D, undo/redo et
  persistance exacte.
- [x] Réaliser UX1-I sur `ORF-PLC-005` : identité et apparence directes avec noms localisés, tags,
  notes, asset, meshes, matériaux, icône, portrait, VFX, sélection 3D, undo/redo et persistance exacte.
- [x] Réaliser UX1-J sur `ORF-PLC-006` : transformation précise avec coordonnées, rotation, échelle,
  pivot, grille, parent humain, surface, animation, sélection 3D, undo/redo et persistance exacte.
- [x] Réaliser UX1-K sur `ORF-PLC-007` : état initial parmi six états, animations, apparences,
  interaction, transitions prévisualisées, sélection 3D, undo/redo et persistance exacte.
- [x] Réaliser UX1-L sur `ORF-PLC-008` : santé, armure, résistances, étapes visuelles, destruction,
  débris, réparation, aperçu de dégâts, sélection 3D, undo/redo et persistance exacte.
- [x] Réaliser UX1-M sur `ORF-PLC-009` : actions protégées, droits requis, feedback joueur,
  dérogation MJ auditée, aperçu de décision, sélection 3D, undo/redo et persistance exacte.
- [x] Réaliser UX1-N sur `ORF-PLC-010` : faction principale, appartenances, hostilité, crime,
  accès, ciblage, aperçu de décision, sélection 3D, undo/redo et persistance exacte.
- [x] Réaliser UX1-O sur `ORF-PLC-011` : dialogue, point d’entrée, cinq sources de déclenchement,
  priorité, usage unique, aperçu de résolution, sélection 3D, undo/redo et persistance exacte.
- [x] Réaliser UX1-P sur `ORF-PLC-012` : quatre états, clé item, justificatifs, défi, code masqué,
  piratage, droits, reverrouillage, brouillage, aperçu runtime, sélection 3D, undo/redo et persistance.
- [x] Réaliser UX1-Q sur `ORF-PLC-013` : conservation, consommation, charges, partage, permission
  virtuelle, audit, aperçu sans mutation d’inventaire, sélection 3D, undo/redo et persistance exacte.
- [x] Réaliser UX1-R sur `ORF-PLC-014` : déclencheur, effet, détection, désamorçage, réarmement,
  propriétaire, alliés, VFX, état armé, usage unique, aperçu runtime, sélection 3D, undo/redo et
  persistance exacte sans inventer les autres événements du placeable.
- [x] Réaliser UX1-S sur `ORF-PLC-015` : table d’objets par noms métier, catégories, poids,
  quantités, qualité, conditions, unicité, moments chargement/ouverture/réinitialisation, graine,
  état généré conservé ou explicitement remis à zéro, aperçu runtime, undo/redo et persistance.
- [x] Réaliser UX1-T sur `ORF-PLC-016` : liaisons OnOpen et OnClose, version ForgeScript, acteur,
  résultat, état précédent, autorité, aperçu runtime sans écriture, sélection 3D, préservation,
  undo/redo et persistance exacte.
- [x] Réaliser UX1-U sur `ORF-PLC-017` : liaisons avant et après le changement de verrou, version
  ForgeScript, méthode, justificatif, acteur, veto contrôlé, aperçus accepté/refusé sans écriture,
  sélection 3D, préservation, undo/redo et persistance exacte.
- [x] Réaliser UX1-V sur `ORF-PLC-018`, `ORF-PLC-019` et `ORF-PLC-020` : événements d'utilisation,
  de perturbation, de dégâts et de destruction depuis la fiche Placeable, sans réimplémenter les
  contrats Domain/Application déjà qualifiés et avec préservation/undo/redo/round-trip.
- [x] Réaliser UX1-W en parallèle contrôlé sur `ORF-EE-002`, `ORF-EE-018` et `ORF-EE-019` : échelle
  de zone, création dynamique de zone et copie dynamique d'objet depuis la page Zones, dans un
  worktree et des fichiers distincts du lot Placeable.
- [x] Préparer en lecture seule les lots Doors puis Triggers pendant UX1-V/UX1-W ; intégrer les
  branches une par une et sérialiser build, tests, smoke, capture, matrice et graphe.
- [x] Réaliser UX1-X sur `ORF-DOOR-001` à `ORF-DOOR-003` : fondation de porte, placement,
  neuf états, interaction d'ouverture, sélection explicite, préservation, undo/redo et persistance.
- [x] Réaliser UX1-Y sur `ORF-TRG-001`, `ORF-TRG-002`, `ORF-TRG-006` et `ORF-TRG-007` : contour
  polygonal, politique générique, OnEnter/OnExit, aperçu uniforme, préservation et layout FHD–4K.
- [x] Réaliser UX1-Z sur `ORF-PLC-021` à `ORF-PLC-024` : réactions d'attaque/capacité, heartbeat,
  événement personnalisé et politique physique honnête, avec préservation et round-trip.
- [x] Clôturer le jalon UX1-X/Y/Z par les trois portails incrémentaux, les preuves Forward+ FHD,
  la revue corrective FHD/QHD/4K d'UX1-Y et le portail transversal complet.
- [x] Réaliser UX1-AA sur `ORF-PLC-025` à `ORF-PLC-027` : sockets/assemblages, actions contextuelles
  et validation de placeable, sans synthétiser les prérequis manquants dans le projet source.
- [x] Réaliser UX1-AB sur `ORF-DOOR-007`, `ORF-DOOR-010`, `ORF-DOOR-014` et `ORF-DOOR-015` :
  dégâts, chargement et événements serveur bornés, avec fusion/préservation canonique.
- [x] Réaliser UX1-AC sur `ORF-TRG-004`, `ORF-TRG-011`, `ORF-TRG-017` et `ORF-TRG-019` : piège,
  filtre, dix volumes et occupation, Foundation explicitement requise.
- [x] Clôturer le jalon UX1-AA/AB/AC par 20/20 tests ciblés, trois portails incrémentaux, les preuves
  Forward+ FHD/QHD/4K, la matrice régénérée et le portail transversal complet.
- [x] Réaliser UX1-AD sur `ORF-DOOR-016`, `ORF-DOOR-017`, `ORF-DOOR-018` et `ORF-DOOR-020` :
  réactions de capacité, heartbeat/personnalisé, conversation/faction et assemblage complexe.
- [x] Réaliser UX1-AE sur `ORF-TRG-005`, `ORF-TRG-008`, `ORF-TRG-012` et `ORF-TRG-013` : secret,
  clic, accès par justificatif et présentation humaine, avec Foundation requise.
- [x] Réaliser UX1-AF sur `ORF-AI-014` et `ORF-AI-015` : proposition, approbation et construction
  de plan de zone, avec confidentialité, provenance, historiques séparés et preuve hors ligne.
- [x] Clôturer le jalon UX1-AD/AE/AF par 26/26 tests ciblés, trois portails incrémentaux, les preuves
  Forward+ FHD/QHD/4K, la matrice régénérée et le portail transversal complet.
- [x] Réaliser UX1-AG sur `ORF-DOOR-004`, `ORF-DOOR-005`, `ORF-DOOR-006`, `ORF-DOOR-012` et
  `ORF-DOOR-013` : accès, refus, piège et événements d'état/verrou, sans promouvoir le portail UX2.
- [x] Réaliser UX1-AH sur `ORF-TRG-009`, `ORF-TRG-010` et `ORF-TRG-018` : automatisation,
  événement personnalisé et contrôle MJ, avec politique runtime honnête.
- [x] Réaliser UX1-AI sur `ORF-TRG-020` : diagnostics de validation canoniques du trigger, sans
  dupliquer le validateur runtime dans le Studio.
- [x] Réaliser UX1-AJ sur `ORF-DOOR-021` : contexte persistant et validation canonique de porte dans
  le Studio, la commande projet, MCP et le prébuild, sans promouvoir la navigation UX2.
- [x] Clôturer UX1 à 71/71 par quatre portails incrémentaux, les preuves Forward+ FHD/QHD/4K, la
  matrice régénérée et le portail transversal complet.
- [x] Réaliser UX2-A sur `ORF-AREA-023`, `ORF-AREA-025` et `ORF-AREA-026` : heartbeat de zone,
  perception et repos par une seule commande area-only, avec préservation complète et preuve isolée.
- [x] Réaliser UX4-A sur `ORF-FAC-001` à `ORF-FAC-005` : création, archive ou suppression sûre,
  parents, héritage, matrice asymétrique et bandes, sans promouvoir la réputation personnelle.
- [x] Réaliser UX5-A sur `ORF-DLG-001` à `ORF-DLG-005` : éditeur ligne/réponses, branches paginées,
  liens existants et réordonnancement réel au clavier ou par glisser-déposer.
- [x] Clôturer le jalon UX2-A/UX4-A/UX5-A par 40/40 tests dans les trois portails incrémentaux,
  un test RpgKit complémentaire, les smokes, les preuves Forward+ FHD/QHD/4K, la matrice régénérée
  et le portail transversal complet.
- [x] Achever UX2 à 98/98 routes Monde : terrain, environnement/audio, monde vivant et navigation,
  avec 36 portails incrémentaux, smokes, preuves Forward+ FHD, matrice et graphe régénérés, puis
  valider le portail transversal complet avec 2 033 tests .NET et 39 tests Python.
- [x] Achever UX4 à 140/140 routes Contenu : factions, créatures, voix, boss assisté, objets,
  marchands et compagnons, avec commandes transactionnelles, preuves isolées et limites runtime
  honnêtes.
- [x] Qualifier UX4-U sur `ORF-CMP-001` à `ORF-CMP-024` par 66/66 tests ciblés, smoke global,
  capture Forward+ FHD inspectée, matrice régénérée et portail transversal complet.
- [x] Achever UX5 à 69/69 routes Histoire : fondation directe des dialogues, production avancée,
  quêtes, localisation/typographie, lore/références et cinématiques/contrôles, avec cinq commandes ou
  historiques explicites, 186/186 tests ciblés, smoke global et captures Forward+ FHD/QHD/4K.
- [x] Réaliser UX3-A sur `ORF-PAL-001`, `ORF-PAL-002`, `ORF-PAL-003`, `ORF-PAL-006` et
  `ORF-PAL-007` : définition réutilisable, instance, dérivation et palettes Standard/Projet avec
  sélection explicite, commandes canoniques, 22/22 tests, portail en 144,41 s et inspections
  Forward+ FHD/QHD/4K.
- [x] Réaliser UX3-B sur `ORF-PAL-004`, `ORF-PAL-005`, `ORF-PAL-008`, `ORF-PAL-013` et
  `ORF-PAL-014` : propagation/migration sélective, catégories hiérarchiques, variantes et
  compositions avec deux approbations indépendantes, 17/17 tests, portail en 146,04 s et deux
  inspections Forward+ FHD.
- [x] Réaliser UX3-C sur `ORF-PAL-009` à `ORF-PAL-012` : favoris/récents, miniature gouvernée,
  recherche avancée et drag-and-drop versionné vers le placement canonique, avec deux historiques
  indépendants, 23/23 tests, portail en 153,00 s et inspection Forward+ FHD.
- [x] Réaliser UX3-D sur `ORF-PAL-015` et `ORF-PAL-016` : manifeste de dépendances résolu avant
  build, budget plateforme, provenance et licence distribuable dans deux transactions indépendantes,
  avec 45/45 tests, portail en 191,90 s et deux inspections Forward+ FHD.
- [x] Réaliser UX3-E sur `ORF-PAL-017` et `ORF-PAL-018` : mesure locale GLB/PNG, six diagnostics
  artistiques et budgets plateforme sur sept axes dans deux transactions indépendantes, avec 11/11
  tests, portail en 156,86 s et deux inspections Forward+ FHD.
- [x] Réaliser UX3-F sur `ORF-CONT-013`, `ORF-CONT-014`, `ORF-CONT-020`, `ORF-CONT-021` et
  `ORF-CONT-022` : navigateur unifié, profils d’import KTX2/GLB et aller-retour Blender allowlisté
  dans deux transactions indépendantes, avec 32/32 tests, portail en 157,96 s et trois inspections
  Forward+ FHD.
- [x] Réaliser UX3-G sur `ORF-CONT-008`, `ORF-CONT-015`, `ORF-CONT-016` et `ORF-CONT-017` :
  manifeste d’assets complet, collision/navigation, animations et VFX/LOD dans une transaction
  canonique, avec 15/15 tests, portail en 160,44 s et quatre inspections Forward+ FHD.
- [x] Réaliser UX3-H sur `ORF-CONT-004`, `ORF-CONT-005`, `ORF-CONT-006` et `ORF-CONT-018` :
  import borné et réversible de fichiers ouverts, export déterministe de blueprints, sources
  textuelles et tables typées dans trois transactions explicites, avec 18/18 tests, portail en
  173,10 s et cinq inspections Forward+ FHD.
- [x] Réaliser UX3-I sur `ORF-CONT-023`, `ORF-CONT-024` et `ORF-AI-024` : provenance gouvernée,
  quarantaine fail-closed et génération média planifiée sans fournisseur dans deux historiques
  explicites, avec 12/12 tests, portail en 171,71 s et quatre inspections Forward+ FHD ; UX3 est
  close à 34/34.
- [x] Réaliser UX6-A sur `ORF-PROJ-023`, `ORF-GAME-017`, `ORF-GAME-023` et `ORF-GAME-024` :
  difficulté/mutateurs/plugins et temps du monde dans deux transactions explicites, 21/21 tests,
  smoke global, round-trip et capture Forward+ FHD inspectée.
- [x] Réaliser UX6-B sur `ORF-GAME-001`, `ORF-GAME-010`, `ORF-GAME-012` et `ORF-GAME-013` :
  déplacement au sol, interactions, caméras et entrées dans une transaction canonique, 8/8 tests,
  smoke global, round-trip et capture Forward+ FHD corrigée puis inspectée.
- [x] Réaliser UX6-C sur `ORF-GAME-003`, `ORF-GAME-004`, `ORF-GAME-011`, `ORF-GAME-015` et
  `ORF-GAME-016` : nage, escalade, menu radial, pause tactique et modes de combat dans une
  transaction canonique, 7/7 tests, smoke global, round-trip et capture Forward+ FHD corrigée.
- [x] Réaliser UX6-D sur `ORF-GAME-002`, `ORF-GAME-008`, `ORF-GAME-009` et `ORF-GAME-014` :
  vol libre, projectiles, physique 3D et ciblage dans une transaction canonique, 11/11 tests,
  smoke global, round-trip et capture Forward+ FHD inspectée.
- [x] Réaliser UX6-E sur `ORF-GAME-005`, `ORF-GAME-006`, `ORF-GAME-007` et `ORF-GAME-028` :
  apesanteur, véhicules, montures et succès dans la commande canonique des extensions, 13/13 tests,
  portail incrémental en 136,22 s, smoke global, round-trip et capture Forward+ FHD inspectée.
- [x] Réaliser UX6-F sur `ORF-SCR-001`, `ORF-SCR-002`, `ORF-SCR-003`, `ORF-SCR-006` et
  `ORF-SCR-007` : atelier C-like, métadonnées/bytecode, compilation/cache, événements typés et
  fonction `main`, 34/34 tests, portail incrémental avec graphe en 284,26 s et capture Forward+ FHD corrigée.
- [x] Réaliser UX6-G sur `ORF-SCR-004`, `ORF-SCR-005`, `ORF-SCR-008`, `ORF-SCR-010` et
  `ORF-SCR-011` : éditeur assisté, Wizard relu, actions différées, combat et effets sur l’autorité
  source UX6-F, 25/25 tests, portail incrémental avec graphe en 251,59 s et capture Forward+ FHD
  corrigée puis inspectée.
- [x] Réaliser UX6-H sur `ORF-SCR-012` à `ORF-SCR-016` : inventaire, journal/quêtes, dialogues,
  factions/réputation et perception dans l’autorité source UX6-F, 13/13 tests, portail incrémental
  en 166,56 s et capture Forward+ FHD corrigée puis inspectée.
- [x] Réaliser UX6-I sur `ORF-SCR-009`, `ORF-SCR-017`, `ORF-SCR-021`, `ORF-SCR-022` et
  `ORF-SCR-027` : zones/objets, mouvement, math/chaînes, variables locales et sandbox R013 sur
  l’autorité UX6-F, 18/18 tests, portail incrémental en 197,50 s et capture Forward+ FHD inspectée.
- [x] Réaliser UX6-J sur `ORF-SCR-028`, `ORF-SCR-029`, `ORF-SCR-030`, `ORF-SCR-031` et
  `ORF-SCR-036` : handles opaques, quotas d’instructions et mémoire, replay déterministe et version
  d’API via une politique R014 versionnée, 27/27 tests, portail incrémental final en 156,85 s et
  capture Forward+ FHD corrigée puis inspectée.
- [x] Réaliser UX6-K sur `ORF-SCR-039` : allowlist ForgeScript du module, kits, liaisons,
  deny-by-default, autorité serveur et signature via le manifeste R015 versionné, 14/14 tests,
  portail incrémental final en 159,24 s et trois captures rendues inspectées.
- [x] Réaliser UX6-L sur `ORF-SCR-019`, `ORF-SCR-020` et `ORF-SCR-023` : audio/musique,
  horloge/timers et événements custom R099 sur l’autorité UX6-F, 15/15 tests, portail incrémental
  final en 156,29 s et trois captures Forward+ FHD inspectées.
- [x] Réaliser UX6-M sur `ORF-SCR-018`, `ORF-SCR-024`, `ORF-SCR-025` et `ORF-SCR-026` : caméra et
  cutscenes, routage par tags, switches module et IA dans `ScriptAutomationProfile`, 60/60 tests,
  portail incrémental en 162,07 s et quatre captures Forward+ FHD inspectées.
- [x] Réaliser UX6-N sur `ORF-SCR-032` et `ORF-SCR-033` : hot reload et débogueur R099 dans la
  même source/historique UX6-F, 28/28 tests, portail incrémental en 131,98 s et deux captures
  Forward+ FHD inspectées.
- [x] Réaliser UX6-O sur `ORF-SCR-034`, `ORF-SCR-035`, `ORF-SCR-037`, `ORF-SCR-038` et
  `ORF-SCR-040` : tests unitaires, profilage, documentation générée, analyse statique et état dans
  un profil qualité R100 versionné, 16/16 tests, portail incrémental en 135,51 s et cinq captures
  Forward+ FHD inspectées.
- [x] Réaliser UX6-P sur `ORF-SCR-041` : interop éditeur/MCP R101 avec profil versionné, plans
  source et liaison, snapshot exact, sandbox, quotas/récupération et adaptateur runtime, 10/10 tests,
  portail incrémental en 135,87 s et trois captures Forward+ FHD inspectées.
- [x] Réaliser UX6-Q sur `ORF-EE-031` et `ORF-AI-018` : compilation incrémentale/cache/invalidation
  et création assistée locale d’une source `.as` gouvernée, deux transactions canoniques, 16/16
  tests, portail incrémental en 137,50 s et deux captures Forward+ FHD inspectées.
- [x] Réaliser UX6-R sur `ORF-EE-011`, `ORF-EE-012`, `ORF-EE-013`, `ORF-EE-015` et
  `ORF-EE-016` : règles data-driven, interface et événements typés, JSON révisionné et politique de
  snapshot sous la commande canonique des extensions, 24/24 tests, portail incrémental en 140,90 s
  et quatre captures Forward+ FHD inspectées.
- [x] Réaliser UX6-S sur `ORF-EE-014`, `ORF-EE-017`, `ORF-EE-020` et `ORF-EE-021` : requêtes de
  périphérique gouvernées, ressources VM bornées, contrôle de groupe et progression data-driven
  sous la commande canonique des extensions, 19/19 tests, portail incrémental en 140,55 s et quatre
  captures Forward+ FHD inspectées.
- [x] Réaliser UX6-T sur `ORF-EE-022` et `ORF-EE-024` : indicateurs AOE complets et capacités NUI
  2.0 sous la commande canonique des extensions, 15/15 tests, portail incrémental en 147,77 s et
  deux captures Forward+ FHD inspectées.
- [x] Réaliser UX6-U sur `ORF-GAME-022` : définition persistante bornée et blackboard copié avec
  handles typés, mutation, debug, réplication, snapshot/reload et frontière legacy, 56/56 tests,
  portail incrémental en 144,09 s et capture Forward+ FHD inspectée.
- [x] Réaliser UX6-V sur `ORF-EE-028` : configuration des profils d’agent et laboratoire A* copié
  avec obstacles, coûts, récupération, budget, mise à jour partielle et plan EngineAdapter,
  10/10 tests, portail incrémental en 144,21 s et capture Forward+ FHD inspectée.
- [x] Réaliser UX6-W sur `ORF-EE-001` et `ORF-EE-027` : overrides hiérarchiques et tweens
  Transform, Paramètre matériau et État sous la commande cinématique canonique, 10/10 tests,
  portail incrémental final en 146,60 s et trois captures Forward+ FHD corrigées puis inspectées.
- [x] Réaliser UX6-X sur `ORF-GAME-026` et `ORF-EE-026` : shell carte/journal/HUD dans le projet,
  police et échelles dans le profil utilisateur, deux transactions explicites avec rollback,
  22/22 tests, portail incrémental final en 162,88 s, smoke global et deux captures Forward+ FHD
  corrigées puis inspectées.
- [x] Réaliser UX6-Y sur `ORF-GAME-018` et `ORF-EE-023` : politique météo autoritaire et
  streaming de zone avec cache, dépendances, progression, annulation et benchmark dans deux
  commandes canoniques, 33/33 tests, portail incrémental final en 163,69 s, smoke global et deux
  captures Forward+ FHD inspectées.
- [x] Réaliser UX6-Z sur `ORF-AI-020` : création assistée locale d'une capacité, cinq scénarios
  de combat, équilibrage, approbation, dry-run, application canonique, undo/redo et persistance,
  6/6 tests, portail incrémental en 146,31 s, smoke global et capture Forward+ FHD inspectée.
- [x] Réaliser UX7-A sur `ORF-AREA-037`, `ORF-AUD-020`, `ORF-QA-001` à `ORF-QA-007` et
  `ORF-QA-021` : atelier de validation canonique à diagnostics humains cliquables, correctifs
  prévisualisés et commande existante, avec 33/33 tests, portail en 300,73 s, smoke global et
  capture Forward+ FHD inspectée.
- [x] Réaliser UX7-B sur `ORF-QA-008`, `ORF-QA-010`, `ORF-QA-015` et `ORF-QA-020` : laboratoire
  isolé et annulable, résultats navigables, replay headless, sauvegardes réelles temporaires et
  logs structurés confidentiels, avec 25/25 tests, portail en 245,20 s, smoke global et capture
  Forward+ FHD corrigée puis inspectée.
- [x] Réaliser UX7-I sur `ORF-EE-029` : profil d'observabilité transactionnel, vraie trace
  OpenTelemetry confinée Editor/Runtime/Serveur/ForgeScript, diagnostic Tracy natif honnête,
  annulation et résultats navigables, avec 14/14 tests, portail en 420,22 s, smoke global et
  capture Forward+ FHD corrigée puis inspectée ; UX7 est close à 43/43.
- [x] Réaliser UX8-F sur `ORF-BUILD-022`, `ORF-BUILD-023`, `ORF-CONT-030` et `ORF-CONT-031` :
  approbation de dépendance transactionnelle, notice déterministe, crédits/provenances et frontière
  IP, avec 20/20 tests, portail en 534,72 s, smoke global et capture Forward+ FHD inspectée.
- [x] Réaliser UX8-G sur `ORF-PROJ-020` et `ORF-CONT-001`, `ORF-CONT-002`, `ORF-CONT-003`,
  `ORF-CONT-007` : catalogue versionné, priorité, dépendances et compilation en mémoire avec
  transaction canonique, 47/47 tests, portail en 544,55 s, smoke global et capture Forward+ FHD
  corrigée puis inspectée.
- [x] Réaliser UX8-H sur `ORF-PROJ-024`, `ORF-BUILD-016`, `ORF-BUILD-017`, `ORF-CONT-028` et
  `ORF-CONT-029` : cinq cibles, trois plateformes, packaging Windows, signatures publiques et
  deltas sous commande canonique, avec vérification réelle isolée, 22/22 tests, portail en 422,07 s,
  smoke global et capture Forward+ FHD inspectée.
- [x] Réaliser UX8-I sur `ORF-PROJ-025` et `ORF-CONT-025` : profil de compatibilité transactionnel,
  migration versionnée en dry-run et résolution réelle des variantes d’assets par plateforme avec
  budgets, rôles et SHA-256, 64/64 tests, portail en 423,41 s, smoke global et capture Forward+ FHD
  inspectée.
- [x] Réaliser UX8-J sur `ORF-AI-023` et `ORF-AI-032` : politique worker confinée, valeurs
  dangereuses verrouillées, consentement explicite et sonde anti-injection avec refus d’outil et
  d’escalade, 35/35 tests, portail en 472,65 s, smoke global et capture Forward+ FHD inspectée.
- [x] Réaliser UX8-K sur `ORF-BUILD-007` : import/export en masse confinés au workspace,
  consentement explicite, staging SHA-256, collisions déterministes, rollback/compensation,
  commande canonique, undo/redo et persistance, 51/51 tests, portail en 423,12 s, smoke global et
  capture Forward+ FHD corrigée puis inspectée.
- [x] Réaliser UX8-L sur `ORF-CONT-009` et `ORF-EE-025` : politique de synchronisation et
  métadonnées NWSync transactionnelles, manifeste SHA-256, confiance/licences, cache local jetable,
  reprise et déduplication, 61/61 tests, portail en 419,84 s, smoke global et capture Forward+ FHD
  inspectée, sans réseau ni serveur NWSync lancé par Studio.
- [x] Réaliser UX8-M sur `ORF-MP-003` à `ORF-MP-006` : découverte, lobby, création et autorité des
  personnages dans un profil workspace atomique, commande réseau canonique, dry-run, undo/redo,
  save/reopen et préservation du combat, avec 9/9 tests, portail en 443,52 s, smoke global et quatre
  captures Forward+ FHD corrigées puis inspectées, sans transport ni serveur lancé par Studio.
- [x] Réaliser UX8-N sur `ORF-CONT-010`, `ORF-CONT-019`, `ORF-CONT-026` et `ORF-CONT-027` :
  politique de packs curatés, namespaces canoniques, migration hôte versionnée et désinstallation
  sûre avec dry-run, consentement, gardes stale, undo/redo, save/reopen, backups et restauration,
  avec 22/22 tests, portail en 581,21 s, smoke global, quatre captures Forward+ FHD inspectées et
  graphe Roslyn à 11 234 nœuds / 86 636 relations sans diagnostic ; UX8 est close à 51/51.
- [x] Réaliser UX9-A sur `ORF-MP-002`, `ORF-MP-007`, `ORF-MP-011` et `ORF-MP-012` : profil de
  fondation réseau persistant, session autoritaire locale, groupes, réplication, filtrage d’intérêt,
  permission, confirmation, audit et déconnexion, avec 17/17 tests, portail en 643,09 s, smoke
  global, capture Forward+ FHD corrigée puis inspectée et graphe Roslyn à 11 253 nœuds / 86 841
  relations sans diagnostic ; `ORF-MP-001` reste planifié faute de transport durable.
- [x] Réaliser UX9-B sur `ORF-MP-013`, `ORF-MP-015`, `ORF-MP-017`, `ORF-MP-020` et `ORF-MP-027` :
  profil workspace atomique, synchronisation reprise et vérifiée, verrou strict, grants runtime,
  chat structuré et abonnements événementiels, avec permission, confirmation, audit, dry-run,
  gardes stale, undo/redo, save/reopen et libération des caches temporaires ; 25/25 tests, portail
  en 532,65 s, smoke global, cinq captures Forward+ FHD inspectées et graphe Roslyn à 11 276 nœuds /
  87 071 relations sans diagnostic portent UX9 à 9/68.
- [x] Réaliser UX9-C sur `ORF-MP-009`, `ORF-MP-010`, `ORF-MP-016`, `ORF-MP-018` et `ORF-MP-019` :
  profil workspace atomique pour instances, zones dynamiques, comptes, administration et modération,
  avec permission, confirmation, dry-run, gardes stale, undo/redo, save/reopen, API/CLI MCP et cinq
  sondes runtime locales ; 17/17 tests, portail en 548,58 s, smoke global, cinq captures Forward+ FHD
  inspectées et graphe Roslyn à 11 299 nœuds / 87 300 relations sans diagnostic portent UX9 à 14/68.
- [x] Réaliser UX9-D sur `ORF-MP-022`, `ORF-MP-023`, `ORF-MP-028`, `ORF-MP-030` et `ORF-MP-032` :
  profil workspace atomique pour snapshots, SQL sécurisé, métriques/health, profilage et reconnexion,
  avec permission Administrateur, confirmation, dry-run, gardes stale, undo/redo, save/reopen et
  cinq sondes runtime locales ; 12/12 tests, portail en 534,22 s, smoke global, cinq captures
  Forward+ FHD inspectées et graphe Roslyn final sans diagnostic portent UX9 à 19/68.
- [x] Réaliser UX9-E sur `ORF-MP-033` et `ORF-MP-034` : profil workspace atomique pour snapshots,
  WAL, répétition de rollback, migrations, PITR, rétention, capacité, slots réservés et file FIFO,
  avec permission Administrateur, confirmation distincte, dry-run, gardes stale, undo/redo,
  save/reopen et sondes runtime locales ; 11/11 tests, portail en 546,18 s, smoke global, deux
  captures Forward+ FHD inspectées et graphe Roslyn final sans diagnostic portent UX9 à 21/68.
- [x] Réaliser UX9-F sur `ORF-DM-001`, `ORF-DM-003`, `ORF-DM-004`, `ORF-DM-005` et
  `ORF-DM-006` : profil workspace atomique et trois vues directes pour client MFA révocable,
  Creator filtré, spawn spatial compensable, Chooser privé et focus spectateur en lecture seule,
  avec permission, confirmation, dry-run, gardes stale, undo/redo, save/reopen et sandbox runtime
  locale ; 12/12 tests ciblés, 54/54 régressions Live Director, portail en 549,52 s, smoke global,
  trois captures Forward+ FHD inspectées et graphe Roslyn à 11 367 nœuds / 87 994 relations sans
  diagnostic portent UX9 à 26/68.
- [x] Réaliser UX9-G sur `ORF-DM-007`, `ORF-DM-008`, `ORF-DM-009`, `ORF-DM-010` et
  `ORF-DM-012` : profil workspace atomique et trois vues directes pour téléportation avec
  orientation, destruction à retour borné, soin, limbo et possession expirante, avec permission,
  confirmation, dry-run, gardes stale, undo/redo, save/reopen, compensation et restitution runtime ;
  18/18 tests ciblés, 56/56 régressions Live Director, portail en 514,28 s, smoke global, trois
  captures Forward+ FHD corrigées puis inspectées et graphe Roslyn à 11 391 nœuds / 88 214
  relations sans diagnostic portent UX9 à 31/68.
- [x] Réaliser UX9-H sur `ORF-DM-013`, `ORF-DM-014`, `ORF-DM-016`, `ORF-DM-017` et
  `ORF-DM-019` : profil workspace atomique et trois vues directes pour incarnation divulguée,
  protection GM expirante, or/expérience persistants et compensables, puis portes avec collision
  et portail, avec permission, confirmation, dry-run, gardes stale, undo/redo, save/reopen et
  restitution runtime ; 18/18 tests ciblés, 58/58 régressions Live Director, portail en 512,27 s,
  smoke global, trois captures Forward+ FHD corrigées puis inspectées et graphe Roslyn à 11 416
  nœuds / 88 452 relations sans diagnostic portent UX9 à 36/68.
- [x] Réaliser UX9-I sur `ORF-DM-020`, `ORF-DM-021`, `ORF-DM-023`, `ORF-DM-024` et
  `ORF-DM-026` : profil workspace atomique et trois vues directes pour triggers, sélection multiple,
  déplacement et attaque de groupe, puis voyage vers un joueur, avec permission, confirmation,
  consentement du groupe, dry-run, gardes stale, undo/redo, save/reopen et restitution runtime ;
  13/13 tests ciblés, 58/58 régressions Live Director, portail en 517,61 s, smoke global, trois
  captures Forward+ FHD inspectées et graphe Roslyn à 11 438 nœuds / 88 669 relations sans
  diagnostic portent UX9 à 41/68.
- [x] Réaliser UX9-J sur `ORF-DM-027`, `ORF-DM-028`, `ORF-DM-029`, `ORF-DM-032` et
  `ORF-DM-033` : profil workspace atomique et trois vues directes pour recherche avec focus,
  ForgeScript borné, variables typées, scènes préparées et visibilité MJ, avec permission,
  confirmation, dry-run, gardes stale, undo/redo, save/reopen, rollback et restitution runtime ;
  16/16 tests ciblés, 58/58 régressions Live Director, portail en 519,91 s, smoke global, trois
  captures Forward+ FHD inspectées et graphe Roslyn à 11 461 nœuds / 88 901 relations sans
  diagnostic portent UX9 à 46/68.
- [x] Réaliser UX9-K sur `ORF-DM-035` et `ORF-DM-036` : profil workspace atomique et deux workflows
  directs pour transition de quête tous joueurs ou sélection avec conséquences et projection RPG,
  puis inspection et commandes de rencontre `activate/pause/wave/scale/despawn`, avec permission,
  confirmation, dry-run, gardes stale, undo/redo, save/reopen, rollback et restitution runtime ;
  10/10 tests ciblés, 58/58 régressions Live Director, portail en 525,88 s, smoke global, trois
  captures Forward+ FHD inspectées et graphe Roslyn à 11 484 nœuds / 89 114 relations sans
  diagnostic portent UX9 à 48/68.
- [x] Réaliser UX9-L sur `ORF-DM-002`, `ORF-DM-011`, `ORF-DM-025`, `ORF-DM-031` et
  `ORF-DM-034` : surface workspace atomique pour authentification MFA, inspection en lecture seule,
  liste joueurs, audit immuable et chat MJ, avec permission, confirmation, dry-run, gardes stale,
  undo/redo, save/reopen, handles ForgeScript et sandbox synthétique libérée ; 13/13 tests ciblés,
  58/58 régressions Live Director, portail en 503,26 s, smoke global, capture Forward+ FHD corrigée
  puis inspectée et graphe Roslyn à 11 502 nœuds / 89 296 relations sans diagnostic portent UX9 à
  53/68.
- [x] Réaliser UX9-M sur `ORF-DM-015`, `ORF-DM-018`, `ORF-DM-022`, `ORF-DM-030` et
  `ORF-DM-037` : surface directe adossée au profil canonique du module pour repos forcé, niveau,
  apparence, groupe sauvegardé et enregistrement expurgé, avec permission, confirmation, motif,
  dry-run, gardes stale, undo/redo, save/reopen et préservation des profils voisins ; 7/7 tests
  ciblés, 60/60 régressions Live Director, portail en 507,90 s, smoke global, capture Forward+ FHD
  corrigée puis inspectée et graphe Roslyn frais sans diagnostic portent UX9 à 58/68.
- [x] Réaliser UX9-N sur `ORF-MP-014`, `ORF-MP-021`, `ORF-MP-024`, `ORF-MP-025`,
  `ORF-MP-026` et `ORF-MP-035` : surface directe adossée aux profils canoniques du module pour
  contenu curaté, voix, shard, hot reload, extensions et protocole cross-platform, avec permission,
  confirmation, dry-run, gardes stale, undo/redo, save/reopen et préservation des profils voisins ;
  8/8 tests ciblés, 99/99 régressions réseau, portail en 507,54 s, smoke global, capture Forward+
  FHD inspectée et graphe Roslyn frais sans diagnostic portent UX9 à 64/68.
- [x] Réaliser UX9-O sur `ORF-MP-001`, `ORF-MP-008`, `ORF-MP-029` et `ORF-MP-031` : surface
  directe coordonnant les profils canoniques de fondation, expérience joueur et assurance pour
  serveur dédié, PvP/tir ami, logs structurés et anti-triche, avec permission, confirmation, trois
  dry-runs, approbations liées aux sources, compensation, undo/redo, sauvegarde/réouverture et
  préservation sémantique des voisins ; 37/37 tests ciblés, 99/99 régressions réseau, 385/385
  régressions ModuleFormat, portail en 515,32 s, smoke global, capture Forward+ FHD corrigée puis
  inspectée et graphe Roslyn sans diagnostic closent UX9 à 68/68.
- [x] Réaliser UX10-A à UX10-D sur les 17 routes Assistant restantes : surface humaine unifiée,
  contexte et outils bornés, fournisseurs locaux consentis, budgets/confidentialité, plans et
  dry-runs, transaction atomique et diff, autorisations, audit, prompts et résultats structurés,
  hors ligne, Blender typé, text/image-to-3D déterministe et cohérence narrative. Le build Studio,
  66/66 tests ciblés, le smoke global et quatre captures Forward+ FHD corrigées puis inspectées
  passent ; le portail en 524,12 s et le graphe Roslyn à 11 566 nœuds / 90 143 relations sans
  diagnostic portent la matrice Studio à 717/717 `functional`, sans commit ni publication.
- [x] Clôturer le jalon UX6-E à UX6-G par le portail transversal complet : 2 035 tests .NET,
  39 tests Python et smokes Studio, Toolset, Runtime/Forward+, Forgotten Forest, Blender, MCP,
  réseau, sauvegarde/reprise et distribution.
- [x] Clôturer le jalon UX6-A à UX6-D par le portail transversal complet : 2 035 tests .NET,
  39 tests Python et smokes Studio, Toolset, Runtime, Forward+, MCP, réseau et distribution.
- [x] Activer **Tester ici** sur snapshot isolé depuis la tranche Tester, sans élargir rétroactivement
  les claims runtime des aperçus UX2 ; le retour restaure le contexte Studio.
- [x] Migrer Contenu, Histoire, Gameplay, Tester, Publier, Projet et Assistant page par page, puis
  porter les 66 lignes historiques à `Parity` avec tests, smoke et preuve visuelle transversale.
- [x] Qualifier les 36 pages du Studio en 1920×1080 à 100 %, 2560×1440 à 150 % et 3840×2160 à
  200 %, sans critère de support sous le Full HD ; la seconde passe corrective couvre 108 captures.
- [x] Exporter et qualifier le Studio Windows x64 : manifeste, conformité 38/38 et smoke autonome
  de retouche, natif, UX3-F et UX5 depuis l'exécutable distribué.
- [x] Exporter le Studio Linux x64 et vérifier ELF, assembly C#, workspace, manifeste, hashes et
  conformité 38/38 sur le poste Windows.
- [x] Rejouer le parcours de retouche et le smoke depuis l'archive Linux exacte sur Debian 13 x86_64,
  sans éditeur Godot, puis fermer `STUDIO-043`.
- [ ] Basculer le lanceur Creator et retirer le plugin historique uniquement après fermeture de
  `STUDIO-043` et accord utilisateur explicite.

## C13 — Ruleset RPG systémique et Crypte des Cendres

- [x] Établir un ruleset clean-room canonique couvrant action directe, groupe, exploration libre,
  progression, magie systémique, furtivité, quêtes, factions, IA, persistance et monde comme arme.
- [x] Ajouter un manifeste versionné propre à la Crypte avec huile, eau, brasero, pilier et pointes.
- [x] Brancher les dégâts environnementaux sur le chemin de combat autoritaire existant.
- [x] Préserver le parcours `Elyra → Clé → porte → Sentinelle → Gardien → sortie`.
- [x] Remplacer la zone 20 × 28 m par une zone 48 × 76 m avec sept espaces et une frontière physique.
- [x] Recomposer la couche KayKit Dungeon Remastered CC0 selon la nouvelle géométrie.
- [x] Repositionner les cinq interactions systémiques et borner leurs prompts à 7,5 m.
- [x] Ajouter les contrats statiques de ruleset et de reconstruction du donjon.
- [x] Remplacer les personnages de la tranche par quatre modèles 3D KayKit CC0 gouvernés.
- [x] Ajouter un smoke natif du mouvement, des passages critiques, de la caméra et de la vue subjective.
- [ ] Remplacer ensuite les colliders de sécurité par des volumes entièrement éditables dans le Studio.
- [ ] Généraliser masse, stabilité, surfaces, substances, supports et causalité dans Domain/Application.
- [ ] Exposer les propriétés systémiques dans le Studio avec validation, aperçu, undo/redo et diagnostics.
- [ ] Ajouter compagnons, témoins, réputation multidimensionnelle et solutions systémiques aux futurs modules.

## Décisions externes avant release publique

- [ ] Choisir et ajouter la licence du code du dépôt.
- [ ] Fournir un certificat Authenticode et signer les exécutables.
- [ ] Rejouer la revue physique multi-écrans et un fournisseur IA réel.
- [ ] Autoriser explicitement un tag et une GitHub Release.
- [ ] Corriger **Billing & plans** puis relancer les deux workflows du commit final.

AV8–AV10 restent postérieures à ce candidat : Vault complet, SQLite, KTX2, surveillance temps réel,
curation de masse et finition artistique exhaustive. Elles ne rouvrent pas la consolidation actuelle.
