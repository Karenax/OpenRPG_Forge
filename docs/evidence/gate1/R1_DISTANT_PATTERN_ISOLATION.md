# Gate 1 — Isolation des trames distantes

**Complément : contribution du terrain isolée** dans l'essai « Surfaces terrain identiques »
ci-dessous. Les résultats négatifs végétaux restent valides, mais la cause n'est plus
entièrement indéterminée. Aucun correctif de production n'est encore qualifié.

6 septembre 2026, Rust, arbre existant préservé, aucune publication.

## Résultat

Les grandes trames visibles sur la vue héroïque actuelle persistent dans trois essais
indépendants. Aucun correctif retenu : les désactivations temporaires ont été retirées.
Ces preuves écartent une explication limitée aux silhouettes HLOD procédurales, aux
ombres des arbres gouvernés ou au seul fondu de visibilité du shader végétal. Elles
ne prouvent pas encore la cause exacte et ne qualifient pas l'image.

## Protocole et observations

Référence : série `r1-current-fixed-views-20260906`, caméra `hero_reference_match`,
WorldSketch rev3, kit/forêt candidats rev2, High, 1920×1080, RX 7800 XT/Vulkan.
Chaque essai modifie uniquement le code indiqué, compile le runtime, puis collecte
clair/pluie avec `Capture-WorldSketch.py --camera hero_reference_match --quality high`
et le même `--forest-catalog`. Les manifestes contiennent toutes les commandes et entrées.
Chaque variante est restaurée avant la suivante. Aucune donnée authorée modifiée.

| Essai temporaire | Modification exacte | Observation de la capture claire |
| --- | --- | --- |
| `r1-hlod-isolation-20260906` | Valeur `-16` à la place de `hlod_placement_dither(root_world.xyz)` dans le shader végétal | Grandes trames toujours visibles |
| `r1-tree-shadow-isolation-20260906` | `NotShadowCaster` sur tous les LOD des arbres gouvernés dans `prototype_projection.rs` | Ombres proches retirées, grandes trames persistantes |
| `r1-all-vegetation-dither-isolation-20260906` | `out.visibility_range_dither = 0` avant le retour du vertex végétal, sous le define correspondant, dans les deux passes | Végétation affectée par la suppression des transitions, grandes trames persistantes |

Les trois PNG clairs ont été ouverts et inspectés. Les trois PNG pluie sont collectés
et contrôlés techniquement, pas inspectés visuellement dans ce diagnostic. Six lancements
terminés code 0, sans événement ERROR, avec caméras/hashes/chargement kit/forêt et budgets
High contrôlés par le collecteur. Les indicateurs de chargement ne détectent pas les
désactivations expérimentales : son statut `capture_checks_passed_review_pending` signifie
uniquement le succès de ces contrôles, **jamais l'acceptabilité de ces variantes**.
Ces six images ne complètent pas les douze vues normalisées demandées.

Racine des preuves : `E:/OpenRPG_Forge/.gate1-evidence-runtime/`.

| Essai | SHA-256 manifeste | SHA-256 binaire expérimental |
| --- | --- | --- |
| HLOD | `37928f3f3f5e10b0bd4061fc8694dc49e3f5aac0b2d19608d627c82a4bcd348f` | `15decd11c83a60112f3cba8245274f7d761f8bfd05e29d9afb8d8448a759703b` |
| Ombres | `b1a9de63a4ed8580459cfba7570ed8a5da0ecafc6746feae98845e1ca0c6df3c` | `c1782b35730a3c1aa82a1389c86aaaabb365147d51a5fe281373390158a468f6` |
| Fondu végétal | `0b5d11992cb7493f3b742b125a821fa42a9887ec3aa8498c19da364985e6129c` | `c7893cce085e31e9204d43569a72cc76e048831ca80dc9a564da23d71d9c16d4` |

## Suite bornée

