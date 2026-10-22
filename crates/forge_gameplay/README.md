# forge_gameplay

`forge_gameplay` porte les définitions RPG authorées et l'état d'une session de jeu,
dans une crate indépendante de Bevy, de Rapier et de l'interface utilisateur. Elle
valide les références, applique les actions autorisées et enregistre leurs
conséquences bornées. Elle ne constitue pas un moteur de géométrie ou de physique.

## Source, état et adaptateur

- `GameplayDocument` est la source authorée : module, zone, `RulesProfile`, joueur,
  acteurs, objets, objets d'inventaire, places, dialogues et quêtes. Le document est
  validé par `GameplayDocument::validate()` et reste séparé de l'état de partie.
- `GameState` est l'état mutable de `GameSession` : positions observées, vitalité,
  inventaires, portes et objets, progression de quête, curseurs de dialogue, journal
  des commandes, contacts, dernier cadre physique et quotas du tick.
- `source_hash` lie une session et sa sauvegarde au document fourni à
  `GameSession::new` ou `GameSession::resume`. La sauvegarde ne contient pas un
  deuxième document authoré : le document courant est fourni séparément lors de la
  reprise.
- L'adaptateur runtime (`crates/forge_engine/src/gameplay`) traduit les entités
  Bevy/Rapier et les observations d'eau en `PhysicsFrame`, `ActionObservation` et
  observations de position. La crate consomme cette preuve de contact, de ligne de
  vue et de géométrie ; elle ne la fabrique pas. Le runtime limite aussi la file de
  commandes au joueur et refuse une sauvegarde tant que des commandes ou effets sont
  encore en attente.

## Profil minimal

Gate 5 est ouverte depuis le 19 octobre. `RulesProfile::rule_fields()` fournit
les sept définitions numériques, unités, bornes, valeurs et aides utilisées par
la validation et le nouvel inspecteur. `with_rule_text()` valide un candidat
complet ; les calculs continuent à lire les champs canoniques du même profil.
Le contrat `forge.rules.minimal.v1` ne prétend pas implémenter tout le manuel.

Une `CreatorConfiguration` optionnelle déclare les capacités requises et les
surfaces visibles de ce document de module/zone. Les capacités futures sont
refusées par le runtime et masquer une surface conserve ses données. L'activation
produit un document gameplay v2 ; les documents v1 conservent leurs octets et
restent lisibles. Les surfaces seules ne changent pas le hash fonctionnel d'une
partie ; changer les règles le change. Voir [ADR-0027](../../docs/ADR/ADR-0027-creator-capability-contract.md).
La cohérence de configuration entre les zones d'un même module et la qualification
des deux modules complets restent à réaliser avant R5-P02.

Le profil est explicite dans `RulesProfile` ; il s'agit d'un petit profil de boucle
de jeu, pas de la totalité d'un manuel RPG. Il peut décrire :

- un et un seul acteur `ActorRole::Player`, avec des acteurs `Friendly` et `Enemy` ;
- des `Pickup`, `Door`, `Prop` et `Container`, avec masse, poussabilité, lançabilité, destruction,
  supports, feu et dégâts de chaleur ;
- un objet d'inventaire, une quête à accepter et rendre, une récompense optionnelle,
  ainsi que des dialogues dont les effets sont `AcceptQuest`, `CompleteQuest` et
  `SetDoor` ;
- des places et leurs liens authorés. Ces liens sont des données de monde ; ils ne
  fournissent pas à eux seuls un solveur de navigation.

`RulesProfile` borne les distances d'interaction et d'attaque, les dégâts et coûts de
stamina, la stamina maximale, le nombre de reçus de commandes et les événements par
tick. Les références croisées, positions, identités et relations de support sont
également contrôlées avant l'activation d'un document.

## Boucle d'API

Une intégration typique suit cette séquence :

