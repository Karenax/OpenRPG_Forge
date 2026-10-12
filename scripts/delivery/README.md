# Reconstruire une livraison Windows locale

Ces outils de développement produisent un installateur EXE hors ligne ou un dossier
de packages : launcher, éditeur, jeu natif, module et assets détachés. Python, Cargo, Trunk et wasm-bindgen
ne sont pas nécessaires sur le poste client. Windows x64 et WebView2 le sont.

## Installateur classique autonome

`build_classic_installer.py --help` assemble un seul EXE NSIS : assistant français,
choix de destination, installation des trois packs, raccourcis pour l’utilisateur
et désinstallation Windows. WebView2 est inclus hors ligne et sa signature Microsoft
est vérifiée avant son exécution lorsqu’il manque.

La voie `--shared-working-tree` recompile directement les workspaces launcher Aube,
Hub et gestionnaire de livraison du checkout commun. Elle exige les reçus actuels
Studio/jeu produits par `complete_rebuild.py`, les données courantes explicites et
l'installateur WebView2 hors ligne avec son SHA-256. Les anciens Game/Studio et
`--baseline` sont refusés dans ce mode.
`--node` désigne le Node de développement qui prépare le frontend embarqué. `Forge.exe`
est le véritable launcher ; `Forge-ContentManager.exe` est le gestionnaire séparé.
Le menu Démarrer propose aussi un accès Studio utilisant la recette installée.
L'ancienne voie `--frozen-archive` conserve les binaires Game/Studio d'une livraison
précédente et leurs preuves séparées. `--reuse-native-build` appartient à cette voie
historique et exige l'égalité exacte des inventaires et hashes. Elle ne remplace pas
la reconstruction de la version commune.

Le compilateur NSIS reçoit explicitement `/INPUTCHARSET UTF8`. `Unicode true` contrôle
la sortie et ne remplace pas ce choix d’encodage des sources. Le répertoire temporaire
NSIS est initialisé avant extraction. Le payload privé de développement et ses archives
ne doivent pas être copiés à côté de l’EXE pour l’utilisateur : ils sont embarqués.

`verify_classic_installer.py` installe le fichier exact dans un chemin Unicode neuf,
vérifie les fichiers et recettes de lancement, réinstalle, puis désinstalle en conservant
les projets, sauvegardes et fichiers étrangers. Par défaut il évite registre/raccourcis
avec `/NOINTEGRATION`. `--shell-integration` n’est permis que si toutes les clés et
tous les noms exacts possédés sont absents; il vérifie leur création puis suppression,
et l’intégrité des anciens raccourcis non correspondants. `--entrypoint-probe` reçoit
`smoke_entrypoints.py` pour contrôler les démarrages launcher/Studio dans des profils WebView2
isolés; cela ne vaut pas acceptation humaine de leurs fenêtres.

La vérification classique exige le marqueur du frontend Aube embarqué, un vrai reçu
WebView/IPC qui voit le jeu installé et des exécutables distincts pour launcher et Hub.
Les rendus mockés du Hub seuls ne valident pas le launcher.

La désinstallation retire uniquement les fichiers de packages dont taille et hash sont
conformes, puis les bootstrap/raccourcis possédés. Les fichiers modifiés ou étrangers
restent en place. Le verrou neutre `installation.lock` est conservé pour prévenir une
course avec un autre installateur. Un dossier de package conservant des retouches peut
rester partiel; sélectionner une autre destination pour une installation neuve plutôt
que supprimer automatiquement ces retouches.

`verify_hub_ui.cjs` contrôle l’adaptateur de présentation dans un navigateur isolé avec
une fixture IPC explicite : destination, invalidation du plan, relecture, retour de
vérification, titres externes rendus comme texte et fenêtre minimale. Ses captures
ne sont pas des captures de la fenêtre Tauri ni de l’assistant NSIS natifs.

## Sources et compilation communes

Depuis le 11 octobre 2026, les conversations travaillent dans le checkout commun
`E:\OpenRPG_Forge\OpenRPG_Forge-Rust`. `complete_rebuild.py --shared-working-tree`
compile directement cette version. Son dossier de preuves contient des inventaires,
commandes et logs ; il ne reçoit aucune copie de sources, frontend ou exécutable.

Le validateur commun produit le seul frontend WASM et le seul Studio release.
Les étapes de livraison adoptent leurs reçus complets avec une option explicite ;
elles ne relancent alors ni Trunk ni Cargo et ne copient aucun produit.
Les commandes suivantes restent à exécuter après stabilisation des sources :

