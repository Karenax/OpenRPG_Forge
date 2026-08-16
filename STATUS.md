# État vérifié — OpenRPG Forge

Dernière mise à jour : 16 août 2026.

## Verdict

OpenRPG Forge est désormais un prototype RPG 3D consolidé, installable et utilisable pour créer,
tester et exporter des modules. Les familles Aurora sont closes ; le travail courant porte sur la
qualification de livraison, pas sur l'ajout de nouvelles fonctions.

La phase consolidée couvre l’édition directe, la qualité visuelle, le lanceur configurable et
l’interface de combat. Elle a franchi le portail complet et constitue une baseline cohérente. Une
migration distincte vers le Creator autonome `Forge.Studio` est engagée : sa fondation est
fonctionnelle et visuellement contrôlée, mais sa parité fonctionnelle et sa distribution ne sont pas
encore qualifiées. Aucun de ces états ne doit être confondu avec une release publique signée.

## Produit disponible

- Creator historique et runtime 3D séparés, exclusivement présentés sous la marque OpenRPG Forge ;
- fondation `Forge.Studio` autonome : Module Hub, une fenêtre, huit rubriques, trente-six pages,
  ouverture de Forgotten Forest, navigation, vue 3D centrale et première retouche transactionnelle ;
- neuf espaces du Creator historique, mode Débutant à quatre espaces et outils spécialisés intégrés ;
- projets, zones, blueprints, assets gouvernés, dialogues, quêtes, scripts et contenu RPG ;
- validation, correctifs prévisualisés, undo/redo, playtest, sauvegarde/reprise et export `.orpmod` ;
- Assistant et host MCP transactionnels, bornés, facultatifs et sans shell arbitraire ;
- distributions Windows x64 autonomes du Creator et du Runtime ;
- lanceur Windows configurable par jeu avec branding, réglages persistants et hébergement local autoritaire ;
- installateur/désinstallateur NSIS avec contrôle de mise à jour et préservation des données utilisateur ;
- Crypte des Cendres et Forgotten Forest jouables en Forward+ avec modèles 3D gouvernés ;
- interface de combat runtime autoritaire, animée, accessible et bornée par des pools stables.

## Couverture et preuves

- 717/717 fonctions au statut `Tested`, réparties dans 187/187 lots clos ;
- P0 : 196/196, P1 : 298/298, P2 : 190/190, P3/P4 : 33/33 ;
- 5 461 chemins de preuve Aurora contrôlés sans chemin absent ;
- parcours produit automatisé : projet, zones, assets, contenu RPG, narration, validation, playtest,
  export et Assistant gouverné ;
- matrice et procédure d'acceptation de bout en bout sous `docs/acceptance/`.
- matrice d’accès humain Studio : 717 routes uniques, dont 65 `functional` avec preuve spécifique
  pour UX0-A à UX1-U et 652 `planned` ; cette matrice ne réutilise pas le statut `Tested` de l’ancien Creator.
- reprise préparée sous forme de train parallèle contrôlé : un lot Placeables et un lot Zones dans
  des worktrees distincts, un audit en avance, puis intégration et validations exclusivement
  séquentielles ; aucune route postérieure à UX1-U n'est encore promue par cette préparation.
- portail local courant réussi avec 1 972 tests .NET, 39 tests Python, un build sans avertissement et
  le graphe Roslyn courant déterministe à 8 877 nœuds, 58 036 relations et 0 diagnostic.

Ces nombres prouvent la présence et la non-régression des capacités. Ils ne remplacent pas une
qualification humaine de release publique.

## Consolidation réalisée

- ADR 0045 accepté, contrat d'acceptation Studio et matrice exhaustive des 66 anciennes `Window`
  vers les 36 pages ; le plugin est conservé jusqu'à parité prouvée ;
