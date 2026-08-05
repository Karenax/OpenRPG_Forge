# G11-K — Cinématiques, ancres caméra et interpolations

## Périmètre

- `ORF-PROJ-018` — flux de démarrage avec fallback et skip accessible ;
- `ORF-GAME-019` — séquences, pistes, signaux, branches, synchronisation et récupération ;
- `ORF-WPT-013` — ancres caméra avec cadrage, lentille, regard, durée et cadre sûr ;
- `ORF-EE-001` — overrides de transform persistants, hiérarchiques et réplicables ;
- `ORF-EE-027` — tween de transform, matériau et état avec easing, autorité et annulation.

## Résultat livré

- profils cinématiques canoniques v1 imbriqués dans les politiques module et zone : flux de
  démarrage, séquences, pistes, branches, Camera Anchors, overrides et tweens portent UUID stable,
  schéma, provenance, autorité, réplication et comportement de sauvegarde ;
- validateur global pour identités propriétaires, références caméra/entité/dialogue, chemins sûrs,
  limites X/Z, parents absents et cycles de hiérarchie ;
- lecteur déterministe qui avance une timeline bornée, interpole position/rotation/échelle, applique
  cinq courbes d'easing, traverse au plus huit branches par avance, émet audio/signaux et refuse une
  séquence synchronisée sans autorité ;
- runtime de tween pour transform, paramètre de matériau et état, avec annulation, autorité et état
  final explicite ;
- studio **Séquences & caméras** accessible depuis Zones : démarrage, durée, skip, synchronisation,
  audio, signal, déplacement d'entité, Camera Anchor, FOV, look-at, cadre sûr et timeline ;
- runtime joueur qui consomme la séquence d'ouverture de la Crypte, prend puis restaure caméra et
  contrôle, applique les transforms, joue les ressources locales disponibles et accepte Échap ;
- API MCP gouvernée en lecture, dry-run/diff, application persistée et réversion ;
- formats zone `1.6.0`, projet `0.6.13` et workspace `1.7.0`, avec migration stable des profils
  absents et migration effective des trois workspaces fournis.

## Preuves

- `CinematicProfileTests` couvre références, limites et cycles hiérarchiques ;
- `SequencePlayerTests` couvre interpolation, quaternion opposé, signaux, skip, autorité, overrides,
  matériau, état et annulation ;
- `SetCinematicProfilesHandlerTests` couvre dry-run, transaction atomique, undo et redo ;
- `CinematicPersistenceTests` couvre round-trip complet et migration pré-cinématique ;
- `ForgeProjectAgentServiceTests` couvre le plan MCP, sa persistance et sa réversion ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_G11K_CINEMATIC_PASS` ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_G11K_CINEMATIC_PASS startup=sequence` sur la vraie Crypte.

## Limites explicites

- le studio édite actuellement une séquence principale, une caméra, un mouvement d'entité, une
  piste audio et un signal. Le modèle accepte plusieurs pistes, branches, overrides et tweens ; leur
  éditeur de collection avancé reste une amélioration d'ergonomie ;
- les vidéos et scènes de démarrage sont chargées lorsqu'elles sont importées comme ressources
  locales. Un fichier externe non importé n'est jamais présenté comme jouable ; le fallback
  canonique est alors sélectionné ;
- la synchronisation multijoueur impose l'autorité au lecteur, mais le transport des horloges de
  timeline entre clients reste la responsabilité de Network Beta ;
- le runtime applique réellement les transforms et la caméra. L'application visuelle d'un tween de
  paramètre matériau ou d'état est exposée par l'API pure et sera reliée aux systèmes concernés
  lorsqu'un contenu l'emploiera ;
- cette fermeture ne constitue pas encore la carte 3D artistique finale : la curation et la
  composition d'assets réels restent un objectif transversal après les lots fonctionnels.

## Critère de passage

G11-K est fermé lorsque tests ciblés, smokes Toolset/runtime, migrations, tracker, graphe et
`scripts/Verify.ps1` sont verts sur le même état Git.
