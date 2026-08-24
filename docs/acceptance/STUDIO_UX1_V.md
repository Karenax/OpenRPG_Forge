# Acceptation Studio — UX1-V Événements de cycle de vie du placeable

État local au 16 août 2026 : **fonctionnel et qualifié** pour `ORF-PLC-018`, `ORF-PLC-019` et
`ORF-PLC-020`. Ce lot rend `OnUsed`, `OnDisturbed`, `OnDamaged` et `OnDeath` directement
configurables et prévisualisables dans l'unique fenêtre `Forge.Studio`. Il ne qualifie ni les
événements physiques `ORF-PLC-021` à `ORF-PLC-024`, ni une distribution publique.

## Preuves communes

- portail incrémental `scripts/Verify-StudioIncrement.ps1` réussi sur la page `placement` et la
  preuve `ux1-v-death`, avec résumé dans
  `artifacts/verification/studio-increment/ux1-v-death.summary.json` ;
- tests ciblés séquentiels : RPG Kit 14/14, Application 2/2, ModuleFormat 4/4 et MCP 1/1, soit
  21/21 ; le portail a duré 63,75 s, dont 2,30 s, 2,48 s, 4,40 s et 5,09 s de tests, 44,02 s de
  smoke headless, 4,59 s de capture et 0,82 s de contrôle de matrice ;
- portail transversal `scripts/Verify.ps1` réussi après gel de la source : 1 980/1 980 tests .NET,
  39/39 tests Python, builds sans avertissement, smokes Studio/runtime/réseau/MCP, assets, package
  et distribution ;
- smoke Studio headless et capture Forward+ Full HD réussis sur Crypte des Cendres, avec preuve
  visuelle inspectée dans `artifacts/verification/studio-shell/ux1-v/death-fhd.png` et journal
  associé `death-fhd.png.log` ;
- la capture 1920×1080 utilise Godot 4.7.1 .NET, Vulkan Forward+, une Radeon RX 7800 XT, 278 assets,
  zéro fallback et une seule fenêtre ; elle montre une surface métier lisible pour
  `OnUsed`/`OnDisturbed`/`OnDamaged`/`OnDeath`, sans UUID ni JSON dans le parcours principal ;
- ce lot ne modifiant ni layout global, ni densité générale du shell, les revues QHD et 4K restent
  réservées au prochain jalon multi-lots UX1.

### ORF-PLC-018

La page **Objets & interactions** expose désormais **Événements du placeable** à côté des autres
capacités de blueprint. `Ctrl+K` et l'action de page ouvrent la même fiche pour configurer
`OnUsed`. L'auteur choisit une version ForgeScript, une liaison humaine et le payload publié sans
manipuler d'identifiant technique. L'aperçu simule l'objet utilisé, conserve l'autorité et le
résultat, puis bloque toute application invalide avant mutation.

### ORF-PLC-019

La même fiche qualifie `OnDisturbed` via un sous-parcours dédié **Contenu modifié**. L'auteur publie
la cause, les révisions avant/après et l'acteur, puis vérifie le dispatch sans ouvrir l'inventaire ni
écrire le module. Le contrat runtime peut rester optionnel ou devenir requis ; le validateur signale
clairement l'absence d'événement demandé.

### ORF-PLC-020

La section **Dégâts et mort** qualifie `OnDamaged` et `OnDeath` avec un payload uniforme. Le Studio
préserve les profils hérités, sait éditer un profil direct ou déjà porté par la composition, partage
le même contrat humain pour les deux événements et synchronise les deux représentations lorsqu'un
ancien module les contient ensemble. Un résolveur Domain unique alimente la validation, les deux
adaptateurs runtime et la projection ForgeScript/MCP ; une divergence est refusée explicitement. Le
lot prouve aussi l'undo/redo, les anciens JSON, les trois formes de round-trip et la réouverture sans
toucher à la santé réelle de la scène pendant l'aperçu.

## Limites honnêtes

- ce lot qualifie seulement `ORF-PLC-018` à `ORF-PLC-020` ;
- l'aperçu construit et inspecte le dispatch typé mais n'exécute pas un handler ForgeScript ni des
  effets de scène autoritaires ;
- Crypte des Cendres sert de catalogue et de rendu réel, pas de module de preuve sauvegardé ;
- l'application reste en mémoire jusqu'à l'enregistrement explicite.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
  'tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj'
)
$filter = 'FullyQualifiedName~PlaceableSecurityEventsRuntimeAdapterTests|' +
  'FullyQualifiedName~PlaceableCompositionEventsRuntimeAdapterTests|' +
  'FullyQualifiedName~R094_PlaceableSecurityEventsValidation|' +
  'FullyQualifiedName~R095_PlaceableCompositionValidation|' +
  'FullyQualifiedName~Placeable_event_extension_fields_default_when_absent_from_existing_json|' +
  'FullyQualifiedName~Placeable_direct_damage_events_round_trip_without_composition_profile|' +
  'FullyQualifiedName~Placeable_composition_damage_events_remain_the_historical_canonical_fallback|' +
  'FullyQualifiedName~Placeable_matching_dual_damage_events_round_trip_as_one_canonical_authority|' +
  'FullyQualifiedName~Placeable_foundation_resource_is_filtered_typed_and_plan_is_dry_run'
& .\scripts\Verify-StudioIncrement.ps1 -Page placement -Evidence ux1-v-death `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-shell/ux1-v/death-fhd.png
```