- coque Godot .NET autonome sans `EditorPlugin` : reprise du module et de la zone, navigation
  persistée, Module Hub, aperçu réel des 218 objets de l'Auberge du Cerf d'Ambre, 211 modèles
  gouvernés en Forward+, sélection 3D et inspecteur unique ;
- UX0-A qualifié : Explorateur compact synchronisé avec la 3D, filtres enregistrables, recherche
  globale `Ctrl+K`, références inverses et aide contextuelle sourcée ; smoke dédié, preuves FHD/QHD
  et chargement Forward+ de Forgotten Forest sans fallback ni erreur d’asset ;
- UX0-B qualifié : dispositions et profils nommés, trois onglets de zone avec contexte restauré,
  comparaison Forward+ en lecture seule et quatorze raccourcis configurables avec conflit, dry-run,
  undo/redo et sauvegarde explicite ; preuves FHD/QHD et préférences locales préservées ;
- UX0-C qualifié : six modes de viewport réellement distincts, sélection simple/multiple avec
  cadrage et verrouillage, caméra configurable avec six vues et bookmarks, gizmos translation/
  rotation/échelle avec repères, pivots, snaps et alignements, puis undo/redo projet/workspace/caméra ;
  preuves Forward+ FHD/QHD avec 291 entités, 278 assets et zéro fallback ;
- UX0-D qualifié : palette contextuelle à sept vues, favoris et palettes nommées, filtres précis,
  placement ligne/grille/dispersion/brosse avec fantômes verts ou rouges et correction avant mutation,
  puis copier/couper/coller/dupliquer en groupe avec choix instance ou copie indépendante ; preuves
  Forward+ FHD/QHD avec 291 entités, 278 assets et zéro fallback ;
- UX0-E qualifié : assistant de création en cinq étapes avec aperçu sans écriture, métadonnées
  localisées, identité technique stable et cachée, obligation de première zone pour les RPG, support
  réel d’un projet Core sans zone et point de départ nommé avec waypoint facultatif ; les choix
  personnalisés sont conservés sans clé technique visible ; commande, undo/redo, sauvegarde/réouverture
  et preuves Forward+ FHD/QHD ;
- UX0-F qualifié : cinq cartes d’événements, vingt liaisons de cycle de vie et de gameplay, recherche
  humaine de scripts ForgeScript, validation des fichiers et handlers, contexte runtime
  Éditeur/Solo/Client/Serveur, autorité serveur, dry-run, undo/redo, sauvegarde/réouverture et format
  projet `0.6.35` ; preuves runtime, headless et Forward+ FHD/QHD ;
- UX0-G qualifié : quatre cartes supplémentaires pour équipement/progression, repos, heartbeat et
  signaux utilisateur typés ; ajout/retrait par noms humains, cadence/budget bornés, scripts requis,
  dry-run, undo/redo, sauvegarde/réouverture et format projet `0.6.36` ; preuves runtime, headless et
  Forward+ FHD/QHD, avec correction visuelle de l’aide comprimée au premier passage ;
- UX0-H qualifié : abandon de cinématique avec restauration et ForgeScript réellement exécuté,
  calendrier configurable avec date, vitesse, pause, fuseaux de zone et repères astronomiques, puis
  courbe de progression par groupe, difficulté et sources nommées ; transaction unique, diagnostics
  humains, préservation de la séquence de démarrage, round-trip et preuves Forward+ exactes en
  1920×1080, 2560×1440 et 3840×2160 ;
- UX0-I qualifié : variables globales typées sur cinq portées, création/édition par noms humains,
  cache ForgeScript local compilé et invalidé par source/API/compilateur, puis huit pages localisées
  de guide joueur directement accessibles ; les profils avancés restent préservés, les trois voies
  passent par leurs commandes canoniques avec dry-run/undo/redo et le round-trip est exact ;
- UX0-J qualifié : renommage sûr depuis la recherche globale avec aperçu obligatoire, révision
  revalidée, identité stable, migration de références et historiques contenu/ressource, puis page
  Validation alimentée par les diagnostics canoniques, liens profonds, correctifs prévisualisés,
  undo/redo, activité bornée et bundle local déterministe expurgé ;
