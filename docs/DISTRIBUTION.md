# Distribution OpenRPG Forge

## Runtime Windows x64

Le Runtime peut être construit comme application autonome Windows, sans lancer le projet depuis l'éditeur :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-RuntimeDistribution.ps1 -Force
```

Le résultat est écrit dans `artifacts/distribution/runtime-windows-x64/`. Il contient :

- `OpenRPG_Forge_Runtime.exe` pour jouer ;
- le PCK et le runtime .NET publiés ;
- les workspaces de modules inspectables sous `content/`, sans caches `.forge` ;
- `distribution-manifest.json`, avec taille et SHA-256 de chaque fichier ;
- les journaux du smoke headless exécuté sur le binaire exporté.

Vérification indépendante :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Verify-RuntimeDistribution.ps1
```

Ajouter `-RunSmoke` pour rejouer le scénario complet depuis la distribution. Ajouter `-CreateArchive` à la construction pour produire l'archive ZIP.

## Studio autonome Windows et Linux x64

Le Studio Godot .NET possède ses propres presets et inclut un workspace Forgotten Forest sans cache
`.forge` :

```powershell
.\scripts\Build-StudioDistribution.ps1 -Platform windows-x64 -Force
.\scripts\Verify-StudioDistribution.ps1 `
  -DistributionDirectory artifacts\distribution\studio-windows-x64

.\scripts\Build-StudioDistribution.ps1 -Platform linux-x64 -Force -SkipSmoke
.\scripts\Verify-StudioDistribution.ps1 `
  -DistributionDirectory artifacts\distribution\studio-linux-x64
```

Le script reconstruit automatiquement la bibliothèque ForgeScript native de la plateforme :
`forge_script_native.dll` pour Windows x64 ou `libforge_script_native.so` compilée en PIC pour Linux
x64. Chaque paquet contient aussi l'exécutable, le PCK, `Forge.Studio.dll`, le workspace, le manifeste
SHA-256 et les fichiers de conformité.

Le build Windows rejoue par défaut le smoke de distribution depuis l'exécutable exporté. Ce contrat
couvre la retouche réelle, undo/redo, le cache script, ForgeScript natif, l'ouverture autonome UX3-F
et UX5-A/UX5-B, puis compare le modèle et le fichier projet avant/après. Il est volontairement
distinct du portail développeur UX0–UX10, lequel reste exécuté depuis le dépôt et peut utiliser le
CLI, les sources et l'éditeur Godot local.

`-SkipSmoke` est réservé au cross-export structurel : sur un hôte Linux, l'omettre puis utiliser
`-RunSmoke` dans le vérificateur pour rejouer le même contrat. Le runtime .NET du paquet demande ICU ;
le rendu des textes demande Fontconfig et une police système. La qualification Debian 13 utilise
`libicu76`, `fontconfig` et `fonts-dejavu-core`, sans installation de l'éditeur Godot. La preuve
détaillée est dans `docs/acceptance/STUDIO_UX11_B.md`.

## Lanceur des jeux Windows x64

Chaque projet possède un profil de lanceur sous **Distribution et conformité**. Il permet de régler
le titre, le slogan, le nom de l'exécutable, la couleur d'accent, une image PNG/JPEG du projet, les
liens d'actualité/support, les menus de configuration et le mode multijoueur. Après enregistrement,
**Créer le lanceur du jeu** produit un dossier Windows autonome.

Le modèle générique peut aussi être construit et utilisé en ligne de commande :

```powershell
.\scripts\Build-GameLauncherTemplate.ps1 -Force
.\scripts\Build-GameDistribution.ps1 `
  -Workspace E:\MonJeu `
  -OutputDirectory E:\Exports\MonJeu-windows-x64
dotnet run --project tools/Forge.Cli -- launcher verify E:\Exports\MonJeu-windows-x64
```

Les restaurations `win-x64` déclenchées par les exports Godot écrivent leurs verrous NuGet dans un
dossier temporaire propre à la distribution. Elles ne modifient donc pas les `packages.lock.json`
canoniques du workspace ou du dépôt.

