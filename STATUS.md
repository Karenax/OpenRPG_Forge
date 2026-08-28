# État vérifié — OpenRPG Forge

Dernière mise à jour : 28 août 2026.

## Verdict

OpenRPG Forge est désormais un prototype RPG 3D consolidé, installable et utilisable pour créer,
tester et exporter des modules. Les familles Aurora sont closes ; le travail courant porte sur la
qualification de livraison et la consolidation des vertical slices.

La phase consolidée couvre l’édition directe, la qualité visuelle, le lanceur configurable et
l’interface de combat. Le Creator autonome `Forge.Studio` expose désormais les 717 routes, possède
66/66 destinations historiques au statut `Parity` et a franchi sa revue humaine corrective FHD,
QHD et 4K. Ses distributions Windows et Linux x64 sont qualifiées sur leur système cible ; le smoke
Linux Debian 13 ferme `STUDIO-043` sans installer l'éditeur Godot. Aucun de ces états ne doit être
confondu avec une release publique signée.

## Produit disponible

- Creator historique et runtime 3D séparés, exclusivement présentés sous la marque OpenRPG Forge ;
- `Forge.Studio` autonome : Module Hub, une fenêtre, huit rubriques, trente-sept pages, 717 routes
  humaines, Crypte des Cendres, édition directe, validation, playtest isolé et Assistant gouverné ;
- neuf espaces du Creator historique, mode Débutant à quatre espaces et outils spécialisés intégrés ;
- projets, zones, blueprints, assets gouvernés, dialogues, quêtes, scripts et contenu RPG ;
- pack Asset Vault **Médiéval** : 365 modèles Quaternius locaux, regroupés à partir de cinq archives
  CC0 vérifiées et visibles comme une collection unique dans le Studio et l’Asset Atlas ;
- validation, correctifs prévisualisés, undo/redo, playtest, sauvegarde/reprise et export `.orpmod` ;
- Assistant et host MCP transactionnels, bornés, facultatifs et sans shell arbitraire ;
- distributions Windows x64 autonomes du Creator et du Runtime ;
- lanceur Windows configurable par jeu avec fond et musique WAV gouvernés, Play/Stop, volume persistant,
  hébergement autoritaire, détection facultative de l'IPv4 publique, invitation privée `openrpg2:` et
  connexion TLS épinglée ; lanceur, module et Studio en plein écran par défaut, avec mode joueur
  **Fenêtré plein écran** sans bordure ;
- installateur/désinstallateur NSIS avec contrôle de mise à jour et préservation des données utilisateur ;
- Crypte des Cendres jouable en Forward+ avec modèles 3D gouvernés ;
- outils réutilisables de terrain, biome forestier et gouvernance d'assets conservés indépendamment
  du module canonique ;
- fondation WorldGeneration gouvernée : recettes et révisions persistantes, previews déterministes,
  régénération partielle, jobs auditables repris après redémarrage, Terrain3D/Godot Flow facultatifs
  et terrain natif de fallback avec maillage et collision sous Godot 4.7.1 ; identité concrète des
  fournisseurs stabilisée par `providerKey`, qualification réelle Windows, benchmark comparatif,
  page Studio et promotion canonique explicite, atomique et réversible ;
- interface de combat runtime autoritaire, animée, accessible et bornée par des pools stables.

WorldGeneration possède désormais une tranche produit : la page Studio partage les révisions MCP,
Solaren porte une seconde zone promue et Runtime la charge depuis le workspace puis le `.orpmod`.
La scène contient 5 904 instances en quatre rôles, collision et navigation, avec capture Forward+
inspectée. La chaîne n'est pas encore une bibliothèque artistique universelle : graphes et assets
restent détenus par les modules, les exports provider-free et la CI Linux restent à qualifier, et
GF-PCGODOT demeure expérimental faute de gain artistique ou d'auteur démontré.

La promotion ne se produit jamais automatiquement : le MCP exige preview, permission, révisions,
job réussi, rapport accepté, empreintes exactes et SHA-256 de destination en cas de remplacement. Il
copie atomiquement la scène, persiste sa provenance, la référence depuis `AreaWorldProfile`, vérifie
la réouverture et fournit un inverse. Le job CI Linux est configuré, mais sa preuve ainsi que les
exports provider-free Windows/Linux et la revue artistique restent ouverts.

## Multijoueur Internet direct

- le profil **Internet direct · héberger et rejoindre** est actif dans le Creator et le lanceur WPF ;
  l'hôte écoute en TLS sur `0.0.0.0` et le client colle une invitation privée `openrpg2:` ;
- le mode Hôte détecte facultativement l'IPv4 publique, accepte un DNS et génère certificat, pin et
  jeton ; `127.0.0.1` reste interne et l'échec de détection ne bloque pas l'hôte ;
- `Forge.Server` utilise le même listener et reste actif jusqu'à son arrêt, avec capacité headless
  configurable de 1 à 64 ; le runtime hôte utilise actuellement une capacité fixe de 8 ;
- l'admission vérifie pin TLS, jeton, empreinte de contenu et protocole avant la présence ; les
  snapshots révisionnés portent zone, position et orientation, puis Runtime interpole les avatars ;
- une tranche serveur réelle refuse le calcul client et exerce attaque, potion, quête et checkpoint ;
  dialogues, transitions collectives et sauvegardes de campagne restent à projeter ;
- reconnexion, révocation individuelle, relais, annuaire et ouverture NAT automatique restent absents ;
- la preuve locale utilise de vrais sockets TCP à deux clients sur loopback. Un essai depuis une
  seconde connexion Internet, avec pare-feu et redirection du routeur, reste à réaliser ;
- le mode d'emploi, les diagnostics et la frontière de sécurité sont centralisés dans
  `docs/MULTIPLAYER_INTERNET.md`.

## Solaren, Les Vallées Libres — première zone jouable

- `Solaren, Les Vallées Libres` est un workspace canonique séparé ; la Crypte des Cendres reste le
  module de test existant et son contenu n'a pas été remplacé ;
- ce workspace Studio est désormais l'unique source de vérité du jeu : le générateur et la commande
  `author-solaren` ont été retirés, et le moteur ne conserve aucun comportement produit dépendant du
  slug Solaren. Rendu, précipitations, caméra, mouvement et clips sont des profils génériques
  versionnés, éditables dans le Creator autonome conformément à l'ADR-0049 ;
- son lanceur emploie désormais un panorama solaire d'Aubeciel issu des captures du module, une
  palette forêt/crème/or et une piste calme originale de 64 secondes. Le fond PNG, le WAV, leurs
  SHA-256 et leur provenance vivent dans `content/solaren/branding` ; le profil générique les exporte
  et le contrôle Play/Stop partage le volume Musique persistant ;
- `Le Val d’Aubeciel` couvre 512 × 512 m avec 16 641 sommets, 32 768 triangles, 64 secteurs,
  relief vallonné, place de village, chemin spline et corridor paysager préservant la perspective ;
- le village assemble quatre maisons modulaires closes et à deux étages, avec fondations, portes
  ouvertes sur leurs gonds, seuils et escaliers centrés, vitrages, volets, pignons, balcons, cheminées
  et vignes. Les quatre ouvertures sont franchissables par la capsule joueur ; quatre torches compactes
  ajoutent une lumière chaude sans bloquer les seuils, tandis que tonneau, cagettes de fruits et repère
  lumineux enrichissent le marché par placements MCP gouvernés ;
- le workspace contient désormais 56 manifestes d’assets et 323 chunks vérifiés. Trois personnages
  complets UAL2 dérivés des sources Quaternius CC0 différencient Aerin, Maélis et Orin ; Toman conserve
  le paysan masculin complet. Leurs six clips `idle_loop`, `jog_fwd_loop`, `jump_start`,
  `sword_attack`, `hit_chest` et `death01` restent raccordés, et quatre portraits 768 px passent par le
  catalogue CC0 et l’installateur gouverné avant d’être projetés dans le HUD ;
- le terrain utilise les textures PBR d'herbe/roche et de pavés. Le réglage biome MCP reconstruit
  36 928 instances dans seize couches — 12 253 de moins qu’avant, soit 24,9 % — avec treize couches
  au vent, des exclusions plus propres autour des seuils et un temps terrain/scatter inférieur au
  budget interne de 2 000 ms. Le runtime joueur ne lance plus les laboratoires Quality/Performance/
  Governance réservés aux smokes ; la liaison wetness utilise des overrides de matériaux sans dupliquer
  profondément les meshes ordinaires. Sur la même session Forward+ Solaren, le démarrage passe de
  45 242,758 ms à 12 206,700 ms (-73,0 %) et la liaison wetness de 11 497,225 ms à 1 795,927 ms (-84,4 %) ;
- le déplacement du héros reste réglé dans le module à 1,2 m/s en marche et 2,64 m/s en course. La
  caméra conserve son recul horizontal sur pente, gère l’occlusion et expose ses limites verticales
  dans Studio ; l’orbite Solaren descend à -85° et vise le ciel depuis sa garde au sol ;
- météo, pluie et mouillure sont raccordées aux données du workspace : averse fine alignée au vent,
  4 060 particules, wetness réversible et coefficients éditables. Le runtime interactif normal reste la
  référence d’exposition, car les fixtures déterministes sont sensiblement plus sombres ;
- HUD, groupe, inventaire, personnage, journal et carte proviennent maintenant de la projection
  canonique du module. Les chemins de portraits sont confinés à la racine ; l’inventaire sépare le socle
  initial authoré des acquisitions persistées, de sorte que `HasItem`, l’interface et les sauvegardes
  restent cohérents sans double comptage, avec migration legacy. Les quêtes suivies produisent des
  diagnostics stables et l’exploration/les étages sont sauvegardés sans données héritées de la Crypte ;
- la carte mono-zone utilise un atlas local avec huit pins, huit labels sans chevauchement, trois routes,
  position joueur, exploration persistée et étage borné. Les données fictives `68 %`, `3 routes actives`
  et `Brume claire` ne sont plus injectées par le runtime ;
- la première boucle narrative est jouable : dialogue de Maélis, choix, acceptation et suivi de
  `Le repère de la place`, objectif authored distinct, interaction avec le repère, variable
  `quest.objectives=complete`, transition terminale et journal mis à jour. Le rejouage de `StartQuest`
  est idempotent ;
- Maélis, Toman et Orin sont liés par override éditable à trois routes locales et se déplacent avec
  attente, orientation et alternance course/idle. Deux émetteurs audio 3D CC0 — vent des frondaisons et
  oiseaux de la place — sont reconstruits comme `AudioStreamPlayer3D`. Les quatre prises françaises de
  Maélis sont des blobs WAV CC0-1.0 content-addressed, synthétiques et reproductibles avec eSpeak NG
  1.52.0 ; `voice-assets.json` divulgue le générateur, les paramètres, l’autorisation, les hashes et
  l’absence de redistribution de l’outil. Elles sont liées aux quatre nœuds, lues depuis les octets
  hashés et conservent le sous-titre comme fallback accessible ;