```rust
let mut session = GameSession::new(document, source_hash)?;

session.observe_position(player, position)?;
session.observe_object(object, position, in_motion)?;
session.advance_frame(&physics_frame)?;
session.execute(&request, action_observation)?;

let bytes = session.save_bytes()?;
let resumed = GameSession::resume(
    document,
    new_or_existing_source_hash,
    &bytes,
    allow_compatible_edits,
)?;
```

`GameRequest` porte un `command_id`, un `expected_sequence`, l'acteur et un
`GameAction` (`Pickup`, `ChooseDialogue`, `Attack`, `SetDoor`, `Push`, `Throw`,
`Ignite` ou `Extinguish`). Une répétition exacte d'une commande déjà reçue renvoie
le même `GameReceipt` sans appliquer une seconde fois ses récompenses ou ses
conséquences. Une observation contradictoire, une séquence périmée, une cible hors
portée ou une preuve de contact manquante est refusée atomiquement.

`PhysicsFrame` doit utiliser le `source_hash` courant et un tick strictement
progressif. Il transporte les acteurs et objets observés, les contacts identifiés
par `CommandId`, ainsi que les observations de chaleur et de danger. Les contacts au
repos ne doivent pas être transformés par l'adaptateur en nouveaux impacts répétés ;
les épisodes persistants ne sont échantillonnés que lorsqu'une vitesse de fermeture
positive le justifie. `ActionObservation` fournit la ligne de vue, le contact
confirmé et le contact avec l'eau pour l'action en cours.

Les refus sont exposés par `GameplayError` (`InvalidDocument`, `MissingReference`,
`ActionRefused`, `IncompatibleSave` ou `Codec`) afin que l'adaptateur puisse les
présenter sans déplacer la règle dans l'UI.

## Sauvegarde, stabilité et retouches

`GameSession::save_bytes()` produit une enveloppe JSON canonique
`forge.game-save`, limitée à 4 MiB : version 1 sans conteneur, version 2 lorsque
le document contient des conteneurs. Elle conserve les identifiants de
module et de zone, le hash source, le hash fonctionnel et le hash canonique de
`GameState`. Avant écriture, l'état, le journal d'actions, les contacts, les flags
d'objet, les références et les quotas sont revalidés.

Une sauvegarde exige une projection physique stable : aucun objet ne peut être
`in_motion`, et une session marquée `requires_physics_revalidation` reste bloquée.
L'adaptateur Bevy ajoute la condition pratique que sa file de commandes et ses effets
soient vides. Après `GameSession::resume`, la sauvegarde doit donc représenter une
session stable et observable, pas une transition en cours.

`GameplayDocument::functional_hash()` permet d'identifier les retouches explicitement
compatibles, notamment le texte authoré et la position d'une porte. Il ne rend pas
ces retouches automatiques : `resume(..., allow_compatible_edits = true)` est requis,
et toute suppression de référence, modification d'identité, de règle ou de
sémantique de quête reste incompatible. Après une reprise compatible, l'ancien
`last_frame` est invalidé et une nouvelle `advance_frame` issue du nouveau
`source_hash` doit réussir avant toute action ou nouvelle sauvegarde.

## Blueprints de personnages — Creator v2

`CreatorConfiguration.blueprints` contient les définitions versionnées et les
liens explicites vers les acteurs. Les valeurs héritables sont le rôle, la
vitalité maximale, la protection, la capacité d'inventaire et la fuite du danger.
Le nom, la position, la patrouille et le dialogue appartiennent à chaque acteur.
Le runtime consomme les mêmes `ActorDefinition` matérialisés et validés ; aucun
simulateur ou comportement propre au nom d'une créature n'est ajouté.

`with_actor_blueprint` exige la version suivante exacte et met à jour les liens.
`with_actor_blueprint_binding` déclare les overrides ;
`with_actor_blueprint_overrides` peut rétablir un champ hérité avec `None`.
Les retouches de statistiques dans le Studio capturent les seuls champs changés.
Toutes ces méthodes retournent un candidat validé sans modifier la source.
Les identités et les références restent contrôlées, y compris dans les panneaux
masqués. Un acteur dont les valeurs contredisent son lien est refusé.

