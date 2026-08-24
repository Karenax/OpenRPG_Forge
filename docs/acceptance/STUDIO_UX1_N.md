# Acceptation Studio — UX1-N Faction et propriété

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-PLC-010`. Ce lot rend le
rattachement d’un placeable à une faction, ses appartenances partagées et leurs effets runtime
directement configurables dans l’unique fenêtre `Forge.Studio`. Il ne qualifie aucune autre route
placeable ni une distribution publique.

## Preuves communes

- build `Forge.Studio` après formatage limité aux sources UX1-N : zéro avertissement et zéro erreur ;
- tests ciblés : validation Application 1/1, runtime RPG Kit 4/4, ModuleFormat 1/1, MCP 1/1 et
  conservation des 66 fenêtres historiques 2/2, soit 9/9 ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX1N_PASS primary_faction=true memberships=true hostility_crime=true
  access_targeting=true decision_preview=true dry_run=true command_parity=true
  instance_selection=true preservation=true runtime_projection=true script_read=true
  optional_required=true undo_redo=true persistence=true invalid_blocked=true human_no_uuid=true
  ui=true ui_transaction=true project_restored=true windows=1` ;
- le même lancement conserve UX1-E à UX1-M au vert, prépare une preuve valide puis termine par
  `OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1` ;
- le smoke contractuel crée un workspace temporaire avec deux placeables, deux instances et trois
  factions directionnelles ; il refuse les identités, appartenances et références invalides,
  compare le dry-run direct à la commande canonique, applique, annule, rétablit, sauvegarde puis
  rouvre ;
- le smoke UI ajoute seulement en mémoire un catalogue de trois factions de preuve lorsque le
  module courant n’en possède pas, applique puis annule la fiche et le catalogue, et restaure
  exactement le projet courant ; aucune faction de preuve n’est sauvegardée dans Crypte des Cendres ;
- captures et journaux Forward+ inspectés dans `artifacts/verification/studio-shell/ux1-n/` :
  `memberships-fhd.png` en 1920×1080, `policies-qhd.png` en 2560×1440 et `decision-4k.png` en
  3840×2160, chacune accompagnée de `.png.log`.

Les trois captures utilisent Godot 4.7.1, Vulkan Forward+, une Radeon RX 7800 XT, 278 assets, zéro
fallback et une seule fenêtre. Elles emploient le catalogue synthétique en mémoire, distinct d’une
qualification visuelle de contenu factionnel réellement authored. Full HD montre la faction
principale et les appartenances humaines. QHD rend les quatre effets de propriété lisibles. 4K
montre un acteur non membre, l’action **Détruire l’objet** et la décision complète : relation −75
hostile, crime signalé, accès refusé et ciblage autorisé, sans coupe d’action principale.

### ORF-PLC-010

La page **Objets & interactions** expose désormais **Faction** à côté des autres fiches placeable.
`Ctrl+K` fournit aussi **Configurer la faction du placeable**. Les deux entrées ouvrent le même
document canonique, sans UUID, hash ni chemin de fichier dans la surface humaine.

Le formulaire choisit la faction principale par son nom et affiche les appartenances sous forme de
cases à cocher. La faction principale reste obligatoirement membre. Une faction archivée déjà
référencée reste visible et conservée, mais ne peut pas être choisie pour un nouveau rattachement.
Le Studio conserve le champ Aurora historique `FactionId` sans le réinterpréter ni le réécrire ; le
nouveau profil typé `PlaceableFactionOwnershipProfile` porte une identité stable, la provenance, la
faction principale, les appartenances et les quatre politiques.

Les effets configurables sont : hériter de l’hostilité entre factions, signaler les actions non
autorisées comme crime, réserver l’accès aux factions membres et bloquer le ciblage offensif des
alliés. L’**Aperçu de relation et d’autorisation** choisit une faction d’acteur et une action humaine,
puis projette appartenance, score et bande de relation, hostilité, crime, accès et ciblage sans
écrire le module. La décision détaillée est également répétée dans la carte latérale afin de rester
lisible aux trois résolutions.

**Prévisualiser** appelle `SetWorldFeatureCatalogHandler` avec `DryRun=true`. **Appliquer au module**
réutilise le handler à la révision courante et rejoint `ProjectCommandHistory`, donc undo/redo et
sauvegarde explicite restent atomiques. Une fiche de faction existante ne peut pas être retirée
accidentellement depuis ce formulaire.

Le domaine exige un identifiant stable, une faction principale active incluse dans au moins une
appartenance, des appartenances uniques et des références présentes dans le catalogue global. Les
identités partagées avec une autre capacité placeable, les références inconnues et l’absence d’un
profil requis au runtime sont localisées par des diagnostics canoniques puis humanisées en français.

`PlaceableFactionRuntimeAdapter` conserve le repli optionnel et bloque l’absence requise. Son runtime
projette les six valeurs typées. `PlaceableFactionService` résout la relation directionnelle : un
membre obtient les droits d’accès, un ennemi peut devenir hostile et criminel, et une attaque alliée
peut être refusée sans bloquer le ciblage d’un ennemi. Le smoke exerce réellement ces branches.

`WorldFeatureScriptService`, donc la ressource MCP filtrée, expose `primaryFaction`,
`factionMemberships`, `inheritFactionHostility`, `unauthorizedFactionActionsAreCrime`,
`factionMembersOnlyAccess` et `blockFriendlyFactionTargeting`. Le champ historique `faction` reste
inchangé et le plan MCP reste un dry-run.

Les données hors périmètre restent conservées : identité et provenance du placeable, faction Aurora
historique, événements réactifs, fondation, opérations, placeables voisins et catalogue global de
factions. Un profil typé inchangé conserve son objet canonique ; un profil édité conserve son
identité stable et le profil complet survit au round-trip.

## Limites honnêtes

- ce lot qualifie seulement `ORF-PLC-010` ; la conversation `ORF-PLC-011`, les événements et le
  verrouillage restent portés par leurs routes dédiées ;
- UX1-N projette les effets canoniques de propriété, mais n’ajoute ni réaction audiovisuelle de
  crime, ni console de réputation, ni synchronisation réseau de faction ;
- les captures valident l’interface avec des factions synthétiques en mémoire, pas une mise en scène
  factionnelle authored de Crypte des Cendres ou de Crypte des Cendres ;
- l’application demeure en mémoire jusqu’à l’enregistrement explicite ;
- la migration des 66 fenêtres, l’export autonome Windows/Linux et le retrait du plugin historique
  restent hors de ce lot.

## Reproduction

```powershell
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~PlaceableFactionOwnershipValidation"
dotnet test tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~PlaceableFactionRuntimeAdapterTests"
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~WorldFeaturePersistenceTests.Aggregate_and_workspace_round_trips"
dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~Placeable_foundation_resource_is_filtered_typed_and_plan_is_dry_run"
dotnet test tests/Forge.Studio.Core.Tests/Forge.Studio.Core.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~LegacyWindowMigrationTests"
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-n-memberships -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-n-memberships -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-shell/ux1-n/memberships-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-n-policies -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux1-n/policies-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-n-decision -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux1-n/decision-4k.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX1-N, 58 fonctions disposent d’une preuve Studio spécifique ; les 659 autres lignes
restent explicitement `planned`.