- l’audit vocal refuse désormais les médias MP3/WAV tronqués, les licences ou provenances inconnues,
  les hashes faux, les chemins non canoniques et les plans d’import forgés au workspace, au package,
  à la conformité et au runtime. Le plafond décompressé par défaut d’un `.orpmod` reste borné à 1 Gio.
  La gate Full intégrée du 27 août 2026 passe 2 315 tests .NET et 67 tests Python, tous les builds sans
  avertissement et les smokes produit. Le graphe Roslyn est frais et déterministe à 11 816 nœuds,
  94 081 relations et zéro diagnostic. Le package Client Solaren contient quatre WAV et aucun MP3 :
  648 594 126 octets, SHA-256
  `549b8b812daa6f568f0e1ac21245348ff4a01080ddc61e5c24455296f3cc9fb5`. La frontière de sécurité
  couvre les mutations Forge coopératives ; une publication no-follow par handle et un verrou
  interprocessus restent le durcissement prévu contre l’échange hostile d’un parent par un autre
  processus local ;
- ce jalon est la première tranche artistique et narrative jouable de Solaren, pas une qualification de
  contenu final ni une release publique signée. Rencontres et transitions vers les vallées voisines
  restent des extensions de contenu explicites.

## Couverture et preuves

- validation locale fondée sur le risque : plans JSON explicables, exécution séquentielle, TRX par
  projet, rejet de zéro test, profils `Core`, `Studio`, `Runtime`, `Assets`, `Network`, `Distribution`
  et `Full`, plus smokes Studio `Route`, `Wave` et `Full`; le portail transversal historique reste
  le profil `Full` par défaut ;
- qualification du routeur le 24 août 2026 : profils Studio, Runtime, Assets, Network, Distribution
  et Core verts séparément, smokes Studio Route et Wave bornés, puis portail Full vert en 873,66 s
  avec 2 130 tests .NET, 64 tests Python, les smokes produit et le graphe Roslyn complet ;
- qualification lanceur/Internet direct le 25 août 2026 : 521/521 tests Domain, 386/386 ModuleFormat et
  112/112 Network en Release ; smoke TCP réel à deux clients avec refus d'un module incompatible,
  détection HTTPS réelle validée sans journaliser l'adresse, distribution Solaren solaire de 228
  fichiers vérifiée, lecture WAV WPF validée et vues Accueil/Configuration/Hôte inspectées en 1440 x 860 ;
- gate Full relancé le 25 août 2026 sous PowerShell 7.6.4 : 2 162 tests .NET et 67 tests Python verts,
  compilation sans avertissement, documentation déterministe, graphe Roslyn complet, Studio, Toolset,
  runtime, réseau, MCP et sauvegarde/reprise validés ;
- requalification Full du 26 août 2026 : 2 314 tests .NET et 67 tests Python verts, builds sans
  avertissement, documentation générée, graphe Roslyn, Studio, Toolset, Runtime/Forward+, assets,
  Blender, MCP, réseau, sauvegarde/reprise et distributions validés ;
- qualification Full intégrée du 27 août 2026 après fusion WorldGeneration : 2 315 tests .NET et
  67 tests Python verts, 32 projets NuGet sans vulnérabilité, builds sans avertissement, documentation
  déterministe, graphe Roslyn à 11 816 nœuds / 94 081 relations / zéro diagnostic, Studio, Toolset,
  Runtime/Forward+, assets, Blender, MCP, réseau, sauvegarde/reprise et distributions validés ;
- qualification consolidée du 28 août 2026 après les audits croisés : 2 345 tests .NET et 68 tests
  Python verts, 32 projets NuGet sans vulnérabilité, builds sans avertissement, documentation et
  717 routes Aurora cohérentes, budgets de maintenabilité verts, MCP à 271 outils, Network à 118 tests,
  Studio à 37 pages et graphe Roslyn déterministe à 11 971 nœuds / 97 386 relations / zéro diagnostic ;
  les smokes produit et distributions de cette gate sont les preuves locales, sans signature publique ;
- vérification de la tranche WorldGeneration courante : build Release complet sans avertissement,
  2 323/2 323 tests .NET verts en passe séquentielle, tests ciblés WorldGeneration, smoke Godot 4.7.1,
  installateur, YAML CI, cohérence projet et budgets verts ; graphe Roslyn déterministe à 11 860
  nœuds / 94 284 relations / zéro diagnostic. Cette passe n'est pas une nouvelle gate Full produit ;
- distribution versionnée `exe/` de 515 fichiers mise à jour avec trois exécutables racine : Studio,
  Solaren et `Cendres.exe`. Les manifestes Studio, Solaren et Crypte couvrent respectivement 281, 227
  et 224 fichiers ; les vérifications ont été exécutées depuis leurs chemins finaux. Solaren embarque
  son package Client ; Crypte conserve son `.orpmod` Editor comme régression interne. Cette preuve
  locale reste `NotSigned` et ne constitue pas une release publique ;
- 717/717 fonctions au statut `Tested`, réparties dans 187/187 lots clos ;
- P0 : 196/196, P1 : 298/298, P2 : 190/190, P3/P4 : 33/33 ;
- 5 461 chemins de preuve Aurora contrôlés sans chemin absent ;
- parcours produit automatisé : projet, zones, assets, contenu RPG, narration, validation, playtest,
  export et Assistant gouverné ;
- matrice et procédure d'acceptation de bout en bout sous `docs/acceptance/`.
- matrice d’accès humain Studio : 717 routes uniques, 717 `functional`, 0 `planned`, avec preuve
  spécifique de UX0 à UX10 ; les 66 anciennes fenêtres possèdent une destination unique au statut
  `Parity`. Cette matrice ne réutilise pas le statut `Tested` de l’ancien Creator.
- premier train parallèle contrôlé livré : UX1-V et UX1-W ont été développés dans des worktrees et
  fichiers distincts, audités avant intégration, puis construits, testés, fumés et capturés
  séquentiellement ; six routes seulement ont été promues sur preuve réelle.
- deuxième train parallèle contrôlé livré : UX1-X, UX1-Y et UX1-Z ont isolé les fondations Door et
  Trigger ainsi que les réactions/physique Placeable ; onze routes ont été promues après 18/18 tests
  ciblés cumulés, trois smokes, trois captures FHD inspectées et une revue QHD/4K corrective d'UX1-Y.
- troisième train parallèle contrôlé livré : UX1-AA, UX1-AB et UX1-AC ont isolé assemblage/validation
  Placeable, dégâts/chargement/événements Door et Trigger avancé ; onze routes supplémentaires ont
  franchi 20/20 tests ciblés, trois portails incrémentaux et les revues Forward+ FHD/QHD/4K.
- quatrième train parallèle contrôlé livré : UX1-AD, UX1-AE et UX1-AF ont isolé réactions/assemblage
  Door, découverte/accès Trigger et plans de zone assistés ; dix routes ont franchi 26/26 tests
  ciblés, trois portails incrémentaux, les revues Forward+ FHD/QHD/4K et le portail complet.
- cinquième et dernier train UX1 livré : UX1-AG, UX1-AH, UX1-AI et UX1-AJ ont isolé accès/événements
  Door, automatisation Trigger et les deux validations canoniques ; dix routes ont franchi 48/48
  tests ciblés, quatre portails incrémentaux en 75,98 s, 69,34 s, 64,99 s et 82,17 s, les revues
  Forward+ FHD/QHD/4K et le portail complet. La vague UX1 est close à 71/71.
- premier train post-UX1 livré : UX2-A, UX4-A et UX5-A ont isolé politiques de zone vivante,
  fondation de factions et fondation de dialogues ; treize routes ont franchi 40 tests dans les
  trois portails incrémentaux, un test RpgKit complémentaire, les smokes, les captures Forward+
  FHD/QHD/4K et le portail complet. `ORF-FAC-006` est désormais `Tested`, conformément au tracker
  canonique ; cette mention de lot est historique et ne décrit pas un reste à faire courant.
- vague UX2 close à 98/98 : huit routes Terrain, 34 Environnement, 29 Monde vivant et 27 Navigation
  disposent de commandes/dry-runs transactionnels, smokes contractuels, sauvegardes/réouvertures et
  captures Forward+ FHD inspectées. Les 36 portails incrémentaux totalisent 3 117,53 s et leurs
  limites distinguent authoring, plans/simulations et runtime réellement branché ; les preuves sont
  liées dans `STUDIO_UX2_TERRAIN.md`, `STUDIO_UX2_ENVIRONMENT.md`,
  `STUDIO_UX2_LIVING_WORLD.md` et `STUDIO_UX2_NAVIGATION.md`.
- vague UX4 close à 140/140 : factions, créatures, voix, assistant de boss, objets, marchands et
  compagnons disposent de commandes/dry-runs transactionnels, smokes contractuels, persistance et
  captures Forward+ FHD inspectées. Le dernier lot Compagnons a franchi 66/66 tests ciblés et son
  portail incrémental en 132,09 s ; les preuves sont liées de `STUDIO_UX4_A.md` à
  `STUDIO_UX4_U.md`.
- vague UX5 close à 69/69 : fondation directe UX5-A puis production complète des dialogues,
  quêtes, catalogues localisés, typographie, lore et cinématiques dans cinq workflows aux historiques
  séparés. Le dernier portail couvre 186/186 tests ciblés, le smoke global et cinq captures Forward+
  FHD inspectées, avec jalon Dialogue également relu en QHD et 4K ; les preuves sont liées dans
  `STUDIO_UX5_A.md` et `STUDIO_UX5_COMPLETE.md`.
- UX8-A ouvre la vague Préparer et publier à 5/51 : build réel `.orpmod` par `Forge.Cli`, graphe
  d'optimisation, annulation du processus, reproductibilité octet par octet et second passage
  entièrement réutilisé. Le portail passe 61/61 tests en 624,28 s, le smoke global, une capture
  Forward+ FHD inspectée et le graphe Roslyn sans diagnostic ; aucune signature, publication,
  installation ou mutation Git n'est revendiquée.
- UX8-B porte la vague Préparer et publier à 10/51 : cache jetable, normalisation, migrations,
  références inverses et diff sémantique opèrent dans une copie confinée et annulable du workspace.
  Le portail passe 25/25 tests en 549,47 s, le smoke global, une capture Forward+ FHD inspectée et
  le graphe Roslyn à 11 043 nœuds / 84 530 relations sans diagnostic ; aucune mutation du module
  source, commande shell arbitraire, opération réseau ou publication Git n'est revendiquée.
- UX8-C porte la vague à 14/51 : merge assisté listes+graphes sans écriture, gouvernance des
  worktrees, sept contrôles CI et cinq fixtures versionnées partagent une surface à deux onglets.
  Le portail passe 23/23 tests en 538,11 s, le smoke global et deux captures Forward+ FHD inspectées ;
  le graphe Roslyn atteint 11 056 nœuds / 84 680 relations sans diagnostic. Aucun merge Git, agent,
  runner distant, shell arbitraire ou publication n'est exécuté.
- UX8-D porte la vague à 18/51 : dépôt Git, structure standard, versions sémantiques et ADR sont
  inspectés localement avec résultats navigables, annulation et journal replié. Le portail passe
  19/19 tests en 535,79 s, le smoke global et une capture Forward+ FHD corrigée puis inspectée ; le
  graphe Roslyn atteint 11 066 nœuds / 84 755 relations sans diagnostic. Aucun état absent n'est
  synthétisé et aucune commande Git ou écriture documentaire n'est exécutée.