`without_actor_blueprint_binding` détache l'acteur en conservant ses valeurs
résolues ; une révision ultérieure ne le touche plus. Un blueprint encore lié
ne peut pas être supprimé. Le Studio soumet ces opérations au preview/apply et à
l'historique habituels. La réouverture conserve les définitions et les overrides.
Un changement purement éditorial de blueprint conserve le hash fonctionnel ;
une modification des statistiques matérialisées le change. La reprise d'une
sauvegarde après retouche reste explicite et soumise à la revalidation physique.

Les tests de contrat de cette extension sont dans `creator_contract.rs` ; leur
succès ne constitue pas à lui seul une qualification du parcours Studio.

## Catalogue et conteneurs

`with_item_definition` crée ou retouche un item sans créer de ramassage physique.
Les références gardent leurs identifiants ; réduire une pile sous une quantité
initiale existante ou supprimer un item encore référencé fait refuser le candidat.
`without_item_definition` valide toutes les références avant de retourner le résultat.

`with_container_definition` convertit un prop ou révise un conteneur, sans remplacer
son identité, sa position ni ses propriétés physiques. La capacité compte les unités
d'items, pas un poids. Un filtre vide accepte tout item déclaré. Une clé optionnelle
verrouille le conteneur au début de la partie ; ouvrir ou verrouiller nécessite de
posséder cette clé, sans la consommer. Le contenu initial appartient au document ;
les transferts de la partie courante appartiennent à `ObjectState.container`.

`TransferItem` et `SetContainerLocked` passent par `execute` et ses reçus idempotents.
Les vérifications portent sur les deux inventaires, la pile, la capacité, le filtre,
la clé, la disponibilité physique, la portée, la ligne de vue et les quotas. Aucun
refus ne modifie partiellement les inventaires ou les journaux. `container_actions`,
`container_action_label` et `container_summary` fournissent les options et unités
communes aux présentations native et Web ; l'exécution revalide la commande.

La reprise vérifie aussi un registre dérivé des contenus initiaux et des reçus :
ramassages uniques, transferts, consommations/récompenses de quête et verrouillages.
Des quantités falsifiées, même avec un hash d'état recalculé, sont refusées. Les
événements d'items ne peuvent pas être attribués à une frame physique.
`potential_item_quantities` fournit un indice structurel pour l'auteur, en tenant
compte des clés et conteneurs ; il ne prouve pas la navigation ou la solvabilité.

Voir [ADR-0028](../../docs/ADR/ADR-0028-container-inventory-contract.md).

## Tests et commandes

`ModuleCreatorProfile` décrit les règles, capacités et surfaces communes à un
module. Son codec canonique borné permet un épinglage par hash ; `apply_to`
matérialise les valeurs sans copier les bibliothèques de blueprints locales et
`validate_document` vérifie la correspondance. Le workspace publie la définition
et toutes les références de zones par une commande commune ; le raccordement
Studio et le transport des paquets restent en cours de réalisation Gate 5.
Voir [ADR-0029](../../docs/ADR/ADR-0029-module-creator-authority.md).

Les tests d'intégration couvrent les contrats locaux de la crate :

- `document_contract.rs` : schéma, identités, références, bornes et sérialisation du
  document ;
- `action_contract.rs` : actions atomiques, ligne de vue, distance, séquence et
  répétition idempotente ;
- `environment_contract.rs` : frames, contacts, supports, impacts, feu, danger,
  eau, quotas et réévaluation après retouche ;
- `first_loop.rs` : boucle dialogue/quête/ramassage/rendu, récompense unique,
  inventaire plein, reprise et retouches compatibles ;
