# Plan de livraison des capacités inspirées d’Aurora

Date de référence : 2 août 2026
Source de recherche : `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/`
Périmètre : 717 exigences, 26 domaines

## 1. Décision de planification

Les 717 exigences ne constituent pas une liste à implémenter séquentiellement. Elles sont le catalogue
de capacités cible. La livraison est organisée en quatorze gates verticaux, chacun produisant un
résultat utilisable, testable et démontrable.

Le programme suit cet ordre :

1. auditer la couverture réelle et installer la gouvernance du catalogue ;
2. terminer la tranche solo « Crypte des Cendres » avec des données canoniques ;
3. sécuriser ForgeScript, la sauvegarde, les modules et MCP ;
4. compléter l’expérience d’auteur du Core et du RPG Kit ;
5. ajouter le Network Kit, puis le Live Director ;
6. terminer le pipeline de contenu moderne et les fonctions IA avancées ;
7. traiter P2, P3 et P4 par valeur produit mesurée.

La priorité `P0` indique une fondation de capacité, pas une autorisation à ouvrir simultanément les
196 fonctionnalités P0. Chaque incrément d’implémentation reste borné à un ensemble cohérent de une à
cinq feature IDs.

## 2. Point de départ prouvé

Au 2 août 2026 :

- les lots T0 à T9 du MVP sont franchis localement ;
- le modèle Project/Module/Area/Blueprint/Entity, le bus de commandes, les révisions, le dry-run,
  l’undo/redo, la sérialisation et les migrations de projet existent ;
- l’éditeur place, transforme et supprime des instances, reconstruit une zone et lance un playtest ;
- le runtime jouable, le scénario headless de la Crypte et la reprise d'une sauvegarde dans un
  second processus sont verts ;
- 133 tests .NET et 17 tests Python sont exécutés par le gate local ;
- dialogue, quête, combat, capacités et butin sont des agrégats data-driven validés et consommés par
  le runtime ;
- ForgeScript natif, `.orpmod`, sauvegarde de partie et MCP transactionnel sont implémentés et testés ;
- le serveur autoritaire et Blender Worker ne sont pas commencés ;
- le tracker Aurora est intégralement au statut `Researched`. Il ne reflète donc pas encore les
  fonctions déjà présentes dans le dépôt.

La prochaine étape n'est plus un lot MVP : **G0 à G11 sont fermés** et leurs scénarios verticaux
restent protégés par le gate intégré. Les lots d’assets AV1 à AV6-F livrent en plus un Vault
gouverné, une carte extérieure 512 × 512 jouable, son rendu PBR stylisé, des personnages animés et
une preuve réelle de retargeting inter-rigs. **G12-A** ouvre désormais les extensions 1.x avec un
Assistant intégré, un fournisseur hors ligne déterministe, des contrats de fournisseurs
interchangeables et des prompts versionnés testés. La fermeture de `TASKS_MVP.md` ne signifie pas
une parité Aurora complète : G12-B ferme les transports locaux et le lore, G12-C livre le premier
plan de zone assisté sans mutation géométrique, puis G12-D construit un plan approuvé par les mêmes
commandes que l’édition manuelle, puis G12-E relie le graphe RPG avancé au dialogue Core avec
citations, localisation, lint et simulation, puis G12-F livre les machines à états de quête,
objectifs, récompenses, échec et scénarios. G12-G ajoute la création/réparation ForgeScript compilée
et testée avant écriture. G12-H compose maintenant un boss canonique multi-domaines, simule son
équilibrage et applique son lot atomique. G12-I crée désormais une capacité autonome, résout ses
dépendances et la soumet à cinq tests du résolveur de combat. G12-J livre désormais la création 3D
gouvernée, du document canonique au GLB Blender réellement exporté. La file active commence à
**G12-K**, fournisseurs text/image-to-3D optionnels et contrôlés.

## 3. Jalons produit

| Jalon | Gates requis | Résultat utilisateur |
|---|---|---|
| Prototype actuel | acquis | La Crypte est jouable, mais plusieurs mécaniques sont codées temporairement dans le runtime. |
| MVP solo data-driven | G0 à G6 | Un auteur construit, scripte, exporte, joue, sauvegarde et fait modifier prudemment la Crypte par un agent. |
| Creator Alpha | G7 | Le Toolset Core couvre les workflows P0/P1 essentiels d’un véritable éditeur de modules. |
| RPG Creator Beta | G8 | Les systèmes RPG P0/P1 permettent un module original complet, sans dépendance à des règles propriétaires. |
| Network Beta | G9 | Deux clients jouent la tranche verticale sur un serveur autoritaire avec synchronisation de contenu. |
| Live Director Preview | G10 | Un GM authentifié inspecte et dirige une partie avec des commandes auditées. |
| OpenRPG Forge 1.0 | G0 à G11 | Toolset, runtime, RPG Kit, réseau optionnel et pipeline de contenu sont distribuables et documentés. |
| Extensions 1.x | G12 et G13 | IA avancée et capacités spécialisées livrées sans devenir des dépendances obligatoires. |

