# ADR-0044 — Graphe commun des ombres source à cascades

Date : 25 octobre 2026. Statut : réalisation Gate 7 en cours.

## Décision

`forge_engine::source_shadows::casters` possède l'exécution existante du graphe
à trois cascades : upload opaque, terrain CDLOD et plantes masquées, états de
génération, poses retenues pendant l'extraction, cadence et reprise des couches
non encodées. Les récepteurs communs sont écrits dans la même soumission que
les profondeurs correspondantes. Les identités de buffers, ABI et formats GPU
restent ceux des consommateurs. Les seuls noms diagnostiques deviennent neutres.

Les familles de casters masqués et de prototypes indexés conservent leurs GPU
résidents et entrées transitoires dans les sous-modules `masked` et `textured`.
Les producteurs fournissent géométrie, shader admis et poses, et leur retrait
enlève la ressource extraite. Le moteur ne charge aucun paquet de carte.
Le codec de profil conserve le classement des attributs source, les quatre
prototypes scannés et les gardes d'acquisition. Le GPU vérifie les indices par
rapport à l'inventaire effectivement admis (1 à 256 prototypes, budget 256 Mio),
plutôt que d'imposer au moteur le nombre de prototypes d'un paquet particulier.

La préparation des cascades et le parcours de hiérarchie affine rejoignent le
moteur. La collecte d'éligibilité des instances/bâtiments de la carte reste
dans son intégration. Les consommateurs appellent explicitement les propriétaires
réels ; aucune façade de réexport ne maintient le moteur dans la carte.

Le marqueur commun `OffscreenSceneCamera` est extrait dans le monde de rendu.
Le graphe ignore ces vues pour choisir la caméra interactive et sa politique
de jitter ; il ne dépend plus d'une caméra de capture nommée par une carte.

## Preuves exigées et limites

Rejouer les tests de cadence, reprise, rétention, validation d'entrées et
retrait des producteurs. Vérifier sur GPU l'installation tardive, le graphe
désactivé, le remplacement et le refus atomique, puis le terrain, les plantes
et objets opaques du profil source réel. Conserver les fonctions shaders et
les ressources originales ; une lecture de profondeur prouve un encodage et
son résultat, sans qualification complète du matériau final ou de Studio.

Cette extraction ne raccorde pas implicitement chaque hôte de module à tous
les effets de la scène de référence. L'emploi des mêmes capacités, leur
configuration canonique, le parcours auteur, les autres formats/profils et la
parité à conditions identiques restent à vérifier. Aucun critère général de
Gate 7 n'est clos par le seul déplacement du graphe.
