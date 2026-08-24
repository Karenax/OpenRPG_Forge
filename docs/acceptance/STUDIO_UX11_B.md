# Acceptation Studio — UX11-B, revue corrective finale

État local au 23 août 2026 : la revue humaine et visuelle transversale est **terminée** pour les
717 routes accessibles dans Studio et pour les 66 destinations de fenêtres historiques. Les paquets
Windows et Linux x64 sont qualifiés sur leur système cible : même workspace Crypte des Cendres,
retouche réelle, ForgeScript natif, ouverture autonome et parcours UX5-A/UX5-B. `STUDIO-043` est
clos techniquement sur les deux systèmes.

Cette qualification ne retire pas le plugin historique, ne bascule pas le lanceur Creator, ne crée
aucun commit et ne publie rien.

## Parcours humain correctif

Les quatorze scénarios transversaux suivants ont été rejoués dans l'application Godot Forward+ :

1. démarrage dans une seule fenêtre produit et ouverture depuis le Module Hub ;
2. création puis ouverture d'un module vide, avec état vide lisible ;
3. ouverture de Crypte des Cendres et restauration du module, de la zone et de la page ;
4. recherche globale `Ctrl+K`, navigation vers un trigger et retour au contexte ;
5. sélection d'une instance dans la 3D, translation et rotation par manipulation directe ;
6. retouche des propriétés dans l'inspecteur unique, avec aperçu avant application ;
7. ajout depuis la palette, duplication voisine et suppression confirmée ;
8. undo/redo, sauvegarde explicite, fermeture et réouverture sans perte ;
9. références inverses, diagnostic cliquable et ouverture de la cible exacte ;
10. dialogue édité ligne par ligne, le graphe restant une vue avancée ;
11. quête réelle sélectionnée dans Production narrative UX5, avec journal, états et transitions
    lisibles avant application ;
12. page ForgeScript et diagnostics vérifiés sans exposer la source technique comme parcours
    principal ;
13. **Tester ici** sur snapshot isolé, lancement Runtime Forward+ et retour au contexte Studio ;
14. inspection des 36 pages aux trois bornes de résolution et d'échelle retenues.

Les mutations humaines ont été réalisées dans un workspace de preuve isolé. Le module source reste
inchangé ; les contrôles de prévisualisation, approbation, undo/redo et persistance utilisent les
commandes canoniques existantes.

## Revue responsive

La seconde passe corrective contient 36 captures par configuration, soit 108 captures inspectées :

- Full HD 1920×1080 à 100 % :
  `artifacts/verification/studio-ux11-b/matrix-fhd-100-pass2/` ;
- QHD 2560×1440 à 150 % :
  `artifacts/verification/studio-ux11-b/matrix-qhd-150-pass2/` ;
- 4K 3840×2160 à 200 % :
  `artifacts/verification/studio-ux11-b/matrix-4k-200-pass2/`.

Les trois planches de contact `contact-matrix-*-pass2.jpg` ont été relues, puis Assets, Dialogues,
Quêtes, Scripts, Validation et Module ont été inspectés individuellement. Aucun contrôle principal
coupé, action masquée ou chevauchement bloquant n'a été conservé. La bibliothèque réelle reste
peuplée de 278 assets dans les captures Forward+.

## Parité et régressions

- `AURORA_STUDIO_ACCESSIBILITY.csv` : 717 IDs uniques, 717 `functional`, 0 `planned` ;
- `AURORA_STUDIO_PROOFS.csv` : 717 preuves spécifiques `functional` ;
- `CREATOR_WINDOW_MIGRATION.csv` : 66 lignes, 66 `Parity`, une destination unique par ligne ;
- tests `Forge.Studio.Core.Tests` : 19/19 ;
- smoke Studio global : `UX5A_PASS`, `UX5B_PASS`, `UX10_PASS` et
  `SHELL_PASS sections=8 pages=36 windows=1` ;
- UX5 : 69/69 routes, dialogue direct et production narrative complète qualifiés.
- portail transversal final : 2 117/2 117 tests .NET, 44/44 tests Python, builds sans
  avertissement, graphe Roslyn frais et déterministe à 11 577 nœuds / 90 175 relations / 0
  diagnostic, puis smokes Studio, Toolset, Runtime/Forward+, Crypte des Cendres, Blender, MCP,
  réseau et sauvegarde/reprise ; marqueur `OPENRPG_FORGE_VERIFICATION=PASS`.