- UX8-E porte la vague à 22/51 : documentation générée, task manifests, canaux de release et crash
  artifacts sont inspectés par leurs autorités canoniques, avec annulation, résultats navigables et
  journaux à la demande. Le portail passe 30/30 tests en 531,26 s, le smoke global et une capture
  Forward+ FHD inspectée ; le graphe Roslyn atteint 11 076 nœuds / 84 866 relations sans diagnostic.
  Aucun agent, publisher, signature, publication ou dump natif n'est exécuté.
- UX8-F porte la vague à 26/51 : les approbations de dépendances passent par le validateur de
  distribution puis `SetModuleDistributionHandler`, avec dry-run, gardes de révision et d’empreinte,
  undo/redo et save/reopen ; notices, crédits et frontière IP restent évalués par leurs autorités
  canoniques. Le portail passe 20/20 tests en 534,72 s, le smoke global et une capture Forward+ FHD
  inspectée ; le graphe Roslyn est frais et sans diagnostic. Aucun téléchargement, audit externe,
  package, signature, registre d’assets ou publication n'est exécuté.
- UX8-G porte la vague à 31/51 : le catalogue de packages versionnés, leurs priorités et leurs
  dépendances passent par `SetContentWorkspaceHandler`, puis `ContentPackageCompiler` produit lock,
  manifeste et résolution en mémoire. Le portail passe 47/47 tests en 544,55 s, le smoke global et
  une capture Forward+ FHD corrigée puis inspectée ; le graphe Roslyn final atteint 11 105 nœuds /
  85 168 relations sans diagnostic. Aucun package disque,
  téléchargement, installation, signature, script ou publication n'est exécuté.
- UX8-H porte la vague à 36/51 : les cinq profils de cible, trois plateformes, le packaging Windows,
  la politique de signature publique et les deltas passent par `SetModuleDistributionHandler`, puis
  les builders réels vérifient signature, delta et bundles dans un isolat. Le portail passe 22/22
  tests en 422,07 s, le smoke global et une capture Forward+ FHD inspectée ; le graphe Roslyn final
  atteint 11 120 nœuds / 85 368 relations sans diagnostic. Aucune clé privée n’est stockée ; aucun
  installateur n’est exécuté et aucune publication ou opération réseau n’est lancée.
- UX8-I porte la vague à 38/51 : le contrat de compatibilité passe par
  `SetWorkspaceBuildProfileHandler`, avec dry-run de migration vers le format courant, tandis que
  `AssetPlatformParityValidator` et `AssetPackageManifestResolver` contrôlent variante exacte ou
  repli commun, rôles, budget, fichiers et SHA-256 sur le module réel. Le portail passe 64/64 tests
  en 423,41 s, le smoke global et une capture Forward+ FHD inspectée ; le graphe Roslyn final atteint
  11 133 nœuds / 85 504 relations sans diagnostic. Aucun manifest n’est modifié, aucune migration
  source, mesure matérielle, import runtime, opération Git ou publication n’est exécuté.
- UX8-J porte la vague à 40/51 : la page Sécurité sépare la politique worker et le profil Assistant,
  exige un consentement après dry-run, puis refuse réellement Python libre, shell, eval, réseau,
  instruction hostile, outil hors liste et élévation de permission. Le portail passe 35/35 tests en
  472,65 s, le smoke global et une capture Forward+ FHD inspectée. Aucun worker, fournisseur IA,
  outil sémantique, réseau ou module source n’est exécuté ou modifié par la preuve UI.
- UX8-K porte la vague à 41/51 : import et export en masse partagent un atelier confiné avec
  consentement, staging SHA-256, collisions déterministes, compensation des fichiers et commande
  canonique du catalogue. Le portail final passe 51/51 tests en 423,12 s, le smoke global et une
  capture Forward+ FHD corrigée puis inspectée ; le graphe Roslyn final est frais et sans
  diagnostic. Aucun téléchargement, watcher, synchronisation, publication ou module source n’est
  exécuté ou modifié par la preuve UI.
- UX8-L porte la vague à 43/51 : la page Packages authorise la politique de synchronisation, le
  cache, la confiance, les licences et les métadonnées NWSync dans une transaction canonique. Un
  dépôt local de validation produit un manifeste SHA-256, exerce reprise et réutilisation du cache,
  puis est supprimé. Le portail passe 61/61 tests en 419,84 s, le smoke global et une capture
  Forward+ FHD inspectée ; le graphe Roslyn final est frais et sans diagnostic. Aucun réseau,
  téléchargement distant, serveur NWSync, watcher, publication ou mutation du module source n’est
  exécuté par la preuve UI.
- UX8-M porte la vague à 47/51 : la page Lanceur configure découverte, lobby, création et autorité
  des personnages dans un profil workspace atomique, avec dry-run, gardes d'empreinte, undo/redo,
  save/reopen et conservation de la politique combat. Le portail passe 9/9 tests en 443,52 s, le
  smoke global et quatre captures Forward+ FHD inspectées après correction des champs tronqués.
  Le graphe Roslyn atteint 11 213 nœuds / 86 391 relations sans diagnostic. Aucun transport,
  serveur, annuaire distant, client, compte ou monde persistant n'est lancé.
- UX8-N clôt la vague à 51/51 : la page Packages sépare politique curatée, registre de namespaces,
  migrations de compatibilité et désinstallation sûre dans quatre historiques. Les 22/22 tests
  ciblés, le smoke global et quatre captures Forward+ FHD inspectées valident dry-run, gardes stale,
  undo/redo, save/reopen, backups et préservation des voisins. Le portail passe en 581,21 s et le
  graphe Roslyn atteint 11 234 nœuds / 86 636 relations sans diagnostic. Aucun script de package,
  agent, shell, réseau, téléchargement, commande Git ou publication n'est exécuté.
- UX9-A ouvre la vague Exploiter en réseau à 4/68 : la page Directeur réseau persiste le socle
  autoritaire, puis sonde réellement groupes, réplication et filtrage d’intérêt dans une session
  locale en mémoire. Permission Administrateur, confirmation distincte, audit et déconnexion sont
  obligatoires ; le build passe sans avertissement, 17/17 tests ciblés, le smoke global et une
  capture Forward+ FHD corrigée puis inspectée. Le portail passe en 643,09 s et le graphe Roslyn
  atteint 11 253 nœuds / 86 841 relations sans diagnostic. Aucun port, client distant ou serveur
  dédié inter-processus n'est lancé ; `ORF-MP-001` reste planifié.
- UX9-B porte la vague à 9/68 : cinq workflows humains configurent synchronisation de contenu,
  verrou strict, rôles appliqués au runtime, chat structuré et bus d'événements. La transaction
  impose Administrateur, confirmation, dry-run, gardes stale, undo/redo et save/reopen ; chaque
  sonde utilise seulement mémoire et cache temporaire puis libère ses ressources. Le build passe
  sans avertissement, 25/25 tests ciblés, le smoke global et cinq captures Forward+ FHD inspectées.
  Le portail passe en 532,65 s et le graphe Roslyn atteint 11 276 nœuds / 87 071 relations sans
  diagnostic. Aucun transport distant, serveur public, compte réel, dépôt distant ou bridge
  externe n'est utilisé.
- UX9-C porte la vague à 14/68 : instances bornées, création dynamique, comptes privés,
  administration gouvernée et modération sont édités dans un profil workspace atomique. Permission,
  confirmation, dry-run, gardes stale, undo/redo, save/reopen, API/CLI MCP et cinq sondes runtime
  sont prouvés par 17/17 tests, smoke global et cinq captures Forward+ FHD inspectées. Le portail
  passe en 548,58 s et le graphe Roslyn atteint 11 299 nœuds / 87 300 relations sans diagnostic.
  Studio n'ouvre aucun listener, socket, compte externe ou serveur public.
- UX9-D porte la vague à 19/68 : persistance révisionnée, opérations SQL nommées, métriques/health,
  profilage borné et reconnexion single-use sont édités dans un profil workspace atomique. Permission
  Administrateur, confirmation distincte, dry-run, gardes stale, undo/redo, save/reopen et cinq
  sondes runtime sont prouvés par 12/12 tests, smoke global et cinq captures Forward+ FHD inspectées.
  Le portail passe en 534,22 s ; le graphe Roslyn final est frais, déterministe et sans diagnostic.
  Studio n'ouvre aucun endpoint, exporter, socket, serveur public ou base SQL distante.
- UX9-E porte la vague à 21/68 : snapshot, WAL, répétition de rollback, migrations, PITR et rétention
  sont séparés de la capacité, des slots Administrateur et de la file FIFO observable. Permission,
  confirmation destructive distincte, dry-run, gardes stale, undo/redo, save/reopen et les vraies
  sondes `RollbackService`/`ConnectionQueue` sont prouvés par 11/11 tests, smoke global et deux
  captures Forward+ FHD inspectées. Le portail passe en 546,18 s ; le graphe Roslyn final est frais,
  déterministe et sans diagnostic. Studio n'ouvre aucun stockage distant, socket ou serveur public
  et n'applique aucun rollback au module réel.
- UX9-F porte la vague à 26/68 : client Live Director séparé avec MFA/expiration/révocation,
  palette Creator filtrée, spawn spatial compensable, Chooser privé hiérarchique et focus spectateur
  en lecture seule sont accessibles dans trois vues directes. Dry-run, gardes stale, undo/redo,
  save/reopen, confirmation Administrateur et les vrais services runtime sont prouvés par 12/12
  tests ciblés, 54/54 régressions Live Director, smoke global et trois captures Forward+ FHD
  inspectées. Le portail passe en 549,52 s ; le graphe Roslyn atteint 11 367 nœuds / 87 994
  relations sans diagnostic. Studio n'ouvre aucun socket, compte externe ou serveur réel et
  compense/révoque toute ressource synthétique.
- UX9-G porte la vague à 31/68 : téléportation sûre avec orientation, destruction à impact quête
  et retour borné, soin RPG, limbo préservant l'identité et possession expirante sont accessibles
  dans trois vues directes. Dry-run, gardes stale, undo/redo, save/reopen, confirmation
  Administrateur, compensation permissionnée et les vraies autorités runtime sont prouvés par
  18/18 tests ciblés, 56/56 régressions Live Director, smoke global et trois captures Forward+ FHD
  corrigées puis inspectées. Le portail passe en 514,28 s ; le graphe Roslyn atteint 11 391 nœuds /
  88 214 relations sans diagnostic. Studio n'ouvre aucun socket, compte externe ou serveur réel et
  restaure toutes les ressources synthétiques avant la sortie.
- UX9-H porte la vague à 36/68 : incarnation chat/dialogue/voix/animation avec divulgation,
  protection GM bornée, ajustements d’or et d’expérience persistants et compensables, puis états de
  porte avec adaptateurs collision/portail sont accessibles dans trois vues directes. Dry-run,
  gardes stale, undo/redo, save/reopen, confirmation Administrateur et autorités runtime réelles
  sont prouvés par 18/18 tests ciblés, 58/58 régressions Live Director, smoke global et trois
  captures Forward+ FHD corrigées puis inspectées. Le portail passe en 512,27 s ; le graphe Roslyn
  atteint 11 416 nœuds / 88 452 relations sans diagnostic. Studio n’ouvre aucun socket, compte,
  serveur ou portefeuille externe et compense toutes les ressources synthétiques avant la sortie.
