# Replays de fixtures privees

Les tests autonomes du workspace doivent fonctionner sur un checkout Windows ou Linux sans variables ni fichiers prives de la machine du developpeur.

Quatorze tests d'integration du profil poisson et de la scene Scanned portent `#[ignore]` : ils exigent le paquet original et les references acquises dont ils verifient les SHA. Ils ne doivent ni etre remplaces par des fixtures inventees ni etre comptes comme executes par la CI publique. Le replay explicite utilise les donnees autorisees suivantes :

- `FORGE_TIDEWATER_FISH_PROP_PACKAGE` : repertoire du paquet original valide et de sa licence ;
- `FORGE_TIDEWATER_FISH_POSE_REFERENCE` : repertoire de la reference de pose GPU acquise ;
- `FORGE_TIDEWATER_FISH_LOD_REFERENCE` : fichier JSON de la reference de culling acquise ;
- `FORGE_DEBRIS_SCANNED_FIXTURE` : repertoire du paquet Scanned acquis ;
- `FORGE_TIDEWATER_SCENE_LIGHTING_SOURCE` : source WGSL de l'eclairage de la scene acquise.

Apres verification du manifeste et des empreintes des entrees, executer avec le toolchain epingle et le cache partage du projet. Sous Windows, le lanceur `../scripts/Invoke-ForgeCargo.ps1` configure ce cache :

```powershell
cargo +1.98.0 test --locked --offline -j4 -p forge_tidewater_assets --lib tidewater_fish_props::tests::real_package_and_refusals -- --ignored --exact
cargo +1.98.0 test --locked --offline -j4 -p forge_tidewater --lib tidewater_fish_props::pose_reference::complete_source_gpu_pose_reference -- --ignored --exact
cargo +1.98.0 test --locked --offline -j4 -p forge_tidewater --lib tidewater_fish_props:: -- --ignored --skip tidewater_fish_props::tests::game_stand_excludes_pool_and_preserves_three_source_lods --skip tidewater_fish_props::pose_reference::complete_source_gpu_pose_reference
cargo +1.98.0 test --locked --offline -j4 -p forge_tidewater --lib tidewater_debris::scanned_scene::tests:: -- --ignored
```

Les quatre replays doivent executer respectivement un, un, neuf et trois tests. Une absence d'entree, un SHA divergent ou un nombre de tests different bloque le verdict. Ces replays comparent les references retenues sur CPU ; ils ne constituent pas une nouvelle acquisition GPU ni une qualification visuelle de la revision courante.

Le test GameFish `game_stand_excludes_pool_and_preserves_three_source_lods`, deja ignore avant cette correction, est explicitement exclu : il exige son propre paquet via `FORGE_TIDEWATER_GAME_FISH_PACKAGE` et ne fait pas partie des quatorze replays ci-dessus.

Les autres tests ignores du workspace conservent leurs propres exigences. Un PASS des tests autonomes ne vaut pas PASS de tous les replays externes, de la CI distante ou de la Gate 3.

Le replay GameFish optionnel utilise `FORGE_TIDEWATER_GAME_FISH_PACKAGE` et la
commande suivante. Il execute un test, y compris les cinq poissons attaches a
leur parent avec la residence moteur ; il reste distinct des quatorze replays
ci-dessus et ne qualifie pas le rendu GPU :

```powershell
cargo +1.98.0 test --locked --offline -j4 -p forge_tidewater --lib tidewater_fish_props::tests::game_stand_excludes_pool_and_preserves_three_source_lods -- --ignored --exact
```

Le rejeu prive du lecteur de ressources du module reutilise les deux repertoires
de paquets ci-dessus. Il execute deux tests distincts avec des archives
temporaires retirees apres usage, sans dessiner de frame :

```powershell
cargo +1.98.0 test --locked --offline -j4 --manifest-path tools/forge_delivery/Cargo.toml --test module_builder fish_packages:: -- --ignored --test-threads=1
```

Ces deux paquets peuvent aussi etre rejoues avec un transport simule du protocole
de ressources du Studio, sans ecrire de copie ni produire de rendu :

```powershell
cargo +1.98.0 test --locked --offline -j4 -p forge_studio --no-default-features --test module_resources_acquisition fish::real_ -- --ignored --test-threads=1
```

Le rejeu prive du materiau d'etal utilise `FORGE_STALL_LIGHTING_REFERENCE`,
chemin du programme WGSL d'eclairage source verifie par le test. Il compare
l'assemblage avec celui du programme commun du moteur ; ce test ne dessine pas
de frame :

```powershell
cargo +1.98.0 test --locked --offline -j4 -p forge_tidewater --lib tidewater_stall::tests::engine_stall_shader_matches_the_source_lighting_program -- --ignored --exact
```
