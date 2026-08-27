# Changelog

Toutes les modifications notables d'OpenRPG Forge sont consignées ici. Les versions produit suivent
SemVer ; les versions de schéma persistent séparément dans leurs documents.

## [Unreleased]

### Changed

- Audio de dialogue : import MP3/WAV durci de bout en bout (structure réelle, hash, licence,
  provenance, plan canonique, archive et fallback runtime), quatre voix Solaren remplacées par des
  WAV CC0 reproductibles et divulgués, documentation auteur/MCP/licences centralisée ; le dossier de
  qualification accepte aussi un lanceur Crypte à manifestes nommés sans croiser le module Solaren ;
  le plafond d’archive reste borné mais passe de 512 Mio à 1 Gio afin de couvrir les 661 Mo
  décompressés du workspace canonique actuel. La requalification Full passe 2 314 tests .NET,
  67 tests Python, les builds sans avertissement et le graphe Roslyn déterministe à 11 809 nœuds /
  94 068 relations / zéro diagnostic ;
- WorldGeneration : orchestration MCP moteur-neutre avec recettes versionnées, dry-run/application,
  révisions, empreinte stable de plan, régénération partielle, jobs persistants et audit ; validation
  renforcée des données, opérations et chemins, protection des retouches humaines, adaptateurs
  Terrain3D/Godot Flow, fallback Godot 4.7.1 produisant un terrain maillé/collisionné, installateur
  reproductible non destructif, tests C#/GDScript et documentation centrale avec ADR-0050. Les
  graphes PCG artistiques et la promotion vers la scène canonique restent explicitement hors de ce lot ;
- qualification WorldGeneration : identité concrète `providerKey` pour Terrain3D, Godot Flow,
  GF-PCGODOT et le fallback natif sans migration de l'enum `PcgGodot`, détection des propriétaires
  concurrents, carte de référence, benchmark qualité et plan Codex intégrés. Aucun addon n'est déclaré
  qualifié ni promu en production par cette seule tranche ;
- Forge Studio UX11-B : revue humaine corrective de quatorze parcours et seconde matrice visuelle
  36/36 pages en FHD 100 %, QHD 150 % et 4K 200 % ; les 717 routes restent `functional`, les
  66 fenêtres historiques passent `Parity`, les 19/19 tests de matrice et le smoke global passent.
  La distribution Windows à 1 109 fichiers et l'export Linux à 1 108 fichiers embarquent leur
  bibliothèque ForgeScript native et passent hashes et conformité 38/38. Leur smoke autonome couvre
  retouche, cache script, natif, UX3-F, UX5-A/UX5-B et préservation du module ; Linux passe sur Debian
  13 x86_64 sans éditeur Godot, ce qui ferme `STUDIO-043`. Le portail transversal final passe 2 117 tests .NET, 44 tests
  Python et tous ses smokes, avec graphe Roslyn à 11 577 nœuds / 90 175 relations / 0 diagnostic.
  Le plugin historique, le lanceur, Git et la publication restent inchangés ;
- Forge Studio UX10 : Assistant gouverné unifié pour les 17 routes restantes, avec contexte et
  outils bornés, fournisseurs locaux consentis, plans/dry-runs/transactions/diffs, autorisations,
  audit, prompts et résultats structurés, mode hors ligne, Blender typé, text/image-to-3D local et
  cohérence narrative ; la matrice atteint 717 `functional` / 0 `planned`, après 66/66 tests ciblés,
  smoke global, quatre captures Forward+ FHD corrigées puis inspectées, portail incrémental en
  524,12 s et graphe Roslyn à 11 566 nœuds / 90 143 relations sans diagnostic, sans modèle externe,
  réseau implicite, Blender exécuté, mutation du module source, commit ou publication ;
- Forge Studio UX9-O : clôture réseau pour `ORF-MP-001/008/029/031`, avec serveur dédié headless,
  PvP et tir ami autoritaires, logs structurés privés et anti-triche borné ; UX9 est clos à 68/68 et
  la matrice atteint 700 `functional` / 17 `planned`, après 37/37 tests ciblés, 99/99 régressions
  réseau, 385/385 régressions ModuleFormat, smoke global, capture Forward+ FHD corrigée puis
  inspectée, portail incrémental en 515,32 s et graphe Roslyn frais sans diagnostic, sans port,
  service Windows, serveur persistant, sink distant, commit ou publication ;
- Forge Studio UX9-N : production réseau avancée pour `ORF-MP-014/021/024/025/026/035`, avec
  contenu curaté et confiance, voix gouvernée, shard référencé, hot reload atomique/réversible,
  extensions signées/isolées et protocole cross-platform ; UX9 atteint 64/68 et la matrice 696
  `functional` / 21 `planned`, après 8/8 tests ciblés, 99/99 régressions réseau, smoke global,
  capture Forward+ FHD inspectée, portail incrémental en 507,54 s et graphe Roslyn final frais sans
  diagnostic, sans socket, audio, serveur, processus d’extension, commit ou publication ;
- Forge Studio UX9-M : interventions et enregistrement Live Director pour
  `ORF-DM-015/018/022/030/037`, avec repos forcé motivé, niveau et snapshot, apparence bornée,
  groupe sauvegardé sur raccourci et enregistrement expurgé ; UX9 atteint 58/68 et la matrice 690
  `functional` / 27 `planned`, après 7/7 tests ciblés, 60/60 régressions Live Director, smoke
  global, capture Forward+ FHD corrigée puis inspectée, portail incrémental en 507,90 s et graphe
  Roslyn final frais sans diagnostic, sans socket, serveur, joueur réel, commit ou publication ;
- Forge Studio UX9-L : accès et supervision Live Director pour
  `ORF-DM-002/011/025/031/034`, avec authentification MFA, inspection en lecture seule, liste des
  joueurs à latence autoritaire, audit immuable et chat MJ modéré ; UX9 atteint 53/68 et la matrice
  685 `functional` / 32 `planned`, après 13/13 tests ciblés, 58/58 régressions Live Director, smoke
  global, capture Forward+ FHD corrigée puis inspectée, portail incrémental en 503,26 s et graphe
  Roslyn à 11 502 nœuds / 89 296 relations sans diagnostic, sans socket, compte, serveur, commit ou
  publication ;
- Forge Studio UX9-K : narration Live Director pour `ORF-DM-035/036`, avec transitions de quête
  tous joueurs ou sélection, conséquences et projection RPG, puis cinq commandes de rencontre,
  scaling borné, adaptateurs spawn/IA et rollback complet ; UX9 atteint 48/68 et la matrice 680
  `functional` / 37 `planned`, après 10/10 tests ciblés, 58/58 régressions Live Director, smoke
  global, trois captures Forward+ FHD inspectées, portail incrémental en 525,88 s et graphe Roslyn
  à 11 484 nœuds / 89 114 relations sans diagnostic, sans socket, serveur, joueur réel, commit ou
  publication ;
- Forge Studio UX9-J : opérations Live Director pour `ORF-DM-027/028/029/032/033`, avec recherche
  indexée et focus visuel, ForgeScript serveur borné, variables typées persistables, scènes à cinq
  facettes avec rollback et visibilité MJ/joueur avec overlay viewport ; UX9 atteint 46/68 et la
  matrice 678 `functional` / 39 `planned`, après 16/16 tests ciblés, 58/58 régressions Live
  Director, smoke global, trois captures Forward+ FHD inspectées, portail incrémental en 519,91 s
  et graphe Roslyn à 11 461 nœuds / 88 901 relations sans diagnostic, sans socket, serveur, joueur
  réel, commit ou publication ;
- Forge Studio UX9-I : opérations de groupe Live Director pour `ORF-DM-020/021/023/024/026`, avec
  triggers tracés, sélection multiple bornée, déplacement à échecs partiels, attaque avec
  avertissement de faction et voyage joueur avec transition caméra ; UX9 atteint 41/68 et la
  matrice 673 `functional` / 44 `planned`, après 13/13 tests ciblés, 58/58 régressions Live
  Director, smoke global, trois captures Forward+ FHD inspectées, portail incrémental en 517,61 s
  et graphe Roslyn à 11 438 nœuds / 88 669 relations sans diagnostic, sans socket, serveur, joueur
  réel, commit ou publication ;