- UX9-I porte la vague à 41/68 : entrée/sortie de trigger, sélection multiple bornée, déplacement
  de groupe avec échecs partiels, attaque avec avertissement de faction et voyage vers un joueur
  avec transition caméra sont accessibles dans trois vues directes. Dry-run, gardes stale,
  undo/redo, save/reopen, consentement du groupe, confirmation Administrateur et vraies autorités
  runtime sont prouvés par 13/13 tests ciblés, 58/58 régressions Live Director, smoke global et
  trois captures Forward+ FHD inspectées. Le portail passe en 517,61 s ; le graphe Roslyn atteint
  11 438 nœuds / 88 669 relations sans diagnostic. Studio n’ouvre aucun socket, serveur ou joueur
  réel et restaure toutes les ressources synthétiques avant la sortie.
- UX9-J porte la vague à 46/68 : recherche indexée avec suivant et focus visuel, ForgeScript
  serveur borné, variables typées persistables, scènes à cinq facettes avec rollback et visibilité
  MJ ou joueur avec overlay viewport sont accessibles dans trois vues directes. Dry-run, gardes
  stale, undo/redo, save/reopen, confirmation Administrateur et vraies autorités runtime sont
  prouvés par 16/16 tests ciblés, 58/58 régressions Live Director, smoke global et trois captures
  Forward+ FHD inspectées. Le portail passe en 519,91 s ; le graphe Roslyn atteint 11 461 nœuds /
  88 901 relations sans diagnostic. Studio n’ouvre aucun socket, serveur ou joueur réel et restaure
  variable, scène et ressources synthétiques avant la sortie.
- UX9-K porte la vague à 48/68 : transition de quête pour tous ou une sélection de joueurs avec
  conséquences et projection RPG, puis inspection et cinq commandes de rencontre avec scaling borné
  sont accessibles dans deux workflows directs. Dry-run, gardes stale, undo/redo, save/reopen,
  confirmation Administrateur, adaptateurs spawn/IA et autorités runtime réelles sont prouvés par
  10/10 tests ciblés, 58/58 régressions Live Director, smoke global et trois captures Forward+ FHD
  inspectées. Le portail passe en 525,88 s ; le graphe Roslyn atteint 11 484 nœuds / 89 114
  relations sans diagnostic. Studio n’ouvre aucun socket, serveur ou joueur réel et restaure quête,
  rencontre et ressources synthétiques avant la sortie.
- UX9-L porte la vague à 53/68 : authentification MFA, inspection en lecture seule, liste joueurs à
  latence autoritaire, audit immuable et chat MJ modéré sont réunis dans une surface directe à cinq
  bornes sûres. Dry-run, gardes stale, undo/redo, save/reopen, confirmation Administrateur, handles
  ForgeScript et vraies autorités runtime sont prouvés par 13/13 tests ciblés, 58/58 régressions
  Live Director, smoke global et capture Forward+ FHD corrigée puis inspectée. Le portail passe en
  503,26 s ; le graphe Roslyn atteint 11 502 nœuds / 89 296 relations sans diagnostic. Studio
  n’ouvre aucun socket, compte, serveur ou canal externe et libère les ressources synthétiques.
- UX9-M porte la vague à 58/68 : repos forcé motivé, ajustement de niveau avec snapshot, apparence
  bornée, groupe sauvegardé sur raccourci et enregistrement expurgé sont réunis dans cinq cartes
  directes qui éditent le profil canonique du module. Dry-run, gardes stale, undo/redo, save/reopen,
  confirmation Administrateur, préservation exacte des profils voisins et vraies autorités runtime
  sont prouvés par 7/7 tests ciblés, 60/60 régressions Live Director, smoke global et capture
  Forward+ FHD corrigée puis inspectée. Le portail passe en 507,90 s ; le graphe Roslyn final est
  frais, déterministe et sans diagnostic. Studio n’ouvre aucun socket, serveur ou joueur réel et
  libère les ressources synthétiques.
- UX9-N porte la vague à 64/68 : contenu curaté avec confiance/licences, voix gouvernée, shard
  référencé, hot reload atomique et réversible, extensions signées/isolées et protocole
  cross-platform sont réunis dans six cartes directes qui éditent les profils canoniques du module.
  Dry-run, gardes stale, undo/redo, save/reopen, confirmation Administrateur et préservation exacte
  des profils voisins sont prouvés par 8/8 tests ciblés, 99/99 régressions réseau, smoke global et
  capture Forward+ FHD inspectée. Le portail passe en 507,54 s ; le graphe Roslyn du portail atteint
  11 528 nœuds / 89 644 relations sans diagnostic, puis est régénéré frais après promotion. Studio
  n’ouvre aucun socket, serveur, audio, processus d’extension ou service externe et libère l’isolat.
- UX9-O clôt la vague à 68/68 : serveur dédié headless, PvP et tir ami, logs structurés privés et
  anti-triche autoritaire sont réunis dans quatre cartes directes qui coordonnent les trois profils
  réseau canoniques. Les trois dry-runs, approbations révisionnées, compensation, undo/redo,
  sauvegardes atomiques par document, réouverture et préservation sémantique des voisins sont
  prouvés par 37/37 tests ciblés, 99/99 régressions réseau, 385/385 régressions ModuleFormat, smoke
  global et capture Forward+ FHD corrigée puis inspectée. Le portail passe en 515,32 s ; le graphe
  Roslyn final atteint 11 548 nœuds / 89 923 relations sans diagnostic, frais et déterministe. La
  matrice atteint 700 `functional` / 17 `planned`. Studio n’ouvre ni port, socket, service Windows,
  serveur persistant ou sink distant et libère toutes les ressources synthétiques.
- UX10 clôt les 17 routes Assistant et porte la matrice à 717 `functional` / 0 `planned`. Une
  surface unifiée sur Contexte, Plans et 3D assistée expose sélection explicite, configuration
  humaine, aperçu sec, approbation exacte, test en copie confinée, preuves lisibles et diagnostics
  pour contexte/outils/fournisseurs/confidentialité, plans/transactions/diffs/autorisations,
  audit/prompts/résultats structurés/hors ligne et Blender/text-to-3D/cohérence narrative. Le build
  Studio passe sans avertissement, les 66/66 tests ciblés et le smoke global passent, les quatre
  captures Forward+ FHD ont été corrigées puis inspectées et le portail incrémental passe en
  524,12 s. Le graphe Roslyn atteint 11 566 nœuds / 90 143 relations / 0 diagnostic, frais et
  déterministe. Aucun modèle externe, réseau implicite, exécutable Blender, module source, commit ou
  publication n’est utilisé ou muté par cette qualification.
- UX11-B termine la revue humaine corrective des 36 pages : module vide et Crypte des Cendres,
  recherche et références, manipulation 3D, ajout/duplication/suppression, sauvegarde/réouverture,
  dialogue, quête UX5, scripts, diagnostics et **Tester ici** ont été rejoués. La seconde matrice
  visuelle couvre 36/36 pages en FHD 100 %, QHD 150 % et 4K 200 % sans défaut bloquant. Les 66 lignes
  historiques sont `Parity`, les 19/19 tests de matrice et le smoke global passent. Le paquet Studio
  Windows à 1 109 fichiers et le paquet Linux à 1 108 fichiers embarquent leur bibliothèque
  ForgeScript native, passent export, hashes et conformité 38/38. Le smoke de distribution passe sur
  Windows et sur Debian 13 x86_64 : retouche, cache script, natif, UX3-F, UX5-A et UX5-B, avec modèle,
  fichier projet, contexte et approbations inchangés. Le portail transversal final passe 2 117 tests
  .NET, 44 tests Python et tous ses smokes ;
  le graphe Roslyn atteint 11 577 nœuds / 90 175 relations / 0 diagnostic, frais et déterministe.
  Le détail et la limite `STUDIO-043` sont consignés dans
  `docs/acceptance/STUDIO_UX11_B.md`.
- UX3-A ouvre la vague Contenu gouverné à 5/34 avec blueprints réutilisables, instances réelles,
  dérivation canonique et palettes Standard/Projet. Le portail final passe 22/22 tests en 144,41 s,
  le smoke global et les inspections Forward+ FHD/QHD/4K ; la surface persiste les données sans
  placer d'objet, charger d'asset ou exécuter de moteur, script ou réseau live.
- UX3-B porte la vague à 10/34 avec propagation sélective, migration persistable, catégories
  hiérarchiques, variantes et compositions. Deux commandes et approbations indépendantes passent
  17/17 tests, le smoke global, un portail incrémental en 146,04 s et deux inspections Forward+ FHD ;
  la projection reste pure et ne charge ni asset, prefab, scène, script, moteur ou réseau live.
- UX3-C porte la vague à 14/34 avec favoris/récents personnels, recherche avancée, miniature locale
  gouvernée et drag-and-drop vers le placement canonique. Les deux historiques indépendants passent
  23/23 tests, le smoke global, un portail incrémental en 153,00 s et l'inspection Forward+ FHD ;
  aucun asset, prefab, script, acteur, moteur ou réseau live n'est chargé ou exécuté par Studio.
- UX3-D porte la vague à 16/34 avec fermeture transitive des dépendances, budget plateforme et
  gouvernance source/auteur/SPDX/SHA-256/permission. Deux commandes et approbations indépendantes
  passent 45/45 tests, le smoke global, un portail incrémental en 191,90 s et deux inspections
  Forward+ FHD ; le build rejette une dépendance absente avant packaging, sans charger d'asset,
  créer de package ni exécuter script, acteur, moteur ou réseau live.
- UX3-E porte la vague à 18/34 avec mesure locale GLB/PNG, six diagnostics artistiques et budget
  par plateforme sur sept axes. Deux commandes et approbations indépendantes passent 11/11 tests,
  le smoke global, un portail incrémental en 156,86 s et deux inspections Forward+ FHD ; le build
  refuse les dépassements marqués bloquants sans instancier d’asset, mesurer le GPU, produire de
  package ni exécuter script, acteur, moteur ou réseau live.
- UX3-F porte la vague à 23/34 avec navigateur unifié, facettes texture/modèle, profils d’import
  KTX2/GLB et requête Blender allowlistée. Deux historiques et approbations indépendants passent
  32/32 tests, le smoke global, un portail incrémental en 157,96 s et trois inspections Forward+
  FHD ; aucun asset n’est téléchargé ou transformé, aucun réimport, processus Blender, package,
  script, acteur, moteur ou réseau live n’est exécuté.
- UX3-G porte la vague à 27/34 avec manifestes gouvernés complets, chunks et budgets plateforme,
  collision/navigation, clips d’animation ordonnés et profils VFX/LOD. La commande canonique passe
  15/15 tests, le smoke global, un portail incrémental en 160,44 s et quatre inspections Forward+
  FHD ; aucun mesh, bake, playback, rendu VFX, génération LOD, benchmark GPU, package, script,
  acteur, moteur ou réseau live n’est exécuté.
- UX3-H porte la vague à 31/34 avec imports de fichiers ouverts configurés et réversibles, export
  déterministe de blueprints transitifs, sources textuelles gouvernées et tables typées. Les trois
  frontières transactionnelles explicites passent 18/18 tests, le smoke global, un portail
  incrémental en 173,10 s et cinq inspections Forward+ FHD ; aucun ERF propriétaire n’est décodé,
  aucun package n’est installé et aucun script, acteur, moteur ou réseau live n’est exécuté.
