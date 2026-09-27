# Tidewater — canopée à maillage partagé

Le prototype natif emploie maintenant le même maillage de quatre sommets pour
les 18 343 arbres et arbustes lointains. Les données source iPos/iDat sont
intercalées dans un buffer de 586 976 octets. Chaque entité porte un `MeshTag`
qui adresse son enregistrement ; maillage, matériau et textures sont partagés.
Le shader source, les positions, la déformation et le masque restent inchangés.
Le chemin standard de regroupement des maillages Bevy est utilisé ; aucun
nombre d'appels GPU ni gain de fréquence d'image n'est revendiqué.

## Contrôles

Racine locale : `E:/OpenRPG_Forge/.tidewater-integration/`.

- Clippy strict : `canopy-instancing-clippy-final.log`, réussi.
- Construction : `canopy-instancing-build-001.log`, réussie.
- Binaire SHA-256 :
  `192b884990bf89c22bf2677f09e2ba067966b09626acdc714330a95f2c2a5c87`.
- Fixture isolée : `instanced-canopy-probe-002`, source-017 et canopy-probe-001.
  Masque `native-instanced-canopy-mask-001.png` strictement identique à l'ancien
  maillage déplié : 138 607 pixels occupés, zéro différence, SHA-256
  `9b40dffef6af5a241e45900a17bb2909d982791f92c17f5f16a1d98e74bff863`.
- Comparaison à l'oracle source inchangée : 16 pixels différents, dont un
  au-delà du voisinage d'un pixel ; IoU 0,9998845707112608.
  Rapports `canopy-instancing-regression-001.json` et
  `canopy-instancing-source-001.json`.
- Fixture combinée : `instanced-canopy-probe-001`, source-017 et village-probe-003.
  Capture `native-instanced-village-001.png` inspectée, identique octet par octet
  à `native-village-002.png` :
  `b3ad8aee1057be9b70b13c7f59c9c314deffb2949596679fe65c5c68a98d0c14`.
  Le log confirme 18 343 instances, 31 nœuds village et 60 nœuds terrain.
  `canopy-instancing-village-regression-001.json` compare les images complètes ;
  ce n'est pas une comparaison d'albédo isolé ni une parité source.
- Ancienne fixture arbre proche : `native-tree-mask-regression-005.png`
  identique aux précédentes, SHA-256
  `49be5ddcbfcb3b7c66ec0bc3a13debeb4e78a2c7f75571477e7ac556f36021ae`.
- Instance contenant NaN avec hash recalculé : refus avant GPU, code 1,
  aucune capture, `instanced-canopy-invalid-001.log`.

Les deux premiers Clippy ont signalé un import générique puis un import devenu
inutile ; ils ont été corrigés avant construction. Le comparateur d'albédo isolé
a refusé la scène complète (fond entièrement occupé) ; une comparaison RGB de
l'image entière a confirmé l'identité ci-dessus sans changer ce comparateur.

## Limites

Cette étape ne remplit pas les zones actuellement vides : tous les objets
lointains sont encore soumis sans sélection spatiale, et les représentations
proches, palmiers, sous-bois et herbe restent à intégrer dans la scène complète.
Pas de benchmark ni de qualification WebGPU navigateur, Studio ou collisions.
L'éclairage Bevy provisoire, les ombres manquantes et l'absence d'océan restent
visibles. Aucun contrat canonique modifié, aucun statut TW fermé, aucun commit
ou push.