Les dates calendaires seront estimées après G0 et trois incréments terminés. Avant cette mesure de
vélocité, donner des dates masquerait l’écart entre les 717 exigences recherchées et leur couverture
réelle.

## 4. Gates de livraison

### G0 — Gouvernance et audit de couverture

**État.** Fermé le 2 août 2026. Les artefacts sont sous `tracking/project/` et
`docs/aurora-parity/` ; leur fraîcheur et l'intégrité du snapshot sont testées.

**But.** Transformer le dossier de recherche en backlog projet vérifiable sans modifier le snapshot
source ni inventer de couverture.

**Travaux.**

- décider par ADR ou note de gouvernance si le dossier de `Divers/` reste le snapshot immuable et
  installer sa copie canonique sous `docs/aurora-parity/` ;
- créer un tracker projet séparé si le snapshot reste immuable ;
- auditer les 76 P0 de `SHELL`, `PROJECT`, `AREA`, `PALETTE`, `BUILD` et `QA` contre code, tests et
  documentation ;
- renseigner pour les 717 lignes un gate cible, sans changer leur statut d’implémentation par simple
  inférence ;
- produire le rapport de lacunes Core et les éventuelles propositions d’ADR ;
- relier `TASKS_MVP.md`, le tracker et le présent plan avec des identifiants stables.

**Sortie obligatoire.** Tracker projet, gap report, matrice feature → gate, liste des preuves et
premier epic borné.

**Gate de passage.** Les 76 P0 de fondation sont classés `NotStarted`, `Designed`, `Implemented` ou
`Tested` avec preuve ; aucune ligne `Tested` ne repose sur un nom de fichier ou sur le seul statut du
MVP.

### G1 — Narration canonique

**Domaines.** `DIALOGUE`, `QUEST`, puis les éléments nécessaires de `WAYPOINT`, `AUDIO` et `CONTENT`.

**Travaux.**

- créer les documents canoniques de dialogue et de quête, avec UUID, versions et ordre stable ;
- modéliser nœuds, choix, liens, conditions typées, actions typées et transitions de quête ;
- fournir validation de graphe, diagnostics localisés et simulation sans moteur ;
- faire passer toutes les mutations d’édition par les commandes, révisions et undo/redo ;
- livrer l’espace Dialogues, l’espace Quêtes et un aperçu minimal ;
- remplacer le PNJ et la quête codés dans le runtime de la Crypte par ces documents ;
- utiliser d’abord des conditions/actions déclaratives intégrées. Les hooks ForgeScript attendent G3.

**Gate de passage.** Le scénario Writer crée un dialogue conditionnel, fait progresser une quête,
sauvegarde, recharge, simule toutes les branches et rejoue la Crypte sans logique narrative codée en
dur dans l’adaptateur runtime.

### G2 — Gameplay RPG data-driven

**Domaines.** `CREATURE`, `FACTION`, `ITEM`, `PLACEABLE`, `DOOR`, `TRIGGER`, `ENCOUNTER` et
`GAMEPLAY`.

**Travaux.**

- introduire les composants minimaux de créature, santé, ressources, équipe et faction ;
- modéliser capacités, coûts, cooldowns, ciblage, effets et indices visuels séparés ;
- ajouter objets, inventaire, équipement minimal et butin ;
- rendre portes, objets utilisables, transitions, triggers et rencontres configurables ;
- publier des événements typés pour spawn, interaction, dommage, mort, entrée et sortie ;
- convertir le combat, la clé, la porte, le boss et le butin de la Crypte en données ;
- décider et documenter la frontière concrète `Forge Core` / `Forge RPG Kit` avant d’ajouter les
  premiers projets du kit ; ne créer aucun projet vide.

**Gate de passage.** La Crypte complète se reconstruit depuis documents, blueprints, composants et
événements. Le scénario headless prouve clé, porte, combat, phase 2, butin, quête et sortie sans
branche spécifique à la démo dans le runtime générique.

### G3 — ForgeScript sûr

**Domaine.** `SCRIPT`, avec les événements stabilisés par G1 et G2.

**Travaux.**

- préparer explicitement CMake et un compilateur natif, absents du `PATH` au point de départ ;
- épingler AngelScript, sa licence et ses notices ;
- livrer la GDExtension C++ minimale et le contrat C# `IForgeScriptRuntime` ;
- compiler source vers bytecode avec diagnostics et métadonnées de versions VM/API ;
- exposer handles opaques, variables, entités, mouvement, dialogue, quête, capacités et spawn ;
- appliquer budgets d’instructions, mémoire, pile, créations et récursion d’événements ;
- interdire fichiers, réseau, processus, threads, chargement natif, mémoire brute et API du moteur ;
- migrer la phase 2 du boss vers ForgeScript et ajouter le hot reload minimal en développement.

**Gate de passage.** Le scénario Scripter compile un script valide, localise une erreur, recharge un
script, arrête une boucle infinie sans tuer le serveur et exécute le boss en headless de manière
déterministe.

### G4 — Sauvegarde, packages et sécurité du contenu

**Domaines.** `GAMEPLAY`, `PROJECT`, `CONTENT`, `BUILD` et `QA`.

**Travaux.**

