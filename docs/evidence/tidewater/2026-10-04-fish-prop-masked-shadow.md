# Préparation des ombres masquées FishProps — 4 octobre 2026

Le nouveau module moteur `tidewater_fish_props/shadow.rs` assemble les entrées
vertex/fragment de profondeur autour du corps `material/main-shadow.wgsl` du
paquet vérifié. Le masque original des nageoires, ses rayons et son dithering
temporel restent verbatim ; les helpers `fishPartOf` et
`interleavedGradientNoise` proviennent du même paquet. Aucun fondu de LOD ni
substitution par une silhouette opaque n'est ajouté.

Le flux profondeur est position posée float3 puis `FISH_DATA` float4, stride
28 octets, indices u32. Le packer refuse attributs manquants, valeurs non finies,
comptes incohérents, géométrie non triangulaire et indices hors limites. Les
uniforms prévus sont clip-from-world, world-from-local et temps avec padding.

`fish-shadow-native-validation-20261004-v4/result.json` : compilation, 19 tests
et Clippy passent, entrées vérifiées intactes. Le test Naga valide les deux entrées
du shader et sa fermeture. Les 78 meshes source réservés aux ombres sont packés ;
chaque valeur de nageoire conserve ses bits exacts. Retirer l'attribut de masque
fait échouer le packer. Les premiers essais v1/v3 gardent leurs erreurs Clippy
de documentation/style, corrigées avant le reçu v4.

Cette preuve est CPU/validation shader, sans pipeline GPU exécuté. Le module
n'est pas encore raccordé au graphe des trois cascades ni aux mises à jour de
pose/time. La couverture raster, la sélection de casters à 22 m, l'absence
d'ombre lointaine et les captures en scène restent à vérifier. Aucune ombre
poisson visible ni parité complète n'est déclarée.

## Raccord GPU et graphe — snapshot suivant

Le module `shadow/gpu.rs` prépare le pipeline profondeur avec fragment masqué,
stride 28, indices u32, culling arrière, Depth32Float LessEqual et biais source
2 / 1,5. Ces réglages sont vérifiés dans le clone figé : Material.side par
défaut `front`, MeshRenderer.frontFace `ccw`, Shadows.depthCompare `less-equal`
et ses deux biais. La géométrie et les bindgroups restent résidents ; les poses,
flags de distance et uniforms de temps/cascades sont mis à jour séparément.

`ResidentFishProps::shadow_inventory` expose un inventaire stable avec flags de
cull source, tandis que `shadow_instances` conserve sa sélection précédente.
La scène publie une ressource dérivée `shadow::Frame` pour le target courant,
seulement après propagation des transformations. Aucun champ Bevy n'est
ajouté au document. Les buffers CPU sont partagés par Arc ; un inventaire vide
retire la ressource et les poissons lointains ne déclenchent pas de redraw par
leur seul temps. Suppression/ajout/changement de pose invalident les trois
cascades pour éliminer une ancienne silhouette.

Le graphe commun extrait cette ressource optionnelle, prépare le batch pour la
génération correspondante et dessine les casters activés avec leur fragment
masqué dans les trois passes existantes. Le receiver et les autres casters
conservent leur mécanisme commun ; aucune API Input existante n'est remplacée.

Validation ciblée `fish-shadow-native-validation-20261004-v6` : compilation,
19 tests et Clippy passent, sans GPU exécuté ; ce harnais ne compile pas le
graphe ou l'adapter Studio. Leur contrôle complet est donc réalisé ensuite par
Cargo WASM Studio sur `studio-fish-scene-check-20261004-v6` : code 0, sources
figées intactes (session 34470). Snapshot v5 intermédiaire conservé, non contrôlé.
Build frontend v6 lancé ensuite (5792). Raccord écrit et contrôlé par Cargo,
mais aucun dessin de profondeur ou coverage pixel n'est encore prouvé.

## Frontend v6 assemblé

La construction frontend v6 (5792) termine code 0, sources figées intactes.
WASM brut : 125 778 784 octets, SHA256
`153f89ff54cbd75de501c352c8adebc906983834b1a23f9f9484d46d7976a5ad`.
Les bindings wasm-bindgen 0.2.127 et les ressources sont assemblés dans le
frontend v6. Le worker de navigation est réutilisé seulement après vérification
de toutes ses entrées source, Cargo.lock compris, et de ses artefacts.
`frontend-ready.json` inventorie 2 771 fichiers ; aucun navigateur exécuté et
aucune parité visuelle ne sont déclarés par ce reçu.

