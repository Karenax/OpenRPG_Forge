# Tidewater — hauteurs GPU pour le joueur

## Portée

Le moteur expose les hauteurs, normales horizontales et fonds calculés par
le shader d'eau utilisé dans la scène native. La caméra et le joueur disposent
de positions de requête mises à jour pendant la promenade. Ce raccordement
prépare la nage ; il ne modifie pas encore la locomotion, la capsule ou la
caméra du contrôleur Forge.

Référence : `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`,
`src/ocean/WaterQuery.js`, SHA-256
`9e23baa182ffb1a08cdb7d70ac70c94d0db9e83b8a0e54e3220bb171de914cc7`.
Le module partagé conserve l'attribution MIT et la notice Tidewater.

## Implémentation

`forge_engine::tidewater_water_query` porte le registre borné à 64 positions,
le calcul GPU et la validation des lectures asynchrones. Le slot zéro est
réservé à la caméra ; les allocations nommées commencent au slot un.
Le shader réutilise `waterQueryHeightAtXZ` et `terrainHeightAt` de la surface
FFT/rivage. Comme dans la source, les normales utilisent une différence finie
sur 0,35 mètre. Le calcul ne modifie aucun buffer de simulation.

Le paquet de 2064 octets contient le pas, le temps, les positions et les
résultats. La même soumission copie des entrées immuables avant le calcul,
puis le readback conserve cette association. Les paquets initiaux et anciens
sont ignorés ; les paquets mal formés, non finis, de temps rétrograde ou de
normale invalide sont rejetés avant de remplacer le résultat précédent.
La passe utilise un seul stockage supplémentaire, soit huit au total avec
les layouts existants, sans nouvelle dépendance de production.

L'adaptateur de scène attend le reçu du pas 120 avant d'autoriser le pas 121.
Les vingt positions fixes de ce reçu permettent la comparaison source ;
ensuite les slots caméra/joueur suivent leurs positions réelles. Le smoke
vérifie plusieurs résultats successifs, le déplacement du slot joueur et
la variation temporelle de l'eau à des positions fixes.

## Référence et vérifications CPU

Artefacts locaux sous `E:/OpenRPG_Forge/.tidewater-integration/`.

- `water-query-source-001/capture.water-query.json` : calcul GPU de la source
  au pas 120, temps 1,9999999999999978 s (uniform f32 : 2), vent 7 m/s,
  niveau marin zéro. Caméra et joueur en (42,−90), seize points de grille,
  un point océan (400,400) et un point récif (−78,58).
- `export-water-query-source-001.mjs` : acquisition bornée depuis le clone
  de référence, processus Node terminé avec code zéro. Les entrées communes
  frame/océan, neuf mips FFT, relief et paramètres correspondent à
  `spray-source-004`. Les deux fonctions de hauteur WGSL correspondent au
  shader du bundle.
- `water-query-cpu-001.run.log` : cinq tests PASS sur le module réel,
  compilé directement avec Rust 1.98.0 et les dépendances du projet. Ils
  couvrent les allocations, l'association position/résultat, l'ordre des
  paquets et le rejet atomique des métadonnées/résultats invalides.
- `water-query-clippy-003.log` : moteur et quatre exemples, Clippy strict
  avec `--no-deps -- -D warnings`, PASS en 28,21 s.

## Qualification native et WASM

- `water-query-build-001.log` : build natif PASS, 7 min 10 s. Binaire SHA-256
  `2ed33f295c40cc210eeac24f24966c95185cf4177724da4c3de75066fb3c5d48`.
- `water-query-studio-wasm-001.log` : Studio lib avec `web`, sans features
  par défaut, cible WASM PASS, 7 min 31 s incluant l'attente du build natif.
  Avertissement préexistant `proc-macro-error2` ; aucun navigateur exécuté.