- définir l’état sauvegardé comme delta versionné par rapport aux définitions ;
- tester sauvegarde, chargement, compatibilité, migration et reprise après échec ;
- finaliser manifeste, schémas, packager et loader `.orpmod` ;
- gérer hashes, dépendances, ordre des packages, namespaces et cache jetable ;
- conserver provenance, licences, localisation, source ForgeScript et bytecode ;
- mettre en quarantaine les imports externes ;
- refuser chemins absolus, traversées, bombes de décompression, doublons, hashes invalides, DLL,
  assemblies, scripts du moteur et binaires natifs ;
- rendre l’export atomique et reproductible.

**Gate de passage.** Un build propre exporte la Crypte, l’installe dans un runtime vierge, la lance,
sauvegarde et recharge une partie. Les fixtures malveillantes sont toutes refusées avec diagnostics
structurés et sans état partiellement installé.

### G5 — MCP transactionnel minimal

**Domaine.** P0 de `AI`, sur les commandes déjà éprouvées par les interfaces humaines.

**Travaux.**

- créer le MCP Host C# et son canal local authentifié, sans écoute externe par défaut ;
- exposer résumé du projet, révision, sélection, diagnostics et graphe de références ;
- définir des outils sémantiques bornés pour zone, entité, dialogue, quête, script et test ;
- séparer lecture, planification et application par autorisation ;
- imposer plan lié à une révision, dry-run, diff sémantique, commit atomique, inverse et audit ;
- produire des résultats structurés et limiter budget, données envoyées et fournisseurs ;
- traiter le contenu du projet comme donnée non fiable contre la prompt injection ;
- garantir un mode hors ligne complet et ne fournir aucun shell ou Python arbitraire.

**Gate de passage.** Le scénario Agent lit la Crypte, propose une modification de dialogue, obtient un
diff, applique avec autorisation, lance les tests, annule la transaction et retrouve un projet
byte-identique.

### G6 — MVP solo distribuable

**But.** Fermer `TASKS_MVP.md` sans élargir le produit.

**Travaux.**

- finaliser la zone de démonstration avec des assets originaux ou explicitement licenciés ;
- valider navigation, collisions, sauvegarde, export, undo, headless et playtest interactif ;
- produire un rapport de performance et fixer une cible matérielle provisoire ;
- fournir crédits, notices, guide créateur et procédure de reproduction des scénarios ;
- exécuter les scénarios Builder, Writer, Scripter et Agent ;
- mettre à jour tracker, `STATUS.md`, `TASKS_MVP.md` et preuves.

**Gate de passage.** Un nouveau poste peut construire et tester le MVP à partir du dépôt, puis un
créateur peut modifier, exporter et jouer la Crypte sans IA et sans outil propriétaire obligatoire.

### G7 — Creator Core complet P0/P1

**État.** Fermé. Le scénario Builder, les 35 tranches Creator, la revue visuelle et le gate intégré
prouvent les workflows Core retenus ; les capacités plus avancées restent affectées à G13 avec une
justification explicite dans le tracker.

**Domaines.** `SHELL`, `PROJECT`, `AREA`, `PALETTE`, parties Core de `PLACEABLE`, `DOOR`, `TRIGGER`,
`WAYPOINT`, `AUDIO`, `GAMEPLAY`, `CONTENT`, `BUILD`, `QA` et `EE` indispensable.

**Travaux majeurs.**

- explorateur de projet, recherche, références inverses et renommage sûr ;
- gizmos et manipulation directe dans le viewport, duplication, copier/coller et retour invalide ;
- palettes standard/projet, drag-and-drop, variantes et dépendances automatiques ;
- construction de zones, navigation, transitions, audio spatial et validation géométrique ;
- content browser, import contrôlé, localisation et outils de build incrémental ;
- diagnostics cliquables, profils de validation, accessibilité et tests UI ciblés ;
- plusieurs espaces de travail cohérents sans remplacer l’éditeur 3D sous-jacent.

**Gate de passage.** Le scénario Builder complet est vert sur un projet créé de zéro et toutes les
P0 Core non différées possèdent une preuve. Les P1 indispensables au workflow sont `Tested`.

### G8 — RPG Kit complet P0/P1

**Domaines.** `CREATURE`, `FACTION`, `DIALOGUE`, `QUEST`, `ITEM`, parties RPG de `PLACEABLE`, `DOOR`
et `TRIGGER`, `ENCOUNTER`, `MERCHANT` et `COMPANION`.

**Travaux majeurs.**

- édition complète des blueprints de créatures, objets, inventaires, équipement et loot ;
- réputation, réactions, hostilité et conséquences de dialogue/quête ;
- éditeurs narratifs avec recherche, commentaires, tests, couverture de branches et localisation ;
- rencontres, spawns, patrouilles, pièges, serrures et transitions ;
- magasins, prix, restrictions et transactions atomiques ;
- recrutement, ordres, équipement, comportement et interface de groupe ;
- API ForgeScript et commandes MCP seulement après stabilisation de chaque workflow manuel.