- Forge Studio UX9-H : autorité Live Director pour `ORF-DM-013/014/016/017/019`, avec incarnation
  explicitement divulguée, protection GM bornée et restituée, or/expérience persistants et
  compensables, puis états de porte reliés aux adaptateurs collision/portail ; UX9 atteint 36/68 et
  la matrice 668 `functional` / 49 `planned`, après 18/18 tests ciblés, 58/58 régressions Live
  Director, smoke global, trois captures Forward+ FHD corrigées puis inspectées, portail
  incrémental en 512,27 s et graphe Roslyn à 11 416 nœuds / 88 452 relations sans diagnostic,
  sans socket, compte, serveur, portefeuille externe, commit ou publication ;
- Forge Studio UX9-G : actions Live Director pour `ORF-DM-007/008/009/010/012`, avec
  téléportation sûre conservant l'orientation, destruction à impact quête et retour de session,
  soin RPG, limbo préservant l'identité et possession bornée avec restitution ; UX9 atteint 31/68
  et la matrice 663 `functional` / 54 `planned`, après 18/18 tests ciblés, 56/56 régressions Live
  Director, smoke global, trois captures Forward+ FHD corrigées puis inspectées, portail
  incrémental en 514,28 s et graphe Roslyn à 11 391 nœuds / 88 214 relations sans diagnostic,
  sans socket, compte externe, serveur réel, commit ou publication ;
- Forge Studio UX9-F : espace Live Director pour `ORF-DM-001/003/004/005/006`, avec client séparé
  MFA/expiration/révocation, palette Creator réellement filtrée, spawn spatial à IDs stables et
  compensation permissionnée, Chooser privé zone/instance et focus spectateur en lecture seule ;
  UX9 atteint 26/68 et la matrice 658 `functional` / 59 `planned`, après 12/12 tests ciblés,
  54/54 régressions Live Director, smoke global, trois captures Forward+ FHD inspectées, portail
  incrémental en 549,52 s et graphe Roslyn à 11 367 nœuds / 87 994 relations sans diagnostic,
  sans socket, compte externe, serveur réel, commit ou publication ;
- Forge Studio UX9-E : résilience et capacité réseau pour `ORF-MP-033/034`, avec snapshots,
  WAL, répétition de rollback, migrations, PITR, rétention, slots Administrateur réservés, file
  FIFO, position, délestage et métriques ; UX9 atteint 21/68 et la matrice 653 `functional` / 64
  `planned`, après 11/11 tests, smoke global, deux captures Forward+ FHD inspectées, portail
  incrémental en 546,18 s et graphe Roslyn final frais, déterministe et sans diagnostic, sans
  stockage distant, serveur public, rollback du module réel, commit ou publication ;
- Forge Studio UX9-D : persistance réseau et observabilité locale pour
  `ORF-MP-022/023/028/030/032`, avec snapshots révisionnés, opérations SQL nommées réellement
  allowlistées, métriques/health filtrés, profilage borné et tickets de reconnexion single-use ;
  UX9 atteint 19/68 et la matrice 651 `functional` / 66 `planned`, après 12/12 tests, smoke global,
  cinq captures Forward+ FHD inspectées, portail incrémental en 534,22 s et graphe Roslyn final
  frais, déterministe et sans diagnostic, sans endpoint externe, base distante, commit ou publication ;
- Forge Studio UX9-C : monde réseau et administration gouvernée pour `ORF-MP-009/010/016/018/019`,
  avec instances bornées, zones dynamiques sous budgets, authentification non énumérable,
  commandes sûres/rate limit réellement appliqués et modération auditée ; UX9 atteint 14/68 et la
  matrice 646 `functional` / 71 `planned`, après 17/17 tests, smoke global, cinq captures Forward+
  FHD inspectées, portail incrémental en 548,58 s et graphe Roslyn à 11 299 nœuds / 87 300 relations
  sans diagnostic, sans listener public, compte externe, commit ou publication ;
- Forge Studio UX9-B : opérations réseau locales gouvernées pour `ORF-MP-013/015/017/020/027`,
  avec synchronisation reprise et vérifiée, verrou de version strict, grants réellement appliqués
  par `AuthorizationService`, canaux de chat bornés et abonnements événementiels tracés ; UX9 atteint
  9/68 et la matrice 641 `functional` / 76 `planned`, après 25/25 tests, smoke global, cinq captures
  Forward+ FHD inspectées, portail incrémental en 532,65 s et graphe Roslyn à 11 276 nœuds / 87 071
  relations sans diagnostic, sans transport distant, serveur public, compte réel, commit ou
  publication ;
- Forge Studio UX9-A : session réseau locale autoritaire pour `ORF-MP-002/007/011/012`, avec profil
  workspace atomique, dry-run, gardes de révision et d'empreinte, undo/redo, permission,
  confirmation, audit et déconnexion réelle ; UX9 atteint 4/68 et la matrice 636 `functional` / 81
  `planned`, après 17/17 tests, smoke global, capture Forward+ FHD corrigée puis inspectée, portail
  incrémental en 643,09 s et graphe Roslyn à 11 253 nœuds / 86 841 relations sans diagnostic, sans
  port public, client distant, serveur dédié inter-processus, commit ou publication ;
- Forge Studio UX8-N : politique de packs curatés, namespaces de ressources, migrations hôte
  versionnées et désinstallation sûre pour `ORF-CONT-010/019/026/027`, avec dry-run, consentement,
  gardes de révision et d'empreinte, undo/redo, save/reopen, backups et restauration ; UX8 est close
  à 51/51 et la matrice atteint 632 `functional` / 85 `planned`, après 22/22 tests, smoke global et
  quatre captures Forward+ FHD inspectées, portail incrémental en 581,21 s et graphe Roslyn à
  11 234 nœuds / 86 636 relations sans diagnostic, sans script de package, shell, agent, réseau,
  téléchargement, mutation du module source, commit ou publication ;
- Forge Studio UX8-M : expérience joueur multijoueur transactionnelle pour `ORF-MP-003` à
  `ORF-MP-006`, avec découverte, lobby, création, autorité des personnages, persistance workspace
  atomique et préservation du combat ; la matrice atteint 628 `functional` / 89 `planned`, après
  9/9 tests, smoke global, portail incrémental en 443,52 s et quatre captures Forward+ FHD corrigées
  puis inspectées, avec graphe Roslyn à 11 213 nœuds / 86 391 relations sans diagnostic, sans
  transport, serveur, annuaire distant, compte, monde persistant, commit ou publication ;
- Forge Studio UX7-I : profil d'observabilité transactionnel et capture OpenTelemetry réelle,
  isolée et annulable pour `ORF-EE-029`, avec résultats Editor/Runtime/Serveur/ForgeScript
  navigables et diagnostic explicite lorsque Tracy natif n'est pas disponible ; UX7 est close à
  43/43 et la matrice atteint 624 `functional` / 93 `planned`, après 14/14 tests, smoke global,
  portail incrémental en 420,22 s, capture Forward+ FHD corrigée puis inspectée et graphe Roslyn à
  11 194 nœuds / 86 220 relations sans diagnostic, sans Godot
  personnalisé, serveur Tracy, compteur GPU, réseau, commit ou publication ;
- Forge Studio UX8-L : politique de synchronisation de dépôt et métadonnées NWSync authorées par
  commande canonique, manifeste SHA-256 et cache local jetable pour `ORF-CONT-009` et
  `ORF-EE-025` ; la matrice atteint 623 `functional` / 94 `planned`, après 61/61 tests, smoke
  global, portail incrémental en 419,84 s, capture Forward+ FHD inspectée et graphe Roslyn frais
  sans diagnostic, sans réseau, serveur NWSync, téléchargement distant, commit ou publication ;
- Forge Studio UX8-K : import et export en masse confinés au workspace avec consentement, staging
  SHA-256, stratégie de collision, compensation fichier+catalogue et transaction canonique pour
  `ORF-BUILD-007` ; la matrice atteint 621 `functional` / 96 `planned`, après 51/51 tests, smoke
  global, portail incrémental en 423,12 s, capture Forward+ FHD corrigée puis inspectée et graphe
  Roslyn frais sans diagnostic, sans source externe, synchronisation, publication, commit ou push ;
