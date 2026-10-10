# Replays de fixtures privees

Les tests autonomes du workspace doivent fonctionner sur un checkout Windows ou Linux sans variables ni fichiers prives de la machine du developpeur.

Treize tests d'integration de `tidewater_fish_props` et de la scene Scanned restent compiles et controles par Clippy, mais portent `#[ignore]` : ils exigent le paquet original et les references acquises dont ils verifient les SHA. Ils ne doivent ni etre remplaces par des fixtures inventees ni etre comptes comme executes par la CI publique. Le replay explicite utilise les donnees autorisees suivantes :

- `FORGE_TIDEWATER_FISH_PROP_PACKAGE` : repertoire du paquet original valide et de sa licence ;
- `FORGE_TIDEWATER_FISH_POSE_REFERENCE` : repertoire de la reference de pose GPU acquise ;
- `FORGE_TIDEWATER_FISH_LOD_REFERENCE` : fichier JSON de la reference de culling acquise ;
- `FORGE_DEBRIS_SCANNED_FIXTURE` : repertoire du paquet Scanned acquis ;
- `FORGE_TIDEWATER_SCENE_LIGHTING_SOURCE` : source WGSL de l'eclairage de la scene acquise.

Apres verification du manifeste et des empreintes des entrees, executer avec le toolchain epingle :

```powershell
cargo +1.98.0 test --locked -p forge_assets --lib tidewater_fish_props::tests::real_package_and_refusals -- --ignored --exact
cargo +1.98.0 test --locked -p forge_engine --lib tidewater_fish_props:: -- --ignored --skip tidewater_fish_props::tests::game_stand_excludes_pool_and_preserves_three_source_lods
cargo +1.98.0 test --locked -p forge_engine --lib tidewater_debris::scanned_scene::tests:: -- --ignored
```

Le premier replay doit executer un test et le second neuf tests et le troisieme trois tests. Une absence d'entree, un SHA divergent ou un nombre de tests different bloque le verdict. Ces replays comparent les references retenues sur CPU ; ils ne constituent pas une nouvelle acquisition GPU ni une qualification visuelle de la revision courante.

Le test GameFish `game_stand_excludes_pool_and_preserves_three_source_lods`, deja ignore avant cette correction, est explicitement exclu : il exige son propre paquet via `FORGE_TIDEWATER_GAME_FISH_PACKAGE` et ne fait pas partie des treize replays ci-dessus.

Les autres tests ignores du workspace conservent leurs propres exigences. Un PASS des tests autonomes ne vaut pas PASS de tous les replays externes, de la CI distante ou de la Gate 3.