**Gate de passage.** Un auteur crée un second module original utilisant tous ces systèmes, sans
modifier le moteur ni les fichiers canoniques à la main. Les P0/P1 nécessaires au scénario sont
`Tested` et les autres sont explicitement `Deferred` avec justification et cible.

### G9 — Network Kit

**Domaine.** `MULTI` et exigences réseau transversales des autres domaines.

**Travaux.**

- serveur headless autoritaire réutilisant les contrats runtime ;
- sessions, identité, rôles, groupes et permissions ;
- réplication d’entités et interest management ;
- synchronisation de contenu, fingerprint et verrouillage de versions ;
- chat, reconnexion, logs, métriques de base et anti-cheat par autorité ;
- tests déterministes à plusieurs clients ;
- persistance externe uniquement derrière une interface et après besoin mesuré.

**Gate de passage.** Deux clients accomplissent la Crypte, se déconnectent et se reconnectent sans
désynchronisation. Un client altéré ne peut imposer une mutation autoritaire.

### G10 — Live Director

**Domaine.** `DM`, construit exclusivement sur G9 et le bus de commandes.

**Travaux.**

- client GM séparé, authentification forte et permissions ;
- listes/recherche de joueurs et objets, inspection et navigation ;
- spawn, téléportation, soin, destruction, portes et rencontres ;
- possession et impersonation avec limites explicites ;
- sélection et ordres de groupe ;
- chat GM, scènes préparées, commandes narratives et audit complet.

Les capacités DM nécessaires se trouvent principalement en P2. Elles sont promues dans ce jalon par
valeur produit explicite ; la priorité historique ne doit pas produire un faux client GM réduit à
l’authentification et au journal.

**Gate de passage.** Le scénario GM est exécuté sur une vraie session réseau et chaque action est
autorisée, validée, attribuée et consultable dans l’audit.

### G11 — Contenu moderne et qualité de distribution

**État.** Fermé le 5 août 2026. G11-A à G11-T couvrent contenu moderne, expérience, extensions,
résilience, qualité, performance, accessibilité, sécurité et gouvernance. AV6-A à AV6-F complètent
la preuve de production 3D gouvernée sur la carte extérieure réelle.

**Domaines.** Reste de `CONTENT` P0/P1, `EE` P0/P1, `BUILD`, `QA`, `AREA`, `AUDIO` et `GAMEPLAY`.

**Travaux.**

- pipeline glTF/GLB, textures, collisions, animations, LOD et round-trip Blender ;
- PBR, HDR, éclairage, UI scalable, sérialisation runtime et profilage ;
- builds déterministes, incrémentaux et multi-cibles ;
- migrations de packages, désinstallation sûre et compatibilité plateforme ;
- performance, accessibilité, sécurité, licences et procédures de release ;
- revalidation visuelle et matérielle sur des scènes représentatives, pas uniquement sur la Crypte.

**Gate de passage.** Les packages de distribution passent les scénarios auteur et joueur sur les
plateformes retenues, avec budgets mesurés, provenance complète et aucune dépendance obligatoire à
Blender au runtime.

### G12 — IA de création avancée

**État.** En cours. G12-A livre l’Assistant intégré, le profil persistant, le fournisseur hors ligne
déterministe et les prompts versionnés. G12-B ajoute les transports locaux compatibles OpenAI et
Ollama, puis l’index de lore gouverné et son contrat MCP. G12-C produit désormais un plan de crypte
ou village contrôlé, persistant et annulable avant toute génération de géométrie. G12-D applique
désormais un plan approuvé par un lot de placements atomique, validé, persistant et réversible. G12-E
livre la création de dialogue assistée sur les agrégats RPG et Core existants. G12-F livre la
création de quête assistée avec le même contrat gouverné. G12-G livre la création de ForgeScript
assistée sans contourner le compilateur ni la sandbox. G12-H livre la création de boss assistée :
créature, combat, capacités, rencontre, phases, scripts, dialogue, loot et 32 simulations sont liés
par un seul lot persistant et réversible. G12-I livre la création de capacité autonome : ciblage,
coût, recharge, effets, cues, tags IA, ForgeScript, item optionnel, cinq tests combat et équilibrage
sont liés par un seul lot persistant et réversible. G12-J livre la création 3D via Blender : recette
canonique de primitives/transforms/matériau/UV/LOD/collision, worker typé sans Python libre,
validation, miniature, audit et GLB réel, avec UI et MCP sur le même lot réversible. G12-K portera
les fournisseurs text/image-to-3D optionnels, leur staging et l'acceptation humaine.

**Domaine.** `AI` P1/P2, après stabilisation des workflows humains correspondants.

**Travaux.**

- assistant intégré, index de lore et fournisseurs interchangeables ;
- génération contrôlée de plans de zones, dialogues, quêtes, capacités et ForgeScript ;
- agent de test, simulation narrative et analyse de cohérence ;
- Blender Worker isolé et tâches 3D bornées avec provenance ;
- prompts versionnés, budgets, confidentialité, traces et évaluations reproductibles ;
- absence de dépendance fonctionnelle à un fournisseur ou à une connexion réseau.