Restauration vérifiée : les deux expressions d'origine sont rétablies, aucune annotation
`Temporary diagnostic` ne subsiste dans les fichiers concernés. Build natif normal PASS,
SHA-256 `337888ca3a8bf0e2e7db8134f314c280f0f6ed85a5f76c9ca8fe918ce27c0ba9`.
Il diffère du binaire de la première série : aucune reproductibilité binaire bit à bit
n'est revendiquée. Rejeu clair/pluie terminé code 0 avec contrôles PASS sous
`r1-distant-pattern-restored-20260906`, manifeste
`0c557ac1307ea7949f03523ebb92ead33e4b3e7364694b028723051c5473f409`.
Ces deux captures de restauration sont contrôlées techniquement, pas une nouvelle revue
artistique. `git diff --check` PASS. Aucun runtime ou build de ce diagnostic ne reste actif.

Ne pas adopter ces désactivations ni retoucher arbitrairement les distances LOD. Isoler
ensuite les autres contributions (terrain et passes associées) avant toute correction.
La suppression du fondu végétal n'est pas une preuve que tout mécanisme de tramage du
renderer est innocent : les autres matériaux ont leurs propres chemins de rendu.
Aucun benchmark, suite Rust complète ou gain de performance revendiqué. Gate 1 reste
ouverte, CI NOT RUN, aucune nouvelle gate ou copie d'asset Cadle.

## Surfaces terrain identiques — essai discriminant

Même jour, même vue héroïque et entrées que précédemment. Inspection du code : la végétation
gouvernée et procédurale force Forward ; la piste d'un shader végétal différé ignoré ne
correspond pas à ces matériaux. Le terrain possède trois surfaces dérivées distinctes,
avec bandes de fondu superposées, sur des reliefs non plans.

Une seule modification expérimentale dans `terrain_quality.rs` : remplacer la liste
`[1, settings.lod1_sample_stride, settings.lod2_sample_stride]` par `[1, 1, 1]`.
Les distances, transitions, shaders, matériaux, végétation, ombres, sources et colliders
sont conservés. Les trois surfaces deviennent identiques, sans simplification réelle.

**Les grandes trames disparaissent dans les deux PNG clair/pluie ouverts et inspectés.**
La contribution des géométries différentes de terrain pendant leur fondu est ainsi isolée ;
la preuve ne distingue pas encore chaque contribution couleur/profondeur/ombre du terrain.
Le motif ne peut plus être attribué uniquement à la végétation. Les bandes de la rivière,
la composition prototypale et les reflets pluie trop forts persistent.

