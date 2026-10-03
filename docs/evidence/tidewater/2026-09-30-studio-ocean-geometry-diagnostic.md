# Tidewater — géométrie océan FFT dans le Playtest Studio

Le Playtest importé peut demander `forge_water_geometry=1` sur l’URL
`?authoring=1`. Ce mode raccorde le maillage CDLOD du probe natif au calcul
FFT déjà résident. Il utilise le shader `ocean-geometry.wgsl` du paquet de
surface Tidewater vérifié et les hauteurs de la carte éditée. Le shader émet
volontairement du blanc : ce mode mesure la présence, la position et le
déplacement des vagues, sans présenter le résultat comme l’eau finale.
Le mode ordinaire du Studio ne change pas.

Le candidat Windows isolé est
`E:/OpenRPG_Forge/.tidewater-integration/studio-tidewater-water-candidate-027/forge-studio-candidate.exe`,
SHA-256 `4e44a0f9b9c7b23dde5e58b9dba9a047276737112602aa50e0f89196de270b09`.
Son WASM est `5159d5213bd8817dcc331706dc5069418dae75a9ef58020dae8e4c7f553bb3c18`.
Le paquet de surface porte la révision source
`1438b1abfcaee3267092b75573014f4d9b4a983c`.

Le [reçu de parcours](../../../../.tidewater-integration/studio-tidewater-water-smoke-028/fish-stand-studio-smoke-ledger.json)
est `passed` : zone enregistrée puis rouverte, simulation active, 101 nœuds
océaniques à l’entrée en jeu, FFT à l’étape 120 puis progression de 130 à 138,
commandes graphiques toujours actives, marche résolue, retour à l’édition,
aucune erreur navigateur capturée, fixture source intacte. Une entrée souris
réelle via CDP a tourné la caméra vers le large pour la
[capture de diagnostic](../../../../.tidewater-integration/studio-tidewater-water-smoke-028/fish-stand-ocean-geometry-seaward.png).
Elle montre une nappe blanche à l’emplacement de l’eau. Cette image prouve
que la géométrie entre dans le rendu WebGPU ; elle ne prouve pas encore la
fidélité optique ni un mouvement mesuré entre deux images.

Les premiers candidats ont révélé deux erreurs d’assemblage : un `index.html`
source Trunk non transformé, puis un dossier `forge-imports` imbriqué qui
laissait le Studio lire l’ancien manifeste sans paquet de surface. Le script
`Build-StudioCandidate.ps1` refuse désormais ces deux entrées ambiguës en
mode authoring. Ses vérifications ont refusé les fronts 025 et 026 et accepté
le front 027 (`INPUTS_VALID`, 1 304 fichiers).

Contrôles de code : `cargo check -p forge_engine --features physics-rapier`
et `cargo check --target wasm32-unknown-unknown -p forge_studio
--no-default-features --features web` passent en mode `--locked --offline -j1` ;
le build WASM release et `wasm-bindgen` passent. Le self-test du parcours,
le formatage ciblé et `git diff --check` passent aussi. Aucune publication.

Reste à porter le matériau optique source sur cette géométrie, avec copie de
profondeur à la taille du viewport, dérivées FFT, reflets/réfraction, mousse,
champ de rive recalculé après édition et simulation de ressac. Ensuite il
faudra une comparaison d’images à caméra, heure et météo appariées avec la
version publique actuelle. Les réglages Studio d’occlusion, de lumière
rebond, de flare et de TAA gardent leurs limitations décrites dans
`2026-09-30-studio-graphics-controls.md`.
