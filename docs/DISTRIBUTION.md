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