- UX1-A qualifié : assistant de zone en trois étapes depuis la page Monde ou `Ctrl+K`, gabarits vide
  et jouable, dimensions et profils de fondation nommés, aperçu `CreateAreaHandler` sans écriture,
  application dans l’historique projet, onglet actif, undo/redo et document indépendant vérifié après
  sauvegarde/réouverture ; preuves Forward+ exactes FHD/QHD/4K avec échelle responsive ;
- UX1-B qualifié : dimensions et copie accessibles dans un overlay humain et via `Ctrl+K`, aperçu
  obligatoire, objets hors limites replacés ou retirés explicitement, copie complète ou gabarit,
  identités internes remappées, source et voisines préservées, undo/redo et documents vérifiés après
  sauvegarde/réouverture ; preuves Forward+ exactes FHD/QHD/4K avec échelle responsive ;
- UX1-C qualifié : écran de chargement accessible depuis Zones et `Ctrl+K`, médias choisis par noms
  humains, astuces françaises/anglaises, progression et préchargement réels, aperçu normal et
  mouvements réduits, fallback accessible, durée minimale, préservation des autres profils,
  undo/redo et round-trip exact ; preuves Forward+ FHD/QHD/4K et smoke de streaming réel ;
- UX1-D qualifié : calques et groupes accessibles depuis Zones et `Ctrl+K`, arbre déplaçable,
  visibilité et verrouillage hérités, responsables, affectation et sélection 3D par noms humains,
  export partiel gouverné, contrat runtime, undo/redo et round-trip exact ; preuves Forward+
  FHD/QHD/4K et smoke contractuel sans écriture de Forgotten Forest ;
- UX1-E qualifié pour `ORF-PLC-001` : blueprint de décor accessible depuis Objets & interactions
  et `Ctrl+K`, ressource choisie par nom humain et usage réel, collision, rôle statique ou utilisable,
  interaction, transform indépendant du moteur et scripts ; aperçu canonique, sélection 3D,
  préservation des catalogues voisins, undo/redo et round-trip exact ; preuves Forward+ FHD/QHD/4K
  et smoke contractuel sans écriture de Forgotten Forest ;
- UX1-F qualifié pour `ORF-PLC-002` : accès direct au décor statique depuis Objets & interactions et
  `Ctrl+K`, regroupement, instanciation, occlusion, fusion avec garde collision et collision globale
  optionnelle ; aperçu d’impact, sélection 3D, parité commande/UI, préservation des données masquées,
  undo/redo et round-trip exact ; preuves Forward+ FHD/QHD/4K et smoke contractuel sans écriture de
  Forgotten Forest ;
- UX1-G qualifié pour `ORF-PLC-003` : accès direct à l’objet utilisable depuis Objets & interactions
  et `Ctrl+K`, cartes d’actions ajoutables, portée, angle, durée, focus, autorité, conditions et
  `OnUsed` gouverné ; aperçu complet, sélection 3D, parité commande/UI, événement runtime typé,
  préservation des données masquées, undo/redo et round-trip exact ; preuves Forward+ FHD/QHD/4K et
  smoke contractuel sans écriture de Forgotten Forest ;
- UX1-H qualifié pour `ORF-PLC-004` : accès direct au conteneur depuis Objets & interactions et
  `Ctrl+K`, capacité, masse, propriétaire, droits, persistance, réplication, inventaire, butin et
  présentation ; aperçu canonique, parité commande/UI, projection RPG Kit, lecture ForgeScript,
  préservation des capacités voisines et références supplémentaires, undo/redo et round-trip exact ;
  preuves Forward+ FHD/QHD/4K et smoke contractuel sans écriture de Forgotten Forest ;
