# P1 R078 — Règles et inventaire de créature

## Périmètre

Lot canonique `R078 / FC-CREATURE-P1-02`, limité à cinq fonctions :

- `ORF-CRE-012` — état initial et pose ;
- `ORF-CRE-013` — attributs ;
- `ORF-CRE-015` — défenses et sauvegardes ;
- `ORF-CRE-021` — capacités spéciales ;
- `ORF-CRE-022` — inventaire de créature.

Le lot étend `CreatureFoundationProfile` par un unique profil canonique optionnel, consommé par le
Toolset, ForgeScript, le MCP, l’adaptateur RPG Kit et le runtime. Il réutilise la commande
transactionnelle de fondation et les blueprints d’objet existants. Il n’ouvre ni R079
(`ORF-CRE-025`, `026`, `028`, `031`, `034`), ni une fonction P2, ni AV7–AV10, ni une publication Git.

## Audit source avant modification

Les blocs complets `ORF-CRE-012` (lignes 914–984), `013` (989–1059), `015` (1139–1209), `021`
(1589–1659) et `022` (1664–1734) de
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/05_CREATURE.md` ont été relus avant
modification. Chaque bloc exige un document canonique, une mutation transactionnelle, un état
optionnel sûr ou requis diagnostiqué, UUID/schéma/provenance/transform/références/overrides, une UI
visible avec états vide et erreur, ForgeScript typé, MCP filtré et dry-run, validation localisée,
persistance exacte et undo/redo du document.

| Critère source | État avant R078 | Limite réelle fermée par R078 |
|---|---|---|
| État initial et pose | valeur RPG Kit isolée, sans chemin auteur ni stockage projet | UUID et profil canonique pour les six états, boucle, pose et invisibilité ; écran, aperçu et nœud runtime |
| Attributs | calcul `AttributeSet` isolé dans RPG Kit | ruleset, bases, modifiers sourcés et formules `Sum`/`Product`/`Ratio` persistés, ordonnés, validés et évalués sans code arbitraire |
| Défenses et sauvegardes | modèle et mitigation testés sans surface produit | évitement, armure, résistances, saves, immunités et mitigation canoniques, aperçus et reconstruits dans le runtime |
| Capacités spéciales | instances RPG Kit sans identité persistante ni édition | UUID, niveau, charges, cooldown, conditions, tags IA et script, avec validation, écran, handle et nœuds runtime |
| Inventaire | conteneurs RPG Kit sans liens aux blueprints du projet ni UI | conteneurs et entrées à UUID, BlueprintId d’objet résolu, slots, poids/volume, ownership, équipement et persistance |
| Surfaces transversales | aucune liaison verticale de ces cinq composants | commande unique dry-run/apply, undo/redo, round-trip, handle, ressource MCP, validation pré-build, adaptateur RPG Kit et cinq groupes de nœuds moteur |

## Matrice d’acceptation

| Critère commun immuable | Preuve R078 |
|---|---|
| Domaine canonique avant moteur | `CreatureRulesCompositionProfile` et son validateur restent dans `Forge.Domain`, sans type moteur |
| Identité, ordre et références | UUID du profil, des cinq composants, modifiers, formules, capacités, conteneurs et entrées ; ordre stable ; `BlueprintId` d’objet résolu par handler et validation projet |
| Validation structurée | diagnostics localisés pour état/pose, attributs et cycles, défenses, capacités, inventaire, doublons, références et extension requise |
| Interface visible, vide et erreur | écran à cinq sections, catalogue, résumé, aperçu dessiné, barres calculées, bouclier, simulation de dégâts et grille d’inventaire |
| UI et commande uniques | l’écran reconstruit la fondation puis appelle `SetCreatureFoundationHandler` en dry-run/apply ; undo/redo restaure le JSON canonique |
| Persistance exacte | sample R078 et round-trip étendu de `ProjectJsonSerializerTests.SerializeAndDeserialize_RoundTripsCreatureFoundation` |
| ForgeScript typé | `CreatureRulesCompositionHandle` expose état, attributs évalués, défenses, capacités et inventaire sans fichiers ni nœuds moteur |
| MCP filtré | `QueryCreatureFoundations` expose le résumé R078 ; `PreviewCreatureFoundation` conserve le dry-run et le diff sémantique |
| Adaptateur RPG Kit | `CreatureRulesCompositionFoundationService` adapte le profil canonique aux calculs existants, sans second stockage |
| Runtime moteur | état/pose, attributs, défenses/saves, capacités et inventaire sont reconstruits en cinq groupes de nœuds enfants |
| Optionnel/requis | absence optionnelle ignorée ; absence requise diagnostiquée par code et chemin stables |

## Validation exécutée

- 4 tests Domain R078 passants ; 2 tests d’adaptateur RPG Kit passants ; 5 tests du handler de
  fondation passants, dont les deux nouveaux chemins R078 ;
- round-trip ModuleFormat, échantillon Crypte des Cendres et ressource MCP ciblés passants ;
- builds Toolset, Runtime et MCP sans avertissement ni erreur ;
- smoke Toolset : `OPENRPG_FORGE_TOOLSET_R078_CREATURE_RULES_PASS` ;
- smoke runtime : `OPENRPG_FORGE_RUNTIME_R078_CREATURE_RULES_PASS` ;
- capture `docs/aurora-parity/evidence/r078-creature-rules-ui.png`, 1500×930, inspectée après une
  correction de finition : catalogue séparé, cinq sections, état assis, attributs 16/8/7,
  défenses, capacité et inventaire lisibles sans chevauchement majeur ;
- suite consolidée attendue à 1 550 tests .NET et 39 tests Python ;
- régénération tracker, backlog, documentation et graphe, puis portail intégré final requis avant R079.

## Décision du palier

Les cinq promotions restent bornées à R078. Seul le marqueur exact
`OPENRPG_FORGE_VERIFICATION=PASS` autorise l’ouverture de R079 ; aucune fonction P2, AV7–AV10,
commit ou publication n’est ouverte.
