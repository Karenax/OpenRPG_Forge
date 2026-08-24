# Acceptation Studio — UX1-M Protection plot

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-PLC-009`. Ce lot rend les
actions protégées, les droits de contournement, le feedback joueur et la dérogation MJ auditée
directement configurables dans l’unique fenêtre `Forge.Studio`. Il ne qualifie aucune autre route
placeable ni une distribution publique.

## Preuves communes

- build `Forge.Studio` après formatage limité aux sources UX1-M : zéro avertissement et zéro erreur ;
- tests ciblés : Application R093 1/1, RPG Kit Placeable Operations 5/5, ModuleFormat 1/1, MCP 1/1
  et conservation des 66 fenêtres historiques 2/2, soit 10/10 ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX1M_PASS protected_actions=true permissions=true feedback=true
  gm_override=true audit_trail=true policy_preview=true dry_run=true command_parity=true
  instance_selection=true preservation=true runtime_enforcement=true script_read=true
  optional_required=true undo_redo=true persistence=true invalid_blocked=true human_no_uuid=true
  ui=true ui_transaction=true project_restored=true windows=1` ;
- le même lancement conserve UX1-H, UX1-I, UX1-J, UX1-K et UX1-L au vert, prépare une preuve valide
  puis termine par `OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1` ;
- le smoke contractuel crée un workspace temporaire avec deux placeables et deux instances, refuse
  les politiques incomplètes ou ambiguës, compare le dry-run direct à la commande canonique,
  applique, annule, rétablit, sauvegarde puis rouvre ;
- le smoke UI crée réellement une protection plot dans Crypte des Cendres, applique puis annule en
  restaurant exactement le projet courant ;
- captures et journaux Forward+ inspectés dans `artifacts/verification/studio-shell/ux1-m/` :
  `actions-fhd.png` en 1920×1080, `permissions-qhd.png` en 2560×1440 et `override-4k.png` en
  3840×2160, chacune accompagnée de `.png.log`.

Les trois captures utilisent Godot 4.7.1, Vulkan Forward+, une Radeon RX 7800 XT, 278 assets, zéro
fallback et une seule fenêtre. Full HD montre les cinq actions humaines, les droits et le feedback.
QHD rend les droits scénario/maintenance, le feedback et l’audit lisibles. 4K montre la dérogation
MJ, le motif obligatoire, l’audit gouverné et la décision prévisualisée, sans coupe d’action
principale.

### ORF-PLC-009

La page **Objets & interactions** expose désormais **Plot** à côté des autres fiches placeable.
`Ctrl+K` fournit aussi **Configurer la protection plot**. Les deux entrées ouvrent le même document
canonique, sans UUID, hash ni fichier généré.

Le formulaire propose cinq actions humaines — destruction, déplacement, utilisation, ouverture et
changement de verrou — ainsi que des actions personnalisées conservées. Les droits **Responsable du
scénario**, **Maintenance du module** et **Administration du module** sont complétés par des droits
personnalisés conservés. Le message de refus est éditable. La dérogation du maître de jeu reste
séparée, exige un motif dans l’aperçu et conserve son binding d’audit gouverné sans l’exposer comme
une clé technique à modifier.

L’**Aperçu d’autorisation et d’audit** évalue localement quatre voies sans écrire le module : objet
non protégé, droits complets, dérogation MJ auditée ou refus accompagné du feedback joueur.
**Prévisualiser** appelle `SetWorldFeatureCatalogHandler` avec `DryRun=true`. **Appliquer au module**
réutilise le handler à la révision courante et rejoint `ProjectCommandHistory`, donc undo/redo et
sauvegarde explicite restent atomiques.

Le domaine exige un identifiant de profil stable, au moins une action protégée unique, au moins un
droit requis unique, un feedback non vide et un binding d’audit. Les listes vides ou dupliquées et
les valeurs manquantes sont refusées avec des diagnostics Studio en français, sans identifiant
technique. Une politique déjà présente ne peut pas être retirée accidentellement depuis cette fiche.
L’exigence runtime est synchronisée avec les autres opérations placeable canoniques.

`PlaceableSecurityService.Authorize` laisse passer une action non protégée, autorise un acteur qui
possède tous les droits requis, autorise et journalise une dérogation MJ lorsque la politique le
permet, et bloque sinon avec le feedback configuré. Le smoke exerce réellement ces quatre branches
et vérifie le motif d’audit. `WorldFeatureScriptService`, donc la ressource MCP filtrée, expose
`protectedActions`, `protectionPermissions`, `protectionFeedback`, `allowGameMasterOverride` et
`protectionAudit` ; le plan MCP reste un dry-run. L’absence optionnelle des opérations produit un
repli propre et leur absence requise produit le diagnostic canonique.

Les données hors périmètre restent conservées : identité et provenance du profil, faction,
événements réactifs, textes, tags, apparence, collision, rôle statique, composants, scripts,
overrides, conteneur, machine d’état, destructibilité, verrou et placeables voisins. Un profil
inchangé conserve même son objet canonique, et le profil complet survit au round-trip.

## Limites honnêtes

- ce lot qualifie seulement `ORF-PLC-009` ; la faction `ORF-PLC-010`, les événements et le
  verrouillage restent portés par leurs routes dédiées ;
- UX1-M modélise et simule le contrat d’autorisation/audit, mais n’ajoute ni console MJ réseau ni
  authentification de rôle ;
- l’application demeure en mémoire jusqu’à l’enregistrement explicite ;
- la migration des 66 fenêtres, l’export autonome Windows/Linux et le retrait du plugin historique
  restent hors de ce lot.

## Reproduction

```powershell
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~R093_PlaceableOperationsValidation"
dotnet test tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~PlaceableOperationsRuntimeAdapterTests"
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~WorldFeaturePersistenceTests.Aggregate_and_workspace_round_trips"
dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~Placeable_foundation_resource_is_filtered_typed_and_plan_is_dry_run"
dotnet test tests/Forge.Studio.Core.Tests/Forge.Studio.Core.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~LegacyWindowMigrationTests"
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-m-actions -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-m-actions -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-shell/ux1-m/actions-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-m-permissions -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux1-m/permissions-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-m-override -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux1-m/override-4k.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX1-M, 57 fonctions disposent d’une preuve Studio spécifique ; les 660 autres lignes
restent explicitement `planned`.
