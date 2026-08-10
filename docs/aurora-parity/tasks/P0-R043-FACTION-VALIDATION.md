# P0 R043 — validation factionnelle

Lot stable : `R043` / `FC-FACTION-P0-01` — une fonction, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-FAC-014` — validation factionnelle.
- Source immuable relue intégralement : section correspondante de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/06_FACTION.md`.

## Audit avant fermeture

| Critère source | Déjà présent | Limite réelle fermée par R043 |
|---|---|---|
| cycles d'héritage | détection déterministe dans `FactionValidator` | assertion dédiée et preuve Toolset intégrée |
| factions parentes absentes | diagnostic `RPG_FACTION_PARENT_ORPHAN` | cas explicite avec chemin stable jusqu'à l'UUID parent |
| seuils de disposition chevauchants | diagnostic `RPG_FACTION_BANDS_OVERLAP` | vérification conjointe dans un catalogue invalide réel |
| membres sans définition | diagnostic `RPG_FACTION_MEMBER_ORPHAN` | vérification conjointe et rejet avant mutation |
| auteur et build | validation du catalogue par la commande et le projet | smoke visible, diagnostics stables, round-trip et portail pré-build |

## Matrice critère → preuve

| Critère source | Implémentation | Preuve reproductible |
|---|---|---|
| cycle | `FactionValidator.HasCycle` | `ValidatorDetectsCycleOrphansMembersAndOverlappingBands` |
| parent orphelin | validation des `ParentIds` | même test, code et chemin UUID exacts |
| bandes chevauchantes | comparaison par priorité et intervalle | même test et smoke Toolset R043 |
| membre orphelin | résolution sur le catalogue canonique | même test et smoke Toolset R043 |
| diagnostic auteur | `SetFactionCatalogHandler` et `FactionAdvancedWindow` | marqueur `OPENRPG_FORGE_TOOLSET_R043_FACTION_VALIDATION_PASS` |
| persistance | sérialiseur projet canonique | round-trip inclus dans le smoke faction |

## Non-objectifs

- modifier les règles avancées de réputation, de témoins ou de diplomatie déjà fermées ;
- ajouter P1/P2, AV7–AV10, commit ou push.

## Validation finale

- test ciblé RPG Kit vert et Toolset compilé sans avertissement ;
- smoke Toolset R043 avec interface visible et quatre diagnostics spécifiques ;
- tracker/backlog, documentation et graphe régénérés, puis portail intégré avant R044.
