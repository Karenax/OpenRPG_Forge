# Édition commune des placables — validation locale du 2 octobre 2026

Les corrections sont validées sur une copie et un candidat isolés : **596 tests CPU PASS, 4 ignorés, builds natif/WASM PASS et parcours UI commun PASS**. Ces résultats ne constituent ni une qualification de CI ou de gate, ni une approbation visuelle humaine.

## Corrections

- Les sources vérifiées, placements manuels et plantes importées utilisent la même façade de sélection et d'édition : déplacement, rotation, échelle, copie, verrouillage, suppression et remplacement d'asset. Les contrôles ne choisissent plus une politique selon le nom ou les tags de Tidewater.
- FishStand lit sa pose depuis le package vérifié. La pose auteur peut être retouchée sans modifier le snapshot source.
- Une source supprimée conserve son état `Removed`, même après retrait de sa dernière copie, Undo/Redo et sauvegarde/réouverture. Une liste auteur vide représente zéro instance.
- Les plantes sélectionnées par un clic GPU utilisent les outils communs. Le sélecteur reste borné aux objets édités/copiés et à la sélection courante.

Les corrections incluent le binding au pin et au repère raster complet, la conservation des sources sœurs lors des opérations manuelles, la sélection Atelier des instances groupées, le stride aligné du readback et la préservation du vrai squelette des occluders GPU.

Le défaut révélé par la recette UI06 était une demande de population sur un hash spéculatif. Le préparateur résout maintenant toutes les cibles sur la tête acceptée **avant** le gel de provenance ou le remplacement multiple. Il valide ensuite la vue candidate avec la population acceptée, tout en conservant les gardes de révision, de hash, de verrou et de provenance.

Les contrôles ciblés du checkpoint sauvegardé ont aussi révélé un sélecteur qui restait vide après le chargement asynchrone des plantes. La clé de disponibilité tient maintenant compte de leur propriétaire vérifié et réveille les choix même lorsque les placements statiques restent identiques. Ce réveil ne reconstruit pas la géométrie et ne réinitialise pas une proposition en cours.

La recette UI10 a ensuite révélé que l'annulation d'un aperçu sur une saisie invalidait inutilement cette clé et remontait l'inspecteur. La restauration conserve maintenant la clé de la tête acceptée ; un memo de l'aire et du descripteur sélectionnés empêche de perdre le brouillon ou le panneau avancé quand seule la disponibilité des sources change. Une vraie modification de l'objet ou un changement de zone renouvelle toujours l'inspecteur.

Les trois sources, leurs backups exacts, leurs différences et leur correspondance avec le candidat/UI finaux sont liés dans la [preuve de disponibilité et conservation du brouillon](E:/OpenRPG_Forge/.editor-placeables-20261001/source-readiness-draft-proof.json).

Contrats : [ADR-0020](../ADR/ADR-0020-common-source-object-editing.md), [architecture commune](../architecture/COMMON_PLACEABLE_EDITING.md).

## Tests CPU exécutés

| Suite | PASS | Ignorés | Preuve |
| --- | ---: | ---: | --- |
| `forge_project` | 212 | 1 | [Domaine](E:/OpenRPG_Forge/.editor-placeables-20261001/domain-tests-after-draft-merge-proof.json) |
| `forge_world` | 263 | 0 | [Domaine](E:/OpenRPG_Forge/.editor-placeables-20261001/domain-tests-after-draft-merge-proof.json) |
| `forge_assets` | 43 | 3 | [Assets](E:/OpenRPG_Forge/.editor-placeables-20261001/domain-tests-after-draft-merge-proof.json) |
| Binding des sources | 7 | 0 | [Binding](E:/OpenRPG_Forge/.editor-placeables-20261001/source-binding-test-proof.json) |
| Compatibilité sources/manuels, rejouée après le correctif | 5 | 0 | [Relance](E:/OpenRPG_Forge/.editor-placeables-20261001/manual-source-replay-after-planning-after-lint-proof.json) |
| Atelier : drafts, protocole, transformations et sélection | 21 | 0 | [Atelier](E:/OpenRPG_Forge/.editor-placeables-20261001/atelier-selection-test-proof.json) |
| Readback RGBA8 | 5 | 0 | [Readback](E:/OpenRPG_Forge/.editor-placeables-20261001/gpu-readback-test-proof.json) |
| Préparation sur la tête acceptée | 6 | 0 | [Planning](E:/OpenRPG_Forge/.editor-placeables-20261001/accepted-planning-test-after-lint-proof.json) |
| Moteur : objets authorés, collisions, plantes et occluders | 34 | 0 | [Moteur](E:/OpenRPG_Forge/.editor-placeables-20261001/engine-final-tests-after-draft-merge-proof.json) |
| **Total distinct** | **596** | **4** | Les relances remplacent leurs résultats précédents. |

