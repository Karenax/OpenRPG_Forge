# P1 R077 — Assistant et composition de créature

## Périmètre

Lot canonique `R077 / FC-CREATURE-P1-01`, limité à cinq fonctions :

- `ORF-CRE-001` — assistant de créature ;
- `ORF-CRE-004` — rôle et défi ;
- `ORF-CRE-005` — race, espèce et sous-type ;
- `ORF-CRE-008` — apparence et modèle ;
- `ORF-CRE-011` — Animation set.

Le lot relie les calculs RPG Kit existants au blueprint canonique, à une interface spécialisée, à la
persistance, à ForgeScript, au MCP et au runtime. Il n’ouvre ni les fonctions P2 intercalées
`ORF-CRE-006`, `007`, `009` et `010`, ni les fonctions R078 (`ORF-CRE-012`, `013`, `015`, `021` et
`022`), ni AV7–AV10.

## Audit source avant modification

Les blocs complets `ORF-CRE-001` (lignes 79–154), `004` (304–377), `005` (379–452), `008`
(607–681) et `011` (832–907) de
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/05_CREATURE.md` ont été relus avant
modification. Les contrats communs imposent document canonique, mutation transactionnelle, absence
optionnelle sûre, diagnostic requis, identité stable, interface visible, ForgeScript typé, MCP
filtré, validation localisée et undo/redo.

| Critère source | État avant R077 | Limite réelle fermée par R077 |
|---|---|---|
| Assistant | service RPG Kit isolé, sans écran ni persistance projet | profil guidé canonique et écran spécialisé, dry-run/apply, règles et aperçu |
| Rôle et défi | `EncounterRole`, menace et tags uniquement dans le modèle RPG Kit | rôle extensible, menace bornée et tags persistés, validés et visibles sans formule D&D imposée |
| Espèce | types et validation unitaire isolés | héritage contrôlé, TraitSet, variantes et squelette requis persistés et adaptés au RPG Kit |
| Apparence | structure isolée sans chemin produit | archetype, mesh, rig, morphs, références de slots, matériaux, palettes, attachments génériques et preset canoniques |
| Animation set | listes isolées sans graphe auteur | graphe à UUID, ordre déterministe, transitions typées, locomotion, combat, emote, additive, retargeting, mini-carte et simulation |
| Surfaces produit | aucune liaison verticale complète | commande unique, round-trip, handle ForgeScript, ressource MCP enrichie, pré-build et nœuds moteur |

## Matrice d’acceptation

| Critère commun immuable | Preuve R077 |
|---|---|
| Domaine canonique avant moteur | `CreatureFoundationProfile.AuthoringComposition` porte le wizard, l’espèce, l’apparence et le graphe sans dépendance moteur |
| Identité et ordre | UUID de profil, graphe, états et transitions ; schéma/provenance ; états et arêtes normalisés dans un ordre stable |
| Validation structurée | diagnostics localisés wizard, espèce, compatibilité rig/squelette, apparence, graphe, overrides, doublons projet et extension requise |
| Interface visible, vide et erreur | formulaire à quatre sections, catalogue, aperçu de modèle, mini-carte, simulation, résumé, états vide/erreur |
| UI et commande uniques | l’écran reconstruit `CreatureFoundationProfile` puis appelle `SetCreatureFoundationHandler` en dry-run/apply ; undo/redo restaure le JSON canonique |
| Persistance exacte | sample R077 et test `ProjectJsonSerializerTests.SerializeAndDeserialize_RoundTripsCreatureFoundation` |
| ForgeScript typé | `CreatureAuthoringCompositionHandle` donne une vue sûre du rôle, de l’espèce, du mesh et des états |
| MCP filtré | `QueryCreatureFoundations` expose rôle, menace, espèce, mesh et AnimationGraph ; `PreviewCreatureFoundation` reste dry-run |
| Adaptateur RPG Kit | `CreatureAuthoringCompositionFoundationService` construit et valide la `CreatureDefinition` existante sans second stockage |
| Runtime moteur | rôle, espèce, modèle et AnimationGraph deviennent des nœuds enfants du personnage 3D déjà visible |
| Optionnel/requis | absence optionnelle ignorée ; absence requise diagnostiquée par code et chemin stables |

## Validation exécutée

- tests ciblés Domain, Application, RPG Kit, ModuleFormat et MCP passants ;
- builds Toolset et Runtime sans avertissement ni erreur ;
- smoke Toolset : `OPENRPG_FORGE_TOOLSET_R077_CREATURE_AUTHORING_PASS` ;
- smoke runtime : `OPENRPG_FORGE_RUNTIME_R077_CREATURE_AUTHORING_PASS` ;
- capture `docs/aurora-parity/evidence/r077-creature-authoring-ui.png`, 1500×930, inspectée : assistant,
  rôle Boss/menace 12, espèce et TraitSet, modèle, mini-carte AnimationGraph, simulation et résumé sont
  lisibles sans chevauchement global ;
- suite consolidée attendue à 1 542 tests .NET et 39 tests Python ;
- registres régénérés : 515 fonctions `Tested`, 201 `Implemented`, 1 `Researched`, avec 131 lots
  clos, 55 à compléter/prouver et 1 à livrer ;
- documentation générée fraîche ; graphe Roslyn déterministe à 6 975 nœuds, 44 346 relations et
  0 diagnostic ;
- portail intégré final requis avant l’ouverture de R078.

## Décision du palier

Les cinq promotions restent bornées à R077. Le marqueur exact `OPENRPG_FORGE_VERIFICATION=PASS`
autorise l’ouverture de R078 uniquement ; aucune fonction P2, AV7–AV10 ou publication n’est ouverte.