- `save_integrity.rs` : états et journaux falsifiés, flags physiques impossibles,
  acteurs hors politique et contacts inventés refusés après recalcul du hash ;
- `container_contract.rs` : catalogue, limites, transferts, clés, filtres, reprise,
  registre de conservation et falsification d'inventaires ou de reçus ;
- `creator_contract.rs` : profils, surfaces, capacités réellement disponibles,
  blueprints, matérialisation commune aux zones et compatibilité des sauvegardes ;
- `narrative_contract.rs` : variables typées, solutions alternatives, échec,
  journal, coûts atomiques et récompense unique après reprise. Ce lot Gate 5
  est en validation ; ses oracles ne constituent pas une qualification UI.

`NarrativeDefinition` est facultatif et exige Creator v2. Les conditions peuvent
comparer des variables et combiner jusqu'à 128 nœuds sur huit niveaux. Les flux
de quête choisissent explicitement une solution et ses consommations, avec une
récompense commune ; les quêtes sans flux conservent leur récupération historique.
`GameSession::quest_journal()` projette le journal depuis l'état canonique.
`with_narrative_definition`, `with_dialogue_definition`, `with_quest_flow` et
`with_quest_flow_and_legacy_solution` ne produisent que des candidats validés,
à promouvoir par la commande normale du projet. Voir
[ADR-0030](../../docs/ADR/ADR-0030-bounded-narrative-transactions.md).

`FactionsDefinition` décrit les groupes locaux, appartenances et relations
initiales orientées. `with_factions_definition` produit un candidat validé par
les commandes habituelles. `execute_with_witnesses` reçoit les observations
physiques de l'adaptateur, conserve les témoins admis dans le reçu et applique
leurs variations dans la transaction d'attaque. `personal_relation` et
`Condition::Relation` consultent la même projection personnelle, avec repli sur
la relation initiale. Les bornes sont 32 groupes, 16 témoins par action et
1 024 paires personnelles modifiées ; la portée est contrôlée par la fonction
partagée `positions_in_perception_range`.

La sauvegarde passe à v4 seulement en présence de factions. Les documents et
reçus historiques omettent les nouveaux champs facultatifs. La reconstruction
contrôle la cohérence des relations et réactions avec les observations locales
retenues et leur contact d'origine. La preuve de visibilité demande le parcours
du moteur physique. Les dix contrats `factions_contract.rs` passent dans le
premier candidat (83 tests gameplay au total) ; le collecteur moteur, les
formulaires et la persistance par le host ont ensuite passé les contrôles
WASM/projet ciblés. Le scénario moteur
`local_faction_witnesses_follow_real_occlusion_and_preserve_their_distinct_causes`
passe le 20 octobre à 04:04 UTC : obstacle Rapier réel, retrait de l'obstacle,
causes distinctes et sauvegarde/reprise des relations. Cette preuve headless ne
qualifie pas les formulaires ni le parcours graphique.
Voir [ADR-0031](../../docs/ADR/ADR-0031-local-faction-witnesses.md).

### Horloge, capacités, effets et rencontres — lot D

`SimulationDefinition` version 1 configure une horloge de simulation, des
capacités acquises par acteur, des protections temporaires, des échéances et des
rencontres. Les candidats `with_simulation_definition`, `with_ability_definition`,
`with_timed_effect_definition`, `with_calendar_event_definition` et
`with_encounter_definition` sont validés avant la promotion habituelle.

`PhysicsFrame::simulation_step_ns` fournit un pas entier, positif et au plus
égal à une seconde. Il est requis quand la simulation est configurée ; le
format historique conserve son absence. Les échéances utilisent les minutes
écoulées depuis le départ ; l'événement initial à zéro est consommé au premier
pas. Le calendrier ne consulte pas l'heure système.

