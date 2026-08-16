# Acceptation Studio — UX1-P Verrouillage

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-PLC-012`. Ce lot rend le
verrouillage d’un placeable directement éditable dans l’unique fenêtre `Forge.Studio`, en réutilisant
le profil canonique Aurora et son exécution RPG Kit. Il ne qualifie aucune autre route placeable ni
une distribution publique.

## Preuves communes

- `dotnet format OpenRPGForge.sln --no-restore --verify-no-changes` réussit sans changement requis ;
- build Debug `Forge.Studio` : zéro avertissement et zéro erreur ;
- tests ciblés : validation Application 1/1, runtime RPG Kit 4/4, ModuleFormat 5/5, MCP 1/1 et
  conservation des 66 fenêtres historiques 2/2, soit 13/13 ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX1P_PASS states_mechanisms=true unlock_methods=true relock_jam=true
  lock_preview=true dry_run=true command_parity=true instance_selection=true preservation=true
  runtime_projection=true script_read=true optional_required=true undo_redo=true persistence=true
  invalid_blocked=true human_no_uuid=true ui=true ui_transaction=true project_restored=true
  windows=1` ;
- le même lancement conserve tous les lots Studio antérieurs au vert puis termine par
  `OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1` ;
- le smoke contractuel crée un workspace temporaire avec un placeable, une clé item réelle et deux
  instances ; il exerce les quatre états, six moyens d’ouverture, le reverrouillage et le brouillage,
  refuse les profils invalides, compare le dry-run direct à la commande canonique, applique, annule,
  rétablit, sauvegarde puis rouvre ;
- le smoke UI utilise le vrai catalogue de Forgotten Forest, applique uniquement en mémoire puis
  annule et restaure exactement le projet courant ; aucun verrou de preuve n’est sauvegardé ;
- captures et journaux Forward+ inspectés dans `artifacts/verification/studio-shell/ux1-p/` :
  `mechanisms-fhd.png` en 1920×1080, `lifecycle-qhd.png` en 2560×1440 et `jam-4k.png` en
  3840×2160, chacune accompagnée de `.png.log`.

Les trois captures utilisent Godot 4.7.1, Vulkan Forward+, une Radeon RX 7800 XT, 278 assets, zéro
fallback et une seule fenêtre. Elles montrent le blueprint réel **Décor de la forêt**, le visuel
**Floor Wood Large** et la clé item **Graine de lune** du catalogue Forgotten Forest. Full HD cadre
l’état initial, la clé, les justificatifs et le défi. QHD rend le code masqué et le cycle de vie
lisibles. 4K confirme **Brouiller après ouverture**, le résultat runtime **Brouillé au chargement**
et l’action principale sans coupe. Cette inspection réelle porte sur le catalogue et l’interface ;
la persistance canonique reste qualifiée par le workspace synthétique afin de ne pas modifier le
module authored.

### ORF-PLC-012

La page **Objets & interactions** expose désormais **Verrouillage** à côté des autres fiches
placeable. `Ctrl+K` fournit aussi **Configurer le verrouillage du placeable**. Les deux entrées
ouvrent le même document canonique, sans UUID, hash ni chemin de fichier dans la surface humaine.

L’auteur choisit l’état au chargement parmi **Déverrouillé**, **Verrouillé**, **Brouillé** et
**Brisé**. Il peut sélectionner une clé dans le vrai catalogue d’items, conserver d’autres
justificatifs, configurer une compétence et sa difficulté, accepter un code, autoriser le piratage,
le reverrouillage et le brouillage, puis choisir les droits reconnus. Les valeurs techniques déjà
présentes mais inconnues de cette surface sont préservées.

Le code est saisi dans un champ secret. Sa valeur n’est jamais affichée dans l’aperçu, le résumé,
ForgeScript ou MCP ; seule une empreinte `sha256:` est persistée. Un hash existant est conservé si
l’auteur laisse le champ vide. Ce mécanisme protège la surface d’édition, mais ne constitue pas un
coffre de secrets ni un protocole d’authentification réseau.

