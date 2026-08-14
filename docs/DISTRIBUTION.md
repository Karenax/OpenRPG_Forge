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

Chaque distribution Creator et Runtime contient aussi, sous `compliance/` :

- `THIRD-PARTY-NOTICES.txt`, inventaire lisible des dépendances redistribuées ;
- `ASSET-CREDITS.md` et `FORGESCRIPT-NOTICES.md`, crédits spécialisés ;
- `licenses.csv`, agrégation déterministe des licences ;
- `sbom.cdx.json`, SBOM CycloneDX 1.6 déterministe ;
- `SIGNING-STATUS.txt`, résultat Authenticode et politique de release.

La chaîne technique de distribution est donc testable de bout en bout. Une release publique reste
cependant conditionnée par deux décisions externes : l'ajout d'une licence explicite pour le code du
dépôt, et la fourniture d'un certificat Authenticode. Tant que ce certificat manque, le manifeste
rapporte volontairement `NotSigned` et aucune promesse SmartScreen n'est faite.
