# Gate 1 — Altitudes authorées de rivière, candidat non promu

Date : 2026-09-05. Branche Rust, local non publié. Gate 1 reste ouverte.

## Périmètre et verdict

Le « oui » utilisateur autorise la correction d'altitude et de creusement de `river_main`,
sans déplacement de son tracé XZ, changement de largeur ou modification des autres éléments
verrouillés. Cette autorisation n'est pas une acceptation artistique du résultat.

**Implémenté et testé** : altitudes d'eau explicites et remblai borné dans le compilateur partagé.
**Vérifié nativement** : eau dégagée du lit, kit chargé, franchissement du pont et arrêt sans ERROR.
**Non qualifié / non promu** : le candidat produit encore un canal encaissé et un remblai central
artificiel. Ni ce candidat ni celui à 40 m ne remplacent la source embarquée. Aucun nouveau
parcours Studio d'édition/promotion et aucune nouvelle gate ne sont commencés.

## Contrat et conservation

`WorldSketchSpline` accepte `surface_heights_m` optionnel : une altitude Y en mètres par contrôle
XZ, finie dans [-1024, 1024], non croissante de l'amont vers l'aval, réservée aux cours d'eau.
L'interpolation linéaire suit les intervalles du tracé Catmull-Rom sans dépassement altimétrique.
`maximum_fill_m` optionnel dans [0, 64] exige un profil explicite ; la coupe reste bornée par
`maximum_cut_m`. Le corridor sculpté conserve la demi-largeur authorée et les 12 m de berge.
Le remblai explicite prend la priorité dans ce corridor, sans réécrire les splines de route/ruisseau.
Les champs absents préservent la recette historique et sa sérialisation. Les anciens lecteurs
stricts v1 refusent les champs ajoutés ; utiliser un lecteur mis à jour. Voir ADR-0008.

Deux nouveaux tests vérifient :

- sauvegarde JSON/réouverture exacte et changement du hash canonique ;
- trois compilations identiques ;
- 73 échantillons XZ identiques, contrôle exact des sept altitudes et absence de remontée ;
- autres données authorées/verrous strictement identiques ;
- caches de route et ruisseau inchangés ;
- hauteur du terrain bit à bit identique hors du corridor de la rivière ;
- budgets de coupe/remblai respectés ;
- rejet des profils mal dimensionnés, non finis, hors bornes, montants ou appliqués à une route,
  et des remblais invalides ou sans profil explicite.

Source embarquée inchangée à la fin de cet incrément (avant la correction élargie suivante) :
SHA-256 octets `87b901540a5f8f6f2faaa431721f120e1a48e2351458ca371caad914c1bae6ee`.

## Candidat externe reproductible

Racine : `E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-river-authored-20260905`.
`world-sketch-candidate.v1.json` ne diffère sémantiquement de la source que par les trois champs
suivants de `river_main` :

```json
{
  "surface_heights_m": [28.0, 23.0, 1.0, -12.0, -12.2, -12.5, -12.9],
  "maximum_cut_m": 24.0,
  "maximum_fill_m": 16.0
}
```

Compilation CLI réelle avec vérification des références originales :

```powershell
forge_cli compile-world-sketch <racine>/world-sketch-candidate.v1.json <nouveau-dossier> --source-root E:/OpenRPG_Forge/OpenRPG_Forge-Rust
```

Les neuf sorties sont conservées sous `compiled/`. Le CLI refuse un dossier de sortie existant.
L'aperçu `runtime-preview-candidate.v1.json` conserve le kit et les 20 placements existants,
avec seulement les hashes WorldSketch/terrain recompilés. Le descripteur de kit demeure
`r1-063-authored-preservation-20260905-b/rebuild-1/asset-kit-build.v1.json` sous la même racine de preuves.

Hashes canoniques :

| Document | SHA-256 |
|---|---|
| WorldSketch | `151c94a924d28a856897b7f20c193e66d491bef33076de991914f75d044e427e` |
| Terrain sculpté | `7f821b0e4e9863c2aeff987904783b7dac99567de677204e59d841d59ca31528` |
| Eau dérivée observée | `de29b33506fc1f219839c9676551eff0f98c8f617f20790e97afc6a91eb923d0` |
| Pont | `59ae8b2cdd0a5d0d0b70984dbd2d25fc076213d8356815e90dc2791d1d5f0a7c` |

## Capture et mesures natives