- vague UX3 close à 34/34 avec provenance SPDX/droits/IA, quarantaine fail-closed et politique de
  génération Image/Audio/VFX planifiée sans fournisseur. Les deux historiques explicites passent
  12/12 tests, le smoke global, un portail incrémental en 171,71 s et quatre inspections Forward+
  FHD ; aucun antivirus n’est simulé dans le produit et aucun média, import, rendu ou appel distant
  n’est exécuté.
- UX6-A ouvre la vague Gameplay avec `ORF-PROJ-023`, `ORF-GAME-017`, `ORF-GAME-023` et
  `ORF-GAME-024` : difficultés, mutateurs, plugins de règles et temps du monde passent par deux
  commandes séparées, deux dry-runs et deux approbations. Le portail incrémental couvre 21/21 tests
  en 163,60 s, le smoke global et une capture Forward+ FHD inspectée ; `full_gate=false` reste
  explicite dans `docs/acceptance/STUDIO_UX6_A.md`.
- UX6-B qualifie `ORF-GAME-001`, `ORF-GAME-010`, `ORF-GAME-012` et `ORF-GAME-013` : déplacement,
  interactions, caméras et entrées partagent une transaction canonique et quatre résolveurs purs.
  Le portail incrémental repasse 8/8 tests en 134,00 s, le smoke global et une capture Forward+ FHD
  corrigée puis inspectée ; UX6 atteint 8/84 et `full_gate=false` reste explicite dans
  `docs/acceptance/STUDIO_UX6_B.md`.
- UX6-C qualifie `ORF-GAME-003`, `ORF-GAME-004`, `ORF-GAME-011`, `ORF-GAME-015` et
  `ORF-GAME-016` : nage, escalade, radial, temps tactique et combat partagent la même transaction
  gameplay et cinq résolveurs bornés. Le portail final repasse 7/7 tests en 133,93 s, le smoke
  global et une capture Forward+ FHD corrigée puis inspectée ; UX6 atteint 13/84 et
  `full_gate=false` reste explicite dans `docs/acceptance/STUDIO_UX6_C.md`.
- UX6-D qualifie `ORF-GAME-002`, `ORF-GAME-008`, `ORF-GAME-009` et `ORF-GAME-014` : vol libre,
  projectiles, physique 3D et ciblage remplacent uniquement la simulation gameplay et consomment
  quatre résolveurs déterministes. Le portail incrémental repasse 11/11 tests en 126,38 s, le smoke
  global et une capture Forward+ FHD inspectée ; UX6 atteint 17/84 et `full_gate=false` reste
  explicite dans `docs/acceptance/STUDIO_UX6_D.md`.
- UX6-E qualifie `ORF-GAME-005`, `ORF-GAME-006`, `ORF-GAME-007` et `ORF-GAME-028` : apesanteur,
  véhicules, montures et succès remplacent uniquement `PostLaunch.Gameplay` via la commande
  canonique des extensions. Le portail incrémental passe 13/13 tests en 136,22 s, le smoke global
  et la capture Forward+ FHD inspectée ; UX6 atteint 21/84 et `full_gate=false` reste explicite dans
  `docs/acceptance/STUDIO_UX6_E.md`.
- UX6-F qualifie `ORF-SCR-001`, `ORF-SCR-002`, `ORF-SCR-003`, `ORF-SCR-006` et `ORF-SCR-007` :
  source C-like, couple source/bytecode, compilateur intégré, handlers d’événements typés et fonction
  `main` passent par l’historique documentaire ForgeScript et l’écriture atomique `.as` + métadonnée.
  Le portail incrémental avec graphe passe 34/34 tests en 284,26 s, le smoke global et une capture Forward+ FHD
  corrigée puis inspectée ; UX6 atteint 26/84 et `full_gate=false` reste explicite dans
  `docs/acceptance/STUDIO_UX6_F.md`.
- UX6-G qualifie `ORF-SCR-004`, `ORF-SCR-005`, `ORF-SCR-008`, `ORF-SCR-010` et `ORF-SCR-011` :
  éditeur assisté, Script Wizard relu, actions différées, combat et effets partagent exactement la
  source, l’historique et l’écriture atomique UX6-F. Le portail incrémental avec graphe passe 25/25
  tests en 251,59 s, le smoke global et une capture Forward+ FHD corrigée puis inspectée ; UX6
  atteint 31/84 et `full_gate=false` reste explicite dans `docs/acceptance/STUDIO_UX6_G.md`.
- UX6-H qualifie `ORF-SCR-012` à `ORF-SCR-016` : inventaire, journal/quêtes, dialogues,
  factions/réputation et perception partagent la source, l’historique, le dry-run, la compilation,
  la sandbox et l’écriture atomique UX6-F. Le portail incrémental passe 13/13 tests en 166,56 s,
  le smoke global et une capture Forward+ FHD corrigée puis inspectée ; UX6 atteint 36/84 et
  `full_gate=false` reste explicite dans `docs/acceptance/STUDIO_UX6_H.md`.
- UX6-I qualifie `ORF-SCR-009`, `ORF-SCR-017`, `ORF-SCR-021`, `ORF-SCR-022` et `ORF-SCR-027` :
  zones/objets, mouvement, math/chaînes, variables locales typées et frontière sandbox partagent
  la source, l’historique, le compilateur et l’écriture atomique UX6-F. Le portail incrémental passe
  18/18 tests en 197,50 s, le smoke global et une capture Forward+ FHD inspectée ; UX6 atteint
  41/84 et `full_gate=false` reste explicite dans `docs/acceptance/STUDIO_UX6_I.md`.
- UX6-J qualifie `ORF-SCR-028`, `ORF-SCR-029`, `ORF-SCR-030`, `ORF-SCR-031` et `ORF-SCR-036` :
  handles opaques, budgets d’instructions et mémoire, déterminisme et version d’API passent par une
  politique R014 versionnée et un historique séparé, sans modifier la source UX6-F. Le portail final
  repasse 27/27 tests en 156,85 s, le smoke global et une capture Forward+ FHD corrigée puis
  inspectée ; UX6 atteint 46/84 et `full_gate=false` reste explicite dans
  `docs/acceptance/STUDIO_UX6_J.md`.
- UX6-K qualifie `ORF-SCR-039` : allowlist du module, kits et liaisons d’événements passent par le
  manifeste R015 versionné et son historique, avec deny-by-default, serveur et signature. Le portail
  final repasse 14/14 tests en 159,24 s, le smoke global et trois captures rendues inspectées ; UX6
  atteint 47/84 et `full_gate=false` reste explicite dans `docs/acceptance/STUDIO_UX6_K.md`.
- UX6-L qualifie `ORF-SCR-019`, `ORF-SCR-020` et `ORF-SCR-023` : audio/musique,
  horloge/timers et événements custom partagent la source, l’historique, le dry-run et l’écriture
  atomique UX6-F. Les simulations C# déterministes et l’exécution native R099 passent 15/15 tests,
  le portail incrémental final en 156,29 s, le smoke global et trois captures Forward+ FHD inspectées ;
  UX6 atteint 50/84 et `full_gate=false` reste explicite dans `docs/acceptance/STUDIO_UX6_L.md`.
- UX6-M qualifie `ORF-SCR-018`, `ORF-SCR-024`, `ORF-SCR-025` et `ORF-SCR-026` : plans caméra et
  cutscenes, routes tag/événement, switches typés du module et valeurs IA partagent un unique
  `ScriptAutomationProfile` et la commande canonique `SetWorldFeatureCatalog`. Le portail passe
  60/60 tests en 162,07 s, le smoke global et quatre captures Forward+ FHD inspectées ; UX6 atteint
  54/84 et `full_gate=false` reste explicite dans `docs/acceptance/STUDIO_UX6_M.md`.
- UX6-N qualifie `ORF-SCR-032` et `ORF-SCR-033` : hot reload versionné, migration/rollback,
  invalidation des dépendances, breakpoints, pile, locales, watches, inspection, événements et pas
  contrôlés complètent l’atelier R099 sans autorité source parallèle. Le portail passe 28/28 tests
  en 131,98 s, le smoke global et deux captures Forward+ FHD inspectées ; UX6 atteint 56/84 et
  `full_gate=false` reste explicite dans `docs/acceptance/STUDIO_UX6_N.md`.
- UX6-O qualifie `ORF-SCR-034`, `ORF-SCR-035`, `ORF-SCR-037`, `ORF-SCR-038` et `ORF-SCR-040` :
  tests unitaires, profilage, documentation générée, analyse statique et état sérialisé partagent un
  profil qualité R100 versionné sans réécrire la source UX6-F. Le portail passe 16/16 tests en
  135,51 s, le smoke global et cinq captures Forward+ FHD inspectées ; UX6 atteint 61/84 et
  `full_gate=false` reste explicite dans `docs/acceptance/STUDIO_UX6_O.md`.
- UX6-P qualifie `ORF-SCR-041` : profil interop R101, plans source et liaison, snapshot exact,
  compilation/tests MCP, quotas/récupération et adaptateur runtime réutilisent la source UX6-F sans
  voie parallèle. Le portail passe 10/10 tests en 135,87 s, le smoke global et trois captures
  Forward+ FHD inspectées ; UX6 atteint 62/84 et `full_gate=false` reste explicite dans
  `docs/acceptance/STUDIO_UX6_P.md`.
- UX6-Q qualifie `ORF-EE-031` et `ORF-AI-018` : profil compilateur incrémental avec cache,
  invalidation et diagnostics stables, puis création assistée locale d’une source canonique `.as`
  et de sa métadonnée gouvernée dans deux transactions distinctes. Le portail passe 16/16 tests en
  137,50 s, le smoke global et deux captures Forward+ FHD inspectées ; UX6 atteint 64/84 et
  `full_gate=false` reste explicite dans `docs/acceptance/STUDIO_UX6_Q.md`.
- UX6-R qualifie `ORF-EE-011`, `ORF-EE-012`, `ORF-EE-013`, `ORF-EE-015` et `ORF-EE-016` :
  règles data-driven, interface scriptable, événements GUI typés, documents JSON révisionnés et
  politique d’état sérialisable partagent `SetRuntimeExtensionsHandler`. Le portail passe 24/24
  tests en 140,90 s, le smoke global renforcé et quatre captures Forward+ FHD inspectées ; UX6
  atteint 69/84 et `full_gate=false` reste explicite dans `docs/acceptance/STUDIO_UX6_R.md`.
- UX6-S qualifie `ORF-EE-014`, `ORF-EE-017`, `ORF-EE-020` et `ORF-EE-021` : requêtes de
  périphérique consenties/anonymisées, ressources VM sous quotas, groupe borné et progression
  data-driven partagent `SetRuntimeExtensionsHandler` et les résolveurs PostLaunch. Le portail
  passe 19/19 tests en 140,55 s, le smoke global et quatre captures Forward+ FHD inspectées ; UX6
  atteint 73/84 et `full_gate=false` reste explicite dans `docs/acceptance/STUDIO_UX6_S.md`.
- UX6-T qualifie `ORF-EE-022` et `ORF-EE-024` : formes AOE, portée, hauteur, couverture,
  confirmation serveur et palettes accessibles partagent la surface avec les sept capacités NUI
  2.0. Le portail passe 15/15 tests en 147,77 s, le smoke global et deux captures Forward+ FHD
  inspectées ; UX6 atteint 75/84 et `full_gate=false` reste explicite dans
  `docs/acceptance/STUDIO_UX6_T.md`.