- UX1-I qualifié pour `ORF-PLC-005` : accès direct à l’identité et à l’apparence depuis Objets &
  interactions et `Ctrl+K`, nom court, noms/invitations localisés, tags, notes, asset, meshes,
  matériaux, icône, portrait et VFX ; aperçu canonique, parité commande/UI, projection RPG Kit,
  lecture ForgeScript/MCP, préservation, undo/redo et round-trip exact ; preuves Forward+
  FHD/QHD/4K et smoke contractuel sans écriture de Forgotten Forest ;
- UX1-J qualifié pour `ORF-PLC-006` : transformation précise depuis Objets & interactions et
  `Ctrl+K`, position, rotation, échelle, pivot, grille, parent humain, surface et animation ; aperçu
  canonique, hiérarchie et géométrie validées, parité commande/UI, projection RPG Kit, lecture
  ForgeScript/MCP, préservation, undo/redo et round-trip exact ; preuves Forward+ FHD/QHD/4K et
  smoke contractuel sans écriture de Forgotten Forest ;
- UX1-K qualifié pour `ORF-PLC-007` : animation initiale depuis Objets & interactions et `Ctrl+K`,
  six états humains, animation, apparence et interaction par état, transitions ajoutables avec
  déclencheur, durée et condition, puis aperçu sémantique ; validation canonique, projection et
  transition RPG Kit, lecture ForgeScript/MCP, préservation, undo/redo et round-trip exact ; preuves
  Forward+ FHD/QHD/4K et smoke contractuel sans écriture de Forgotten Forest ;
- UX1-L qualifié pour `ORF-PLC-008` : destructibilité depuis Objets & interactions et `Ctrl+K`,
  santé, armure, résistances typées, étapes visuelles, effet de destruction, débris, réparation et
  aperçu local de dégâts ; validation canonique, projection puis dégâts/destruction/réparation RPG
  Kit, lecture ForgeScript/MCP, préservation, undo/redo et round-trip exact ; preuves Forward+
  FHD/QHD/4K et smoke contractuel restaurant Forgotten Forest ;
- UX1-M qualifié pour `ORF-PLC-009` : protection plot depuis Objets & interactions et `Ctrl+K`,
  actions protégées, droits requis, feedback joueur et dérogation MJ avec motif et audit gouverné ;
  validation canonique, quatre décisions runtime, lecture ForgeScript/MCP, préservation, undo/redo
  et round-trip exact ; preuves Forward+ FHD/QHD/4K et smoke contractuel restaurant Forgotten Forest ;
- UX1-N qualifié pour `ORF-PLC-010` : faction et propriété depuis Objets & interactions et `Ctrl+K`,
  faction principale, appartenances, hostilité, crime, accès et ciblage, avec aperçu de décision sans
  écriture ; validation canonique, projection et décisions RPG Kit, lecture ForgeScript/MCP,
  préservation du champ Aurora historique, undo/redo et round-trip exact ; preuves Forward+
  FHD/QHD/4K sur factions synthétiques en mémoire et smoke contractuel restaurant Forgotten Forest ;
- UX1-O qualifié pour `ORF-PLC-011` : conversation depuis Objets & interactions et `Ctrl+K`,
  dialogue, point d’entrée, interaction, état, utilisateur, objet et événement, priorités et usage
  unique, avec aperçu de résolution sans écriture ; validation canonique, projection et décision RPG
  Kit, lecture ForgeScript/MCP, préservation du dialogue Aurora historique, undo/redo et round-trip
  exact ; preuves Forward+ FHD/QHD/4K sur le catalogue réel et smoke contractuel restaurant Forgotten Forest ;
- UX1-P qualifié pour `ORF-PLC-012` : verrouillage depuis Objets & interactions et `Ctrl+K`, quatre
  états, clé item réelle, justificatifs, défi, code masqué, piratage, droits, reverrouillage et
  brouillage, avec aperçu sans écriture ; validation canonique, résolution RPG Kit, lecture
  ForgeScript/MCP sans secret, préservation, undo/redo et round-trip exact ; preuves Forward+
  FHD/QHD/4K sur le catalogue réel et smoke contractuel restaurant Forgotten Forest ;