- Forge Studio UX8-J : politique humaine de worker confinée et sonde anti-injection locale pour
  `ORF-AI-023` et `ORF-AI-032` ; la matrice atteint 620 `functional` / 97 `planned`, après 35/35
  tests, smoke global, portail incrémental en 472,65 s, capture Forward+ FHD inspectée et graphe
  Roslyn frais sans diagnostic, sans exécution Python/Blender/shell, réseau, fournisseur IA,
  mutation du module source, commit ou publication ;
- Forge Studio UX8-I : contrat projet versionné, migrations automatiques en dry-run et compatibilité
  réelle des manifests d’assets par plateforme pour `ORF-PROJ-025` et `ORF-CONT-025` ; la matrice
  atteint 618 `functional` / 99 `planned`, après 64/64 tests, smoke global, portail incrémental en
  423,41 s, capture Forward+ FHD inspectée et graphe Roslyn à 11 133 nœuds / 85 504 relations sans
  diagnostic, sans migration du module source, modification de manifest, mesure matérielle,
  benchmark, commit ou publication ;
- Forge Studio UX8-H : paramètres par cible, bundles multi-plateformes, packaging Windows,
  signatures publiques et mises à jour delta pour `ORF-PROJ-024`, `ORF-BUILD-016/017` et
  `ORF-CONT-028/029` ; la matrice atteint 616 `functional` / 101 `planned`, après 22/22 tests,
  smoke global, portail incrémental en 422,07 s, capture Forward+ FHD inspectée et graphe Roslyn à
  11 120 nœuds / 85 368 relations sans diagnostic, sans clé privée, installation, publication,
  notarisation, téléversement ni mutation du module source ;
- Forge Studio UX8-G : catalogue de packages versionnés, priorité, dépendances et compilation
  canonique en mémoire pour `ORF-PROJ-020` et `ORF-CONT-001/002/003/007` ; la matrice atteint 611
  `functional` / 106 `planned`, après 47/47 tests, smoke global, portail incrémental en 544,55 s,
  capture Forward+ FHD corrigée puis inspectée et graphe Roslyn frais sans diagnostic, sans
  installation, téléchargement, signature, exécution de script, package disque ou publication ;
- Forge Studio UX8-F : approbation transactionnelle des dépendances, notice tierce déterministe,
  crédits/provenances et frontière IP pour `ORF-BUILD-022/023` et `ORF-CONT-030/031` ; la matrice
  atteint 606 `functional` / 111 `planned`, après 20/20 tests, smoke global, portail incrémental en
  534,72 s, capture Forward+ FHD inspectée et graphe Roslyn frais sans diagnostic, sans téléchargement,
  audit externe, package, signature, écriture du registre d’assets ou publication ;
- Forge Studio UX8-E : inspection annulable de la documentation générée, des task manifests, des
  canaux de release et des crash artifacts pour `ORF-BUILD-020/021/024/025` ; la matrice atteint
  602 `functional` / 115 `planned`, après 30/30 tests, smoke global, portail incrémental en 531,26 s,
  capture Forward+ FHD inspectée et graphe Roslyn à 11 076 nœuds / 84 866 relations sans diagnostic,
  sans génération documentaire, agent, bundle, signature, publication ou dump natif ;
- Forge Studio UX8-D : inspecteur local annulable du dépôt Git canonique, des huit emplacements
  standards, du versioning Core/kits/package/schémas et des ADR pour `ORF-BUILD-001/002/018/019` ;
  la matrice atteint 598 `functional` / 119 `planned`, après 19/19 tests, smoke global, portail
  incrémental en 535,79 s, capture Forward+ FHD corrigée puis inspectée et graphe Roslyn à 11 066
  nœuds / 84 755 relations sans diagnostic, sans commande Git, générateur, écriture documentaire,
  commit ou publication ;
- Forge Studio UX8-C : merge assisté listes+graphes en mémoire, conservation déterministe des deux
  variantes, profil transactionnel de worktrees/propriété, sept contrôles CI et cinq fixtures de
  référence pour `ORF-BUILD-012` à `ORF-BUILD-015` ; la matrice atteint 594 `functional` / 123
  `planned`, après 23/23 tests, smoke global, portail incrémental en 538,11 s, deux captures Forward+
  FHD inspectées et graphe Roslyn à 11 056 nœuds / 84 680 relations sans diagnostic, sans merge Git,
  agent, runner distant, shell arbitraire, commit ou publication ;
- Forge Studio UX8-B : maintenance collaborative entièrement confinée dans une copie jetable avec
  cache reconstructible, normalisation canonique, migrations versionnées idempotentes, références
  inverses et diff sémantique sans UUID visible pour `ORF-BUILD-006` et `ORF-BUILD-008` à
  `ORF-BUILD-011` ; la matrice atteint 590 `functional` / 127 `planned`, après 25/25 tests, smoke
  global, portail incrémental en 549,47 s, capture Forward+ FHD inspectée et graphe Roslyn à
  11 043 nœuds / 84 530 relations sans diagnostic, sans mutation du module source, shell
  arbitraire, réseau, commit ou publication Git ;
- Forge Studio UX8-A : build réel d'un module par `Forge.Cli`, graphe et plan d'optimisation,
  annulation du processus, reproductibilité octet par octet et réutilisation incrémentale complète ;
  `ORF-SHELL-019`, `ORF-EE-004` et `ORF-BUILD-003` à `ORF-BUILD-005` portent la matrice à 585
  `functional` / 132 `planned`, après 61/61 tests, smoke global, portail incrémental en 624,28 s,
  capture Forward+ FHD inspectée et graphe Roslyn à 11 032 nœuds / 84 434 relations sans
  diagnostic, sans signature, publication, installation ni mutation Git ;
- Forge Studio UX7-B : laboratoire qualité isolé et annulable pour tests Domaine, replay headless,
  six garanties de sauvegarde et logs JSON structurés ; quatre routes portent la matrice à 552
  `functional` / 165 `planned`, après 25/25 tests, smoke global, portail incrémental en 245,20 s et
  capture Forward+ FHD corrigée puis inspectée, sans runner xUnit embarqué, runtime Godot de jeu,
  acteur, script, playback, journal joueur ou transport réseau live ;
- Forge Studio UX7-A : atelier de validation canonique pour schéma, références, graphes,
  géométrie, navigation, localisation, licences, préparation de zone, audio et diagnostics
  cliquables ; dix routes portent la matrice à 548 `functional` / 169 `planned`, après 33/33 tests,
  smoke global, portail incrémental en 300,73 s et capture Forward+ FHD inspectée, sans build,
  packaging, exécution gameplay, playback, acteur, moteur ou réseau live ;
- Forge Studio UX3-I : provenance SPDX/droits/IA, quarantaine confinée avec scan fail-closed et
  décision humaine, puis politique de génération Image/Audio/VFX planifiée sans appel fournisseur
  pour `ORF-CONT-023`, `ORF-CONT-024` et `ORF-AI-024` ; deux historiques explicites ferment UX3 à
  34/34 et portent la matrice à 538 `functional` / 179 `planned`, après 12/12 tests, smoke global,
  portail incrémental en 171,71 s et quatre captures Forward+ FHD inspectées, sans téléchargement,
  génération, import, rendu, playback, publication ou appel distant ;
- Forge Studio UX3-H : import borné et réversible de fichiers ouverts, export déterministe de
  blueprints transitifs, sources textuelles gouvernées et tables typées pour `ORF-CONT-004`,
  `ORF-CONT-005`, `ORF-CONT-006` et `ORF-CONT-018` ; trois frontières transactionnelles explicites
  portent la matrice à 535 `functional` / 182 `planned`, après 18/18 tests, smoke global, portail
  incrémental en 173,10 s et cinq captures Forward+ FHD inspectées, sans décodage ERF propriétaire,
  installation de package, exécution de script, acteur, moteur ou réseau live ;