```powershell
$validatedGate3Evidence = 'E:\OpenRPG_Forge\.gate3-evidence\20261011\shared-006'
$sharedNativeEvidence = 'E:\OpenRPG_Forge\.gate3-evidence\20261011\shared-delivery'
$sharedCargoCache = 'E:\OpenRPG_Forge\.gate3-ntfs-cargo-20261002'
$env:FORGE_STUDIO_LOCAL_ASSETS = 'E:\OpenRPG_Forge\.forge-local\studio-assets'
$env:CARGO_INCREMENTAL = '0'
$env:RUSTFLAGS = '-C debuginfo=0'
python scripts/Validate-Gate3WorkingTree.py wasm --evidence $validatedGate3Evidence --target-dir $sharedCargoCache
python scripts/Validate-Gate3WorkingTree.py release --evidence $validatedGate3Evidence --target-dir $sharedCargoCache
python scripts/delivery/complete_rebuild.py --shared-working-tree --stage prepare-frontend --validated-gate3-evidence $validatedGate3Evidence --evidence $sharedNativeEvidence --cache $sharedCargoCache
python scripts/delivery/complete_rebuild.py --shared-working-tree --stage studio --validated-gate3-evidence $validatedGate3Evidence --evidence $sharedNativeEvidence --cache $sharedCargoCache
python scripts/delivery/complete_rebuild.py --shared-working-tree --stage game --evidence $sharedNativeEvidence --cache $sharedCargoCache
```

L'adoption appelle les fonctions du validateur commun et exige des reçus `PASS`
complets : sources actuelles, suppressions, driver, commandes, logs, cache et
environnement identiques à l'intention de livraison. Le frontend actuel ferme
les artifacts du worker, ses sources et les assets locaux acquis. L'adoption
Studio exige aussi le hash et la taille du binaire release, ainsi que sa chaîne
WASM ; les SHA des manifestes et résultats originaux restent liés dans les preuves
de livraison. Le packaging rouvre cette chaîne avant de consommer le Studio.
La commande d'adoption ne lance pas le compilateur ; `rustc_version` reste `null`
et le hash du fichier `rust-toolchain.toml` accompagne la provenance originale.

Sans `--validated-gate3-evidence`, `prepare-frontend` lance
`Invoke-StudioTrunk.ps1 -Action build`, donc Trunk et ses hooks communs, dans
`apps/forge_studio/dist`. `studio` compile directement le même target release
que le validateur, avec le même profil et les mêmes features. Il embarque ce
`dist` avec `tauri.conf.json` et son URL `index.html?authoring=1`, sans override
`TAURI_CONFIG`. Le binaire reste dans `CHEMIN_CACHE/release/forge-studio.exe`.
Les assets locaux passent par le hook commun ; `--frontend-assets` et les anciens
candidats sont refusés. L'option d'adoption est réservée à ces deux étapes.
`game` compile le même checkout et conserve son résultat dans
`CHEMIN_CACHE/debug/examples/tidewater_plant_probe.exe`. Le profil optimise le moteur
et l'exemple natif au niveau 3 ; il ne constitue pas un build release/LTO du workspace.
Le runtime `forge_runtime` contrôlé par le validateur conserve son propre rôle ;
la recette du jeu installé utilise le target Tidewater et ses arguments explicites.
Sérialiser les compilations lourdes.

`source-inputs.json` reprend la carte de compilation du validateur commun : fichiers
suivis et non suivis des périmètres `crates`, `apps`, `tools`, `third_party`, `scripts`,
`examples`, `.github`, manifestes racine, `deny.toml` et `.cargo`, suppressions suivies
explicites, lockfiles locaux voisins des manifestes présents et configurations Cargo
ignorées. Seuls les scripts Python sous `scripts/hivemind` sont classés comme
orchestration non consommée par ces builds ; tous leurs autres formats restent des
entrées bloquantes, dont le candidat Rust inclus par un exemple Cargo.

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

## Installateur principal de la version commune

La commande suivante est une recette à exécuter après préparation et validation des
binaires communs ; aucune compilation ou installation n'est acquise par sa présence.
`$sharedNativeEvidence` désigne le même dossier utilisé pour les trois stages ci-dessus.
`$currentData` doit contenir les données courantes `data/scene`, `data/workspace`,
`content` et `licenses` ; aucun exécutable historique n'est requis ni repris.
Les outils Node, Cargo et NSIS ci-dessous ont été localisés le 11 octobre 2026 ;
l'installateur WebView2 indiqué existe avec le hash épinglé.

```powershell
$sharedNativeEvidence = 'E:\OpenRPG_Forge\.gate3-evidence\20261011\shared-delivery'
$sharedPackagingEvidence = 'E:\OpenRPG_Forge\.gate3-evidence\20261011\shared-installer'
$sharedCargoCache = 'E:\OpenRPG_Forge\.gate3-ntfs-cargo-20261002'
$currentData = 'E:\OpenRPG_Forge\.forge-local\runtime-data'
$offlineWebview = 'E:\OpenRPG_Forge\.delivery-evidence\20261010\classic-build-005-game-sessions\payload\MicrosoftEdgeWebView2RuntimeInstallerX64.exe'
$principalInstaller = 'E:\OpenRPG_Forge\OpenRPG-Forge-Setup.exe'

python scripts/delivery/build_classic_installer.py --shared-working-tree `
  --native-evidence $sharedNativeEvidence --evidence $sharedPackagingEvidence `
  --target $sharedCargoCache --playtest $currentData `
  --area-id e620dd19-ead9-5913-a6d6-0f93109148c0 `
  --webview2 $offlineWebview `
  --webview2-sha256 2a6add76c37bfa872eb8c2b22d45b3216b8e2a1f193e0774006340ff62ac5fa0 `
  --cargo 'C:\Users\karen\.cargo\bin\cargo.exe' `
  --node 'C:\Users\karen\AppData\Local\hermes\node\node.exe' `
  --nsis 'C:\Program Files (x86)\NSIS\makensis.exe' `
  --version 0.1.0-preview.20261011 --output $principalInstaller