Preuves : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-terrain-identical-lods-20260906`.
Manifeste SHA-256 : `947ad24a0b01bad1c0c71f2721feb7602e6e7f08432e54bcaf409565e4d298e8`.
Binaire expérimental : `f77086db27a587ef10c5aa44ce20f988b2188e08d3e6474af55bdebfd9eb2bfa`.
Deux sorties 0, sans ERROR, contrôles caméra/entrées/kit/forêt PASS. Le compteur alloué
devient 192 meshes, 209 088 sommets, 393 216 triangles ; ce n'est pas le nombre rendu.
Le `projected_tier=high` du diagnostic ne prouve pas le respect des strides High, explicitement
neutralisés pour cet essai. Les mesures courtes ne qualifient ni performance ni gain.

La modification temporaire est retirée. **Ne pas promouvoir les trois copies pleine résolution**
comme solution : elles annuleraient la simplification demandée. Le prochain correctif doit
traiter la transition entre surfaces (par exemple une transition géométrique cohérente),
avec continuité inter-chunks, passes profondeur/couleur/ombres concordantes, préservation
du terrain canonique et des ancrages, puis qualification en mouvement natif/Web. Cette
direction est prévue, pas implémentée ; aucune désactivation LOD conservée.

Build natif après restauration PASS ; binaire courant
`16b063ae6ce9a3e1dda4aa340b35cb82ffeed6cab4e5def30a0da3947826c71c`.
Expression des strides vérifiée restaurée et `git diff --check` PASS. Aucun nouveau
rejeu visuel de ce binaire restauré ni test Rust revendiqué. Tous les processus de cet
essai sont terminés ; les huit captures de référence restent les preuves du binaire antérieur.

## Préparation des cibles de transition — implémentation CPU

`TerrainLodTopology::project_morph_target(source, target)` calcule désormais les attributs
de la surface cible aux sommets du maillage courant : hauteur, normale interpolée non
normalisée et poids matériaux. Interpolation barycentrique f64 sur la topologie validée,
conversion f32 bornée aux valeurs finies des sommets ; indices, XZ et UV inchangés.
Les sommets communs, notamment tout le périmètre, restent copiés bit à bit. La source
est validée par le chemin existant, sans mutation et sans dépendance moteur.

Deux nouveaux tests passent : bosse fine projetée sur un plan parent (pas de plus proche
voisin), identité de cible identique, source intacte, entrée NaN refusée, conservation
exacte des sommets partagés pour les six strides 1/2/4/8/16/32. Les 85 tests `forge_world`
et Clippy strict de cette crate passent. Les erreurs initiales Clippy sur conversions et
comparaisons flottantes ont été corrigées avant ce verdict.

**Préparation seulement, pas correctif visuel livré.** Aucun appel dans le renderer et
aucun shader modifié. Une interpolation correcte aux sommets ne suffit pas : les éventails
de bord peuvent traverser des arêtes parentes, donc les surfaces entre sommets doivent
encore être rendues compatibles. Ne pas supprimer le dither sur cette seule preuve.
La recherche des triangles se fait à la préparation, pas chaque frame ; aucun gain de
performance revendiqué. Pas de nouvelle capture nécessaire à ce calcul non raccordé.

Validation élargie après intégration : `cargo test --workspace --locked -q` termine
code 0, **445 PASS / 1 timing ignoré** ; Clippy workspace toutes cibles/toutes features
avec `-D warnings`, `cargo fmt --all -- --check` et `git diff --check` PASS. Pas de
build WASM ou qualification GPU ajouté dans ce lot. CI distante toujours NOT RUN.

## Raffinement commun des triangles

`project_transition` construit deux maillages partageant indices et coordonnées XZ, par
intersection des triangles de départ et d'arrivée. Chaque triangle produit appartient
ainsi à une face de chacun des deux LOD ; les attributs de chaque extrémité sont interpolés
dans sa face originale. Les éventails ne sont plus supposés imbriqués. La source est
validée avant préparation ; les allocations sont bornées à 65 536 sommets par extrémité.
Pas de dépendance, de document persistant ou de changement de collider.

Régression synthétique pas 1 → 4, relief irrégulier : centres de tous les triangles comparés
aux deux surfaces originales (tolérance hauteur 0,2 mm), aire de 4 096 m² conservée à
0,002 m², winding ascendant, XZ identiques bit à bit, rejeu identique et source intacte.
Le découpage ajoute bien des triangles là où les éventails croisent les faces fines.
Ce test passe ; il ne prouve pas encore toutes les paires de LOD, les jonctions en mouvement
ou la précision GPU. L'intersection s'effectue en préparation, jamais par frame.

**Implémenté côté données seulement.** Renderer et shaders restent inchangés, aucun nouveau
résultat visuel revendiqué. Les meshes intermédiaires ont un coût de préparation et de
stockage supplémentaire ; ils ne doivent pas remplacer arbitrairement tous les LOD stables.
L'adapter doit encore choisir les phases de transition et préserver les extrémités exactes.

Validation de ce raffinement : **446 tests workspace PASS / 1 timing ignoré** ; Clippy
workspace toutes cibles/toutes features avec warnings refusés et `git diff --check` PASS.
Formatage appliqué. Les premiers diagnostics Clippy (style des tests/import) sont corrigés.
Pas de build du runtime, de capture ou de qualification Web supplémentaire pour ce code
non raccordé. Aucun processus de validation restant actif.

## Raccord natif du morphing Bevy

`VisualValleyTerrainQualityPlugin` prépare maintenant les deux raffinements communs
LOD0→1 et LOD1→2, plus le LOD2 stable. Les deltas positions/normales utilisent les
`MorphAttributes` et `MeshMorphWeights::Value` de Bevy 0.19.1. Le vertex standard de Bevy
traite le morphing dans le rendu et sa prépasse ; aucun shader Forge supplémentaire.
Une seule caméra active fournit les poids scalaires bornés 0–1. Les plages de visibilité
sont exclusives : le morph atteint sa cible avant le changement de topologie. Le dernier
LOD conserve son fondu terminal de culling. Pas de reconstruction des sommets par frame.

Sources et collisions restent canoniques, ressources de rendu réutilisées après changements
de qualité ; meshes préparés avant application. Deux captures héroïques natives 1080p
clair/pluie produites et inspectées : **grandes trames supprimées sur cette vue**. Deux sorties
0, aucun ERROR, entrées et chargement kit/forêt contrôlés. Dossier :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-terrain-morph-native-20260906`.
Manifeste `c1f51422d6a72c82b0b391c6ac261907d70a932265df1d62037f02c73ea188c6` ;
binaire `28efd5654a99d9dd79a79d767bbd9d1742b5815604c92cd78caeb171057ba6d5`.

