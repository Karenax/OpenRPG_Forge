# Tidewater — cartes GPU de caustiques par projection de photons

Référence figée : `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`,
`src/ocean/Caustics.js`, SHA-256
`e1ff6e262c6e87f1e6859833a6e2dc5eb4e1b76da98707cdc4ee29b9b204ab12`.

`tidewater_caustics.rs` décrit les deux couches de photon splatting de la
référence. La couche fine a une cible de 512², la cascade FFT la plus fine,
un niveau de pente 1 et des foyers à 1,2 et 4 m. La couche large a une cible
de 256², la cascade précédente, un niveau de pente 0,5 et des foyers à 3 et
9 m. Chacune étend la grille sur `[-0,35 ; 1,35]²` ; les grilles réellement
rasterisées ont donc `ceil(256 × 1,7) = 436` et `ceil(128 × 1,7) = 218`
cellules par axe. Un index partage les sommets entre les six indices de chaque
cellule.

`tidewater_caustics.wgsl` lit les pentes de la cascade FFT, réfracte la
direction solaire dans la normale des vagues, projette chaque photon sur son
plan focal et calcule la concentration de lumière à partir du rapport des
aires avant/après déformation. Les deux plans sont destinés aux canaux R et G
d'une cible additive, comme dans la référence.

Validation locale : `cargo test -p forge_engine tidewater_caustics --lib
--locked --offline -j 1` : **3/3 PASS**, dont parsing et validation Naga du
shader. `rustfmt --check` du fichier Rust et `git diff --check` : **PASS**.
Clippy strict sur `forge_engine --lib` : **PASS** (avec les exemptions
préexistantes pour `too_many_lines` et `needless_pass_by_value`). Le check
`wasm32-unknown-unknown` de `forge_engine --no-default-features --features web`
est aussi **PASS**. Après le test unitaire, les champs du profil ont été
encapsulés et la justification des conversions exactes a été ajoutée ; le
contrôle Clippy recompilé valide ce dernier état.

`tidewater_caustics/gpu.rs` crée maintenant deux textures `Rgba16Float` avec
mipmaps. Après `OceanComputeSet`, quatre projections additives alimentent les
canaux R/G, puis chaque niveau inférieur est filtré depuis le précédent.
La passe ne se relance que lorsque l'étape FFT publiée change. L'exemple de
promenade partage les pentes FFT et la direction du soleil de sa vraie scène.

Le parcours natif `Walk-Tidewater.ps1 -SkipBuild -Smoke`, avec lecture GPU
activée, a terminé : **PASS**, 8,9937 m et 303 ticks au sol. Son reçu est
`E:/OpenRPG_Forge/.tidewater-integration/caustic-gpu-proof-20260928-004.caustics.json`.
Les deux lectures `RGBA16F` de mip 0 ont les tailles attendues (512² et 256²),
les quatre canaux focaux R/G ont des valeurs non nulles à chaque pixel, et
aucune valeur R/G n'est NaN/Inf. L'analyse des données brutes avec
`scripts/tidewater/inspect_caustics.py` donne :

| Couche/canal | Minimum | Maximum | Moyenne | Écart type |
|---|---:|---:|---:|---:|
| Fine R | 0,0523 | 25,0625 | 0,8068 | 1,2215 |
| Fine G | 0,0107 | 16,9375 | 0,9325 | 0,9391 |
| Large R | 0,2771 | 13,7656 | 0,8441 | 0,5604 |
| Large G | 0,0826 | 35,9063 | 0,8107 | 1,2059 |

Les aperçus PNG normalisés de Fine R et Large G montrent les réseaux lumineux
de focalisation ; ils restent locaux avec les données GPU. La promenade sur
la plage a été capturée et inspectée, sans erreur de pipeline. Le nouveau
raccordement compile aussi pour `wasm32-unknown-unknown` avec la feature `web` ;
Clippy strict de l'exemple passe avec les deux exemptions préexistantes.

`tidewater_caustics/sample.wgsl` porte désormais la recherche dans les deux
cartes : entrée du rayon solaire réfracté, profondeur, interpolation des deux
foyers, flou par mip et empreinte du pixel, dispersion chromatique, mélange
large/fine, variation du détail marin et suppression par la mousse. Le contrat
`CausticSampleParams` valide tailles des deux tuiles, soleil et intensité.
Validation ciblée après cet ajout : **6/6 tests PASS**, dont parsing et
validation Naga du shader d'échantillonnage et le raccordement du terme direct
distinct de l'ambiance. Le format du fichier Rust ciblé passe ; le format
global est interrompu par un module `terrain_ao.rs` absent dans un autre
exemple de la branche de travail.

Le matériau commun du terrain reçoit maintenant les deux cartes de caustiques,
le déplacement et les dérivées FFT. Les scènes sans océan utilisent des
textures neutres avec le terme sous-marin désactivé. Dans la scène native, le
shader du terrain module séparément soleil et ambiance sous l'eau ; il lit
provisoirement les trois cascades FFT grossières par fragment. La passe GPU
complète, la promenade sur plage et le parcours nage/plongée passent sans
erreur de pipeline. `forge_studio` compile pour `wasm32-unknown-unknown`, et
Clippy strict passe sur `forge_engine --lib` et l'exemple natif avec les deux
exemptions préexistantes.

Pour isoler l'effet visible, une copie locale du paquet a fixé la caméra à
`(40 ; -1,4 ; 29,5)` m. L'oracle de sélection du gazon, calibré pour une autre
caméra, a été omis uniquement dans cette copie de diagnostic. Deux exécutions
du même binaire ont changé uniquement l'intensité des caustiques (`0,75` ou
`0`). Les SHA-256 des sorties GPU déplacement FFT mip 0, dérivées FFT mip 0 et
rivage frame 120 sont strictement identiques. La bande supérieure de la capture
(lignes 0–319) est identique pixel à pixel ; dans la zone du fond marin
`x=200..1799, y=500..999`, l'écart absolu moyen est de 2,319 niveaux par canal
et **49,51 %** des pixels changent de plus de deux niveaux sur au moins un
canal. Le reçu reproductible est
`E:/OpenRPG_Forge/.tidewater-integration/caustic-static-ab-20260928-012.json` ;
les PNG activé, désactivé et différence sont conservés à côté. Ils ont été
inspectés visuellement : les différences suivent les motifs lumineux du fond,
pas le ciel.

La scène n'utilise pas encore les deux cartes d'éclairage sous-marin centrées
sur la caméra de la référence ; l'évaluation FFT par fragment n'intègre pas les
vagues de rivage, la mousse ni le facteur rafale/nappe. Les objets immergés et
la passe volumétrique restent hors de ce raccordement. L'effet sur le fond est
prouvé en natif, mais la comparaison source/WebGPU et la parité de TW-008
restent ouvertes.

Le raccordement source utilise `ocean::Targets.images[1]` pour les dérivées
FFT et conserve leur tableau `Rgba16Float` de quatre cascades et neuf niveaux.
Les deux cartes de caustiques restent des cibles 2D distinctes. Leur contenu
est capturé depuis la vraie GPU, sans remplacer les dérivées de l'océan.
