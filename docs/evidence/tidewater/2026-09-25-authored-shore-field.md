# Tidewater — propagation côtière dérivée du terrain effectif

`shore_field.rs` porte le calcul `ShoreField.js` de la source épinglée :
Fast Marching, tas minimal avec le même ordre des égalités, extensions sur terre,
lissage, directions et exposition. Les calculs intermédiaires sont en f64 et
les écritures des tableaux en f32, comme les Float32Array source.
Le domaine est celui de Tidewater : 2048 m, origine -1024 m, 512² cellules,
niveau marin zéro et direction normalisée (-0.12, -1).

Le matériau optique recalcule ce champ au chargement d'un terrain modifié depuis
`SampledTerrainPlan::height_at`, sans aplatir les corrections aux coins dans le
raster source. Il conserve les octets acquis pour un terrain sans retouche.

## Preuves exécutées

Racine `E:/OpenRPG_Forge/.tidewater-integration/`.

- `shore-field-source-proof-001.json` : 1048576 valeurs f32 strictement égales
  au champ acquis. SHA-256
  `573846d4d3bfb0145951b9b560ccfa8d2b2cf6cb64a2f19d1866895cfe6ef833`.
- `verify-authored-shore-field.mjs` vérifie le hash du fichier source et
  exécute son code avec la constante GRAVITY source. Le callback de hauteur
  applique séparément le raster et les corrections canoniques.
- `shore-field-raised-oracle-001` : 2204 valeurs changent par rapport au champ
  initial ; zéro différence JS/Rust. Hash
  `820fb577153095b7c70deaaa06f7a0161259fa02190bbad681a03eeb69ffd2fc`.
- `shore-field-global-oracle-001` : 745887 valeurs changent ; zéro différence
  JS/Rust. Hash
  `876d4d72382cb6615857b473170aa2386ee2374f024726b18eb8270d8043c8c8`.
- `relief-shore-field-proof-001` : chargement du champ local confirmé par le
  même hash dans le journal natif. Les 203 plantes suivent toujours la bosse.
  Undo retrouve exactement l'image initiale, zéro pixel différent.
- `native-global-shore-field-001.png` inspectée : champ global confirmé par
  son hash dans le journal, sans erreur GPU. Comparaison au lot eau/terrain :
  128512 pixels changent dans le rivage (rectangle 306,370 à 1919,485).
  Pour la bosse locale, 567 pixels changent (265,401 à 502,423).
  Les ombres proches et lointaines restent identiques dans les deux essais.
  Rapport `shore-field-render-comparison-001.json`. Les bâtiments enterrés
  et la simulation de plage encore ancienne restent visibles sur l'essai global.
- Compilation et Clippy strict réussis pour le comparateur et le prototype.
  Binaire de rendu :
  `7d1a699fca2db2f21ec732ad3c78efa5ec7de3a87394853dac6ed4b9e431bf25`.

## Portée restante

Le champ dérivé est raccordé au matériau d'eau. Les simulations couplées de
ressac, déferlements et embruns relisent encore leurs entrées acquises ; elles
doivent recevoir le même champ et les hauteurs effectives avant qualification
complète de la côte. Les directions côtières filtrées, bâtiments, édition
interactive, parité temporelle et validation WebGPU restent également ouverts.
Ce prototype au démarrage n'est pas encore le service de reconstruction Studio.
Aucun commit ni publication.