Les six nouveaux tests exécutent le préparateur de production : première retouche de plante, remplacement de deux plantes avec gel du nouveau pin statique, lot verrouillé refusé atomiquement, population périmée, insertion manuelle après retouche et vue candidate invalide. Les quatre tests ignorés sont opt-in et ne sont pas comptés comme PASS.

Les suites domaine et moteur ont été rejouées après la dernière fusion FishStand. Les inputs et les dépendances compilées des 44 tests auxiliaires restent byte-identiques ; leur [preuve de portée actualisée](E:/OpenRPG_Forge/.editor-placeables-20261001/helper-cpu-scope-after-draft-merge-proof.json) conserve le total distinct de 596 sans additionner les relances.

## Candidat et parcours UI

Les [builds finaux](E:/OpenRPG_Forge/.editor-placeables-20261001/candidate/build-proof.json) sont terminés à `2026-10-02T10:21:34.7705334Z`. Leurs 867 inputs sont épinglés et vérifiés ; le [build WASM](E:/OpenRPG_Forge/.editor-placeables-20261001/candidate/wasm-build-proof.json) concorde.

| Artefact | SHA-256 |
| --- | --- |
| Shell natif | `dd9697cc99a877cefd41d4e22b58286b30ae46a530e67de6d4bde2917eb444d3` |
| Frontend WASM | `4d150150fa5f4c1c7ff8513b9e25465d9726694c706fad107e25a3c508fe965a` |
| Harness UI | `775d109459cf66a745f25b452f4e8723e767ebf529c7a8a8eedcff584166a4f7` |

La [recette finale common-ui-final-11](E:/OpenRPG_Forge/.editor-placeables-20261001/common-ui-final-11/common-placeable-studio-ledger.json), SHA-256 `f458b96b89f689817cbe8cb65d1e0c01b9723d6986fe6bd4054bab48e9172796`, passe les six cas : boardwalk, pier, plaza, FishStand, placement manuel et plante sélectionnée physiquement par le GPU. Elle vérifie les champs communs, translation, rotation, échelle XYZ, copie d'un original verrouillé vers une copie déverrouillée, verrouillage/déverrouillage, aperçu/apply, suppression de l'original et de sa dernière copie, Undo/Redo, deux sauvegardes et leurs réouvertures.

Les six suppressions finales sont persistantes. Les sources sœurs restent présentes ; les pins, blocs d'instances et métadonnées de prototype sont conservés. Le candidat, son frontend et la fixture d'origine restent inchangés. Le processus et le listener CDP propres à l'essai sont fermés. Captures : [objets retouchés](E:/OpenRPG_Forge/.editor-placeables-20261001/common-ui-final-11/common-edited.png), [après suppression et réouverture](E:/OpenRPG_Forge/.editor-placeables-20261001/common-ui-final-11/common-removed-reopened.png).

La vérification finale exige d'abord tous les choix non supprimés de l'état initial et zéro requête en attente, puis l'absence des douze identités retirées et leurs tombstones dans le checkpoint natif. Une liste vide pendant le chargement ne peut donc pas suffire. La dernière capture précède la fin de projection 3D ; elle ne qualifie pas le rendu final des objets absents.

