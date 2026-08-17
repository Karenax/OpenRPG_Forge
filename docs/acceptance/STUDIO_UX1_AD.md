# Acceptation Studio — UX1-AD Réactions et assemblage de porte

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-DOOR-016`,
`ORF-DOOR-017`, `ORF-DOOR-018` et `ORF-DOOR-020`. Le lot expose réactions aux capacités,
heartbeat/événement personnalisé, conversation/faction et panneaux coordonnés depuis
**Objets & interactions**.

## Preuves communes

- portail incrémental `scripts/Verify-StudioIncrement.ps1` réussi sur la page `placement` et la
  preuve `ux1-ad-reactions` en 68,48 s, avec résumé dans
  `artifacts/verification/studio-increment/ux1-ad-reactions.summary.json` ;
- tests ciblés : Domain 1/1, Application 2/2 et ModuleFormat 1/1, soit 4/4 ;
- smoke Studio headless réussi : sélections explicites, libellés homonymes désambiguïsés, profils
  d'assemblage dangereux refusés, fusion bornée des événements, doubles gardes de révision,
  commande canonique, préservation, undo/redo, round-trip et transaction UI isolée ;
- captures Forward+ Full HD, QHD et 4K inspectées dans
  `artifacts/verification/studio-shell/ux1-ad/` : trois onglets, contenu scrollable, aperçu et actions
  restent visibles sans coupe ni chevauchement ;
- portail transversal `scripts/Verify.ps1` réussi au jalon UX1-AD/AE/AF : 1 980 tests .NET,
  39 tests Python, builds sans avertissement, graphe Roslyn déterministe à 9 059 nœuds et
  60 404 relations sans diagnostic, puis smokes Studio, Runtime, Forward+, MCP et réseau.

### ORF-DOOR-016

Le ciblage et l'impact d'une capacité disposent de liaisons serveur distinctes et de scripts
nommés. Le dry-run vérifie les deux événements sans toucher aux liaisons de dégâts, d'attaque
physique ou aux événements inconnus.

### ORF-DOOR-017

Le heartbeat annonce honnêtement sa cadence canonique d'une seconde et son budget de soixante
exécutions par minute. Une seule entrée `custom:` explicitement choisie est créée ou remplacée ;
les autres entrées personnalisées conservent référence et ordre relatif.

### ORF-DOOR-018

Conversation et faction sont choisies par libellés humains issus du projet. Les homonymes sont
désambiguïsés par slug lisible, sans exposer d'UUID, et l'absence de référence reste un choix
explicite.

### ORF-DOOR-020

Simple, double, herse, élévateur et sas configurent jusqu'à huit panneaux ordonnés avec offsets,
délais, inversion, interlock, séquence et récupération. Les valeurs existantes hors bornes sont
refusées avant chargement plutôt que silencieusement écrasées par les contrôles.

## Limites honnêtes

- le heartbeat utilise la politique runtime canonique fixe ; le lot n'invente pas de scheduler
  éditable propre aux portes ;
- une entrée personnalisée dupliquée/non conforme reste préservée mais n'est pas proposée comme
  cible d'édition ambiguë ;
- les assemblages de plus de huit panneaux ou aux offsets/délais hors bornes restent bloqués avec
  diagnostic humain ;
- l'aperçu valide événements et coordination ; il ne simule ni animation physique des panneaux ni
  transport réseau réel.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~G13Q_DoorReactiveComplex_ResolvesBindingsAndAssembly|' +
  'FullyQualifiedName~Command_supports_preview_apply_history_and_typed_script_query|' +
  'FullyQualifiedName~Command_rejects_revision_identity_and_invalid_script_contracts|' +
  'FullyQualifiedName~Aggregate_and_workspace_round_trips_preserve_all_world_feature_families'
& .\scripts\Verify-StudioIncrement.ps1 -Page placement -Evidence ux1-ad-reactions `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-shell/ux1-ad/ux1-ad-reactions-fhd.png
```