**Prévisualiser la tentative** résout réellement le même `PlaceableLockProfile` que le runtime sans
écrire le module. Les six chemins d’ouverture sont exercés : clé ou justificatif, compétence, code,
piratage, permission et dérogation MJ. Les cycles de reverrouillage et de brouillage utilisent le
même composant canonique. Le résultat humain indique l’état obtenu et la raison, sans exposer
d’identifiant ni de hash.

**Prévisualiser** appelle `SetWorldFeatureCatalogHandler` avec `DryRun=true`. **Appliquer au module**
réutilise le handler à la révision courante et rejoint `ProjectCommandHistory`, donc undo/redo et
sauvegarde explicite restent atomiques. Une fiche de verrouillage existante ne peut pas être retirée
accidentellement depuis une autre fiche placeable.

Le domaine exige une identité stable, une difficulté comprise entre 0 et 1000, des justificatifs et
droits uniques, une clé qui référence un blueprint item réel, un code lorsque ce mécanisme est
activé, une compétence pour le piratage et au moins un moyen d’ouverture. Les identités partagées
avec une autre capacité placeable, les références inconnues et l’absence d’un profil requis au
runtime sont localisées par des diagnostics canoniques puis humanisées en français.

`PlaceableLockRuntimeAdapter` conserve le repli optionnel et bloque l’absence requise. Son handle
typé devient invalide après une révision du catalogue. La projection transporte l’état, les
justificatifs, la compétence, la difficulté, la présence d’un code, le piratage, le reverrouillage,
le brouillage et les permissions sans recopier le secret.

`WorldFeatureScriptService`, donc la ressource MCP filtrée, expose `lockState`,
`lockCredentialIds`, `lockSkill`, `lockDifficulty`, `lockHasCode`, `lockHackable`,
`lockRelockable`, `lockCanJam` et `lockPermissions`. Le champ résumé historique `lock` reste
disponible et le plan MCP demeure un dry-run.

Les données hors périmètre restent conservées : identité et provenance du placeable, fondation,
état visuel, protection, faction, conversation, événements réactifs, placeables voisins, items et
instances. Un profil de verrouillage inchangé conserve son objet canonique ; un profil édité garde
son identité stable et le profil complet survit au round-trip.

## Limites honnêtes

- ce lot qualifie seulement `ORF-PLC-012` ; la consommation de clé `ORF-PLC-013`, les pièges et les
  événements restent portés par leurs routes dédiées ;
- UX1-P ne fournit ni mini-jeu physique de crochetage, ni coffre de mots de passe, ni nouvelle
  synchronisation réseau ;
- les captures valident l’interface avec le catalogue réel de Forgotten Forest, pas un nouveau
  verrou authored et sauvegardé dans Forgotten Forest ou Crypt of Ashes ;
- l’application demeure en mémoire jusqu’à l’enregistrement explicite ;
- la migration des 66 fenêtres, l’export autonome Windows/Linux et le retrait du plugin historique
  restent hors de ce lot.

## Reproduction

```powershell
dotnet format OpenRPGForge.sln --no-restore --verify-no-changes
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~R093_PlaceableOperationsValidation"
dotnet test tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~PlaceableLockRuntimeAdapterTests"
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~WorldFeaturePersistenceTests"
dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~Placeable_foundation_resource_is_filtered_typed_and_plan_is_dry_run"
dotnet test tests/Forge.Studio.Core.Tests/Forge.Studio.Core.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~LegacyWindowMigrationTests"
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-p-mechanisms -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-p-mechanisms -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-shell/ux1-p/mechanisms-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-p-lifecycle -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux1-p/lifecycle-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-p-jam -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux1-p/jam-4k.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX1-P, 60 fonctions disposent d’une preuve Studio spécifique ; les 657 autres lignes
restent explicitement `planned`.