Run Vulkan, RX 7800 XT, pilote 26.8.1, 1920 × 1080, même caméra héroïque et kit corrigé.
Options : `--world-sketch-visual-valley --world-sketch-input <candidat>`, kit et aperçu ci-dessus,
`--bridge-placement <compiled>/bridge-placements.v1.json`, capture frame 300, métriques après
180 frames, sortie frame 420. `native-run.jsonl` : arrêt du **17:03:51.862953 UTC**, code 0,
`automated_run=true`, `runtime_errors_observed=false`, aucune ligne ERROR.

- 73 contrôles, 346 sections d'eau : marge des trois voies mesurée entre **0,5617113 et 2,8000002 m**.
- 9 150 hauteurs modifiées ; coupe maximale **21,763813 m**, remblai maximal **14,471998 m**.
- 20/20 scènes du kit chargées, 36/36 colliders UCX prêts, aucun échec.
- 240 frames : moyenne 114,22 FPS, p95 9,3475 ms, 1 % low 47,93 FPS ; un hitch de 42,2317 ms.
  Smoke court avec capture, pas un benchmark de qualification ni une comparaison de performance.
- `hero.png` a été ouverte et inspectée : eau visible, mais grandes parois sombres et remblai
  central en forme de canal surélevé. La palette et la végétation restent prototypales.
  Une bonne marge eau/lit ne prouve pas que les berges retiennent naturellement le cours d'eau.

## Franchissement réel du pont

Run séparé : `forge_runtime --terrain <compiled>/terrain-carved.v2.json --bridge-placement
<compiled>/bridge-placements.v1.json --bridge-crossing-probe`.

Événement `bridge_crossing_probe_completed` du **17:04:54.369693 UTC**, code 0 :

- progression 46,55471 m pour une cible 46,50001 m ;
- 570/570 ticks de traversée au sol, aucune séquence sans contact ;
- 32 ticks de contact avec le collider du tablier, centre du croisement franchi ;
- pont de 40 m horizontaux, pente 5,01761°, hauteur maximale au-dessus du terrain 18,97553 m ;
- bindings terrain/joueur/pont conservés, état final au sol ; fournisseur de navigation non choisi.

Le probe émet `automated_run=false` dans le verdict du harness général : il reste auto-borné
par son propre contrôleur. Son événement de succès et l'absence d'ERROR ont été vérifiés
séparément ; ne pas confondre ce flag avec une observation humaine du franchissement.

## Validations de code

- 309 tests workspace PASS : Assets 14, CLI 7, Core 11, Engine 101, Project 45,
  Runtime 45, Studio 24, World 62.
- Premier Clippy refusé pour comparaison flottante exacte dans un nouveau test ; corrigée en
  comparaison de `to_bits()` pour préserver l'exigence d'identité, sans tolérance ni désactivation.
- Clippy workspace, toutes cibles et features, `-D warnings` : PASS après correction.
- `cargo check -p forge_world --target wasm32-unknown-unknown --locked` : PASS.
- Build CLI/runtime natifs, formatage et `git diff --check` : PASS.
- Pas de relance Chrome/WASM : le candidat reste extérieur à la source embarquée.
  Le check WASM n'est pas une preuve visuelle WebGPU du nouveau profil.

## SHA-256 des preuves locales

| Fichier | SHA-256 octets |
|---|---|
| `forge_runtime.exe` | `d6175b47bc66647fd7a31fc772ac57794c49a83208bd991a4263f538ee0d27ef` |
| `hero.png` | `fcd0b53723ebd4f2877698edb9c1242889376930f89150db353d41f03e6a5319` |
| `metrics.json` | `ab379af3005c0e714d498e3e6b19c39913f9ebe10df1a6e286a379eeb444628b` |
| `native-run.jsonl` | `f7479b9886a47a0b77a0c7c3648c8663c288d47357e3342c776ecad02b538a7c` |
| `bridge-probe.jsonl` | `823e05acf8d696b6b5a4a872d243c7694ca0820b67a535f69b9d198b1e03b6e2` |

## Réorientation nécessaire

Le profil généré d'origine descend dans une cuvette proche de −26 m puis rencontre un terrain
aval vers +6 m. Une correction altimétrique confinée au corridor de 42 m de large échange un
canyon profond contre un remblai élevé : ce n'est pas un simple défaut CPU ou un réglage d'eau.
La prochaine correction artistique doit traiter le raccord au relief environnant et ses conflits
avec les accès, au lieu d'augmenter encore les budgets ou de déclarer ce candidat acceptable.
Ce travail plus large n'est pas réalisé ici. Les validations humaines explicites restent dues.
Aucun changement de branche, commit, push, suppression de travail ou asset Cadle.

Suite autorisée : le relief environnant est ensuite corrigé et intégré en révision 2, sans promouvoir
le candidat de ce rapport. Voir `R1_RIVER_VALLEY_REGRADING.md` pour l'état plus récent.