- UX6-U qualifie `ORF-GAME-022` : définition typée par commande canonique et blackboard copié avec
  handles, mutation, vue debug, deltas de réplication, snapshot/reload et frontière legacy. Le
  portail passe 56/56 tests en 144,09 s, le smoke global et une capture Forward+ FHD inspectée ;
  UX6 atteint 76/84 et `full_gate=false` reste explicite dans `docs/acceptance/STUDIO_UX6_U.md`.
- UX6-V qualifie `ORF-EE-028` : configuration persistante des profils d’agent et laboratoire A*
  copié avec obstacles, coûts, récupération, budget, mise à jour partielle et plan EngineAdapter.
  Le portail passe 10/10 tests en 144,21 s, le smoke global et une capture Forward+ FHD inspectée ;
  UX6 atteint 77/84 et `full_gate=false` reste explicite dans `docs/acceptance/STUDIO_UX6_V.md`.
- UX6-W qualifie `ORF-EE-001` et `ORF-EE-027` : overrides hiérarchiques et tweens Transform,
  Paramètre matériau et État passent par `SetCinematicProfilesHandler`, avec runtimes purs copiés,
  autorité, réplication, sauvegarde et annulation. Le portail final passe 10/10 tests en 146,60 s,
  le smoke global et trois captures Forward+ FHD inspectées après correction responsive ; UX6
  atteint 79/84 et `full_gate=false` reste explicite dans `docs/acceptance/STUDIO_UX6_W.md`.
- UX6-X qualifie `ORF-GAME-026` et `ORF-EE-026` : le shell carte/journal/HUD passe par la
  commande gameplay du projet, tandis que police et échelles passent par l’historique du profil
  utilisateur. Le portail final repasse 22/22 tests en 162,88 s, le smoke global et deux captures Forward+ FHD
  corrigées puis inspectées ; UX6 atteint 81/84 et `full_gate=false` reste explicite dans
  `docs/acceptance/STUDIO_UX6_X.md`.
- UX6-Y qualifie `ORF-GAME-018` et `ORF-EE-023` : la politique météo runtime passe par
  `SetAreaEnvironment`, tandis que concurrence, cache, annulation et benchmark de streaming passent
  par `SetAreaWorldProfile`. Le portail final repasse 33/33 tests en 163,69 s, le smoke global et
  deux captures Forward+ FHD inspectées après correction du libellé d’isolation ; UX6 atteint 83/84
  et `full_gate=false` reste explicite dans `docs/acceptance/STUDIO_UX6_Y.md`.
- UX6-Z qualifie `ORF-AI-020` : l'assistant local produit une capacité canonique pour un porteur
  explicite, traverse cinq scénarios `CombatResolver`, l'équilibrage, l'approbation et le dry-run,
  puis applique par `ApplyAssistedAbilityHandler`. Le portail passe 6/6 tests en 146,31 s, le smoke
  global et une capture Forward+ FHD inspectée ; UX6 est close à 84/84 et `full_gate=false` reste
  explicite dans `docs/acceptance/STUDIO_UX6_Z.md`.
- UX7-A qualifie `ORF-AREA-037`, `ORF-AUD-020`, `ORF-QA-001` à `ORF-QA-007` et
  `ORF-QA-021` : l'atelier Validation agrège les autorités de schéma, références, graphes,
  géométrie, navigation, localisation, licences, zone et audio, humanise les diagnostics et ouvre
  leur cible. Le portail passe 33/33 tests en 300,73 s, le smoke global et une capture Forward+ FHD
  inspectée ; UX7 atteint 10/43 et `full_gate=false` reste explicite dans
  `docs/acceptance/STUDIO_UX7_A.md`.
- UX7-B qualifie `ORF-QA-008`, `ORF-QA-010`, `ORF-QA-015` et `ORF-QA-020` : le laboratoire
  Qualité exécute dans un isolat quatre harness navigables, une annulation réelle, un replay
  headless, de vrais fichiers `.orpsave` et un échantillon JSON confidentiel, puis applique le
  profil par sa commande canonique. Le portail passe 25/25 tests en 245,20 s, le smoke global et
  une capture Forward+ FHD corrigée puis inspectée ; UX7 atteint 14/43 et `full_gate=false` reste
  explicite dans `docs/acceptance/STUDIO_UX7_B.md`.
- UX7-I qualifie `ORF-EE-029` et clôt UX7 à 43/43 : le profil d'observabilité passe par
  `SetRuntimeExtensionsHandler`, le fallback OpenTelemetry produit une trace JSON réelle et
  confinée pour Editor/Runtime/Serveur/ForgeScript, tandis que Tracy natif reste conditionné à un
  Godot instrumenté et renvoie un diagnostic stable lorsqu'il manque. Le portail passe 14/14 tests
  en 420,22 s, le smoke global et une capture Forward+ FHD corrigée puis inspectée ; la matrice
  atteint 624 `functional` / 93 `planned` et `full_gate=false` reste explicite dans
  `docs/acceptance/STUDIO_UX7_I.md`.
- portail transversal complet du jalon UX6-E à UX6-G réussi avec 2 035 tests .NET, 39 tests Python
  et les smokes Studio, Toolset, Runtime/Forward+, Crypte des Cendres, Blender, MCP, réseau,
  sauvegarde/reprise et distribution ;
- portail transversal complet du jalon UX6-A à UX6-D réussi avec 2 035 tests .NET, 39 tests Python
  et les smokes Studio, Toolset, Runtime, Forward+, Crypte des Cendres, Blender, MCP, réseau,
  sauvegarde/reprise et distribution ;
- portail transversal complet du lot Crypte réussi avec 2 118 tests .NET, 64 tests Python et les
  smokes Studio, Toolset, Runtime/Forward+, Crypte des Cendres, Blender, MCP, réseau,
  sauvegarde/reprise et distribution ;
- graphe Roslyn courant déterministe et sans diagnostic ; les compteurs évolutifs restent lus dans
  `docs/architecture/graph.json` et ne sont plus recopiés manuellement dans ce statut.

## Gameplay systémique — preuve actuelle

- ruleset clean-room canonique dans `docs/design/OPENRPG_FORGE_RULESET_FR.md` ;
- manifeste versionné de la Crypte dans
  `apps/Forge.Runtime/content/crypt-of-ashes/systemic-gameplay.json` ;
- cinq interactions environnementales : huile et feu, eau et givre, brasero suspendu, pilier
  fracturé et fosse de pointes ;
- contrôles isolés au module : `X` poussée, `F` feu, `G` givre, `B` force et `Y` concentration ;
- dégâts indirects résolus par le chemin de combat autoritaire existant afin de conserver phases,
  événements, butin, progression et transitions de quête ;
- contrat Python historique : 8 tests déjà exécutés avec succès avant la reconstruction spatiale.

## Crypte des Cendres — reconstruction spatiale intégrée et qualifiée

La reconstruction intégrée à `main` remplace la salle canonique 20 × 28 m par une zone 48 × 76 m,
40 entités et 30 collisions. Les identifiants critiques sont conservés mais répartis dans un petit
donjon :

1. Seuil des Braises ;
2. Sanctuaire d’Elyra ;
3. Catacombes et reliquaires ;
4. porte scellée et salle de la Sentinelle ;
5. Ossuaire inondé ;
6. Grande Nef ;
7. Sanctuaire du Gardien.

Le snapshot de zone lui-même a été modifié : sols, murs internes, verrou spatial de la porte, zones
latérales et positions des acteurs ne reposent plus uniquement sur un décorateur runtime. La couche
artistique pilotée par `kaykit-dungeon-remastered.layout.json` compose les mêmes espaces via `GodotArtKitBuilder`
Remastered CC0-1.0 déjà gouverné. Des colliders de sécurité ferment les silhouettes étroites jusqu’à
leur exposition complète dans le Creator.

Les interactions systémiques ont été repositionnées dans les nouvelles salles : huile et givre dans
l’Ossuaire inondé, brasero, pilier et pointes dans le Sanctuaire. Les prompts d’interaction sont
maintenant masqués au-delà de 7,5 m afin de ne pas traverser visuellement tout le donjon.

Les contrats statiques `tests/test_crypt_systemic_gameplay.py` et
`tests/test_crypt_dungeon_rebuild.py` passent. Le portail Windows complet passe avec 2 118 tests
.NET et 64 tests Python. Le smoke natif Forward+ sur AMD Radeon RX 7800 XT charge quatre modèles
KayKit gouvernés et animés, 340 instances artistiques — 197 sols, 110 murs et 33 accessoires —,
valide les cinq interactions et cinq contrôles, la caméra libre et subjective, le passage critique
par la porte de 4 m, l’acquisition de la clé, le déverrouillage, la seconde salle et le déplacement
au sol sans saut parasite. Le snapshot de playtest copie désormais ses ressources `assets` et
`scripts`, ce qui rend cette preuve autonome. Cette qualification est automatisée ; aucune nouvelle
revue visuelle humaine complète FHD/QHD n’a été réalisée dans ce lot.

## Consolidation réalisée

- ADR 0045 accepté, contrat d'acceptation Studio et matrice exhaustive des 66 anciennes `Window`
  vers les 36 pages ; le plugin est conservé jusqu'à parité prouvée ;
- coque Godot .NET autonome sans `EditorPlugin` : reprise du module et de la zone, navigation
  persistée, Module Hub, aperçu réel des 218 objets de l'Auberge du Cerf d'Ambre, 211 modèles
  gouvernés en Forward+, sélection 3D et inspecteur unique ;
- UX0-A qualifié : Explorateur compact synchronisé avec la 3D, filtres enregistrables, recherche
  globale `Ctrl+K`, références inverses et aide contextuelle sourcée ; smoke dédié, preuves FHD/QHD
  et chargement Forward+ de Crypte des Cendres sans fallback ni erreur d’asset ;
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
  FHD/QHD/4K et smoke contractuel sans écriture de Crypte des Cendres ;
- UX1-E qualifié pour `ORF-PLC-001` : blueprint de décor accessible depuis Objets & interactions
  et `Ctrl+K`, ressource choisie par nom humain et usage réel, collision, rôle statique ou utilisable,
  interaction, transform indépendant du moteur et scripts ; aperçu canonique, sélection 3D,
  préservation des catalogues voisins, undo/redo et round-trip exact ; preuves Forward+ FHD/QHD/4K
  et smoke contractuel sans écriture de Crypte des Cendres ;
- UX1-F qualifié pour `ORF-PLC-002` : accès direct au décor statique depuis Objets & interactions et
  `Ctrl+K`, regroupement, instanciation, occlusion, fusion avec garde collision et collision globale
  optionnelle ; aperçu d’impact, sélection 3D, parité commande/UI, préservation des données masquées,
  undo/redo et round-trip exact ; preuves Forward+ FHD/QHD/4K et smoke contractuel sans écriture de
  Crypte des Cendres ;
- UX1-G qualifié pour `ORF-PLC-003` : accès direct à l’objet utilisable depuis Objets & interactions
  et `Ctrl+K`, cartes d’actions ajoutables, portée, angle, durée, focus, autorité, conditions et
  `OnUsed` gouverné ; aperçu complet, sélection 3D, parité commande/UI, événement runtime typé,
  préservation des données masquées, undo/redo et round-trip exact ; preuves Forward+ FHD/QHD/4K et
  smoke contractuel sans écriture de Crypte des Cendres ;
