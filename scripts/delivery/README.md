# Reconstruire une livraison Windows locale

Pour préparer le poste et retrouver les données locales, commencer par
[le guide de reprise](../../docs/HANDOFF.md). Gate 4 est clôturée localement
sur le lot 019 selon [le rapport final](../../docs/evidence/gate4/GATE4_CLOSURE_20261016.md).
Les exemples datés ci-dessous restent des procédures et preuves de leurs lots ;
leurs chemins et reçus ne doivent pas être réutilisés pour qualifier de nouvelles sources.

Les recettes courantes produisent le paquet et le Setup du Studio.
Le Studio exporte chaque jeu avec son launcher et le runtime commun.

## Installateur Windows

`build_classic_installer.py studio-only --help` décrit les paramètres du
Setup Studio. Le profil NSIS vérifie le prérequis WebView2 hors ligne et
embarque les fichiers nécessaires. La désinstallation retire les fichiers
vérifiés du Studio et conserve les paquets auteur à leur emplacement.
Le Setup reprend leurs révisions épinglées à la réinstallation.

Le Setup groupé, son Hub et les recettes de compilation historiques sont
conservés dans l'archive du retrait 048. Les vérificateurs historiques
`verify_classic_installer.py` et `repair_installed_launcher.py` concernent
uniquement des installations antérieures.

## Sources et compilation communes

Depuis le 11 octobre 2026, les conversations travaillent dans le checkout commun
`E:\OpenRPG_Forge\OpenRPG_Forge-Rust`. `complete_rebuild.py --shared-working-tree`
compile directement cette version. Son dossier de preuves contient des inventaires,
commandes et logs ; il ne reçoit aucune copie de sources, frontend ou exécutable.

Le validateur commun produit le frontend WASM, le Studio et le jeu natif release.
Les étapes de livraison adoptent leurs reçus complets avec une option explicite ;
elles ne relancent alors ni Trunk ni Cargo et ne copient aucun produit.
Les commandes suivantes restent à exécuter après stabilisation des sources :

```powershell
$validatedGate3Evidence = '<preuves Gate 3 valides pour les sources courantes>'
$sharedNativeEvidence = 'F:\OpenRPG_Forge\Preuves-et-analyses\actives\gate7-monde-distribution\shared-delivery'
$sharedCargoCache = 'E:\OpenRPG_Forge\.build\cargo-target'
$env:FORGE_STUDIO_LOCAL_ASSETS = 'E:\OpenRPG_Forge\.forge-local\studio-assets'
$env:CARGO_HOME = 'E:\OpenRPG_Forge\.build\cargo-home'
$env:TEMP = $env:TMP = 'E:\OpenRPG_Forge\.build\tmp'
$env:CARGO_INCREMENTAL = '0'
$env:RUSTFLAGS = '-C debuginfo=0'
python scripts/Validate-Gate3WorkingTree.py wasm --evidence $validatedGate3Evidence --target-dir $sharedCargoCache
python scripts/Validate-Gate3WorkingTree.py release --evidence $validatedGate3Evidence --target-dir $sharedCargoCache
python scripts/delivery/complete_rebuild.py --shared-working-tree --stage prepare-frontend --validated-gate3-evidence $validatedGate3Evidence --evidence $sharedNativeEvidence --cache $sharedCargoCache
python scripts/delivery/complete_rebuild.py --shared-working-tree --stage studio --validated-gate3-evidence $validatedGate3Evidence --evidence $sharedNativeEvidence --cache $sharedCargoCache
python scripts/delivery/complete_rebuild.py --shared-working-tree --stage game --validated-gate3-evidence $validatedGate3Evidence --evidence $sharedNativeEvidence --cache $sharedCargoCache
python scripts/delivery/complete_rebuild.py --shared-working-tree --stage module-game --evidence $sharedNativeEvidence --cache $sharedCargoCache
```

`module-game` compile `forge-module.exe` en release depuis `forge_runtime` et
lie son reçu aux mêmes sources que les autres applications. Le Studio installé
l'utilise pour les essais des modules ; le gestionnaire utilise la même entrée
`module-game` pour les modules installés. Le bouton de jeu de la livraison conserve
la démo Tidewater et son interface de lancement existante. Ces builds et leur
assemblage ne prouvent ni rendu, ni performance, ni qualification de Gate 7.

L'adoption appelle les fonctions du validateur commun et exige des reçus `PASS`
complets : sources actuelles, suppressions, driver, commandes, logs, cache et
environnement identiques à l'intention de livraison. Le frontend actuel ferme
les artifacts du worker, ses sources et les assets locaux acquis. L'adoption
de Studio et du jeu exige aussi le hash et la taille de chaque binaire release,
ainsi que sa chaîne WASM ; les SHA des manifestes et résultats originaux restent liés dans les preuves
de livraison. Le packaging rouvre ces chaînes avant de consommer les deux exécutables.
La commande d'adoption ne lance pas le compilateur ; `rustc_version` reste `null`
et le hash du fichier `rust-toolchain.toml` accompagne la provenance originale.

