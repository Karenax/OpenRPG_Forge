# Acceptation Studio — UX1-Q Clé consommée

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-PLC-013`. Ce lot rend la
politique de justificatif d’un placeable directement éditable dans l’unique fenêtre `Forge.Studio`,
sans confondre le moyen d’ouverture du verrou avec le devenir de la clé après usage. Il ne qualifie
aucune autre route placeable ni une distribution publique.

## Preuves communes

- build Debug `Forge.Studio` : zéro avertissement et zéro erreur ;
- tests ciblés : résolveur Domain 22/22, adaptateur RPG Kit 7/7, validation Application 1/1,
  ModuleFormat 1/1, MCP 1/1 et conservation des 66 fenêtres historiques 2/2, soit 34/34 ;
- smoke Godot 4.7.1 .NET du Studio :
  `OPENRPG_FORGE_STUDIO_UX1Q_PASS policy_modes=true consumption_decisions=true
  virtual_permission=true credential_preview=true dry_run=true command_parity=true
  instance_selection=true preservation=true runtime_projection=true script_read=true
  optional_required=true undo_redo=true persistence=true invalid_blocked=true human_no_uuid=true
  ui=true ui_transaction=true project_restored=true windows=1` ;
- le même lancement conserve tous les lots Studio antérieurs au vert puis termine par
  `OPENRPG_FORGE_STUDIO_SHELL_PASS sections=8 pages=36 windows=1` ;
- le smoke contractuel crée un workspace temporaire avec un placeable, une clé item réelle, un
  verrou préexistant, un voisin et deux instances ; il exerce les cinq politiques, les décisions
  autorisée/refusée, le dry-run direct et la commande canonique, puis applique, annule, rétablit,
  sauvegarde et rouvre ;
- le smoke UI utilise le vrai catalogue de Forgotten Forest, applique uniquement en mémoire puis
  annule et restaure exactement le projet courant ; aucune politique de preuve n’est sauvegardée ;
- captures et journaux Forward+ inspectés dans `artifacts/verification/studio-shell/ux1-q/` :
  `policy-fhd.png` et `denied-fhd.png` en 1920×1080, `charge-qhd.png` en 2560×1440 et
  `virtual-4k.png` en 3840×2160, chacune accompagnée de `.png.log`.

Les quatre captures utilisent Godot 4.7.1, Vulkan Forward+, une Radeon RX 7800 XT, 278 assets,
zéro fallback et une seule fenêtre. Elles montrent le blueprint réel **Décor de la forêt**, le
visuel **Floor Wood Large** et la clé item **Graine de lune** du catalogue Forgotten Forest. Full HD
confirme la conservation et le refus sans justificatif ; QHD rend le débit d’une charge et son solde
lisibles ; 4K confirme la permission virtuelle, l’audit et l’aperçu runtime. Cette inspection réelle
porte sur le catalogue et l’interface ; la persistance canonique reste qualifiée par le workspace
synthétique afin de ne pas modifier le module authored.

### ORF-PLC-013

La page **Objets & interactions** expose désormais **Clé consommée** à côté de **Verrouillage**.
`Ctrl+K` fournit aussi **Configurer la clé consommée**. Les deux entrées ouvrent la même fiche
canonique, sans UUID, hash ni chemin de fichier dans la surface humaine.

L’auteur choisit **Conserver le justificatif**, **Consommer après usage**, **Dépenser une charge**,
**Partager sans consommer** ou **Vérifier une permission virtuelle**. Une clé réelle se choisit dans
le catalogue d’items ; l’alternative métier reste disponible pour les données importées et se
désactive lorsqu’un objet est déjà choisi. Les charges n’apparaissent comme éditables que pour leur
politique dédiée, tandis que la permission virtuelle possède son champ propre. L’audit et le contrat
requis au runtime sont explicites.

**Prévisualiser l’accès** utilise `PlaceableCredentialRuntimeAdapter` et
`WorldFeatureRuntimeResolver.ResolvePlaceableAccess`. Le résultat humain indique accès autorisé ou
refusé, conservation, consommation, charge restante, partage ou permission reconnue. Cet aperçu ne
retire aucun objet, ne débite aucun inventaire et n’écrit pas le module. Le runtime consommateur reste
responsable d’appliquer la décision à l’inventaire autoritaire.

**Prévisualiser** appelle `SetWorldFeatureCatalogHandler` avec `DryRun=true`. **Appliquer au module**
réutilise le handler à la révision courante et rejoint `ProjectCommandHistory`, donc undo/redo et
sauvegarde explicite restent atomiques. Un profil existant ne peut pas être retiré accidentellement
depuis une autre fiche placeable ; identité, provenance, fondation, opérations, verrou, événements,
voisins, items et instances non édités sont préservés.

Le domaine refuse une identité vide, une référence `item:` inconnue, un nombre de charges négatif,
une politique à charges sans charge, une permission virtuelle vide et l’absence d’un profil déclaré
requis. Les diagnostics canoniques sont humanisés en français. Le drapeau
`CredentialRequiredAtRuntime` est facultatif par défaut, ce qui préserve la compatibilité des anciens
modules ; lorsqu’il est actif, l’absence produit `WORLD_PLACEABLE_CREDENTIAL_REQUIRED_MISSING` au
lieu d’un accès libre.

`PlaceableCredentialRuntimeAdapter` fournit une résolution optionnelle/requise, une projection typée
et un handle qui devient invalide si l’identité, le mode, les charges, la permission ou l’audit ont
changé. Les décisions `Retain`, `Consume`, `Charge`, `Shared` et `VirtualPermission` restent celles du
résolveur canonique historique.

`WorldFeatureScriptService`, donc la ressource MCP filtrée, expose `credentialId`,
`credentialMode`, `credentialCharges`, `credentialVirtualPermission`, `credentialAudit` et
`credentialRequiredAtRuntime`. Le plan MCP demeure un dry-run et n’exécute aucune mutation
d’inventaire.

## Limites honnêtes

- ce lot qualifie seulement `ORF-PLC-013` ; les pièges `ORF-PLC-014` et les événements placeable
  restent portés par leurs routes dédiées ;
- la prévisualisation calcule la décision de consommation, mais ne fournit pas un nouveau service
  d’inventaire réseau ni une transaction multijoueur ;
- les captures valident l’interface avec le catalogue réel de Forgotten Forest, pas une nouvelle
  politique authored et sauvegardée dans Forgotten Forest ou Crypt of Ashes ;
- l’application demeure en mémoire jusqu’à l’enregistrement explicite ;
- la migration des 66 fenêtres, l’export autonome Windows/Linux et le retrait du plugin historique
  restent hors de ce lot.

## Reproduction

```powershell
dotnet format OpenRPGForge.sln --no-restore --verify-no-changes
dotnet test tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~WorldFeatureRuntimeResolverTests"
dotnet test tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~PlaceableCredentialRuntimeAdapterTests"
dotnet test tests/Forge.Application.Tests/Forge.Application.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~PlaceableCredentialValidation"
dotnet test tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~Aggregate_and_workspace_round_trips_preserve_all_world_feature_families"
dotnet test tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~Placeable_foundation_resource_is_filtered_typed_and_plan_is_dry_run"
dotnet test tests/Forge.Studio.Core.Tests/Forge.Studio.Core.Tests.csproj --no-restore `
  --filter "FullyQualifiedName~LegacyWindowMigrationTests"
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-q-charge -Headless -Smoke -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-q-policy -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-shell/ux1-q/policy-fhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-q-charge -Resolution 2560x1440 `
  -Capture artifacts/verification/studio-shell/ux1-q/charge-qhd.png -Wait
& .\scripts\Open-Studio.ps1 -Page placement -Evidence ux1-q-virtual -Resolution 3840x2160 `
  -Capture artifacts/verification/studio-shell/ux1-q/virtual-4k.png -Wait
& .\scripts\Update-StudioAuroraAccessibilityPlan.ps1 -Check
```

Avec UX0-A à UX1-Q, 61 fonctions disposent d’une preuve Studio spécifique ; les 656 autres lignes
restent explicitement `planned`.