**Gate de passage.** Chaque workflow IA est comparé au workflow manuel, produit les mêmes commandes
et validations, peut être annulé, fonctionne avec un provider factice en CI et se désactive sans
dégrader l’édition manuelle.

### G13 — Capacités avancées P2/P3/P4

**Périmètre.** Toutes les fonctions restantes du catalogue : persistent worlds, fonctions de
production studio, création dynamique avancée, véhicules/espace, effets avancés, collaboration et
recherche longue durée.

Chaque capacité entre dans une release seulement si elle possède :

- un besoin utilisateur ou un scénario démontrable ;
- un sponsor et une priorité produit ;
- un contrat Core/Kit clair ;
- une analyse sécurité, compatibilité et maintenance ;
- un benchmark lorsque la performance motive le travail ;
- un ADR si elle modifie une décision structurante.

`ORF-MP-024` (sharding et zones serveur), seule P4, reste une recherche explicite et ne bloque aucune
release antérieure.

## 5. Affectation complète des 717 exigences

La colonne « premier gate » indique où chaque domaine commence. Les priorités supérieures du même
domaine peuvent continuer dans les gates suivants ; le tracker projet porte l’affectation précise de
chaque feature.

| Domaine | Total | P0 | P1 | P2 | P3/P4 | Premier gate | Gate de complétude principal |
|---|---:|---:|---:|---:|---:|---|---|
| SHELL | 24 | 17 | 6 | 1 | 0 | G0 | G7 |
| PROJECT | 25 | 11 | 8 | 6 | 0 | G0 | G7 |
| AREA | 37 | 9 | 16 | 12 | 0 | G0 | G7/G11 |
| PALETTE | 18 | 8 | 8 | 2 | 0 | G0 | G7 |
| CREATURE | 49 | 8 | 20 | 17 | 4 | G2 | G8 |
| FACTION | 14 | 1 | 9 | 3 | 1 | G2 | G8 |
| DIALOGUE | 32 | 10 | 15 | 6 | 1 | G1 | G8 |
| QUEST | 25 | 8 | 12 | 5 | 0 | G1 | G8 |
| ITEM | 32 | 4 | 13 | 13 | 2 | G2 | G8 |
| PLACEABLE | 27 | 7 | 13 | 6 | 1 | G2 | G7/G8 |
| DOOR | 21 | 7 | 6 | 7 | 1 | G2 | G7/G8 |
| TRIGGER | 20 | 6 | 4 | 9 | 1 | G2 | G7/G8 |
| ENCOUNTER | 20 | 3 | 11 | 5 | 1 | G2 | G8 |
| WAYPOINT | 17 | 4 | 9 | 3 | 1 | G1 | G7/G8 |
| AUDIO | 20 | 5 | 11 | 4 | 0 | G1 | G7/G11 |
| MERCHANT | 19 | 2 | 8 | 7 | 2 | G8 | G8 |
| COMPANION | 24 | 1 | 13 | 10 | 0 | G8 | G8 |
| SCRIPT | 41 | 16 | 21 | 3 | 1 | G3 | G8 |
| GAMEPLAY | 29 | 7 | 10 | 8 | 4 | G2 | G7/G11 |
| DM | 37 | 0 | 5 | 27 | 5 | G10 | G10/G13 |
| MULTI | 36 | 0 | 13 | 17 | 6 | G9 | G9/G13 |
| CONTENT | 31 | 14 | 12 | 4 | 1 | G4 | G11 |
| EE | 31 | 3 | 19 | 9 | 0 | G7 | G11 |
| AI | 33 | 14 | 15 | 3 | 1 | G5 | G12 |
| BUILD | 25 | 17 | 6 | 2 | 0 | G0 | G11 |
| QA | 30 | 14 | 15 | 1 | 0 | G0 | transversal |
| **Total** | **717** | **196** | **298** | **190** | **33** |  |  |

Cette matrice couvre toutes les exigences. Elle ne remplace pas le catalogue JSON/CSV, qui reste la
source de vérité pour les feature IDs, dépendances et critères d’acceptation.

## 6. Historique d’exécution MVP et reprise Creator Core

Les incréments suivants donnent un ordre directement utilisable. Un même feature ID peut apparaître
dans un incrément de domaine puis d’éditeur/runtime ; il ne passe `Tested` qu’une fois tous ses
critères d’acceptation et preuves satisfaits.