- UX1-Q qualifié pour `ORF-PLC-013` : clé consommée depuis Objets & interactions et `Ctrl+K`, avec
  conservation, consommation, charges, partage, permission virtuelle et audit, plus aperçu de la
  décision sans mutation d’inventaire ; validation canonique, adaptateur RPG Kit, lecture
  ForgeScript/MCP, préservation du verrou et des données cachées, undo/redo et round-trip exact ;
  preuves Forward+ FHD/QHD/4K sur le catalogue réel et smoke contractuel restaurant Forgotten Forest ;
- UX1-R qualifié pour `ORF-PLC-014` : piège depuis Objets & interactions et `Ctrl+K`, avec
  déclencheur, effet, détection, désamorçage, réarmement, propriétaire, politique alliée, VFX, état
  armé et usage unique, plus aperçu sans effet de scène ; sous-profils de sécurité indépendants,
  validation canonique, adaptateur RPG Kit, lecture ForgeScript/MCP, préservation des événements
  voisins, undo/redo et round-trip exact ; preuves Forward+ FHD/QHD/4K sur le catalogue réel et
  smoke contractuel restaurant Forgotten Forest ;
- UX1-S qualifié pour `ORF-PLC-015` : trésor initial depuis Objets & interactions et `Ctrl+K`, avec
  table d’objets nommés, catégories, poids, quantités, qualité, conditions, unicité, moment, graine et
  état généré persistant ; modification d’un état existant protégée par remise à zéro explicite,
  validation canonique, adaptateur RPG Kit indépendant, lecture ForgeScript/MCP, préservation du
  piège et des événements voisins, undo/redo et round-trip exact ; preuves Forward+ FHD/QHD/4K sur
  le catalogue réel et smoke contractuel restaurant Forgotten Forest ;
- UX1-T qualifié pour `ORF-PLC-016` : événements d’ouverture et de fermeture depuis Objets &
  interactions et `Ctrl+K`, avec version ForgeScript, liaisons nommées, acteur, résultat, état
  précédent et autorité ; aperçu du dispatch typé sans écriture, validation canonique, adaptateur RPG
  Kit indépendant, lecture ForgeScript/MCP, préservation du piège, du trésor et des événements
  voisins, undo/redo et round-trip exact ; preuve Forward+ FHD sur le catalogue réel et smoke
  contractuel restaurant Forgotten Forest ;
- UX1-U qualifié pour `ORF-PLC-017` : événements avant et après verrouillage/déverrouillage depuis
  Objets & interactions et `Ctrl+K`, avec version ForgeScript, méthode, justificatif, acteur et veto
  contrôlé ; aperçus accepté ou refusé sans écriture, validation canonique, adaptateur RPG Kit
  indépendant, lecture ForgeScript/MCP, préservation du verrou et des événements voisins, undo/redo
  et round-trip exact ; preuve Forward+ FHD sur le catalogue réel et smoke contractuel restaurant
  Forgotten Forest ;
- densité native compacte à partir du Full HD : base typographique 12 px, barre 48 px, rail 72 px,
  palette 188 px et inspecteur 280/360 px afin de rendre la vue 3D prioritaire en Full HD et QHD ;
- retouche d'instance directe : gizmo X/Y/Z dans la 3D, déplacement avec pas fin, clic droit intégré,
  transformation, tags et propriétés locales, commandes Application, undo/redo et sauvegarde atomique ;
- création et gestion directe des objets : palette filtrable, ajout à la position visée, duplication
  fidèle à côté et suppression avec confirmation compacte, toutes réversibles avant sauvegarde ;
- page Dialogues documentaire alimentée par les lignes réelles, actions incomplètes honnêtement
  désactivées et lanceur de développement unique `OpenRPG Forge Studio.cmd` ;
