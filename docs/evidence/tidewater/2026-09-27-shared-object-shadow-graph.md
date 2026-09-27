# Passe d'ombres partagee : village, terrain et feuillages

Les preparations GPU du terrain et des feuillages sont maintenant dans
forge_engine::tidewater_object_shadow::{terrain,plants}. La selection canonique,
les lectures de fichiers et la composition des shaders restent dans les adapters
d'acquisition. Le moteur valide tailles, indices, valeurs finies, textures,
matrices et budgets avant envoi GPU. Aucun nouveau device ni dependance wgpu.

La passe complete a trois cascades est partagee dans le sous-module graph.
register est idempotent ; stage valide les entrees avant remplacement et alloue
une nouvelle cible. Input et Target sont extraits vers le render world. Le cache
GPU est reconstruit pour une nouvelle generation, identifiee par son signal
partage. Ready signifie commandes encodees, pas completion CPU du GPU.
Le prototype utilise cette passe pour opaque, terrain et feuillages.

Le cas desactive conserve une cible 1x1 a trois couches nettoyees a profondeur 1.
La preparation opaque accepte zero cascade seulement en absence de dessins,
comme le cas desactive du prototype avant extraction. Une selection terrain vide
est admise dans la scene mais ne provoque aucun upload de noeuds vides.

Clippy cible moteur et tidewater_object_shadow_reload_probe --no-deps avec
-D warnings : PASS (session 71214). Verification de la disposition du readback
1x1 array contre bevy_render 0.19.1 : lignes alignees a 256 octets, soit 768 octets
pour trois couches ; seuls les quatre octets utiles de chaque couche sont testes.

## Validations executees

Compilation native des deux probes : PASS (session 41305). Le probe de
remplacement termine avec code 0 : adoption aux frames 8 et 10, trois couches
initiales effacees a 1, puis 1572864 echantillons occultes par le triangle
synthetique. Les anciennes cibles restent accessibles ; une matrice NaN est
refusee sans changer la cible courante ni le nombre d'images. Double registration
et refus avant initialisation sont verifies.

Preuve locale : E:/OpenRPG_Forge/.tidewater-integration/late-object-shadow-proof-001.json.
Executable du probe : 95adc56ba16899a1f2da38dc1402c518889cb737a709f2ea00bd8891a6437e30.
Ce cas synthetique valide le cycle de remplacement, pas la fidelite de la carte.

Rejeu natif complet : PASS (session 55380, code 0), depuis preview-scene-bundle-001,
avec vegetation du workspace et building-package-003. Comparaison mesuree de
native-shared-object-scene-001 avec native-shared-opaque-scene-001 :

- PNG : 0 pixel different, ecart maximal 0 ; fichiers identiques.
- Ombres de relief : 2097152 octets identiques.
- Profondeur des trois cascades : 50331648 octets identiques.

SHA-256 PNG : 243ddf9659f95afea4bbd978b2b47006983d052fe3d841958c6f45182a3c4e78.
SHA-256 relief : a8077ba841b789d6acfc7c7bc7da39496920a3b294068eab4b5ad25cb7ee8288.
SHA-256 cascades : 0c38d6ff937fbf5fe6248bbf5a272781804876bbe081222545f0334b611553a1.
Executable scene : 84eb336d1d22cb453ce190e05800e99900a4f3c36201df34f05343007185a669.
Preuve locale : E:/OpenRPG_Forge/.tidewater-integration/shared-object-scene-proof-001.json.

Compilation Studio WASM : PASS, session 83402, 42.20 s :

```text
cargo check -p forge_studio --lib --no-default-features --features web --target wasm32-unknown-unknown --locked --offline -j 1
```

Avertissement de compatibilite future preexistant : proc-macro-error2 2.0.1.
Cette compilation ne constitue pas une execution WebGPU.

## Portee et suite

Le transfert du graphe opaque/terrain/feuillages conserve ce rendu natif fixe.
Les selections de casters et la composition des shaders restent dans les adapters
du prototype. Le branchement sur les commandes de placement et de terrain de
Studio, la mise a jour pendant l'edition, les performances et la qualification
visuelle WebGPU restent ouverts. Le candidat Studio precedemment construit ne
contient pas ce graphe. Aucun verdict de carte editable ni de parite source
complete n'est emis ici.
