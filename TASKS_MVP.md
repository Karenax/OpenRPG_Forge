# File active — consolidation OpenRPG Forge

Dernière mise à jour : 16 août 2026.

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
- [ ] Achever la tranche Monde : terrain, navigation, puis activer **Tester ici** sur
  snapshot isolé.
- [ ] Migrer Contenu, Histoire, Gameplay, Tester, Publier, Projet et Assistant page par page, en
  passant chaque ligne de la matrice à `Parity` uniquement avec tests et preuve visuelle.
- [ ] Qualifier toutes les pages du Studio en 1920×1080, 2560×1440 et 3840×2160, sans conserver de
  critère de support pour les résolutions inférieures au Full HD.
- [ ] Exporter et qualifier le Studio sur Windows et Linux, basculer le lanceur Creator, puis retirer
  le plugin historique seulement après parité des 66 lignes et accord utilisateur.

## Décisions externes avant release publique

- [ ] Choisir et ajouter la licence du code du dépôt.
- [ ] Fournir un certificat Authenticode et signer les exécutables.
- [ ] Rejouer la revue physique multi-écrans et un fournisseur IA réel.
- [ ] Autoriser explicitement un tag et une GitHub Release.
- [ ] Corriger **Billing & plans** puis relancer les deux workflows du commit final.

AV8–AV10 restent postérieures à ce candidat : Vault complet, SQLite, KTX2, surveillance temps réel,
curation de masse et finition artistique exhaustive. Elles ne rouvrent pas la consolidation actuelle.