- `native-water-query-bootstrap-001` : replay statique, sortie zéro ; reçu
  GPU des vingt positions au pas/temps attendus. Les 47 IDs plantes restent
  résolus. PNG, ombre du relief, profondeur objets et IDs plantes identiques
  octet par octet au candidat herbe précédent, hashes conservés dans
  `water-query-static-comparison-001.json`.
- `compare-water-query.py` et `water-query-comparison-001.json` : comparaison
  des vingt points avec la source ; ordre/positions f32 et temps f64 identiques.
  Les calculs FFT natifs sont refaits localement. Les sorties ne sont pas
  toutes identiques bit à bit ; aucun seuil ajusté après mesure ne transforme
  ces écarts en égalité.

| Composante | Erreur absolue maximale | Erreur absolue moyenne | Valeurs f32 identiques |
|---|---:|---:|---:|
| Hauteur (m) | 0,0000361204 | 0,00000784453 | 6/20 |
| Normale X | 0,000103140 | 0,0000249410 | 6/20 |
| Normale Z | 0,000119444 | 0,0000239685 | 6/20 |
| Fond (m) | 0,000000238419 | 0,0000000178814 | 18/20 |

L'écart maximal de hauteur représente 0,0361 mm sur cet échantillon. Les
deux écarts de fond valent chacun un ULP ; les autres composantes dépassent
un ULP et demeurent documentées comme différentes. Cette mesure bornée ne
prouve pas une égalité sur toute la carte ou tous les instants.

`native-walk-water-query-001` : PASS sur 14,887203 m, 1845 ticks dont 1840
au sol. 4364 paquets acceptés ; premier live au pas 121 / 2,0189975 s,
dernier au pas 4483 / 32,9610563 s. Le point joueur se déplace de 14,887 m,
et les points d'eau fixes changent de hauteur. La capture est inspectée :
capsule, village, ponton, végétation et océan conservés. Cette inspection ne
constitue pas une qualification artistique de la scène.

Cadence de ce parcours : 143,651504 FPS moyens, p95 7,6928 ms et 1% low
82,113813 FPS. Ratio temps simulé/réel de l'eau : 1,001513. Ombres :
4231 soumissions sur 4231 frames main, périodes 1/2/4 et zéro récupération.
Mesures des intervalles de frames main, sans timestamps GPU ; l'écart avec
la mesure précédente ne suffit pas à établir une variation causale de coût.

`native-walk-water-query-30hz-001` : PASS avec limitation volontaire à
29,769238 FPS, même déplacement de 14,887203 m. 2828 paquets acceptés,
du pas live 121 / 2,0261622 s au pas 2947 / 97,0571229 s ; 5677 ticks
dont 5672 au sol. Les contrôles GPU prolongent ce test après la fenêtre
de mesure de cadence. Ratio temps simulé/réel de l'eau : 1,000390.
Ombres : 829 soumissions sur 829 frames main dans leur fenêtre, périodes
1/2/4 et zéro récupération. Capture inspectée, même scène et vagues visibles.
Le ralentissement volontaire de l'affichage ne ralentit donc pas le temps
simulé dans les deux fenêtres mesurées ; la parité animée source reste ouverte.

Essais séquentiels sur RX 7800 XT/Vulkan, AMD 26.8.1, 1920×1080, sans
compilation concurrente. Un avertissement de canal readback fermé apparaît
à l'arrêt après les sorties réussies et reste visible dans les logs.
Les processus des essais et des builds sont terminés. Formatage ciblé et
`git diff --check` PASS ; aucun commit ni push.

## Limites

Le bundle et cet oracle ne comportent pas les sillages `WakeSim`, présents
dans l'application source complète. Leur intégration et leur contribution
aux hauteurs restent ouvertes. Ce lot ne qualifie ni la nage, ni la
flottabilité, ni les transitions de caméra, ni les interactions maritimes.
Les essais du contrôleur utilisent des commandes Forge et ne valent pas
preuve de saisie clavier/souris par le système d'exploitation.

L'exécution WebGPU dans un navigateur, la parité animée et artistique de la
scène complète, l'édition Studio et la distribution autonome restent ouvertes.