- Forge Studio UX3-G : manifestes d’assets gouvernés avec chunks et budgets plateforme,
  collision/navigation, animations et VFX/LOD pour `ORF-CONT-008`, `ORF-CONT-015`,
  `ORF-CONT-016` et `ORF-CONT-017` ; la transaction canonique porte la matrice à 531
  `functional` / 186 `planned`, après 15/15 tests, smoke global, portail incrémental en 160,44 s et
  quatre captures Forward+ FHD inspectées, sans génération de mesh ou LOD, bake navigation,
  playback animation, rendu VFX, benchmark GPU, packaging, script, acteur, moteur ou réseau live ;
- Forge Studio UX3-F : navigateur unifié de 665 assets, profils d’import texture KTX2/modèle GLB
  et aller-retour Blender typé pour `ORF-CONT-013`, `ORF-CONT-014`, `ORF-CONT-020`,
  `ORF-CONT-021` et `ORF-CONT-022` ; deux transactions indépendantes portent la matrice à 527
  `functional` / 190 `planned`, après 32/32 tests, smoke global, portail incrémental en 157,96 s et
  trois captures Forward+ FHD inspectées, sans téléchargement, transformation, réimport, lancement
  de Blender, packaging, script, acteur, moteur ou réseau live ;
- Forge Studio UX3-E : mesure locale GLB/PNG et six diagnostics de compatibilité artistique, puis
  budgets par plateforme sur sept axes avec avertissement ou refus pré-build pour `ORF-PAL-017`
  et `ORF-PAL-018` ; deux transactions indépendantes portent la matrice à 522 `functional` / 195
  `planned`, après 11/11 tests, smoke global, portail incrémental en 156,86 s et deux captures
  Forward+ FHD inspectées, sans instanciation d’asset, mesure GPU, packaging, script, acteur,
  moteur ou réseau live ;
- Forge Studio UX3-D : manifeste de dépendances avec fermeture transitive et budget, puis
  provenance/licence distribuable dans deux transactions indépendantes pour `ORF-PAL-015` et
  `ORF-PAL-016` ; le build refuse désormais une dépendance déclarée absente d'un blueprint utilisé,
  et la matrice atteint 520 `functional` / 197 `planned` après 45/45 tests, smoke global, portail
  incrémental en 191,90 s et deux captures Forward+ FHD inspectées, sans chargement d'asset,
  création de package, exécution de script, acteur, moteur ou réseau live ;
- Forge Studio UX3-C : favoris/récents personnels, miniatures gouvernées avec fallback explicite,
  recherche avancée et vrai payload drag-and-drop pour `ORF-PAL-009` à `ORF-PAL-012` ; deux
  transactions et approbations indépendantes portent la matrice à 518 `functional` / 199 `planned`,
  après 23/23 tests, smoke global, portail incrémental en 153,00 s et capture Forward+ FHD inspectée,
  sans chargement d'asset ou prefab, exécution de script, acteur, moteur ou réseau live ;
- Forge Studio UX3-B : propagation sélective et migration de blueprints, catégories hiérarchiques,
  variantes et compositions humaines pour `ORF-PAL-004`, `ORF-PAL-005`, `ORF-PAL-008`,
  `ORF-PAL-013` et `ORF-PAL-014` ; deux transactions et approbations indépendantes portent la
  matrice à 514 `functional` / 203 `planned`, après 17/17 tests, smoke global, portail incrémental
  en 146,04 s et deux captures Forward+ FHD inspectées, sans chargement d'asset, instanciation de
  prefab, exécution de script, moteur ou réseau live ;
- Forge Studio UX3-A : fondation humaine des blueprints et palettes avec sélection explicite,
  édition Projet, dérivation depuis une instance réelle, inventaires Standard/Projet et
  gouvernance protégée ; `ORF-PAL-001`, `ORF-PAL-002`, `ORF-PAL-003`, `ORF-PAL-006` et
  `ORF-PAL-007` portent la matrice à 509 `functional` / 208 `planned`, après 22/22 tests, portail
  incrémental final en 144,41 s, smoke global et inspections Forward+ FHD/QHD/4K, sans claim de
  placement, instanciation, chargement d'asset, script, moteur ou réseau live ;
- Forge Studio UX6-Z : assistant humain de création de capacité avec porteur explicite, effets
  ordonnés, dépendances gouvernées, cinq scénarios `CombatResolver`, équilibrage, approbation,
  dry-run et application canonique ; `ORF-AI-020` clôt UX6 à 84/84 et porte la matrice à 504
  `functional` / 213 `planned`, après 6/6 tests, portail incrémental en 146,31 s, smoke global et
  inspection Forward+ FHD, sans claim de fournisseur IA, ForgeScript, VFX, audio, combat, moteur
  ou réseau live ;
- Forge Studio UX6-Y : surface humaine Météo runtime et Streaming de zone, avec deux commandes
  canoniques, rollback borné, machine météo/événement typé et streaming cache/progression/benchmark
  réellement exercés sur copie ; `ORF-GAME-018` et `ORF-EE-023` portent la matrice à 503
  `functional` / 214 `planned`, après 33/33 tests, portail incrémental final en 163,69 s, smoke
  global et inspection de deux captures Forward+ FHD, sans claim de réseau, ForgeScript,
  ressource, scène ou chargement moteur live ;
- Forge Studio UX6-X : surface humaine Carte/journal/HUD et Police/échelles, avec commande projet
  et historique utilisateur explicitement séparés, rollback croisé, résolution copiée et aperçu
  typographique local ; `ORF-GAME-026` et `ORF-EE-026` portent la matrice à 501 `functional` /
  216 `planned`, après 22/22 tests, portail incrémental final en 162,88 s, smoke global et inspection de deux captures Forward+ FHD
  corrigées, sans claim de HUD, thème, périphérique, viewport ou session de jeu live ;
- Forge Studio UX6-W : workbench responsive pour overrides hiérarchiques et tweens Transform,
  Paramètre matériau et État, avec autorité, réplication, sauvegarde, annulation et runtimes purs
  sur copie ; `ORF-EE-001` et `ORF-EE-027` portent la matrice à 499 `functional` / 218 `planned`,
  après 10/10 tests, smoke global, portail incrémental final en 146,60 s et inspection de trois
  captures Forward+ FHD, sans claim de nœud Godot, matériau, état d’acteur, serveur ou réseau live ;
- Forge Studio UX6-V : atelier Pathfinding amélioré combinant configuration persistante des
  profils d’agent, laboratoire A* copié, obstacles et coûts dynamiques, récupération bornée,
  budget de recherche, rebake et plan EngineAdapter ; `ORF-EE-028` porte la matrice à 497
  `functional` / 220 `planned`, après 10/10 tests, smoke global, portail incrémental en 144,21 s
  et inspection Forward+ FHD, sans claim de `NavigationAgent3D`, acteur, évitement local,
  déplacement, serveur ou transport réseau live ;
- Forge Studio UX6-U : surface Variables & blackboard qui complète UX0-I avec édition persistante
  bornée par `SetModuleConfigurationHandler`, handles typés, mutation normalisée, vue debug, deltas
  de réplication, snapshot/reload et frontière legacy ; `ORF-GAME-022` porte la matrice à 496
  `functional` / 221 `planned`, après 56/56 tests, smoke global, portail incrémental en 144,09 s et
  inspection Forward+ FHD, sans claim de serveur, client, GameRoot, ForgeScript, sauvegarde produit
  ou transport réseau live ;
- Forge Studio UX6-T : atelier AOE et NUI 2.0 pour formes/portées/couverture/confirmation/palettes
  et capacités responsive/formulaires/listes/canvas/accessibilité/localisation/inspecteur ; deux
  routes portent la matrice à 495 `functional` / 222 `planned`, après 15/15 tests, smoke global,
  portail incrémental en 147,77 s et inspection Forward+ FHD des deux onglets, sans claim de mesh,
  ciblage serveur, document NUI, événement GUI ou réseau live ;
- Forge Studio UX6-S : surface Creator technique pour requêtes de périphérique consenties et
  anonymisées, commandes VM de ressources sous quotas/namespaces, contrôle de groupe borné et
  progression data-driven ; quatre routes portent la matrice à 493 `functional` / 224 `planned`,
  après 19/19 tests, smoke global renforcé, portail incrémental en 140,55 s et inspection Forward+
  FHD des quatre onglets, sans claim d’accès matériel, chargement de ressource, pilotage d’acteur,
  création de classe ou réseau live ;