- UX1-H qualifié pour `ORF-PLC-004` : accès direct au conteneur depuis Objets & interactions et
  `Ctrl+K`, capacité, masse, propriétaire, droits, persistance, réplication, inventaire, butin et
  présentation ; aperçu canonique, parité commande/UI, projection RPG Kit, lecture ForgeScript,
  préservation des capacités voisines et références supplémentaires, undo/redo et round-trip exact ;
  preuves Forward+ FHD/QHD/4K et smoke contractuel sans écriture de Crypte des Cendres ;
- UX1-I qualifié pour `ORF-PLC-005` : accès direct à l’identité et à l’apparence depuis Objets &
  interactions et `Ctrl+K`, nom court, noms/invitations localisés, tags, notes, asset, meshes,
  matériaux, icône, portrait et VFX ; aperçu canonique, parité commande/UI, projection RPG Kit,
  lecture ForgeScript/MCP, préservation, undo/redo et round-trip exact ; preuves Forward+
  FHD/QHD/4K et smoke contractuel sans écriture de Crypte des Cendres ;
- UX1-J qualifié pour `ORF-PLC-006` : transformation précise depuis Objets & interactions et
  `Ctrl+K`, position, rotation, échelle, pivot, grille, parent humain, surface et animation ; aperçu
  canonique, hiérarchie et géométrie validées, parité commande/UI, projection RPG Kit, lecture
  ForgeScript/MCP, préservation, undo/redo et round-trip exact ; preuves Forward+ FHD/QHD/4K et
  smoke contractuel sans écriture de Crypte des Cendres ;
- UX1-K qualifié pour `ORF-PLC-007` : animation initiale depuis Objets & interactions et `Ctrl+K`,
  six états humains, animation, apparence et interaction par état, transitions ajoutables avec
  déclencheur, durée et condition, puis aperçu sémantique ; validation canonique, projection et
  transition RPG Kit, lecture ForgeScript/MCP, préservation, undo/redo et round-trip exact ; preuves
  Forward+ FHD/QHD/4K et smoke contractuel sans écriture de Crypte des Cendres ;
- UX1-L qualifié pour `ORF-PLC-008` : destructibilité depuis Objets & interactions et `Ctrl+K`,
  santé, armure, résistances typées, étapes visuelles, effet de destruction, débris, réparation et
  aperçu local de dégâts ; validation canonique, projection puis dégâts/destruction/réparation RPG
  Kit, lecture ForgeScript/MCP, préservation, undo/redo et round-trip exact ; preuves Forward+
  FHD/QHD/4K et smoke contractuel restaurant Crypte des Cendres ;
- UX1-M qualifié pour `ORF-PLC-009` : protection plot depuis Objets & interactions et `Ctrl+K`,
  actions protégées, droits requis, feedback joueur et dérogation MJ avec motif et audit gouverné ;
  validation canonique, quatre décisions runtime, lecture ForgeScript/MCP, préservation, undo/redo
  et round-trip exact ; preuves Forward+ FHD/QHD/4K et smoke contractuel restaurant Crypte des Cendres ;
- UX1-N qualifié pour `ORF-PLC-010` : faction et propriété depuis Objets & interactions et `Ctrl+K`,
  faction principale, appartenances, hostilité, crime, accès et ciblage, avec aperçu de décision sans
  écriture ; validation canonique, projection et décisions RPG Kit, lecture ForgeScript/MCP,
  préservation du champ Aurora historique, undo/redo et round-trip exact ; preuves Forward+
  FHD/QHD/4K sur factions synthétiques en mémoire et smoke contractuel restaurant Crypte des Cendres ;
- UX1-O qualifié pour `ORF-PLC-011` : conversation depuis Objets & interactions et `Ctrl+K`,
  dialogue, point d’entrée, interaction, état, utilisateur, objet et événement, priorités et usage
  unique, avec aperçu de résolution sans écriture ; validation canonique, projection et décision RPG
  Kit, lecture ForgeScript/MCP, préservation du dialogue Aurora historique, undo/redo et round-trip
  exact ; preuves Forward+ FHD/QHD/4K sur le catalogue réel et smoke contractuel restaurant Crypte des Cendres ;
- UX1-P qualifié pour `ORF-PLC-012` : verrouillage depuis Objets & interactions et `Ctrl+K`, quatre
  états, clé item réelle, justificatifs, défi, code masqué, piratage, droits, reverrouillage et
  brouillage, avec aperçu sans écriture ; validation canonique, résolution RPG Kit, lecture
  ForgeScript/MCP sans secret, préservation, undo/redo et round-trip exact ; preuves Forward+
  FHD/QHD/4K sur le catalogue réel et smoke contractuel restaurant Crypte des Cendres ;
- UX1-Q qualifié pour `ORF-PLC-013` : clé consommée depuis Objets & interactions et `Ctrl+K`, avec
  conservation, consommation, charges, partage, permission virtuelle et audit, plus aperçu de la
  décision sans mutation d’inventaire ; validation canonique, adaptateur RPG Kit, lecture
  ForgeScript/MCP, préservation du verrou et des données cachées, undo/redo et round-trip exact ;
  preuves Forward+ FHD/QHD/4K sur le catalogue réel et smoke contractuel restaurant Crypte des Cendres ;
- UX1-R qualifié pour `ORF-PLC-014` : piège depuis Objets & interactions et `Ctrl+K`, avec
  déclencheur, effet, détection, désamorçage, réarmement, propriétaire, politique alliée, VFX, état
  armé et usage unique, plus aperçu sans effet de scène ; sous-profils de sécurité indépendants,
  validation canonique, adaptateur RPG Kit, lecture ForgeScript/MCP, préservation des événements
  voisins, undo/redo et round-trip exact ; preuves Forward+ FHD/QHD/4K sur le catalogue réel et
  smoke contractuel restaurant Crypte des Cendres ;
- UX1-S qualifié pour `ORF-PLC-015` : trésor initial depuis Objets & interactions et `Ctrl+K`, avec
  table d’objets nommés, catégories, poids, quantités, qualité, conditions, unicité, moment, graine et
  état généré persistant ; modification d’un état existant protégée par remise à zéro explicite,
  validation canonique, adaptateur RPG Kit indépendant, lecture ForgeScript/MCP, préservation du
  piège et des événements voisins, undo/redo et round-trip exact ; preuves Forward+ FHD/QHD/4K sur
  le catalogue réel et smoke contractuel restaurant Crypte des Cendres ;
- UX1-T qualifié pour `ORF-PLC-016` : événements d’ouverture et de fermeture depuis Objets &
  interactions et `Ctrl+K`, avec version ForgeScript, liaisons nommées, acteur, résultat, état
  précédent et autorité ; aperçu du dispatch typé sans écriture, validation canonique, adaptateur RPG
  Kit indépendant, lecture ForgeScript/MCP, préservation du piège, du trésor et des événements
  voisins, undo/redo et round-trip exact ; preuve Forward+ FHD sur le catalogue réel et smoke
  contractuel restaurant Crypte des Cendres ;
- UX1-U qualifié pour `ORF-PLC-017` : événements avant et après verrouillage/déverrouillage depuis
  Objets & interactions et `Ctrl+K`, avec version ForgeScript, méthode, justificatif, acteur et veto
  contrôlé ; aperçus accepté ou refusé sans écriture, validation canonique, adaptateur RPG Kit
  indépendant, lecture ForgeScript/MCP, préservation du verrou et des événements voisins, undo/redo
  et round-trip exact ; preuve Forward+ FHD sur le catalogue réel et smoke contractuel restaurant
  Crypte des Cendres ;
- UX1-V qualifié pour `ORF-PLC-018` à `ORF-PLC-020` : événements d'utilisation, de contenu modifié,
  de dégâts et de mort, avec résolveur Domain commun pour les autorités directe/composée, anciens
  JSON, validation runtime, lecture ForgeScript/MCP, préservation, undo/redo et round-trip ; portail
  incrémental 21/21 et preuve Forward+ FHD inspectée ;
- UX1-W qualifié pour `ORF-EE-002`, `ORF-EE-018` et `ORF-EE-019` : échelle explicite de la sélection,
  politique globale de zones dynamiques et aperçu de copie runtime limité au contexte réellement
  disponible, avec commandes canoniques, préservation, undo/redo et round-trip ; portail
  incrémental 27/27 et preuve Forward+ FHD inspectée ;
- UX1-X qualifié pour `ORF-DOOR-001` à `ORF-DOOR-003` : porte choisie explicitement, placement
  libre ou ancré, neuf états et interaction d'ouverture ; navigation interne non exposée, données
  voisines préservées, portail incrémental 5/5 en 71,99 s et preuve Forward+ FHD inspectée ;
- UX1-Y qualifié pour `ORF-TRG-001`, `ORF-TRG-002`, `ORF-TRG-006` et `ORF-TRG-007` : polygone
  extrudé, politique générique, OnEnter/OnExit, empreinte X/Z uniforme et scripts facultatifs ;
  Portal/Validation/Advanced préservés, portail incrémental 8/8 en 62,22 s et preuves Forward+
  FHD/QHD/4K inspectées après correction du bornage vertical ;
- UX1-Z qualifié pour `ORF-PLC-021` à `ORF-PLC-024` : attaques, capacités, heartbeat/événement
  personnalisé et politique physique, avec valeurs dormantes/capacités voisines préservées, aperçu
  explicitement sans moteur ni réseau, portail incrémental 5/5 en 60,77 s et preuve FHD inspectée ;
- UX1-AA qualifié pour `ORF-PLC-025` à `ORF-PLC-027` : sockets/assemblages, huit actions standard
  plus personnalisées et validation localisée, avec prérequis explicitement refusés, preuve isolée,
  portail incrémental 8/8 en 54,91 s et captures Forward+ FHD/QHD/4K inspectées ;
- UX1-AB qualifié pour `ORF-DOOR-007`, `ORF-DOOR-010`, `ORF-DOOR-014` et `ORF-DOOR-015` : dégâts,
  chargement, OnDamaged/OnDeath et attaque physique, fusion autoritaire sans toucher aux événements
  voisins, portail incrémental 2/2 en 55,42 s et captures Forward+ FHD/QHD/4K inspectées ;
- UX1-AC qualifié pour `ORF-TRG-004`, `ORF-TRG-011`, `ORF-TRG-017` et `ORF-TRG-019` : piège,
  filtre, dix volumes et occupation, Foundation requise, preuve isolée, préservation des voisins,
  portail incrémental 10/10 en 59,92 s et captures Forward+ FHD/QHD/4K inspectées ;
- UX1-AD qualifié pour `ORF-DOOR-016`, `ORF-DOOR-017`, `ORF-DOOR-018` et `ORF-DOOR-020` :
  capacités, heartbeat/événement personnalisé, conversation/faction et panneaux coordonnés, avec
  fusion bornée, profils dangereux refusés, portail 4/4 en 68,48 s et preuves FHD/QHD/4K ;