- captures Studio contrôlées en 1920×1080 et 2560×1440, avec une seule fenêtre et aucun contrôle
  principal coupé ; UX0-H fournit la première preuve exacte 3840×2160 via un viewport Forward+
  hors-écran lorsque Windows borne la fenêtre, désormais rejouée sur le guide UX0-I et les
  diagnostics UX0-J, l’assistant UX1-A, les opérations de zone UX1-B, le chargement UX1-C, les
  calques UX1-D, le blueprint de décor UX1-E, le décor statique UX1-F, l’objet utilisable UX1-G,
  le conteneur UX1-H, l’identité UX1-I, la transformation UX1-J, l’animation initiale UX1-K, la
  destructibilité UX1-L, la protection plot UX1-M, la faction UX1-N et la conversation UX1-O.
  Le verrouillage UX1-P, la clé consommée UX1-Q, le piège UX1-R et le trésor UX1-S disposent désormais de la même
  qualification FHD/QHD/4K. Les événements d’ouverture/fermeture UX1-T et de
  verrouillage/déverrouillage UX1-U disposent de leur preuve FHD réelle ; leurs revues QHD/4K sont
  réservées au jalon multi-lots.
  La revue 4K des autres pages reste à qualifier ;
- CI rendue indépendante de la cadence du runner et des dossiers locaux `.artifacts` ;
- contexte réduit à `PROJECT_CONTEXT.md`, `STATUS.md`, `TASKS_MVP.md` et la roadmap courante ;
- manifestes SHA-256, crédits, licences agrégées, notices et SBOM CycloneDX 1.6 ;
- installateur validé sur un profil Windows temporaire propre : installation, smoke Creator, mise à
  jour sur place, désinstallation et conservation des données utilisateur ;
- détails techniques UUID/hash/provenance repliés par défaut et fenêtres spécialisées intégrées,
  redimensionnables et non exclusives ;
- parcours Plaçables simplifié dans le Monde : assets prioritaires, sélection de scène,
  déplacement/rotation/échelle avec snap, action de placement fixe et propriétés avancées repliées ;
- parcours direct du viewport : Forgotten Forest au démarrage normal, choix d'objet ou de brosse,
  clic dans la scène, sculpture de terrain transactionnelle et cadrage de la zone active ;
- rendu Forward+ enrichi par profil avec ciel procédural cinématique, SSAO/SSIL, glow et brouillard
  volumétrique, avec repli GL sûr et neutralisation explicite d'une LUT absente ;
- Crypte habillée avec 74 instances d'un kit Dungeon Remastered CC0 ;
- Forgotten Forest corrigée : maillage d'asset réel préféré au cube auxiliaire Blender, arbres
  stylisés gouvernés, échelle et éclairage rééquilibrés, caméra/HUD lisibles ;
- module connecté démarrant dans l’Auberge du Cerf d’Ambre : déplacement au sol accéléré, mini-carte,
  atlas des six zones sur `M` et HUD RPG noir/ambre compact validés en Forward+ à 1920×1080 et 2560×1440 ;
- feedback de combat branché sur les résultats réels : barres/chip, cible et plaques, nombres,
  recharge exacte, toasts, pings, impacts, caméra, hit-stop et SFX ; 30 captures Forward+ acceptées
  en 1920×1080/2560×1440, mouvements réduits et texte ×2, puis parcours interactif jusqu'à Clairval ;
- géométrie jouable de l’auberge sécurisée : plancher physique continu, quatre limites murales
  infranchissables (porte comprise jusqu’à interaction), pivots visuels des murs recalés sur le sol,
  pieds du personnage alignés et récupération automatique après toute chute sous une carte intérieure
  ou extérieure ;
- caméra de déplacement stabilisée par interpolation physique, point de visée lissé et évitement des
  murs ; l’ancien bouton central de sortie est supprimé et la porte 3D de l’auberge se clique directement
  pour charger Clairval ;