- Forge Studio UX6-R : atelier humain pour règles data-driven, interface scriptable, événements GUI
  typés, JSON runtime révisionné et politique d’état sérialisable, tous portés par
  `SetRuntimeExtensionsHandler` ; cinq routes portent la matrice à 489 `functional` / 228
  `planned`, après 24/24 tests, smoke global renforcé, portail incrémental en 140,90 s et inspection
  Forward+ FHD des quatre onglets, sans claim de nœud Godot, acteur, sauvegarde produit, bus live,
  processus ou réseau ;
- Forge Studio UX6-Q : profil compilateur incrémental avec cache, invalidation, diagnostics stables,
  télémétrie et conformité, plus création assistée hors ligne d’une source `.as` et de sa métadonnée
  gouvernée dans une transaction distincte ; `ORF-EE-031` et `ORF-AI-018` portent la matrice à 484
  `functional` / 233 `planned`, après 16/16 tests, smoke global, portail incrémental en 137,50 s et
  inspection Forward+ FHD des deux onglets, sans claim de fournisseur IA, acteur, bus runtime,
  objet Godot, distribution de bytecode ou réseau live ;
- Forge Studio UX6-P : interop éditeur/MCP R101 ajoutée à l’unique source UX6-F avec profil
  versionné, plans source et liaison, snapshot exact, quotas/récupération et adaptateur runtime ;
  `ORF-SCR-041` porte la matrice à 482 `functional` / 235 `planned`, après 10/10 tests, smoke
  global, portail incrémental en 135,87 s et inspection Forward+ FHD des trois vues, sans claim
  d’acteur, bus d’événements, objet moteur, fichier arbitraire, processus ou réseau live ;
- Forge Studio UX6-O : atelier qualité R100 ajouté à l’unique source UX6-F pour tests unitaires,
  profilage, documentation générée, analyse statique et sérialisation d’état ; cinq routes portent
  la matrice à 481 `functional` / 236 `planned`, après 16/16 tests, smoke global, portail
  incrémental en 135,51 s et inspection Forward+ FHD des cinq onglets, sans claim de monde de jeu,
  acteur, sauvegarde gameplay, processus distant ou réseau live ;
- Forge Studio UX6-N : hot reload versionné et débogueur R099 ajoutés à l’unique source UX6-F,
  avec migrations/rollback, dépendances, breakpoints, pile, watches, inspection et pas permissionnés ;
  `ORF-SCR-032` et `ORF-SCR-033` portent la matrice à 476 `functional` / 241 `planned`, après
  28/28 tests, smoke global, portail incrémental en 131,98 s et inspection Forward+ FHD des deux
  onglets, sans claim de remplacement de module, acteur, processus distant ou réseau live ;
- Forge Studio UX6-M : atelier humain pour automatisation caméra/cutscenes, routage exact par tags,
  switches typés du module et réglages IA bornés, tous portés par `ScriptAutomationProfile` et la
  commande canonique du catalogue monde ; `ORF-SCR-018`, `ORF-SCR-024`, `ORF-SCR-025` et
  `ORF-SCR-026` portent la matrice à 474 `functional` / 243 `planned`, après 60/60 tests, smoke
  global, portail incrémental en 162,07 s et inspection Forward+ FHD des quatre onglets, sans claim
  de caméra, acteur IA, exécution ForgeScript ou transport réseau live ;
- Forge Studio UX6-L : atelier R099 pour audio/musique, horloge/timers et événements custom,
  avec bloc source revu, états C# déterministes et intentions natives réellement exécutées ;
  `ORF-SCR-019`, `ORF-SCR-020` et `ORF-SCR-023` portent la matrice à 470 `functional` / 247
  `planned`, après 15/15 tests, smoke global, portail incrémental final en 156,29 s et inspection
  Forward+ FHD des trois onglets, sans claim de lecteur audio, horloge Godot, session de jeu ou
  transport réseau live ;
- Forge Studio UX6-K : manifeste R015 de permissions ForgeScript par module éditable sans JSON,
  avec allowlist, kits, liaisons, signature, autorité serveur et sandbox de refus/récupération ;
  `ORF-SCR-039` porte la matrice à 467 `functional` / 250 `planned`, après 14/14 tests, smoke
  global, portail incrémental final en 159,24 s et inspections Forward+ FHD des trois onglets,
  sans claim de signature de package, acteur, session réseau ou runtime de jeu live ;
- Forge Studio UX6-J : politique ForgeScript R014 versionnée pour handles opaques, quotas
  d’instructions et mémoire, replay déterministe et compatibilité API ; cinq routes portent la
  matrice à 466 `functional` / 251 `planned`, après 27/27 tests, smoke global, portail incrémental
  final en 156,85 s et capture Forward+ FHD corrigée puis inspectée, sans claim d’acteur, serveur
  de partie, mémoire machine, fichier arbitraire, processus ou réseau live ;
- Forge Studio UX6-I : surface ForgeScript R013 pour zones/objets, mouvement, math/chaînes,
  variables locales typées et sandbox sans fichier/réseau, sur la même source et le même historique
  UX6-F ; cinq routes portent la matrice à 461 `functional` / 256 `planned`, après 18/18 tests,
  smoke global, portail incrémental en 197,50 s et capture Forward+ FHD inspectée, sans claim
  d’acteur, pathfinding, physique, fichier, processus ou réseau live ;
- Forge Studio UX6-H : cinq API monde ForgeScript pour inventaire, quêtes, dialogues, factions et
  perception, dans la même source/historique UX6-F et avec catalogues humains issus du projet ;
  cinq routes portent la matrice à 456 `functional` / 261 `planned`, après 13/13 tests, smoke
  global et capture Forward+ FHD corrigée puis inspectée, sans claim d’inventaire, journal,
  dialogue, réputation, perception moteur ou réseau live ;
- Forge Studio UX6-G : éditeur ForgeScript assisté, complétions/signatures/formatage/diagnostics,
  Script Wizard relu et simulations déterministes des actions différées, du combat et des effets,
  en réutilisant strictement l’autorité source UX6-F ; cinq routes portent la matrice à 451
  `functional` / 266 `planned`, après 25/25 tests, smoke global et capture Forward+ FHD corrigée
  puis inspectée, sans claim d’acteur, moteur, fichier projet ou réseau live ; le jalon UX6-E à
  UX6-G a ensuite repassé le portail transversal complet avec 2 035 tests .NET et 39 tests Python ;
- Forge Studio UX6-F : atelier humain ForgeScript pour sources `.as` C-like, métadonnées et bytecode,
  compilateur/cache, handlers d’événements par objet et fonction `main`, avec historique source,
  écriture atomique, sandbox bornée et protection explicite des `.forge` historiques ; cinq routes
  portent la matrice à 446 `functional` / 271 `planned`, après 34/34 tests, smoke global et capture
  Forward+ FHD corrigée puis inspectée, sans claim de hot reload, débogage ou événement gameplay live ;
- Forge Studio UX6-E : workbench humain « Production gameplay » pour apesanteur, véhicules,
  montures et succès ; quatre routes portent la matrice à 441 routes `functional` / 276 `planned`,
  avec packs gouvernés et profils PostLaunch voisins strictement préservés et sans claim de
  gravité, acteur, véhicule, monture, plateforme, télémétrie, physique, ForgeScript ou réseau live ;
- Forge Studio UX6-D : workbench humain « Simulation gameplay » pour vol libre, projectiles,
  physique 3D et ciblage ; quatre routes portent la matrice à 437 routes `functional` / 280
  `planned`, avec temps du monde et queues UX6-B/UX6-C strictement préservés et sans claim
  d’acteur, physique Godot, caméra, projectile natif, ForgeScript ou réseau live ; le jalon UX6-A
  à UX6-D a ensuite repassé le portail transversal complet avec 2 035 tests .NET et 39 tests Python ;
- Forge Studio UX6-C : workbench humain « Capacités gameplay » pour nage/plongée, escalade, menu
  radial, temps tactique et modes de combat ; cinq routes portent la matrice à 433 routes
  `functional` / 284 `planned`, avec fondation UX6-B et queues gameplay strictement préservées et
  sans claim d’acteur, horloge, combat ou réseau live ;