- UX1-AE qualifié pour `ORF-TRG-005`, `ORF-TRG-008`, `ORF-TRG-012` et `ORF-TRG-013` : secret,
  clic, justificatif et présentation, avec Foundation exigée, script allowlisté, preuve isolée,
  portail 5/5 en 68,12 s et preuves FHD/QHD/4K ;
- UX1-AF qualifié pour `ORF-AI-014` et `ORF-AI-015` : document de plan puis construction de zone,
  avec confidentialité/provenance, deux historiques, double dry-run, preuve hors ligne isolée,
  portail 17/17 en 75,68 s et preuves FHD/QHD/4K ;
- UX1-AG qualifié pour `ORF-DOOR-004`, `ORF-DOOR-005`, `ORF-DOOR-006`, `ORF-DOOR-012` et
  `ORF-DOOR-013` : verrou/clé, refus, piège et événements terminaux, avec scaffold PortalUse interne
  non promu, préservation stricte, portail 18/18 en 75,98 s et preuves FHD/QHD/4K ;
- UX1-AH qualifié pour `ORF-TRG-009`, `ORF-TRG-010` et `ORF-TRG-018` : heartbeat, événement
  personnalisé et contrôle MJ audité, avec preuve isolée et sans dispatch Live Director réel,
  portail 6/6 en 69,34 s et preuves FHD/QHD/4K ;
- UX1-AI qualifié pour `ORF-TRG-020` : contexte et diagnostics canoniques du trigger, état optionnel
  ou requis et références humaines protégées, sans duplication du validateur, portail 6/6 en
  64,99 s et preuves FHD/QHD/4K ;
- UX1-AJ qualifié pour `ORF-DOOR-021` : Foundation/Navigation requises, cinq catalogues connus,
  collision authorée distincte, liens orphelins/inverses et garde canonique commande/MCP/prébuild,
  sans promouvoir `ORF-DOOR-019`, portail 18/18 en 82,17 s et preuves FHD/QHD/4K ;
- UX2-A qualifié pour `ORF-AREA-023`, `ORF-AREA-025` et `ORF-AREA-026` : heartbeat de zone,
  perception et repos dans une transaction area-only, sans prétendre exécuter scripts, moteur ou
  réseau, portail 11/11 en 75,69 s, test RpgKit complémentaire 1/1 et preuves FHD/QHD/4K ;
- UX2-B à UX2-M qualifient le reste de la vague Monde : terrain modulaire et relief, environnement/
  audio/qualité, événements et rencontres, navigation/portails/waypoints/routes/visibilité/LOD et
  ancres caméra. Chaque route conserve ses tails non exposés et refuse les valeurs ou références
  invalides sans synthèse silencieuse ; 35 portails supplémentaires et 35 captures FHD sont liés aux
  documents d’acceptation consolidés UX2 ;
- UX4-A qualifié pour `ORF-FAC-001` à `ORF-FAC-005` : création/archive/suppression sûre, parents,
  héritage, matrice directionnelle et bandes, avec références externes bloquées, preuve Crypte
  réelle ou fallback isolé, portail 13/13 en 79,97 s et preuves FHD/QHD/4K ;
- UX5-A qualifié pour `ORF-DLG-001` à `ORF-DLG-005` : graphe et lignes, locuteurs protégés,
  branches paginées, liens existants et réordonnancement clavier/glisser-déposer, portail 16/16 en
  64,03 s et preuves FHD/QHD/4K ;
- UX6-A qualifié pour `ORF-PROJ-023`, `ORF-GAME-017`, `ORF-GAME-023` et `ORF-GAME-024` : deux
  transactions canoniques séparées, double garde, undo/redo, round-trip et preuve Forward+ FHD,
  sans charger de package ni distribuer d’événement ;
- densité native compacte à partir du Full HD : base typographique 12 px, barre 48 px, rail 72 px,
  palette 188 px et inspecteur 280/360 px afin de rendre la vue 3D prioritaire en Full HD et QHD ;
- retouche d'instance directe : gizmo X/Y/Z dans la 3D, déplacement avec pas fin, clic droit intégré,
  transformation, tags et propriétés locales, commandes Application, undo/redo et sauvegarde atomique ;
- création et gestion directe des objets : palette filtrable, ajout à la position visée, duplication
  fidèle à côté et suppression avec confirmation compacte, toutes réversibles avant sauvegarde ;
- page Dialogues transactionnelle alimentée par les lignes réelles, avec éditeur ligne/réponses,
  mini-carte avancée en lecture seule et lanceur de développement unique `OpenRPG Forge Studio.cmd` ;
- captures Studio contrôlées en 1920×1080 et 2560×1440, avec une seule fenêtre et aucun contrôle
  principal coupé ; UX0-H fournit la première preuve exacte 3840×2160 via un viewport Forward+
  hors-écran lorsque Windows borne la fenêtre, désormais rejouée sur le guide UX0-I et les
  diagnostics UX0-J, l’assistant UX1-A, les opérations de zone UX1-B, le chargement UX1-C, les
  calques UX1-D, le blueprint de décor UX1-E, le décor statique UX1-F, l’objet utilisable UX1-G,
  le conteneur UX1-H, l’identité UX1-I, la transformation UX1-J, l’animation initiale UX1-K, la
  destructibilité UX1-L, la protection plot UX1-M, la faction UX1-N et la conversation UX1-O.
  Le verrouillage UX1-P, la clé consommée UX1-Q, le piège UX1-R et le trésor UX1-S disposent désormais de la même
  qualification FHD/QHD/4K. Les événements d’ouverture/fermeture UX1-T et de
  verrouillage/déverrouillage UX1-U, de cycle de vie UX1-V, de zones dynamiques UX1-W, de portes
  UX1-X et de réactions/physique UX1-Z disposent de leur preuve FHD réelle ; leurs revues QHD/4K sont
  réservées à un jalon ultérieur. UX1-AA à UX1-AJ ainsi que UX2-A, UX4-A et UX5-A disposent de
  preuves FHD/QHD/4K inspectées aux jalons de leurs nouveaux layouts.
  Les 35 autres surfaces UX2 possèdent chacune une preuve Forward+ FHD inspectée ; leur revue
  corrective QHD/4K globale reste distincte de cette promotion fonctionnelle.
  La fondation polygonale UX1-Y possède déjà ses preuves FHD/QHD/4K après la correction de layout.
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
- parcours direct du viewport : Crypte des Cendres au démarrage normal, choix d'objet ou de brosse,
  clic dans la scène, sculpture de terrain transactionnelle et cadrage de la zone active ;
- rendu Forward+ enrichi par profil avec ciel procédural cinématique, SSAO/SSIL, glow et brouillard
  volumétrique, avec repli GL sûr et neutralisation explicite d'une LUT absente ;
- Crypte reconstruite en sept espaces, habillée par KayKit Dungeon Remastered CC0 et quatre
  personnages KayKit gouvernés ; navigation, collisions, mouvement, caméra et parcours critique
  qualifiés automatiquement en Forward+, la revue visuelle humaine multi-résolution restant externe ;
- feedback de combat branché sur les résultats réels : barres/chip, cible et plaques, nombres,
  recharge exacte, toasts, pings, impacts, caméra, hit-stop et SFX ; 30 captures Forward+ acceptées
  en 1920×1080/2560×1440, mouvements réduits et texte ×2 ;
- géométrie jouable de l’auberge sécurisée : plancher physique continu, quatre limites murales
  infranchissables, alignement visuel murs/sol et récupération automatique après chute ;
- caméra de déplacement stabilisée par interpolation physique, point de visée lissé et évitement des murs ;
- premiers découpages des orchestrateurs : politique de fenêtres Creator, décorateur artistique de
  la Crypte et mapping des ressources de quête MCP ;
- cycle de vie des outils Creator centralisé : aucun outil secondaire au démarrage, un seul outil
  actif à la fois, fermeture, déplacement, redimensionnement et maximisation vérifiés sous Windows ;
- édition manuelle consolidée : collections Dialogue, Quête, Blueprint, contenu, propriétés d’objet
  et slots d’équipement présentées sous forme de listes et champs nommés, sans syntaxe brute visible ;
- sauvegarde non destructive des profils Objet et Terrain et suppressions transactionnelles ;
- profil de lanceur v1 persistant, application WPF RPG cinématique, générateur `Client` atomique,
  manifestes SHA-256 complets et modèle embarqué dans la distribution Creator ;
- réglages d'affichage, qualité, VSync, audio et accessibilité appliqués au runtime ; transport TLS
  direct partagé avec le serveur headless, pin, jeton, admission module/protocole, deux clients,
  avatars distants et tranche gameplay autoritaire validés. La campagne complète, la reconnexion et
  le relais/NAT restent hors de cette base Internet.

## Distribution locale qualifiée

- version : `0.1.0-dev` ; cible : Windows x64 ;
- conformité : 38 composants documentés et 38 licences agrégées ;
- licence du projet : PolyForm Noncommercial 1.0.0 à la racine, recopiée et contrôlée sous
  `compliance/PROJECT-LICENSE.txt` lors de la génération d'une distribution ;
- Runtime : manifeste validé et smoke rejoué depuis l'exécutable exporté ;
- Creator historique : manifeste validé et smoke rejoué depuis le lanceur autonome ;
- Studio Windows x64 : export autonome, manifeste, conformité 38/38 et smoke de distribution réussi ;
- Studio Linux x64 : ELF, assembly C#, workspace, manifeste et conformité 38/38 vérifiés ; smoke de
  distribution réussi sur Debian 13 x86_64 sans éditeur Godot ;
- installateur : `OpenRPG_Forge_Setup_0.1.0-dev_x64.exe`, cycle propre réussi ;
- signature constatée : `NotSigned` ;
- modèle de lanceur de jeu : export Godot et smoke runtime réussis pour la baseline ;
- isolation NuGet de distribution : les restaurations RID utilisent des verrous temporaires.

## Limites externes avant release publique

- la licence PolyForm Noncommercial 1.0.0 n'autorise pas les usages commerciaux ; une autorisation
  distincte du concédant est nécessaire pour ces usages ;
- aucun certificat Authenticode n'est fourni, donc l'installateur reste un candidat de développement ;
- aucun tag ni aucune GitHub Release n'est créé sans autorisation distincte ;
- une revue sur un second écran physique et avec un fournisseur IA réel reste une qualification humaine ;
- les workflows GitHub Actions peuvent rester bloqués par l’état de facturation du compte.

## Publication Git

- `main` est la branche canonique et `origin/main` la publication de référence ;
- le lot Crypte est intégré à `main`, validé par le portail natif complet et publié sur `origin/main` ;
- l'ancienne branche de travail Crypte et les worktrees de lots clos sont supprimés après publication ;
- distributions, installateur et captures constituent des preuves distinctes d’un simple push Git.

## Commandes de vérité

```powershell
dotnet run --project tools/Forge.ArchitectureGraph -- check --full
& .\scripts\Update-CompleteFunctionBacklog.ps1 -Check
& .\scripts\Verify.ps1
& .\scripts\Open-Studio.ps1
& .\scripts\Verify-StudioDistribution.ps1 `
  -DistributionDirectory artifacts\distribution\studio-windows-x64
& .\scripts\Verify-RuntimeDistribution.ps1 -RunSmoke
& .\scripts\Verify-CreatorDistribution.ps1 -RunSmoke
& .\scripts\Verify-WindowsInstaller.ps1 -RunInstallCycle
```