Sans `--validated-gate3-evidence`, `prepare-frontend` lance
`Invoke-StudioTrunk.ps1 -Action build`, donc Trunk et ses hooks communs, dans
`apps/forge_studio/dist`. `studio` compile directement le même target release
que le validateur, avec le même profil et les mêmes features. Il embarque ce
`dist` avec `tauri.conf.json` et son URL `index.html?authoring=1`, sans override
`TAURI_CONFIG`. Le binaire reste dans `CHEMIN_CACHE/release/forge-studio.exe`.
Les assets locaux passent par le hook commun ; `--frontend-assets` et les anciens
candidats sont refusés. L'option d'adoption est disponible pour `prepare-frontend`,
`studio` et `game`. Pour `game`, elle adopte le jeu natif validé dans
`CHEMIN_CACHE/release/examples/tidewater_plant_probe.exe`, sans reconstruction.
Sans cette option, `game` compile le même checkout et conserve son résultat dans
`CHEMIN_CACHE/debug/examples/tidewater_plant_probe.exe`. Le profil optimise le moteur
et l'exemple natif au niveau 3 ; il ne constitue pas un build release/LTO du workspace.
Ce build dev reste destiné aux essais locaux ; l'assembleur commun exige le jeu release.
Le runtime `forge_runtime` contrôlé par le validateur conserve son propre rôle ;
la recette du jeu installé utilise le target Tidewater et ses arguments explicites.
Sérialiser les compilations lourdes.

`source-inputs.json` reprend la carte de compilation du validateur commun : fichiers
suivis et non suivis des périmètres `crates`, `apps`, `tools`, `third_party`, `scripts`,
`examples`, `.github`, manifestes racine, `deny.toml` et `.cargo`, suppressions suivies
explicites, lockfiles locaux voisins des manifestes présents et configurations Cargo
ignorées. Depuis l'extraction des harnesses du 17 octobre, `scripts/hivemind/**`
est classé comme orchestration/acquisition non consommée par ces builds. Avant
cette exclusion, le validateur refuse toute inclusion Rust explicite depuis le
produit vers ce dossier, y compris les chemins construits avec `concat!`/`env!`.
Les chemins de dépendances, de targets et de membres des manifestes Cargo sont
contrôlés également ; une dépendance de chemin ne contourne pas cette exclusion.
Le code compilé des probes réside dans `forge_tidewater/examples/support`. Les assets
statiques réellement consommés gardent leurs manifestes, pins et contrôles propres.

`working_tree_files` et les métadonnées Git conservent aussi l'inventaire complet du
checkout. Chaque étape garde les inventaires avant/après et leurs SHA ; les différences
informatives restent dans `working_tree_changes`. L'égalité de compilation compare
les cartes `chemin → SHA/None`, indépendamment des métadonnées Git et des scripts
d'orchestration exclus. Une modification d'entrée de compilation entre étapes ou
pendant le build invalide le reçu. Conserver les logs et relancer les étapes nécessaires
dans un nouveau dossier de preuves avec les mêmes sources et sorties communes.

Les anciennes copies produites par `freeze_sources.py`, les appels
`Build-StudioCandidate.ps1` et la voie `complete_rebuild.py --frozen-archive` décrivent
des preuves historiques. Elles ne constituent plus la procédure de livraison active
et ne sont pas rejouées pour finaliser la version commune.

La livraison complète exige aussi le reçu `forge-studio-local-assets-manifest.json`
dans le dist : `STAGED`, assets présents, inventaire exact de `forge-imports` et
`forge-kit`, hash du manifeste local et fermeture identique au frontend embarqué.
Un frontend CI public sans ces assets ne devient pas un Studio installable complet.

## Cible Gate 7 : Studio et launcher de chaque jeu

La cible produit est le Studio Forge et le launcher propre à chaque jeu exporté.
Le Hub du parcours historique ci-dessous ne fait pas partie de cette cible. Le
chemin privé en cours d'intégration assemble le kit avec `game_kit.py`, puis
utilise les modes `build_delivery.py studio-only` et
`build_classic_installer.py studio-only` ; leurs options et leurs épingles sont
décrites par `--help` après le nom du mode. Les tests de fixtures ne constituent
pas une installation réelle. Le kit Studio Linux et sa qualification restent
en cours ; voir le [suivi de Gate 7](../../docs/evidence/gate7/GATE7_PROGRESS_20261023.md).

L'installateur WebView2 historique a été retrouvé dans les archives puis vérifié
sur E: ; son SHA est celui de la recette ci-dessous. Son programme d'amorçage
signé est un PE x86 : cela ne démontre pas à lui seul l'architecture du runtime
installé. Les conditions Microsoft et l'installation réelle restent à vérifier.

## Contrôles des sources de dépendances

Les contrôles de provenance peuvent être exécutés sans compiler.

```powershell
python -B scripts/delivery/application_sources.py --verify-only `
  --cargo-cache 'E:/OpenRPG_Forge/.build/cargo-home/registry'
python -B scripts/delivery/test_application_sources.py -v
python -B scripts/delivery/test_runtime_notices.py -v
```

La bibliothèque complète reste un transfert séparé via
`prepare_asset_library.py` et `export_asset_catalog.py`. Un catalogue
ou un plan de lots décrit l'inventaire. Les droits de redistribution
restent attachés aux paquets et à leurs notices.

Les preuves de compilation portent le statut `BUILD_ONLY`. L'installation
locale, le diagnostic du jeu et la validation humaine ont leurs propres
reçus.