- Forge Studio UX6-B : workbench humain « Fondations du gameplay » pour déplacement au sol,
  interactions contextuelles, huit modes de caméra et input mapping ; quatre routes portent la
  matrice à 428 routes `functional` / 289 `planned`, avec sauvegarde UX7 et profils voisins
  strictement préservés et sans claim d’acteur, caméra ou InputMap live ;
- Forge Studio UX6-A : workbench humain « Règles & temps du monde » avec deux transactions
  explicites pour difficultés/mutateurs/plugins et horloge globale ; quatre routes ouvrent UX6 et
  portent la matrice à 424 routes `functional` / 293 `planned` sans revendiquer l’exécution live
  des packages, événements, scripts, moteur ou réseau ;
- Forge Studio UX5 complet : workbench narratif en cinq workflows distincts pour Dialogue, Quête,
  Localisation/typographie, Lore/références et Cinématiques/contrôles, chacun conservant sa commande,
  son dry-run et son historique canoniques ; les 64 routes restantes portent la matrice à 420 routes
  `functional` / 297 `planned` et clôturent UX5 à 69/69 ;
- Forge Studio UX4-U : auteur de compagnon complet en cinq sections via la commande canonique du
  catalogue WorldFeatures, couvrant les 24 routes CMP, portant la matrice à 356 routes
  `functional` / 361 `planned` et clôturant UX4 à 140/140 ;
- Forge Studio UX4-T : auteur de marchand complet en cinq sections via la commande canonique du
  catalogue WorldFeatures, couvrant les 19 routes MER et portant la matrice à 332 routes
  `functional` / 385 `planned` ;
- Forge Studio UX4-S : auteur d’objet complet en cinq sections et transaction atomique couvrant
  les 32 routes ITEM, avec matrice portée à 313 routes `functional` / 404 `planned` ;
- Forge Studio UX4-R : création assistée de boss hors ligne avec approbation explicite, simulation
  déterministe et lot atomique annulable, portant la matrice à 281 routes `functional` / 436
  `planned` ;
- Forge Studio UX4-Q : prises vocales localisées, sous-titres, phonèmes, fallback TTS et
  consentement via `SetAreaExperienceProfileHandler`, avec matrice portée à 280 routes
  `functional` / 437 `planned` ;
- Forge Studio UX4-J complété : handler OnConversation serveur éditable avec projection typée,
  dry-run, historique et persistance, portant la matrice à 279 routes `functional` / 438 `planned` ;
- Forge Studio UX4-D complété : handler OnSpawn éditable et émission typée uniquement après
  validation complète, avec matrice portée à 278 routes `functional` / 439 `planned` ;
- Forge Studio UX4-P : perception, attaque physique, capacité ciblée, récupération après blocage et
  contrôleur IA par profil via `SetCreatureFoundationHandler`, avec vrai profil Crypte, sept bindings
  canoniques et matrice portée à 277 routes `functional` / 440 `planned` ;
- Forge Studio UX4-O : cycle BeforeDamage/DamageApplied/EntityDied et validation complète de la
  créature via `SetCreatureEventsHandler`, avec profil réel Crypte, bindings UX4-N préservés et
  matrice portée à 272 routes `functional` / 445 `planned` ;
- Forge Studio UX4-N : frontière de combat, inventaire, repos, heartbeat budgété et événements
  utilisateur typés via `SetCreatureEventsHandler`, avec profil réel Crypte, tail dégâts/mort
  préservé et matrice portée à 269 routes `functional` / 448 `planned` ;
- Forge Studio UX4-M : rôle/lore, animations ambiantes et auto-level de créature via
  `SetCreatureRoleBehaviorHandler`, avec preuve réelle Crypte et matrice portée à 264 routes
  `functional` / 453 `planned` ;
- Forge Studio UX4-L : profil voix et soundset de créature, banques, émotions, lipsync, variantes
  et gouvernance des droits via `SetCreaturePresentationHandler`, avec matrice portée à 261 routes
  `functional` / 456 `planned` ;
- Forge Studio UX4-K : six modes et valeurs complètes de déplacement de créature via
  `SetCreatureFoundationHandler`, sans clamp silencieux, avec preuve réelle Crypte et matrice
  portée à 260 routes `functional` / 457 `planned` ;
- Forge Studio UX4-J : factions et appartenances de créature, perception multi-sens, conversations
  associées et protections narratives via `SetCreatureFoundationHandler`, avec références réelles,
  preuve Crypte et matrice portée à 259 routes `functional` / 458 `planned` ;
- Forge Studio UX4-I : objets droppables, pickpocket et cycle de cadavre via
  `SetCreatureLootHandler`, avec références réelles, preuve Crypte et matrice portée à 255 routes
  `functional` / 462 `planned` ;
- Forge Studio UX4-H : capacités spéciales, inventaire, conteneurs et équipement via
  `SetCreatureFoundationHandler`, avec références aux objets réels, preuve Crypte et matrice portée
  à 252 routes `functional` / 465 `planned` ;
- Forge Studio UX4-G : classes et niveaux, alignement data-driven, compétences, talents et pouvoirs
  appris/préparés via `SetCreatureProgressionHandler`, avec préservation de l'auto-level voisin,
  preuve réelle Crypte et matrice portée à 250 routes `functional` / 467 `planned` ;
- Forge Studio UX4-F : graphe d'animation, état initial, ressources, attributs, défenses et
  sauvegardes via `SetCreatureFoundationHandler`, avec preuve réelle Crypte et matrice portée à
  245 routes `functional` / 472 `planned` ;
- Forge Studio UX4-E : genre et pronoms, portraits gouvernés réellement prévisualisés, modèle,
  parties du corps, couleurs, ailes et queues via `CreaturePresentationProfile` et commande
  canonique ; cinq routes CREATURE qualifiées et matrice portée à 240 `functional` / 477 `planned` ;
- Forge Studio UX4-D : assistant et fondation de créature, identité localisée, rôle/menace et
  composition d'espèce via commande canonique ; cinq routes CREATURE qualifiées et matrice portée
  à 235 `functional` / 482 `planned` ;
- Forge Studio UX4-C : tags sémantiques, matrice directionnelle versionnée, import/export de
  heatmap, réaction au joueur et validation factionnelle ; les 14 routes FACTION sont closes et la
  matrice atteint 230 `functional` / 487 `planned` ;
- Forge Studio UX4-B : réputation personnelle et globale, témoins, affiliations multiples et
  factions dynamiques éditables avec commande canonique, preuves isolées et captures Forward+ FHD
  fallback/réelle Crypte ; matrice portée à 226 routes `functional` et 491 `planned` ;
- Forge Studio UX2 clos à 98/98 routes Monde : terrain et tuiles, environnement/audio/qualité,
  événements et rencontres, navigation/portails/waypoints/routes/visibilité/LOD et ancres caméra,
  avec commandes canoniques, préservation, undo/redo et round-trip ;
- 36 portails UX2 réussis en 3 117,53 s cumulées, chacun avec smoke et capture Forward+ FHD
  inspectée ; les documents d’acceptation distinguent systématiquement authoring, plans/simulations
  et runtime réellement exécuté ;
- matrice d’accès humain Studio portée à 221 routes `functional` et 496 `planned` ; les quatre pages
  UX2 Terrain, Environnement, Monde vivant et Navigation sont fonctionnelles à 98/98 ;
- clôture UX2 validée par le portail transversal complet : 2 033 tests .NET, 39 tests Python,
  builds sans avertissement, graphe Roslyn déterministe à 9 968 nœuds et 71 303 relations sans
  diagnostic, puis smokes Studio, Toolset, Runtime, Forward+, MCP, réseau et distribution ;
- Forge Studio UX5-A : éditeur transactionnel de dialogues pour graphe, nœuds PNJ/joueur,
  branches paginées, liens existants et réponses réordonnables au clavier ou par glisser-déposer,
  avec locuteurs hérités protégés et preuves Forward+ FHD/QHD/4K ;
- Forge Studio UX4-A : création/archive/suppression sûre de faction, parents, héritage, matrice
  directionnelle et bandes hostile/neutre/allié, avec références externes bloquées, preuve Crypte
  réelle ou fallback isolé et contrôles Forward+ FHD/QHD/4K ;