Les échecs précédents et leurs preuves restent conservés. Le candidat ayant révélé le défaut du planner est [archivé byte-exact](E:/OpenRPG_Forge/.editor-placeables-20261001/candidate-plant-planning-before-proof.json), avec 1 578 fichiers. Les corrections du harness possèdent leurs [preuves de sélection physique](E:/OpenRPG_Forge/.editor-placeables-20261001/common-plant-repick-fix-proof.json) et de [traitement strict des listes vides](E:/OpenRPG_Forge/.editor-placeables-20261001/common-empty-options-fix-proof.json) ; elles n'affaiblissent pas les gardes du produit.

La [barrière d'ouverture réelle](E:/OpenRPG_Forge/.editor-placeables-20261001/common-actual-open-fix-proof.json) exige une nouvelle requête unique pour le bon UUID, sa réponse native résolue et une session publique ouverte. La bibliothèque est vérifiée par son contrôle public après chaque ouverture. L'audit est réinstallé lorsque le document est effectivement renouvelé ; aucun renouvellement n'est imposé au scénario. Les trois ouvertures et leurs identités de document sont consignées dans la recette finale.

## Préservation du travail concurrent

Le périmètre comprend 61 fichiers de correction et ce rapport. Les 29 fichiers repris uniquement comme contexte restent `integrate:false`. Le travail du chat « Évaluer modèles Unreal pour OpenRPG », ses assets, ses processus, son port et ses caches partagés sont préservés. Les évolutions concurrentes des fichiers partagés ont été conservées par des fusions bornées et des baselines révisées ; le Gameplay Studio relâché reste hors ownership. La [dernière fusion Playtest](E:/OpenRPG_Forge/.editor-placeables-20261001/playtest-resume-final-merge-proof.json) conserve le repositionnement du joueur avant reprise ; son compagnon moteur est repris comme contexte et exclu de l’intégration. Une évolution ultérieure de présentation dans `forge_engine/gameplay/projection.rs` reste dans le live, hors intégration et hors qualification du candidat gelé.

La réintégration utilise un contrôle frais des empreintes avant les écritures, des backups exacts et un remplacement atomique par fichier. Ses résultats sont consignés séparément dans la [preuve d'intégration](E:/OpenRPG_Forge/.editor-placeables-20261001/integration-proof.json) et la [vérification finale](E:/OpenRPG_Forge/.editor-placeables-20261001/integration-verification-proof.json). Le vérificateur compare manifeste, plan, copie, live et backups, puis exécute `git diff --check` sur le périmètre. Aucun commit ni push ne fait partie de cette correction.

Une déclaration additive de `tidewater_boat`, conditionnée par `physics-rapier`, est aussi arrivée dans le `forge_engine/lib.rs` live après le gel. Ses déclarations existantes restent identiques. Cette [évolution concurrente](E:/OpenRPG_Forge/.editor-placeables-20261001/unowned-boat-lib-boundary-proof.json) est préservée, exclue de l'intégration et de la qualification du candidat.

## Limites

La recette est bornée aux six cas et adapters couverts. Elle ne mesure pas la performance de la population maximale, le parcours joué, la parité visuelle complète ou tous les autres domaines générés. Les captures automatiques restent distinctes d'une approbation visuelle humaine. La CI distante et les gates ne sont pas qualifiées par cette passe.

Le build de production WASM passe ; la compilation distincte des tests moteur WASM conserve son [échec documenté](E:/OpenRPG_Forge/.editor-placeables-20261001/wasm-engine-tests-check-proof.json) sur un `Instant` dans un fichier moteur non possédé. Les diagnostics Clippy/warnings préexistants hors périmètre ne sont pas présentés comme un contrôle global PASS.

Le [Clippy ciblé du préparateur](E:/OpenRPG_Forge/.editor-placeables-20261001/accepted-planning-clippy-metadata-after-lint-proof.json) applique les lints du workspace aux sources et tests concernés : compilation metadata PASS, zéro avertissement. Il ne remplace pas un contrôle Clippy global.

Le [contrôle rustfmt du périmètre](E:/OpenRPG_Forge/.editor-placeables-20261001/owned-rustfmt-check-proof.json) passe sur les 58 fichiers Rust concernés, sans retoucher les modules enfants ou les sources. La vérification d'intégration fournit le `git diff --check` borné au périmètre.