| Ordre | Incrément borné | Feature IDs | Résultat |
|---:|---|---|---|
| 1 | G0-A | Audit Core P0 | Audit, tracker projet et gap report ; aucune implémentation. |
| 2 | G1-A | ORF-DLG-001, 002, 003, 004, 029 | Noyau de graphe canonique, IDs, liens et validation structurelle. |
| 3 | G1-B | ORF-DLG-005, 006, 009, 011, 021 | Ordre, localisation, conditions/actions typées et simulation. |
| 4 | G1-C | ORF-DLG-001, 002, 005, 006, 021 | Éditeur minimal, commandes, undo/redo et aperçu. |
| 5 | G1-D | ORF-QST-001, 002, 003, 004, 007 | Machine à états de quête et transitions. |
| 6 | G1-E | ORF-QST-005, 022, 023 | Localisation, simulation et validation narrative. |
| 7 | G1-F | DLG/QST déjà ouverts | Migration du PNJ et de la quête de la Crypte, preuves headless. |
| 8 | G2-A | ORF-CRE-002, 003, 014, 027, 049 | Blueprint de créature minimal et validation. |
| 9 | G2-B | ORF-CRE-025, ORF-FAC-001, 003, 005, 014 | Affectation, relations, seuils et validation de faction. |
| 10 | G2-C | ORF-CRE-033, 036, 043, ORF-GAME-023 | Événements de cycle de vie et branchement du ruleset. |
| 11 | G2-D | ORF-CRE-013, 015, 021, ORF-GAME-014 | Attributs, défense, capacités et ciblage minimaux. |
| 12 | G2-E | ORF-ITEM-002, 003, 004, 032 | Objet canonique minimal et validation. |
| 13 | G2-F | ORF-DOOR-002, 003, 008, 009, 021 | États, interaction, transitions et validation de porte. |
| 14 | G2-G | ORF-TRG-001, 002, 006, 007, 020 | Volumes, entrée/sortie et validation de trigger. |
| 15 | G2-H | ORF-ENC-002, 003, 020 | Rencontre minimale et validation. |
| 16 | G3-A | ORF-SCR-001, 002, 003, 027, 036 | VM, compilation, bytecode, sandbox et version d’API. |
| 17 | G3-B | ORF-SCR-028, 029, 030, 031, 039 | Handles, quotas, déterminisme et permissions. |
| 18 | G3-C | ORF-SCR-006, 007, 009, 017, 022 | Événements et première API runtime. |
| 19 | G4-A | ORF-GAME-020, ORF-QA-015, ORF-PROJ-025 | Sauvegarde, chargement, compatibilité et migrations. |
| 20 | G4-B | ORF-CONT-001, 003, 006, 007, 019 | Package versionné, dépendances, sources et namespace. |
| 21 | G4-C | ORF-CONT-023, 024, 030, 031, ORF-QA-026 | Provenance, quarantaine, notices, IP et fixtures hostiles. |
| 22 | G5-A | ORF-AI-002, 004, 005, 006, 007 | Ressources, références, outils, plan et dry-run. |
| 23 | G5-B | ORF-AI-008, 009, 010, 011, 031 | Transaction, diff, autorisations, audit et résultats. |
| 24 | G5-C | ORF-AI-013, 023, 032, 033 | Budget, absence de Python libre, injection et mode hors ligne. |

Les incréments G1 à G5 ci-dessus ont fourni le socle du MVP et ne constituent plus la file active.
Leur couverture exacte reste à reporter feature par feature : un lot MVP vert ne transforme pas
automatiquement ses lignes en `Tested`.

La file active est maintenant :

| Ordre | Incrément borné | Feature IDs | Résultat |
|---:|---|---|---|
| 1 | G0-A | 76 P0 Core | Tracker projet, preuves, gap report et matrice des 717 gates. |
| 2 | G7-01 | ORF-PROJ-001, 003, 004, 005, ORF-QA-010 | Second module générique chargé et testé sans logique Crypte. |
| 3 | G7-02 | ORF-SHELL-001, 002, 006, 020 | Terminé : coque Forge, explorateur, sélection et diagnostics partagés. |
| 4 | G7-03 | ORF-PAL-001, 002, 003, 007, ORF-BUILD-010 | Terminé partiellement : catalogue éditable, dérivation d’instance et références inversées. |
| 5 | G7-04 | ORF-SHELL-010, 011, 013, ORF-PAL-012 | Terminé partiellement : placement direct, fantôme validé, gizmo X/Z et historique. |
| 6 | G7-05 | ORF-AREA-001, 002, 030, 037 | Terminé partiellement : documents indépendants, zone active, transitions et validation. |
| 7 | G7-06 | ORF-SHELL-014, ORF-AREA-012 | Terminé partiellement : clipboard sémantique, duplication, nouveaux UUID et remapping contrôlé. |
| 8 | G7-07 | ORF-SHELL-015, 016, 017 | Terminé partiellement : recherche globale, références et renommage sûr transactionnel. |
| 9 | G7-08 | ORF-PROJ-002, 019 | Terminé partiellement : métadonnées localisées du module et blackboard global typé. |
| 10 | G7-09 | ORF-SHELL-005, ORF-PAL-006, 016 | Terminé partiellement : palettes Standard/Custom, filtres, lecture seule et provenance. |
| 11 | G7-10 | ORF-SHELL-007 | Terminé partiellement : navigation caméra, focus, vues, vitesse, pilote et bookmarks. |
| 12 | G7-11 | ORF-PROJ-006, ORF-AREA-022 | Terminé partiellement : événements Core ordonnés, versionnés, observables et branchés au runtime. |
| 13 | G7-12 | ORF-PROJ-021, ORF-PAL-015, ORF-BUILD-005 | Terminé partiellement : cache ForgeScript vérifié, dépendances automatiques et rebuild incrémental inspectable. |
| 14 | G7-13 | ORF-PROJ-024, ORF-BUILD-022, ORF-QA-007 | Terminé partiellement : cinq profils canoniques, registre tiers, NOTICE et gate de conformité. |
| 15 | G7-14 | ORF-AREA-031, ORF-QA-004, ORF-QA-005 | Terminé partiellement : navigation par zone, validation géométrique et reachability depuis les spawns. |
| 16 | G7-15 | ORF-QA-021 | Terminé partiellement : diagnostics à UUID/sévérité/source, navigation et quick fixes transactionnels. |
| 17 | G7-16 | ORF-CONT-020, ORF-PAL-010, ORF-PAL-011 | Terminé partiellement : catalogue v1 de 605 entrées, facettes, aperçu local et import gouverné en dry-run. |
| 18 | G7-17 | ORF-CONT-011 | Terminé partiellement : catalogue canonique, fallback, extraction, pseudo-localisation, diagnostics et édition transactionnelle. |
| 19 | G7-18 | ORF-EE-002 | Terminé partiellement : échelle précise/uniforme, Ctrl-molette, contraintes, validation et transaction multi-entités. |
| 20 | G7-19 | ORF-EE-028 | Terminé partiellement : A* déterministe, coûts, obstacles dynamiques, récupération et invalidation partielle. |
| 21 | G7-20 | ORF-SHELL-008, 009, 012, 021, 023 | Gate P1 terminé : filtres restaurés dans le viewport, placement répété atomique, zones comparables, aide sourcée et profils persistants ; extensions avancées explicitement différées. |

