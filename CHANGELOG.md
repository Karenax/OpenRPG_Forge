# Changelog

Toutes les modifications notables d'OpenRPG Forge sont consignées ici. Les versions produit suivent
SemVer ; les versions de schéma persistent séparément dans leurs documents.

## [Unreleased]

### Changed

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
