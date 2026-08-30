# forge_project

Définit les documents JSON persistants et moteur-neutres de Forge :

- `forge.scene-proof` version 1 pour la preuve native/WASM de Gate 0 ;
- `forge.terrain` version 1 pour la source de vérité terrain de Visual Valley.

`TerrainDocument` conserve les identités projet/zone, la révision, les coordonnées en mètres Y-up,
la seed, l'étendue, les paramètres logiques de height sampling et les identifiants des couches
matériau. La désérialisation refuse les champs inconnus. La validation exige des étendues, un pas et
une fréquence finis et strictement positifs, une élévation finie, une amplitude finie positive ou
nulle, 1 à 12 octaves, une lacunarité finie supérieure à 1 et une persistance dans `]0, 1]`. Un
document contient entre 1 et 32 couches ; leurs identifiants uniques contiennent 1 à 64 caractères
ASCII minuscules, chiffres ou `_`, en commençant par une lettre. Les noms non vides sont limités à
128 octets. L'étendue doit être un multiple du pas d'échantillonnage dans la tolérance flottante
documentée par le code, et chaque axe est limité à 8 192 intervalles. Le round-trip et l'empreinte JSON
canonique sont testés.

Ces données ne contiennent aucun type Bevy, handle GPU, mesh, texture ou chemin d'asset. Les caches et
liaisons renderer sont produits par les crates d'exécution.