Coût explicite High : 192 meshes, **620 608 sommets / 220 416 triangles alloués**,
plus les deltas de morphing. Les sommets du raffinement ne sont pas encore partagés.
Ce ne sont ni les triangles rendus ni une mesure VRAM ; aucun gain de FPS revendiqué.
La préparation initiale est plus coûteuse et les chronométrages de capture ne sont pas
un benchmark. Les tests de décomptes ont été adaptés aux payloads de transition, sans
retirer les assertions de source/colliders/handles/bornes/refus de projection incomplète.

Réserves : les poids de matériaux/couleurs ne sont pas morphés par le mécanisme standard ;
des changements de teinte aux bascules restent possibles. Les berges canoniques ne suivent
pas encore la déformation du terrain et montrent des intersections/bords irréguliers.
Mouvement, jonctions sur tous les profils et Web/Tauri non qualifiés. Caméra absente ou
ambiguë : les poids ne sont pas actualisés, sans prétendre à une qualification multi-vue.
La correction des grandes trames ne clôt ni R1-040, ni R1-065, ni Gate 1.

Validation après raccord : **447 tests workspace PASS / 1 timing ignoré**, dont cycle
des poids 0→0,5→1→0,5→0 sans reconstruction, présence des deltas pour les deux transitions,
conservation des sources/handles et refus atomique d'un binding manquant. Clippy strict
workspace PASS, check Studio WASM/web PASS (avertissement de compatibilité future existant
`proc-macro-error2`), formatage et diff check PASS. Check WASM ne vaut pas essai navigateur.
Les échecs initiaux des deux anciens tests de décompte, puis Clippy sur la longueur du test
d'intégration, sont résolus ; aucun contrôle de sécurité supprimé. Tous les lancements
de ce lot sont terminés. CI NOT RUN, aucun commit/push ni nouvelle gate.

## Partage exact des sommets de transition

Le raffinement compacte désormais les sommets identiques aux **deux extrémités** : clé
bit à bit positions, normales, UV et poids matériaux, sans quantification ni fusion de
coutures différentes. Les indices sont remappés, les capacités des tableaux réduites.
Ordre et attributs de chaque coin des triangles conservés ; un test conserve volontairement
deux sommets dont seule la normale cible diffère, vérifie tous les coins et l'idempotence.
Le test de surfaces compare désormais les triangles indexés, pas des sommets supposés uniques.

Rejeu même vallée/caméra/High/kit/forêt, clair et pluie, deux sorties 0 et contrôles PASS.
Les deux PNG ont été inspectés : grandes trames toujours absentes, mêmes réserves de berges.

| Allocation terrain dérivé High | Avant partage | Après partage |
| --- | ---: | ---: |
| Meshes | 192 | 192 |
| Sommets | 620 608 | 122 841 |
| Triangles | 220 416 | 220 416 |

Réduction de **80,2 % des sommets stockés**, pas de la VRAM totale ni du temps de frame.
Les comptages proviennent des métriques réelles de chaque lancement. Les statistiques
de tests somment maintenant les meshes réels : le nombre de doublons exacts dépend des
attributs de chaque chunk, multiplier le premier par 64 n'est plus un oracle valide.
Les vérifications de géométrie/source/handles/morphing restent actives. Aucun gain FPS
revendiqué ; préparation, animation, Web et budget sur GPU cible restent à qualifier.

