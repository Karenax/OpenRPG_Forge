# Acceptation Studio — UX1-H Conteneur

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-PLC-004`. Ce lot rend le profil
de conteneur directement éditable dans l’unique fenêtre `Forge.Studio`. Il ne qualifie aucune autre
route placeable ni une distribution publique.

## Preuves communes

- build `Forge.Studio` : zéro avertissement et zéro erreur ;
- tests ciblés : Application R093 1/1, adaptateur RPG Kit 5/5, ModuleFormat 5/5 et MCP 1/1,
  soit 12/12 ;
- `dotnet format apps/Forge.Studio/Forge.Studio.csproj --no-restore --verify-no-changes` réussi ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX1H_PASS container_component=true capacity=true ownership_access=true
  persistence_replication=true references=true dry_run=true command_parity=true
  instance_selection=true preservation=true runtime_projection=true script_read=true
  optional_required=true undo_redo=true persistence=true invalid_blocked=true human_no_uuid=true
  ui=true ui_transaction=true project_restored=true windows=1` ;
- le même lancement termine par `OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1` ;
- le smoke contractuel crée un workspace temporaire avec deux instances, modifie un conteneur
  existant, compare le brouillon direct à la commande canonique, applique, annule, rétablit,
  sauvegarde et rouvre le document ;
- le smoke UI applique un conteneur depuis l’overlay réel de Crypte des Cendres puis annule et
  restaure exactement le projet courant ;
- journal headless : `artifacts/verification/studio-shell/ux1-h/headless-smoke.log` ;
- captures et journaux Forward+ inspectés dans `artifacts/verification/studio-shell/ux1-h/` :
  `capacity-fhd.png` en 1920×1080, `access-qhd.png` en 2560×1440 et `references-4k.png` en
  3840×2160, chacune accompagnée de `.png.log`.

Les trois captures chargent 278 assets sans fallback, conservent une fenêtre unique et finissent
avec un code de sortie nul. Full HD montre capacité, masse, propriétaire, droits et persistance.
QHD montre le sélecteur de propriétaire, les trois droits, la réplication et les références. 4K
montre les références, l’exigence runtime, la transformation et le résumé avant application. Le
formulaire reste explicitement défilant et les actions principales ne sont pas coupées.

### ORF-PLC-004

La page **Objets & interactions** expose désormais **Conteneur** à côté de **Blueprint de décor**,
**Décor statique** et **Objet utilisable**. `Ctrl+K` fournit également **Configurer un conteneur**.
Cette entrée active le composant et place le profil spécialisé au premier plan sans afficher
d’UUID, de fichier généré ni de hash.

Le formulaire édite le nombre d’emplacements, la masse maximale, le propriétaire, les droits
ouvrir/déposer/retirer, la conservation dans les sauvegardes, la réplication multijoueur, la
référence d’inventaire, la politique de butin, le profil de présentation et l’exigence du profil
d’opérations au runtime. Un état vide explicite initialise des valeurs sûres ; un profil existant
annonce que ses capacités voisines et permissions supplémentaires seront conservées.

**Prévisualiser** appelle `SetWorldFeatureCatalogHandler` avec `DryRun=true`. Le résumé expose les
limites, le propriétaire, les droits, la persistance, la réplication et les trois références sans
écrire le module. **Voir les instances en 3D** synchronise les instances réelles du blueprint avec
la sélection et le gizmo. L’application réutilise le même handler à la révision courante et rejoint
`ProjectCommandHistory`.

Le smoke compare le `PlaceableContainerProfile` produit directement à celui de la commande
appliquée. `PlaceableOperationsRuntimeAdapter` projette ensuite exactement capacité, masse,
propriétaire, accès, persistance, butin, interface, réplication et inventaire, sans diagnostic.
`WorldFeatureScriptService` expose les mêmes valeurs par handle typé. L’absence optionnelle du
profil produit un repli propre ; son absence lorsqu’il est requis produit
`WORLD_PLACEABLE_OPERATIONS_REQUIRED_MISSING`.

Les données non affichées restent conservées : permission supplémentaire `container.audit`, états
visuels, dégâts, protection, verrou, localisation anglaise, meshes, matériaux, VFX, scripts,
overrides, faction, credential et événements réactifs. Lorsque l’inventaire change, seule son
ancienne référence est remplacée dans la validation runtime ; les autres inventaires et budgets
restent intacts. Undo/redo restaure les références exactes et le round-trip conserve l’ensemble.

Pour un placeable sans agrégat R093, le Studio crée les états standards et une protection
non bloquante déterministes exigés par le validateur d’agrégat. Ces valeurs de compatibilité ne
rendent ni `ORF-PLC-007` ni `ORF-PLC-009` fonctionnels : aucune surface d’auteur ni preuve dédiée
n’est livrée pour ces routes dans UX1-H.

Trois invalidités sont refusées avant mutation avec des messages français sans UUID, hash ni chemin
interne : zéro emplacement, aucun droit et référence d’inventaire vide.

## Limites honnêtes

- ce lot qualifie seulement `ORF-PLC-004` ; identité/apparence, transformation précise et état
  initial d’animation restent `planned` sous `ORF-PLC-005`, `ORF-PLC-006` et `ORF-PLC-007` ;
- les références d’inventaire, de butin et d’interface restent des contrats existants ; UX1-H ne
  crée pas encore leur contenu depuis cette fiche ;
- l’application demeure en mémoire jusqu’à l’enregistrement explicite ;
- la migration des 66 fenêtres, l’export autonome Windows/Linux et le retrait du plugin historique
  restent hors de ce lot.

## Reproduction

```powershell
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~R093_PlaceableOperations"
dotnet test tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~PlaceableOperationsRuntimeAdapterTests"
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~WorldFeaturePersistenceTests"
dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~Placeable_foundation_resource_is_filtered_typed_and_plan_is_dry_run"
dotnet format apps/Forge.Studio/Forge.Studio.csproj --no-restore --verify-no-changes
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-h-capacity -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-h-capacity -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-shell/ux1-h/capacity-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-h-access -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux1-h/access-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-h-references -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux1-h/references-4k.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX1-H, 52 fonctions disposent d’une preuve Studio spécifique ; les 665 autres lignes
restent explicitement `planned`.
