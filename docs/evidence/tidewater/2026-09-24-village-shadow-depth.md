# Profondeur des ombres du village — GPU natif

`tidewater_object_shadow_probe` calcule les trois cartes d'ombre sur le device
Bevy, dans son graphe de rendu, avant les caméras. Il utilise les caméras Rust
de `sun_cascades`, les positions/indices et matrices de modèle extraits, le
format Depth32Float, les faces arrière éliminées et les biais source 2 / 1,5.
Aucun device secondaire, texture d'ombre source ou runtime JavaScript n'est
utilisé pour cette génération native.

La passe couvre les cinq lots opaques du village et les deux morceaux de
l'enseigne. La source les dessine comme un lot opaque étendu par callback et
deux morceaux d'enseigne ; le prototype réunit les cinq sous-ensembles avec
leurs indices existants. Les positions opaques ne sont pas déformées par les
programmes village. Tissus, filets, lanternes et poissons sont exclus, comme
les casters village de l'oracle. Terrain et plantes sont ici explicitement
isolés et restent à ajouter à la passe.

## Comparaison à la source

Deux caméras, trois couches 2048² chacune : **tous les octets de profondeur
sont identiques**, couverture et profondeur comprises.

| Caméra | Couche 0 occupée | Couche 1 occupée | Couche 2 occupée |
|---|---:|---:|---:|
| Village | 333 711 | 130 778 | 5 315 |
| Île éloignée | 0 | 0 | 2 279 |

Les deux couches vides de la vue éloignée vérifient le rejet spatial, sans
constituer à elles seules une preuve de rendu. Les quatre couches non vides
vérifient 472 083 profondeurs de village. La répétition après correction du
cycle de lecture GPU conserve les mêmes octets ; le warning de fermeture
de readback du premier essai ne réapparaît plus.

Compilation et Clippy strict réussis. Indice hors limites avec manifeste
rehashé : rejet sémantique avant GPU (`invalid village depth geometry`, code 1).

## Artefacts

Racine `E:\OpenRPG_Forge\.tidewater-integration` :

- `source-village-depth-001.png` et ses trois `.cascade-N.r32.bin`, oracle
  `source-village-depth-001.png.shadows.json`.
- `native-village-depth-002.bin` : 50 331 648 octets, SHA-256
  `f9e1e99b41b1456ebe5679ed24afd3b1f79c4ffcf0cecda49ce23ba1482bdd4d`.
- `source-village-island-depth-001.png` et couches associées ;
  `native-village-island-depth-002.bin`, SHA-256
  `8c9cdeaf52267caa4c1740c39cc56b8a4bfeafd78715445af2c85e9142a6d474`.
- `village-depth-comparison-002.json`, `village-island-depth-comparison-002.json`.
- `object-depth-clippy-003.log`, `object-depth-build-002.log`,
  `native-village-invalid.log`, fixture `object-depth-invalid-001`.
- Binaire final SHA-256
  `5d84a28bdd83dfb850136ec941ef22b3212b36cfb981dfb9f8851be486765510`.

Cette preuve porte sur la génération de profondeur statique. Le filtrage
PCSS/PCF, la réception des ombres dans la scène assemblée, les masques
végétaux, le terrain, les mises à jour animées et l'édition Studio ne sont pas
encore raccordés à cette passe. Aucun verdict de parité globale ni publication.