Les fiches exécutées sont `docs/aurora-parity/tasks/G7-01-GENERIC-SECOND-MODULE.md`,
`docs/aurora-parity/tasks/G7-02-CREATOR-SHELL.md` et
`docs/aurora-parity/tasks/G7-03-BLUEPRINT-CATALOG.md` et
`docs/aurora-parity/tasks/G7-04-DIRECT-WORLD-MANIPULATION.md` et
`docs/aurora-parity/tasks/G7-05-INDEPENDENT-AREAS.md`,
`docs/aurora-parity/tasks/G7-06-SEMANTIC-CLIPBOARD.md`,
`docs/aurora-parity/tasks/G7-07-GLOBAL-SEARCH-SAFE-RENAME.md` et
`docs/aurora-parity/tasks/G7-08-MODULE-METADATA-VARIABLES.md` et
`docs/aurora-parity/tasks/G7-09-STANDARD-CUSTOM-PALETTES.md` et
`docs/aurora-parity/tasks/G7-10-EDITOR-CAMERA-NAVIGATION.md` et
`docs/aurora-parity/tasks/G7-11-CORE-LIFECYCLE-EVENTS.md` et
`docs/aurora-parity/tasks/G7-12-INCREMENTAL-BUILD-CACHE.md` et
`docs/aurora-parity/tasks/G7-13-TARGET-PROFILES-DISTRIBUTION-COMPLIANCE.md`,
`docs/aurora-parity/tasks/G7-14-NAVIGATION-SPATIAL-VALIDATION.md` et
`docs/aurora-parity/tasks/G7-15-ACTIONABLE-DIAGNOSTICS.md` et
`docs/aurora-parity/tasks/G7-16-ASSET-CONTENT-BROWSER.md` et
`docs/aurora-parity/tasks/G7-17-LOCALIZATION-AUTHORING.md`.

G7 à G12 sont désormais fermés par leurs preuves respectives. La file active est G13, détaillée
dans `docs/aurora-parity/G13_DELIVERY_PLAN.md` : 135 extensions P2/P3/P4, toujours livrées par
tranches de une à cinq feature IDs. **G13-A** ferme `ORF-SHELL-022` avec keymap configurable,
profils Standard/Aurora/Blender, recherche, conflits, commande transactionnelle, persistance locale
et exécution Toolset. **G13-B** (`ORF-CRE-006`, `007`, `009`, `010`, `029`) est désormais fermée
avec identité inclusive, portraits gouvernés, corps modulaire, couleurs, attachments, voix, UI,
preview 3D, runtime et export canonique. **G13-C** (`ORF-CRE-016`, `017`, `018`, `019`, `020`) ferme
la progression data-driven : pistes multiclasses, axes de disposition libres, compétences,
talents, pouvoirs préparés, UI résumée, handle ForgeScript, runtime et `.orpmod`. La prochaine
tranche **G13-D** ferme `ORF-CRE-023`, `024` et `032` autour du butin de créature, avec commande,
UI, persistance, script typé, fallback historique et dépouille runtime interactive. **G13-E** ferme
`ORF-CRE-030`, `047` et `048` avec lore sans mécanique implicite, routines ambiantes et auto-level
déterministe. **G13-F** cible ensuite les cinq événements avancés de créature encore recherchés.

## 7. Graphe de dépendances

```text
G0 Audit et gouvernance
 └─ G1 Narration canonique
     └─ G2 Gameplay RPG data-driven
         ├─ G3 ForgeScript sûr
         │   └─ G4 Sauvegarde et packages
         │       └─ G5 MCP transactionnel
         │           └─ G6 MVP solo
         └───────────────┘

G6 MVP solo
 └─ G7 Creator Core
     └─ G8 RPG Kit
         └─ G9 Network Kit
             └─ G10 Live Director

G7 + G8
 └─ G11 Contenu moderne et distribution
     └─ G12 IA avancée
         └─ G13 Extensions P2/P3/P4
```

