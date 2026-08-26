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

Le Studio Godot .NET possède ses propres presets et inclut un workspace Crypte des Cendres sans cache
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
le titre, le slogan, le nom de l'exécutable, la couleur d'accent, une image PNG/JPEG du projet, une
musique WAV facultative, les liens d'actualité/support, les menus de configuration et le mode
multijoueur. Après enregistrement, **Créer le lanceur du jeu** produit un dossier Windows autonome.

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
- le module `Client` sous `game/` et les médias optionnels de fond et de musique sous `branding/` ;
- `game-launcher.json`, `game-distribution-manifest.json` et `LISEZ-MOI.txt`.

Le lanceur WPF, le module et OpenRPG Forge Studio s'ouvrent en plein écran par défaut. Dans les
réglages du jeu, **Fenêtré plein écran · sans bordure** utilise toute la surface du moniteur sans
changer de mode d'affichage ; les préférences v1 `Windowed` et `Borderless` existantes sont toutes
deux interprétées de cette façon.

La présentation n'utilise ni WinForms, ni WebView, ni Electron. WPF fournit les transitions de page,
la parallaxe, les brumes et les particules animées sans dépendance de production supplémentaire. Le
décor générique embarqué possède une provenance et un SHA-256 documentés ; une image PNG/JPEG
configurée par le jeu le remplace. Une musique WAV configurée démarre en boucle et reste contrôlable
par **Play/Stop** et un curseur synchronisé avec le volume Musique du jeu. Le réglage **Réduire les
mouvements** coupe parallaxe et animations ; les captures automatisées restent silencieuses.

L'exécutable accepte `--verify` pour une vérification silencieuse et
`--capture=<fichier.png> --page=home|settings|multiplayer` pour reproduire les preuves visuelles sans
lancer le jeu. La page multijoueur accepte aussi `--session-mode=solo|host|join` et, pour une preuve
Hôte sans exposer l'adresse réelle du poste, `--public-address=<IPv4-ou-DNS>`.

Au lancement, tous les fichiers déclarés sont contrôlés par taille et SHA-256. Les préférences sont
stockées par jeu dans `%LOCALAPPDATA%\OpenRPG Forge\Games\<slug>`. Le solo, l'hébergement local et le
profil **Internet direct · héberger et rejoindre** sont utilisables. Le client accepte une IPv4 ou un
nom DNS et refuse la partie avant le monde si l'empreinte du module ou la version du protocole ne
correspond pas.

En mode **Héberger**, le lanceur tente pendant cinq secondes de détecter l'IPv4 publique via
`https://api.ipify.org/`, puis affiche avec le port l'adresse à transmettre. Cette aide HTTPS est
facultative : le champ accepte une saisie manuelle d'IPv4 publique ou de DNS et l'échec du service
n'empêche pas l'hébergement. L'adresse de boucle locale `127.0.0.1` reste interne au runtime et n'est
pas présentée comme adresse à partager.

Pour héberger depuis Internet :

1. choisir **Héberger**, vérifier l'adresse affichée et un port TCP entre 1024 et 65535 ;
2. copier `adresse:port` avec le bouton du lanceur et le transmettre aux invités ;
3. autoriser le jeu dans le pare-feu Windows lors de la première écoute ;
4. rediriger ce même port TCP du routeur vers l'IPv4 locale de l'ordinateur hôte.

Le transport direct v1 réplique l'admission, la présence, la zone et les déplacements des avatars.
Il ne transporte aucun mot de passe ou jeton, mais il n'est pas chiffré et ne prouve pas l'identité
de l'hôte : l'utiliser avec des joueurs de confiance. Il ne fournit pas encore UPnP/NAT-PMP, relais,
annuaire public, reconnexion ni synchronisation complète des combats, inventaires, quêtes, dialogues
et sauvegardes. Ces limites restent visibles et ne doivent pas être décrites comme une campagne
coopérative Internet entièrement qualifiée.

Le parcours complet, les prérequis routeur/pare-feu, le serveur headless, les codes de diagnostic et
la matrice exacte des fonctions raccordées sont documentés dans
[`MULTIPLAYER_INTERNET.md`](MULTIPLAYER_INTERNET.md).

### Distribution versionnée dans le dépôt

Le dossier `exe/` conserve les deux points d'entrée Windows livrables du dépôt :

- `OpenRPG_Forge_Studio.exe`, avec ses données Creator ;
- `Solaren_Les_Vallees_Libres.exe`, avec `branding/`, `game/`, `runtime/`, `game-launcher.json` et
  `game-distribution-manifest.json`.

Le manifeste Solaren courant couvre 227 fichiers et inclut explicitement le panorama solaire et la
musique WAV. `forge launcher verify exe` et `exe\Solaren_Les_Vallees_Libres.exe --verify` doivent tous
deux réussir avant publication. Les exécutables seuls ne sont pas portables hors de ce dossier et
restent `NotSigned` tant qu'aucun certificat Authenticode n'a été fourni.

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