- Forge Studio UX2-A : heartbeat de zone, perception et repos dans une transaction area-only,
  avec valeurs numériques exactes, voisins préservés, preuve isolée et contrôles Forward+
  FHD/QHD/4K ;
- matrice d'accès humain Studio portée à 126 routes `functional` et 591 `planned` après preuves
  individuelles des treize routes UX2-A/UX4-A/UX5-A ; `ORF-FAC-006` était alors encore `planned` ;
- jalon UX2-A/UX4-A/UX5-A validé par 40/40 tests dans les trois portails incrémentaux en 75,69 s,
  79,97 s et 64,03 s, un test RpgKit complémentaire, puis par le portail transversal complet :
  1 988 tests .NET, 39 tests Python, builds sans avertissement, graphe Roslyn déterministe, smokes
  Studio, Toolset, Runtime, Forward+, MCP, réseau et distribution ;
- validation des factions de trigger maintenue canonique : les fixtures Crypte et Toolset résolvent
  désormais `ash-watch` depuis le catalogue réel au lieu de fabriquer un UUID orphelin ;
- Forge Studio UX1-AJ : validation canonique de porte sur cinq catalogues de références, collision
  authorée distincte, diagnostics de liens, suggestions humaines et garde commune commande/MCP/
  prébuild, avec preuve isolée et contrôles Forward+ FHD/QHD/4K ;
- Forge Studio UX1-AI : contexte de validation du trigger optionnel ou requis, destinations humaines
  protégées, diagnostics canoniques groupés, dry-run, undo/redo, round-trip et preuves FHD/QHD/4K ;
- Forge Studio UX1-AH : heartbeat, événement personnalisé et contrôle MJ audité, avec valeurs hors
  bornes refusées, aucune prétention de dispatch Live Director et preuves isolées FHD/QHD/4K ;
- Forge Studio UX1-AG : accès, refus, piège et événements terminaux des portes, avec scaffold
  PortalUse interne non promu, préservation stricte, commande canonique et preuves FHD/QHD/4K ;
- schémas projet `0.6.37` et workspace `1.29.0`, compatibles en lecture avec `0.6.36` et `1.28.0`,
  pour la politique optionnelle de validation runtime Door ;
- matrice d'accès humain Studio portée à 113 routes `functional` et 604 `planned` ; la vague UX1 est
  close à 71/71 après preuves individuelles des dix routes UX1-AG/AH/AI/AJ ;
- jalon UX1-AG/AH/AI/AJ validé par 48/48 tests ciblés, quatre smokes et portails incrémentaux en
  75,98 s, 69,34 s, 64,99 s et 82,17 s, puis par le portail transversal complet : 1 988 tests .NET,
  39 tests Python, builds sans avertissement, graphe Roslyn à 9 132 nœuds et 61 393 relations,
  smokes Studio, Toolset, Runtime, Forward+, MCP et réseau ;
- parcours d'authoring et smoke Toolset G13-N alignés sur la validation Door canonique : ils ne
  fabriquent plus un lien orphelin vers une destination de portail et rapportent désormais des
  sous-verdicts runtime nommés ;
- Forge Studio UX1-AF : création gouvernée de plan de zone puis construction assistée, avec
  confidentialité/provenance, deux historiques séparés, doubles gardes de révision, réponses
  asynchrones périmées bloquées, preuve hors ligne isolée et contrôles Forward+ FHD/QHD/4K ;
- Forge Studio UX1-AE : trigger secret, clic et présentation, clé/justificatif et feedback d'accès,
  avec Foundation requise, scripts allowlistés, quatre aperçus runtime, preuve isolée, préservation,
  undo/redo, round-trip et contrôles Forward+ FHD/QHD/4K ;
- Forge Studio UX1-AD : réactions de capacité, heartbeat/événement personnalisé,
  conversation/faction et portes complexes, avec sélection explicite, libellés homonymes lisibles,
  fusion bornée, assemblages dangereux refusés et contrôles Forward+ FHD/QHD/4K ;
- matrice d'accès humain Studio portée à 103 routes `functional` et 614 `planned` après preuves
  individuelles des dix routes UX1-AD/AE/AF ;
- jalon UX1-AD/AE/AF validé par 26/26 tests ciblés, trois smokes et portails incrémentaux en
  68,48 s, 68,12 s et 75,68 s, puis par le portail transversal complet : 1 980 tests .NET,
  39 tests Python, builds sans avertissement, graphe Roslyn à 9 059 nœuds et 60 404 relations,
  smokes Studio, Runtime, Forward+, MCP et réseau ;
- smoke Studio UX0-D rendu indépendant de la page initiale afin que la preuve de placement soit
  exercée depuis sa propre page 3D, y compris lorsque le portail courant cible `plans` ;
- Forge Studio UX1-AC : piège, filtre de cible, dix volumes de gameplay et occupation, avec
  Foundation exigée, preuve temporaire isolée, double garde de révision, adaptateur runtime,
  préservation, undo/redo, round-trip et contrôles Forward+ Full HD/QHD/4K ;
- Forge Studio UX1-AB : dégâts, chargement, OnDamaged/OnDeath et attaque physique des portes, avec
  sélection explicite, liaisons serveur, fusion des événements voisins, ordre canonique, transaction
  UI isolée, undo/redo, round-trip et contrôles Forward+ Full HD/QHD/4K ;
- Forge Studio UX1-AA : sockets/assemblages, actions contextuelles et validation du placeable, avec
  refus humain des prérequis manquants, trois dry-runs, preuve isolée, préservation, undo/redo,
  round-trip et contrôles Forward+ Full HD/QHD/4K ;
- matrice d'accès humain Studio portée à 93 routes `functional` et 624 `planned` après preuves
  individuelles des onze routes UX1-AA/AB/AC ;
- jalon UX1-AA/AB/AC validé par 20/20 tests ciblés, trois smokes Studio et trois portails
  incrémentaux en 54,91 s, 55,42 s et 59,92 s, puis par le portail transversal complet : 1 980 tests
  .NET, 39 tests Python, builds sans avertissement, graphe Roslyn à 9 010 nœuds et 59 698 relations,
  smokes Studio, Runtime, Forward+, MCP et réseau ;
- Forge Studio UX1-Z : réactions `OnPhysicalAttacked`, `OnSpellCastAt`, heartbeat, événement
  personnalisé et politique physique de placeable, avec sélection explicite, quatre dry-runs,
  préservation des valeurs dormantes et capacités voisines, undo/redo, round-trip et preuve
  Forward+ Full HD ;
- Forge Studio UX1-Y : fondation polygonale de trigger, filtres et actions génériques, `OnEnter` et
  `OnExit`, avec scripts facultatifs normalisés, empreinte X/Z à échelle uniforme, volumes 3D
  préservés en lecture seule, layout scrollable contrôlé en Full HD/QHD/4K et lecture MCP ;
- Forge Studio UX1-X : fondation de porte avec sélection explicite, placement libre/ancré, neuf
  états et interaction d'ouverture, valeurs au quart de mètre, navigation interne non promue,
  préservation du catalogue, undo/redo, round-trip et preuve Forward+ Full HD ;
- matrice d'accès humain Studio portée à 82 routes `functional` et 635 `planned` après preuves
  individuelles des onze routes UX1-X/Y/Z ;
- jalon UX1-X/Y/Z validé par 18/18 tests ciblés, trois smokes Studio, les preuves Forward+ requises
  et le portail complet : 1 980 tests .NET, 39 tests Python, builds sans avertissement et graphe
  Roslyn déterministe à 8 959 nœuds, 59 067 relations et 0 diagnostic ;
- Forge Studio : préparation d'une cadence parallèle contrôlée avec deux worktrees d'implémentation
  bornés par page, un slot d'audit en avance, propriété centrale des routeurs/matrices/preuves et
  gel obligatoire avant les builds, tests, smokes et captures séquentiels ;
- Forge Studio UX1-W : échelle multi-objet sur sélection explicite, politique globale de zones
  dynamiques et aperçu honnête de copie runtime avec identités éphémères, contexte propriétaire et
  références déclarés indisponibles, commandes canoniques, undo/redo, round-trip et preuve Forward+
  Full HD ;