G1 peut définir des contrats de conditions/actions sans ForgeScript, mais leur extension scriptée
attend G3. G5 attend des workflows manuels stables. G10 ne contourne jamais G9. G12 attend que chaque
workflow manuel ciblé soit validé.

## 8. Discipline d’exécution

Pour chaque incrément :

1. sélectionner une à cinq feature IDs et leurs critères d’acceptation ;
2. interroger le sous-graphe d’architecture et inspecter code/tests concernés ;
3. lister fichiers autorisés, non-objectifs et risques ;
4. produire un ADR si une frontière ou un format public change ;
5. implémenter domaine, commande et validation avant les adaptateurs ;
6. ajouter persistance, UI, runtime, ForgeScript ou MCP seulement si l’incrément les exige ;
7. exécuter tests unitaires, round-trip, commande/undo, headless et sécurité pertinents ;
8. régénérer et contrôler le graphe après modification de source indexée ;
9. mettre à jour la preuve dans le tracker, puis `STATUS.md` et `TASKS_MVP.md` ;
10. terminer avec une seule prochaine tâche recommandée.

Vérification globale de référence :

```powershell
.\scripts\Verify.ps1
python scripts/architecture_graph.py check
```

Les tests spécialisés s’ajoutent à cette vérification ; ils ne la remplacent pas.

## 9. Règles de passage et preuves

Une feature ne passe à `Tested` que si le tracker référence :

- le document de conception ou l’ADR applicable ;
- le code ou commit correspondant ;
- un test automatisé ou une procédure manuelle reproductible ;
- une capture pour les comportements visuels ;
- un benchmark pour une promesse de performance ;
- une notice de licence et la provenance pour tout asset ou dépendance.

Un gate se ferme lorsque :

- toutes ses P0 ciblées sont `Tested` ou explicitement `Deferred` par décision documentée ;
- les P1 nécessaires à son scénario sont `Implemented` ou `Tested` selon la roadmap source ;
- le scénario de sortie est vert en environnement propre ;
- build, tests, graphe d’architecture et documentation sont cohérents ;
- aucune vulnérabilité critique, perte de données ou frontière IP inconnue ne reste ouverte.

## 10. Risques principaux et réponses

| Risque | Réponse planifiée |
|---|---|
| Les 196 P0 sont interprétées comme un seul MVP. | Les gates définissent le produit livrable ; le tracker conserve la priorité sans tout ouvrir simultanément. |
| Le tracker `Researched` masque le code existant. | G0 impose un audit par preuve avant toute mesure de progression. |
| Le RPG Kit contamine le Core. | ADR de frontière, dépendances dirigées et aucun type Godot ou ruleset RPG dans Domain/Core. |
| Dialogue/quête dépendent trop tôt de ForgeScript. | Conditions/actions déclaratives en G1 ; extensions scriptées seulement après G3. |
| AngelScript ajoute une chaîne native fragile. | Spike borné, versions/licences épinglées, façade C# étroite et tests de quotas avant API large. |
| Les formats évoluent sans migration. | Versions explicites, fixtures anciennes, round-trip et migration obligatoires à chaque changement. |
| `.orpmod` introduit exécution ou extraction hostile. | Validation avant extraction, quotas, namespaces, types réels, hashes et fixtures malveillantes. |
| MCP devient une écriture de fichiers ou un shell distant. | Outils sémantiques, dry-run, permissions, canal local et absence de shell/Python libre. |
| Le réseau duplique les règles métier. | Le Network Kit ajoute autorité et réplication sans changer les commandes de domaine. |
| Le Live Director contourne le serveur. | Toutes les actions GM sont des commandes autoritaires auditées. |
| L’IA ou Blender devient obligatoire. | Providers optionnels, mode hors ligne et runtime sans dépendance aux outils de création. |
| La parité fonctionnelle dérive vers une copie. | Noms, formats, UI, règles et assets originaux ; provenance et contrôle IP au build. |
| La qualité visuelle est jugée uniquement par les tests. | Captures reproductibles, scènes de référence et validation visuelle sur matériel cible. |

## 11. Mesure de progression

Le tableau de bord doit publier au minimum :

- nombre de features par statut, priorité, domaine et gate ;
- P0 sans preuve, P0 bloquées et âge des blocages ;
- couverture des scénarios Builder, Writer, Scripter, Agent, Persistent World et GM ;
- temps de build, temps de chargement, performance runtime et taille des packages ;
- nombre de migrations et fixtures historiques encore supportées ;
- vulnérabilités, dépendances, licences et assets sans provenance ;
- taux de commandes disposant de dry-run, inverse et audit ;
- taux de branches de dialogue/quête simulées ;
- stabilité des tests headless et réseau.

La progression produit est mesurée par gates et scénarios, jamais par le nombre de classes créées ou
par le simple pourcentage des 717 lignes.
