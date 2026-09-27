# Embruns — assets de rendu et référence visuelle appariée

Source intacte : `1438b1abfcaee3267092b75573014f4d9b4a983c`.
Artefacts : `E:/OpenRPG_Forge/.tidewater-integration/`.

`export-spray.mjs` acquiert désormais le vertex shader, la sortie fragment,
les paramètres et les quatre modules du matériau original. Les hooks sont
résolus par `pipelineKey()`, comme au premier dessin : ombre des déferlantes
active, ombre des nuages désactivée. Deux textures RGBA8 sont conservées avec
tous leurs mipmaps GPU : puff 64²/7 niveaux, dots 128²/8 niveaux. L'exporteur
ajoute uniquement l'usage CopySrc à ces deux textures avant leur allocation ;
la première tentative sans cet usage a été rejetée et n'a produit aucun export.
Le prototype de Texture est restauré immédiatement après construction.

Les exports `spray-source-011/012` contiennent 137 fichiers chacun. Les 23
fichiers du rendu des sprites sont identiques ; le calendrier, les autres
données déterministes et les particules actives modulo emplacements restent
inchangés par rapport à `spray-source-010`. Rapport `spray-sprite-assets-001.json`.

## Vent de scène et captures

Le probe numérique historique utilisait les valeurs globales par défaut :
vitesse 7, direction [0,34893736 ; 0,93714607]. La scène fixe utilise vitesse 6,
direction [1 ; 0]. Ce vent global agit sur les trajectoires ; il est distinct
des paramètres du spectre FFT. `--scene-wind` acquiert explicitement les valeurs
de la scène dans `spray-scene-source-001`. Les anciennes preuves numériques
restent valables pour leur configuration propre.

`render-source-vegetation.mjs --water-spray` exécute la boucle originale complète,
dessine le ruban puis les sprites en alpha prémultiplié, sans écriture de
profondeur, et utilise la même copie de profondeur opaque que l'eau. Les
particules, le rivage et le calendrier d'émission sont sauvegardés en sidecars.
Le manifeste identifie désormais la consommation des dépôts GPU, le vent et
les limites de la capture.

Les trois PNG `source-water-spray-001/002/003.png` sont identiques ; le troisième
porte les métadonnées finales. SHA-256 :
`6bdb84e91956ba86cb691046f0ef625417b4cc4939ec8646529392a26ef1dbde`.
La première capture a été inspectée. Les 7 013 particules actives et le rivage
correspondent exactement à l'acquisition de calcul avec vent apparié, modulo
emplacements des particules ; les 120 budgets correspondent également.
L'ajout des embruns et de leur effet sur l'écume modifie 14 532 pixels de plus
de trois niveaux sur au moins un canal, en FHD. Cette mesure n'isole pas les
sprites de leur effet sur l'écume.
Rapport reproductible `spray-sprite-reference-001.json`, produit par
`verify-spray-sprite-reference.py`.

## Simulation native avec le vent apparié

`native-spray-scene-coupled-001/002` utilise le binaire natif déjà qualifié dans
le lot précédent. La répétition charge `spray-scene-inputs-001`, réduit aux 14
entrées nécessaires, sans états de particules. Les deux calculs natifs donnent
les mêmes particules actives modulo emplacements et le même rivage.

Source et natif : 7 013 particules actives, dont 7 000 identités communes ;
13 identités remplacées restent à diagnostiquer. Le dépôt du pas 120 est
exactement identique (282 unités, tous texels concordants). Cela ne prouve pas
l'identité des dépôts antérieurs : l'effet accumulé modifie 5 233 texels d'écume
source contre 5 197 natifs. Écart maximal de position sur identités communes :
269,512 mm ; écart maximal du canal écume : 0,056641.
Rapport `spray-scene-coupled-comparison-001.json`.

Le rendu natif des sprites, leur raccordement aux buffers vivants dans la scène
et l'état de rivage couplé dans les matériaux restent à réaliser. Ces captures
proviennent du moteur source et ne constituent pas une preuve visuelle Rust.

La régression `source-water-spray-breakers-regression-001.png` conserve exactement
le PNG sans embruns et ses quatre sidecars (crêtes, rivage, SeaDetail, Frame),
rapport `spray-sprite-render-regression-001.json`. Vérifications syntaxiques JS
et Python réussies ; le clone source est toujours intact.