- Forge Studio UX1-V : événements `OnUsed`, `OnDisturbed`, `OnDamaged` et `OnDeath` de placeable,
  avec autorités dégâts directe/composée résolues par un contrat Domain commun, divergence refusée,
  compatibilité des anciens JSON, projection runtime/ForgeScript/MCP, undo/redo, round-trip et preuve
  Forward+ Full HD ;
- Forge Studio UX1-U : événements de verrouillage et de déverrouillage avec version ForgeScript,
  liaisons avant/après, méthode, justificatif, acteur, veto contrôlé, aperçu accepté ou refusé sans
  écriture, projection RPG Kit indépendante, lecture ForgeScript/MCP, mode optionnel ou requis,
  parité commande/UI, préservation, undo/redo, round-trip et preuve Forward+ Full HD ;
- Forge Studio UX1-T : événements d’ouverture et de fermeture de placeable avec version ForgeScript,
  liaisons nommées, acteur, résultat, état précédent et autorité, aperçu de dispatch typé sans
  écriture, projection RPG Kit indépendante, lecture ForgeScript/MCP, mode optionnel ou requis,
  parité commande/UI, préservation, undo/redo, round-trip et preuve Forward+ Full HD ;
- Forge Studio UX1-S : trésor initial de placeable avec table d’objets nommés, catégories, poids,
  quantités, qualité, conditions, unicité, moments chargement/ouverture/réinitialisation, graine et
  état généré persistant, avec remise à zéro explicite, aperçu déterministe RPG Kit, lecture
  ForgeScript/MCP, parité commande/UI, préservation des événements voisins, undo/redo, round-trip et
  preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-R : piège de placeable avec déclencheur, effet, détection, désamorçage,
  réarmement, propriétaire, politique alliée, VFX, état armé et usage unique, avec aperçu RPG Kit
  sans effet de scène, sous-profils de sécurité indépendants, lecture ForgeScript/MCP, parité
  commande/UI, préservation des événements voisins, undo/redo, round-trip et preuves Forward+ Full
  HD, QHD et 4K ;
- Forge Studio UX1-Q : politique de justificatif de placeable avec conservation, consommation,
  charges, partage, permission virtuelle et audit, avec aperçu de décision RPG Kit sans mutation
  d’inventaire, lecture ForgeScript/MCP, parité commande/UI, préservation du verrou et des données
  cachées, undo/redo, round-trip et preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-P : verrouillage de placeable avec quatre états, clé item réelle, justificatifs,
  défi, code masqué, piratage, droits, reverrouillage et brouillage, avec aperçu et résolution RPG
  Kit, lecture ForgeScript/MCP sans secret, parité commande/UI, préservation, undo/redo, round-trip
  et preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-O : liaisons de conversation de placeable vers un dialogue et son point d’entrée,
  avec déclencheurs interaction, état, utilisateur, objet et événement, priorité, usage unique,
  aperçu de résolution RPG Kit, lecture ForgeScript/MCP, préservation du dialogue Aurora historique,
  parité commande/UI, undo/redo, round-trip et preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-N : faction principale et appartenances de placeable, avec politiques d’hostilité,
  crime, accès et ciblage, aperçu de décision RPG Kit, lecture ForgeScript/MCP, préservation du champ
  Aurora historique, parité commande/UI, undo/redo, round-trip et preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-M : protection plot avec actions protégées, droits requis, feedback joueur et
  dérogation MJ auditée, avec aperçu des quatre décisions, enforcement RPG Kit, lecture
  ForgeScript/MCP, parité commande/UI, préservation, undo/redo, round-trip et preuves Forward+ Full
  HD, QHD et 4K ;
- Forge Studio UX1-L : destructibilité avec santé, armure, résistances typées, étapes visuelles,
  effet de destruction, débris, réparation et aperçu local des dégâts, avec projection et exécution
  RPG Kit, lecture ForgeScript/MCP, parité commande/UI, préservation, undo/redo, round-trip et preuves
  Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-K : état visuel initial parmi six états humains, animations, apparences,
  interaction et transitions ajoutables/prévisualisées, avec projection RPG Kit, lecture
  ForgeScript/MCP, parité commande/UI, préservation, undo/redo, round-trip et preuves Forward+ Full
  HD, QHD et 4K ;
- Forge Studio UX1-J : transformation précise avec position, rotation, échelle, pivot, grille,
  parent humain, alignement de surface et animation, avec validation de hiérarchie, projection RPG
  Kit, lecture ForgeScript/MCP, parité commande/UI, préservation, undo/redo, round-trip et preuves
  Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-I : identité et apparence directes, noms et invitations localisés, tags, notes,
  asset, meshes, matériaux, icône, portrait et VFX, avec projection RPG Kit, lecture ForgeScript/MCP,
  parité commande/UI, préservation, undo/redo, round-trip et preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-H : accès direct au conteneur, capacité et masse, propriétaire, droits,
  persistance, réplication, inventaire, butin et présentation, avec projection RPG Kit,
  lecture ForgeScript, parité commande/UI, préservation, undo/redo, round-trip et preuves Forward+
  Full HD, QHD et 4K ;
- Forge Studio UX1-G : accès direct à l’objet utilisable, actions nommées ajoutables, portée, angle,
  durée, focus, autorité, conditions et événement `OnUsed`, avec aperçu complet, exécution runtime,
  sélection 3D, parité commande/UI, undo/redo, round-trip et preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-F : accès direct au décor statique, regroupement, instanciation, occlusion,
  fusion sûre et collision optionnelle, avec aperçu d’impact, sélection 3D, parité commande/UI,
  undo/redo, round-trip et preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-E : blueprint de décor humain avec ressource importée et usage réel, collision,
  rôle statique ou utilisable, interaction, transform indépendant du moteur, scripts et sélection 3D,
  avec aperçu canonique, undo/redo, round-trip et preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-D : arbre humain de calques et groupes avec glisser-déposer, visibilité et
  verrouillage hérités, responsables, affectation et sélection d’objets, aperçu d’export partiel,
  contrat runtime, undo/redo et round-trip, avec preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-C : écran de chargement humain avec sélection de médias importés, astuces
  localisées, progression et préchargement réels, aperçu mouvements réduits, fallback accessible,
  undo/redo et round-trip, avec preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-B : redimensionnement avec ancrage et traitement explicite des objets hors
  limites, copie complète ou gabarit avec transitions configurables et identités remappées, aperçu
  canonique, undo/redo et round-trip, avec preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX1-A : assistant humain de création de zone, gabarits vide/jouable, profils de
  fondation, aperçu sans écriture, undo/redo et document indépendant vérifié après réouverture,
  avec overlay responsive et preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX0-J : renommage humain prévisualisé avec identité stable, migration de références
  et undo/redo, puis diagnostics structurés, liens profonds, correctifs transactionnels et export
  expurgé, avec preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX0-I : variables de projet typées par noms humains, cache ForgeScript local
  réellement compilé/incrémental et guide joueur à huit pages localisées, avec commandes canoniques,
  undo/redo, round-trip et preuves Forward+ Full HD, QHD et 4K ;
- Forge Studio UX0-H : configuration humaine et transactionnelle de l’abandon de cinématique, du
  calendrier du monde et de l’échelle de progression, avec exécution runtime et preuves Forward+
  exactes Full HD, QHD et 4K ;
- schéma projet persistant `0.6.36` : chemins/handlers ForgeScript sûrs pour repos, heartbeats
  globaux et événements utilisateur typés, avec migration compatible depuis `0.6.35` ;
- schéma projet persistant `0.6.35` : liaisons de cycle de vie par contexte et chemins/handlers
  ForgeScript facultatifs pour les événements de jeu, avec migration déterministe depuis `0.6.34`.

## [0.6.34] — 2026-08-09

### Added

- profil canonique de build déterministe et incrémental ;
- gouvernance v1 des worktrees, de la CI, des fixtures, des versions et des ADRs.
- fondation de créature persistante, éditable, validée et résolue au runtime.

### Validation

- portail intégré `scripts/Verify.ps1` ;
- fixtures tiny, vertical slice, stress, malicious et migration versionnées.