Preuves : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-terrain-morph-compacted-20260906`.
Manifeste `f36bc5e13f940ab55e9522ec73904f640758305efe2372e5c58fa3c21970abbe` ;
binaire `0b8ab2c15927b81c28777dd22d2ac6bc4a2bdb935dd497aa2d62289ecda862bb`.
Le comparateur avant est le dossier `r1-terrain-morph-native-20260906` ci-dessus.

Validation après partage : **448 tests workspace PASS / 1 timing ignoré** ; Clippy strict
workspace, check Studio WASM/web et diff check PASS. Formatage appliqué. L'erreur initiale
Clippy sur la position du module de tests a été corrigée en le plaçant après le code.
Le check Web conserve l'avertissement connu de compatibilité future `proc-macro-error2`.
Pas de benchmark FPS ou de session navigateur revendiqué. Aucun processus restant actif,
aucune publication ; Gate 1 et CI NOT RUN inchangées.

## Contraintes locales sous les berges — qualification partielle

Le renderer conserve la surface canonique dans les chunks intersectant les boîtes XZ
des triangles de berges. Détection géométrique, sans coordonnées de vallée codées en dur ;
aucune mutation des documents ou des colliders. Les autres chunks conservent le morphing.
Cette règle conservative reste raccordée au marqueur de berges du prototype ; elle ne
qualifie pas encore un éditeur générique de rivières ni des hiérarchies transformées.

Build natif PASS ; `cargo test -p forge_engine --lib bank_ --locked` : **5 PASS**,
dont deux nouveaux tests : triangle translaté, absence de berge, index invalide/NaN,
ajout puis retrait d'une berge, reconstruction d'un seul chunk, handles conservés,
trois surfaces pleine résolution sans morph sous la berge. Frontières entre chunks,
mutation du mesh en place, récupération après erreur et suite globale restent à vérifier.

Deux captures héroïques High clair/pluie terminées (sorties 0), contrôles du script PASS,
PNG inspectés : les intersections dentelées des berges ne sont plus visibles sur cette vue.
Le bord sombre près du pont, les bandes de l'eau et le caractère prototype restent visibles.
**20/64 chunks contraints**, 192 meshes, **149 789 sommets / 274 416 triangles** dérivés,
contre 122 841 / 220 416 avant contrainte. Surcoût explicite, aucun gain FPS revendiqué.
Terrain hash inchangé : `18bd933d37f06a6a08a34b19938a78cb8f6fe8d9667b34bd2dcd6355956fd208`.

Preuves : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-terrain-bank-constraints-20260906`.
Manifeste `bece3e53646a0679f191d556a1cc3b30b9f6c5367fd375e1ba066c0f20321390` ;
binaire `3f0836ff2f22e5c9f0231e1d9017f096b52f2e5f9246bf05945e7144c2ceed60`.
Les anciens résultats workspace/Clippy/WASM ci-dessus précèdent cette modification.
Mouvement, autres vues/profils, Web et validation humaine restent ouverts ; Gate 1 ouverte.

### Reprise après édition de berge

Un troisième test de contrainte modifie le mesh existant pour franchir la frontière X
entre deux chunks : le compteur passe de 1 à 2 sans changer les handles de terrain.
Il injecte ensuite un index hors limites, vérifie le diagnostic d'échec, puis répare le
même mesh. Correction associée : le chemin de cache identique rétablit désormais les
statistiques valides après réparation ; auparavant le diagnostic pouvait rester périmé.
La réparation ne reconstruit aucun chunk et conserve le hash terrain source.
`cargo test -p forge_engine --lib bank_ --locked` : **6 PASS**, sortie 0, 6,69 s de tests
(compilation 34,55 s). Cette correction de diagnostic ne change pas la géométrie des
captures précédentes ; leur binaire reste celui épinglé ci-dessus, pas un nouveau build.

Régression : `cargo test --workspace --all-features --locked` **451 PASS / 1 timing ignoré**,
sortie 0 (compilation 3 min 34 s, tests moteur 45,56 s). Clippy a signalé la longueur de
`refresh_terrain_lods` : préparation des payloads et validation des bindings extraites en
fonctions dédiées, sans désactivation du lint. Après ce refactoring : Clippy strict workspace
all-targets/all-features PASS ; tests moteur all-features **186 PASS / 1 ignoré**, sortie 0,
40,82 s ; check Studio WASM/web PASS avec le seul avertissement de compatibilité future
préexistant `proc-macro-error2`. Formatage et diff check PASS. Ce check n'est pas une
qualification visuelle navigateur. Tous les processus de validation de ce lot sont terminés.
CI NOT RUN ; aucune publication Git ; Gate 1 ouverte.