```

Ces noms de dossiers de preuves sont à réserver pour l'exécution réelle. S'ils existent,
les conserver et choisir de nouveaux dossiers de preuves ; les chemins des sources,
du dist, du cache et de l'installateur principal restent communs. Le chemin de données
`runtime-data` doit être préparé et identifié comme autorité courante avant cette commande.
Les reçus natifs exigés pointent vers `E:\OpenRPG_Forge\.gate3-ntfs-cargo-20261002\release\forge-studio.exe`
et `E:\OpenRPG_Forge\.gate3-ntfs-cargo-20261002\debug\examples\tidewater_plant_probe.exe` ;
le builder refuse un exécutable de candidat situé ailleurs.

Si le Setup principal existe déjà, ajouter explicitement `--replace-output
--expected-output-sha256 SHA256_ANCIEN_SETUP_VERIFIE`. Le builder contrôle le hash
avant compilation et juste avant remplacement. NSIS produit d'abord son résultat
interne dans le dossier de preuves, puis le déplace atomiquement vers ce seul chemin
principal. Un échec ou une dérive des sources conserve l'ancien Setup. Le staging
des packages et bootstrap de l'assistant reste dans le payload normal ; aucune copie
de sources ou de frontend par conversation n'est créée.

L’ancienne voie `--stage reuse-launcher` limitée au Hub/CLI est désactivée.
La réutilisation historique du launcher installé passe par `build_classic_installer.py
--frozen-archive --reuse-native-build`, avec les trois binaires et leurs sources identiques.

## Assemblage et vérification

`build_delivery.py --shared-working-tree` décrit les entrées explicites. Pour un jeu reconstruit,
fournir aussi `--launcher` avec le véritable launcher Aube de production ; le Hub
seul est refusé pour cette entrée. `--aube-proof` conserve et vérifie sa preuve de compilation ; vérifier
le reçu WebView/IPC dans l’installation finale pour valider le raccord des recettes.
fournir `--runtime`, `--runtime-proof`, `--source-manifest`, `--studio-proof` et
`--launcher-proof`, ainsi qu'une version et un dossier de sortie neufs. La recette
vérifie les preuves et les hashes, puis produit trois `.forgepack` indépendants.
Le manifeste commun doit couvrir effectivement `apps/forge_launcher`, `apps/forge_hub`
et `tools/forge_delivery`, y compris leurs manifestes Cargo. Le reçu launcher porte
`source_manifest_sha256` et `current_source_manifest_sha256` identiques au manifeste
Studio/jeu ; les sorties restent dans le cache commun jusqu'au staging normal des packages.
Tous les changements de sources imposent de nouveaux reçus ; les métadonnées de
packaging, logs et preuves sont stockés hors des périmètres de compilation.

`verify_delivery.py --delivery CHEMIN_LIVRAISON --evidence NOUVEAU_DOSSIER_TEST`
vérifie une installation fraîche et un Apply rejoué.
`verify_upgrade.py --previous CHEMIN_ANCIENNE_LIVRAISON --delivery CHEMIN_LIVRAISON
--evidence NOUVEAU_DOSSIER_TEST` vérifie ancien → nouveau → rollback, avec une retouche auteur
et des fichiers utilisateur dont tous les hashes doivent rester inchangés.
`smoke_entrypoints.py --delivery CHEMIN_LIVRAISON --installation CHEMIN_INSTALLATION
--output NOUVEAU_DOSSIER_TEST --game-smoke` contrôle les démarrages natifs et conserve le
reçu diagnostic du jeu ; la survie d'un processus ne vaut pas validation visuelle.
Un démarrage interrompu, un timeout ou un échec du diagnostic produit un verdict
d'échec et un code de sortie non nul, avec le rapport conservé.
Tous les dossiers de test sont explicites, neufs et conservés.

La bibliothèque complète reste un transfert séparé : voir
`prepare_asset_library.py` et `export_asset_catalog.py`. Un catalogue ou un plan
de lots ne signifie pas que les modèles ont été exportés ou envoyés. Le contrat
d'installation et les limites sont définis dans
[ADR-0021](../../docs/ADR/ADR-0021-detached-delivery-installation.md).

Les preuves de compilation restent `BUILD_ONLY`. Installation locale, diagnostic
du jeu, validation humaine, CI distante, Gate 3 et qualification de release
conservent chacun leur verdict propre.
