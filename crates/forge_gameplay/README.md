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

Le profil est explicite dans `RulesProfile` ; il s'agit d'un petit profil de boucle
de jeu, pas de la totalité d'un manuel RPG. Il peut décrire :

- un et un seul acteur `ActorRole::Player`, avec des acteurs `Friendly` et `Enemy` ;
- des `Pickup`, `Door` et `Prop`, avec masse, poussabilité, lançabilité, destruction,
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
`forge.game-save` version 1, limitée à 4 MiB. Elle conserve les identifiants de
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

## Tests et commandes

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
  acteurs hors politique et contacts inventés refusés après recalcul du hash.

Commandes ciblées :

```text
cargo test -p forge_gameplay
cargo test -p forge_gameplay --test document_contract
cargo test -p forge_gameplay --test action_contract
cargo test -p forge_gameplay --test environment_contract
cargo test -p forge_gameplay --test first_loop
cargo test -p forge_gameplay --test save_integrity
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