Le workspace doit avoir activé son profil de lanceur et être conforme à l'export `Client`. La
génération refuse une destination existante et assemble atomiquement :

- l'exécutable WPF single-file renommé du jeu et son interface RPG cinématique
  Accueil / Configuration / Multijoueur ;
- le runtime OpenRPG Forge et son PCK sous `runtime/` ;
- le module `Client` sous `game/` et le branding optionnel sous `branding/` ;
- `game-launcher.json`, `game-distribution-manifest.json` et `LISEZ-MOI.txt`.

La présentation n'utilise ni WinForms, ni WebView, ni Electron. WPF fournit les transitions de page,
la parallaxe, les brumes et les braises animées sans dépendance de production supplémentaire. Le
décor générique embarqué possède une provenance et un SHA-256 documentés ; une image PNG/JPEG
configurée par le jeu le remplace. Le réglage **Réduire les mouvements** coupe parallaxe et animations.

L'exécutable accepte `--verify` pour une vérification silencieuse et
`--capture=<fichier.png> --page=home|settings|multiplayer` pour reproduire les preuves visuelles sans
lancer le jeu.

Au lancement, tous les fichiers déclarés sont contrôlés par taille et SHA-256. Les préférences sont
stockées par jeu dans `%LOCALAPPDATA%\OpenRPG Forge\Games\<slug>`. Le solo et l'hébergement local
autoritaire sont utilisables. **Rejoindre par adresse** reste désactivé tant que le transport réseau
inter-processus n'est pas branché ; cette limite est volontairement visible dans l'interface.

La distribution portable du Creator embarque ce modèle sous
`workspace/templates/game-windows-x64`, de sorte que la création depuis l'interface n'exige pas le
dépôt de développement.

## Creator portable Windows x64

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-CreatorDistribution.ps1 -Force
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Verify-CreatorDistribution.ps1
```

Le point d'entrée utilisateur est `OpenRPG_Forge_Creator.exe`. Il vérifie son layout puis ouvre
directement le workspace OpenRPG Forge avec le moteur embarqué sous `engine/`. Le workspace, les
modules de démonstration, les catalogues CC0 et le host MCP autonome sont inclus. `-RunSmoke` rejoue
le scénario Creator depuis le bundle ; `-CreateArchive` produit le ZIP portable.

## Installateur Windows x64

NSIS 3 est utilisé pour transformer le bundle Creator validé en installateur Windows :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-WindowsInstaller.ps1 -Force
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Verify-WindowsInstaller.ps1 -RunInstallCycle
```

Le second portail vérifie le manifeste et le SHA-256, puis exécute sur un profil AppData temporaire
vide une installation silencieuse, le smoke Creator, une mise à jour sur place et une désinstallation.
Il prouve également que les données utilisateur externes au répertoire d'installation sont préservées.

Chaque distribution Creator, Runtime et Studio contient aussi, sous `compliance/` :

- `PROJECT-LICENSE.txt`, copie contrôlée de la licence PolyForm Noncommercial 1.0.0 du projet ;
- `THIRD-PARTY-NOTICES.txt`, inventaire lisible des dépendances redistribuées ;
- `ASSET-CREDITS.md` et `FORGESCRIPT-NOTICES.md`, crédits spécialisés ;
- `licenses.csv`, agrégation déterministe des licences ;
- `sbom.cdx.json`, SBOM CycloneDX 1.6 déterministe ;
- `SIGNING-STATUS.txt`, résultat Authenticode et politique de release.

La chaîne technique de distribution est donc testable de bout en bout. Le code propre au projet est
placé sous PolyForm Noncommercial 1.0.0 ; les licences et notices tierces restent indépendantes. Une
release publique reste conditionnée par la fourniture d'un certificat Authenticode. Tant que ce
certificat manque, le manifeste rapporte volontairement `NotSigned` et aucune promesse SmartScreen
n'est faite.