- premiers découpages des orchestrateurs : politique de fenêtres Creator, décorateur artistique de
  la Crypte et mapping des ressources de quête MCP ;
- cycle de vie des outils Creator centralisé : aucun outil secondaire au démarrage, un seul outil
  actif à la fois, fermeture, déplacement, redimensionnement et maximisation vérifiés sous Windows ;
- édition manuelle consolidée : collections Dialogue, Quête, Blueprint, contenu, propriétés d’objet
  et slots d’équipement présentées sous forme de listes et champs nommés, sans syntaxe brute visible ;
- sauvegarde non destructive des profils Objet et Terrain : les entrées non affichées par un
  formulaire rapide sont conservées, et les suppressions Zone/Blueprint/Dialogue/Quête passent par
  une prévisualisation transactionnelle, la validation des dépendances et l’historique undo/redo.
- profil de lanceur v1 persistant, application WPF RPG cinématique avec fond gouverné, brumes,
  braises, parallaxe, transitions et réduction des mouvements, générateur `Client` atomique,
  manifestes SHA-256 complets et modèle embarqué dans la distribution Creator ;
- réglages d'affichage, qualité, VSync, audio et accessibilité appliqués au runtime ; hébergement
  local autoritaire validé, connexion distante maintenue comme aperçu explicitement désactivé.

## Distribution locale qualifiée

- version : `0.1.0-dev` ; cible : Windows x64 ;
- conformité : 38 composants documentés et 38 licences agrégées ;
- Runtime : manifeste validé et smoke rejoué depuis l'exécutable exporté ;
- Creator historique : manifeste validé et smoke rejoué depuis le lanceur autonome ;
- Studio : fondation locale validée ; export Windows/Linux et bascule du lanceur non encore qualifiés ;
- installateur : `OpenRPG_Forge_Setup_0.1.0-dev_x64.exe`, cycle propre réussi ;
- signature constatée : `NotSigned`.
- modèle de lanceur de jeu : export Godot et smoke runtime réussis ; exécutable WPF single-file
  vérifié, trois écrans cinématiques capturés et session locale autoritaire rejouée depuis l'export ;
- isolation NuGet de distribution : les restaurations RID utilisent des verrous temporaires ; une
  reconstruction complète du modèle a conservé à l'octet près les 29 verrous canoniques contrôlés.

## Limites externes avant release publique

- le dépôt ne possède pas encore de licence explicite pour son propre code ; cette décision juridique
  appartient au propriétaire du projet ;
- aucun certificat Authenticode n'est fourni, donc l'installateur reste un candidat de développement
  susceptible de déclencher SmartScreen ;
- aucun tag ni aucune GitHub Release n'est créé sans autorisation distincte ;
- une revue sur un second écran physique et avec un fournisseur IA réel reste une qualification
  humaine de release, pas un blocage du prototype local.

## Publication Git

- `main` est la branche canonique et `origin/main` la publication de référence ;
- cette phase est publiée uniquement après portail local vert, worktree propre, divergence `0/0` et
  concordance entre `HEAD` et `git ls-remote origin refs/heads/main` ;
- distributions, installateur et captures constituent des preuves locales ; un workflow GitHub doit
  être rapporté séparément selon son état réel et ne peut pas être déduit du seul push Git.

## Commandes de vérité

```powershell
dotnet run --project tools/Forge.ArchitectureGraph -- check --full
& .\scripts\Update-CompleteFunctionBacklog.ps1 -Check
& .\scripts\Verify.ps1
& .\scripts\Open-Studio.ps1
& .\scripts\Verify-RuntimeDistribution.ps1 -RunSmoke
& .\scripts\Verify-CreatorDistribution.ps1 -RunSmoke
& .\scripts\Verify-WindowsInstaller.ps1 -RunInstallCycle
```