Un frontend indépendant sans forge-kit est préparé pour la validation du
constructeur ; le frontend embarqué conserve le kit complet vérifié. La
construction native v6 est lancée (56928), sans verdict runtime anticipé.
La construction native v6 termine code 0 (56928), sources et frontend intacts.
EXE SHA256 : `17d03a459c35abf55e9efc264336384de499db15b730969ab4b646b223dac87c`.
La copie de test indépendante `.placeables-smoke-fish-20261004-v6/candidate`
est préparée avec les 2 771 fichiers vérifiés. Le parcours de réouverture,
sélection des 78 poissons et cadrage est lancé sur ce candidat (86693).
Ce parcours doit encore terminer ; il ne constitue pas à lui seul une preuve
de couverture raster des nageoires ou de parité des ombres.
Le parcours natif v6 (86693) termine code 0 : réouverture, inventaire des 78
poissons et cadrage 180 vers 329 degrés passent. `reopen-result.json` confirme
source et frontend préservés, listener fermé, aucune erreur du parcours.
Capture `fish-shadow-focused-view-v6/fish-focused.png` inspectée : quai et
accessoires visibles, village et végétation au fond. Ce contrôle prouve le
parcours Studio sur le nouveau candidat ; il n'isole pas les pixels du poisson
ou la couverture de sa shadow map. Lecture de profondeur et comparaison des
silhouettes restent nécessaires avant d'annoncer des ombres poissons validées.
## Pipeline et profondeur GPU — preuve isolée

`fish-masked-depth-gpu-proof-20261004-v1/result.json` rapporte compilation,
Clippy et exécution GPU code 0. Le harnais importe directement le module
canonique shadow/gpu.rs (SHA256
4ff1d5212c3a02a03e053b90500f788fb0d4ad56406044a46cd8fd6676774bce) et consomme
le shader émis exact (b243f6efb09353a09d11ea0d5e1268e8efa4886751bfa86a51399282fcfe4eb7).
Device du RenderApp Bevy : AMD Radeon RX 7800 XT, Vulkan. Le pipeline est créé,
les appels prepare/update sont exécutés et la texture Depth32Float 64x64 est
lue après les dessins. Les six sorties profondeur float32 sont conservées.

Corps opaque : 4096/4096 pixels. Nageoire masquée : 2255 puis 2256 pixels aux
deux temps, motifs différents. Caster désactivé : zéro pixel. Une mise à jour
de pose conserve 1024 pixels et déplace le centre horizontal de 31,5 à 47,5.
La planche `depth-comparison-v2.png` est dérivée des lectures conservées ; son
inspection confirme corps opaque, motif ajouré, disparition et déplacement.

Cette preuve utilise des quads synthétiques et un pass profondeur isolé : elle
ne prouve pas encore la silhouette des meshes FishProps, leur réception dans
Studio ou le raccord des trois cascades en scène. Les rapports initiaux et leurs
corrections de harnais restent conservés. Aucun verdict de parité complète.

## Profondeur GPU sur les véritables meshes

`fish-masked-real-depth-gpu-proof-20261004-v1/result.json` passe compilation,
Clippy et exécution GPU. Le harnais consomme les modules canoniques, prépare
les 78 templates depuis le paquet FishProps épinglé et construit les 78 proxies
LOD2 par `MaskedMesh::from_mesh`. Device Bevy : Radeon RX 7800 XT, Vulkan.
La texture Depth32Float 256x256 est effectivement lue après les dessins.

Les 78 dessins déposent 39 488 pixels de profondeur. Le candidat FishProp 39,
avec 388 sommets classés nageoire, dépose 19 413 puis 19 397 pixels aux deux
temps ; les empreintes des deux lectures diffèrent. Toutes les valeurs sont
finies. Les trois lectures float32 originales sont conservées.

La planche `depth-real-comparison-v1.png`, dérivée des lectures hashées, est
inspectée : corps opaque et nageoires ajourées sont visibles sur le candidat.
La première colonne superpose les 78 meshes dans une projection synthétique ;
elle ne représente pas leurs placements sur l’île. Le test couvre le pipeline
et le masque avec la véritable géométrie. La réception des ombres, les trois
cascades et leur comportement pendant l’édition dans Studio restent à vérifier
en scène. Aucune parité visuelle complète n’est déclarée.