La garde ajoutée à l'éditeur de conversation de placeable conserve le module vide ouvrable lorsqu'il
n'existe aucun choix de locuteur. Les libellés Trigger et l'action de recherche globale ont également
été corrigés pendant la passe humaine.

## Distributions locales

### Windows x64

`artifacts/distribution/studio-windows-x64/` contient 1 109 fichiers, l'assembly
`Forge.Studio.dll`, `forge_script_native.dll`, le workspace Crypte des Cendres, un manifeste SHA-256
et la conformité CycloneDX 1.6 avec 38 composants et 38 licences. L'exécutable exporté passe le
smoke de distribution : retouche ajout/duplication/suppression, undo/redo, cache script, ForgeScript
natif, ouverture autonome UX3-F, `UX5A_PASS`, `UX5B_PASS` et
`DISTRIBUTION_SMOKE_PASS source_unchanged=true`. Son état Authenticode reste `NotSigned` : il s'agit
d'un candidat local de développement.

### Linux x64

`artifacts/distribution/studio-linux-x64/` contient 1 108 fichiers, un ELF x86_64, l'assembly
`Forge.Studio.dll`, `libforge_script_native.so`, le même workspace, un manifeste SHA-256 et la même
conformité 38/38. Tous les fichiers, tailles et hashes passent `Verify-StudioDistribution.ps1` ;
Authenticode est correctement indiqué comme non applicable à cet ELF.

L'archive immuable `studio-linux-x64-final-v4.tar.gz` fait 197 735 722 octets et porte le SHA-256
`e9d4118a08895d48be8ec1f08b053a333b7f860bccabf5f51650838216f48593`. Elle a été exécutée dans une
VM Debian 13 x86_64 sous QEMU 11.1.0 TCG, sans éditeur Godot installé. Godot
`4.7.1.stable.mono.official.a13da4feb` retourne `0` après `EDIT_PASS`, `UX0I_PASS`, `UX2_LW_C_PASS`,
`UX3_F_PASS`, `UX5A_PASS`, `UX5B_PASS` et `DISTRIBUTION_SMOKE_PASS`. Le seul avertissement du journal
est l'exécution de la VM comme superutilisateur. Les dépendances système observées sont `libicu76`,
`fontconfig` et `fonts-dejavu-core`.

La preuve brute est
`artifacts/verification/studio-ux11-b/studio-linux-qemu-smoke.log` — 4 361 octets, SHA-256
`9ff9f938e482e98141042903a9751e618ff858909f49918de42a6e95384a069d` — et son résumé structuré est
`studio-linux-qemu-smoke.summary.json`. Le manifeste du cross-export conserve honnêtement
`smoke_tested=false` / `not-run-host-unavailable` : la preuve runtime externe qualifie l'archive
exacte sans falsifier le poste qui a construit le manifeste.

Le smoke de distribution correspond à `STUDIO-043`. Le portail développeur UX0–UX10 reste une
validation séparée depuis le dépôt ; il n'exige donc pas d'embarquer le dépôt, le CLI de build ou
l'éditeur Godot dans le produit autonome.

## État des critères de sortie

- `STUDIO-040` : **PASS**, 66 destinations uniques ;
- `STUDIO-041` : **PASS**, 66 lignes promues avec tests, smoke et preuve visuelle ;
- `STUDIO-042` : **préservé**, le plugin n'est pas retiré sans accord utilisateur ;
- `STUDIO-043` : **PASS Windows / PASS Linux runtime**.

## Reproduction

```powershell
dotnet test tests/Forge.Studio.Core.Tests/Forge.Studio.Core.Tests.csproj --no-restore
& .\scripts\Open-Studio.ps1 -Workspace apps\Forge.Runtime\content\crypt-of-ashes `
  -Headless -Smoke -SkipBuild -Wait
& .\scripts\Build-StudioDistribution.ps1 -Platform windows-x64 -Force
& .\scripts\Verify-StudioDistribution.ps1 `
  -DistributionDirectory artifacts\distribution\studio-windows-x64 -RunSmoke
& .\scripts\Build-StudioDistribution.ps1 -Platform linux-x64 -Force -SkipSmoke
& .\scripts\Verify-StudioDistribution.ps1 `
  -DistributionDirectory artifacts\distribution\studio-linux-x64
```

Sur un hôte Linux, reconstruire sans `-SkipSmoke` ou exécuter l'archive vérifiée avec :

```bash
./OpenRPG_Forge_Studio.x86_64 --headless --log-file studio-smoke.log -- \
  --workspace "$PWD/workspaces/crypt-of-ashes" --page placement \
  --studio-distribution-smoke-test
```