`UseAbility` applique les soins et protections contre les attaques directes,
puis consomme les coûts en endurance et objets dans la même transaction. Un
refus annule le candidat complet. Le cumul est refusé, remplacé ou limité selon
la définition ; les exclusions s'appliquent dans les deux sens. L'expiration
et `CancelEffect` conservent les causes individuelles. Un retry d'un reçu
réussi conserve son résultat et ses coûts déjà appliqués.

Les participants d'une rencontre restent absents avant leur échéance.
L'activation utilise leurs identités existantes ; le réarmement après défaite
requiert un événement récurrent et son délai. Il restaure les caractéristiques
de présence et combat tout en conservant les inventaires et conséquences.
La sauvegarde v5, utilisée seulement pour une simulation configurée, vérifie
horloge, effets, coûts et activations par leurs reçus retenus.

Le candidat du 20 octobre à 04:42 UTC passe 102 tests de cette crate, dont onze
contrats temporels. Les contrôles stricts gameplay/projet passent sur le candidat
précédent. Le raccordement moteur, les formulaires et leurs parcours restent à
qualifier séparément ; ces résultats ne ferment pas Gate 5.

## Scripts bornés et cinématiques

Les champs facultatifs `scripting` et `cinematics` requièrent les capacités
Creator correspondantes. Leur présence sélectionne la sauvegarde v6 ; leur
absence conserve les versions historiques. `RunScript` interprète un arbre JSON
typé sur un clone de partie : variables narratives, portes par l'API métier et
messages. Les permissions de lecture, écriture et porte sont distinctes.
Le langage limite chaque source à 128 instructions et huit niveaux, chaque
répétition à 16 itérations et chaque appel à 256 étapes. Les quotas d'événements
de partie restent applicables. Un refus conserve toute la partie antérieure et
fournit l'identité du script, l'instruction, la cause et la correction proposée.
Il n'expose ni accès fichier, ni réseau, ni processus, ni évaluation de code hôte.

`StartCinematic`, `SkipCinematic` et `InterruptCinematic` conservent une cause
stable. Le curseur est dérivé de l'horloge de simulation, sans second compteur
persistant. Une séquence contient au plus 32 plans, pour dix minutes au total ;
texte, locuteur et caméra utilisent des types neutres. Le joueur retrouve les
actions ordinaires à la fin ou à l'interruption. Les replays de sauvegarde
vérifient les sorties de scripts, l'état des portes et la progression des séquences,
y compris après recalcul frauduleux de l'empreinte d'état.

Le candidat du 20 octobre à 05:02 UTC passe onze scénarios de session E,
incluant budgets/refus atomiques, Save/Resume/retry, interruption et conservation
d'une quête active puis de sa récompense unique. Cette preuve ne qualifie pas
encore la caméra Bevy, le stockage du Studio ou les parcours graphiques.

Commandes ciblées :

```text
cargo test -p forge_gameplay
cargo test -p forge_gameplay --test document_contract
cargo test -p forge_gameplay --test action_contract
cargo test -p forge_gameplay --test environment_contract
cargo test -p forge_gameplay --test first_loop
cargo test -p forge_gameplay --test save_integrity
cargo test -p forge_gameplay --test factions_contract
cargo test -p forge_gameplay --test temporal_contract
cargo test -p forge_gameplay --test presentation
```

Ces commandes vérifient les contrats de la crate. Elles ne remplacent ni le
playtest de Tidewater, ni la validation de l'adaptateur moteur, ni une qualification
visuelle ou une clôture de Gate.

## Limites

Ce README décrit l'API de raccordement et le profil minimal ; ce n'est pas un manuel
d'auteur complet. La crate ne fournit pas de collision ou de navigation, ne choisit
pas les cibles de ligne de vue, ne rend pas les dialogues, ne pilote pas Bevy/Rapier
et ne remplace pas les règles de présentation ou de contrôle du runtime. Les
positions, contacts, vitesses, eau et lignes de vue reçus dans les observations sont
des faits produits par l'adaptateur ; les conséquences sont ensuite validées et
bornées par `forge_gameplay`.
