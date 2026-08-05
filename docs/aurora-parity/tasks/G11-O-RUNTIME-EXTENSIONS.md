# G11-O — Règles data-driven, UI scriptable, JSON et état runtime

## Périmètre

- `ORF-EE-011` — schémas de classes, progression, armes, VFX et modèles d’incantation ;
- `ORF-EE-012` — documents UI déclaratifs, état réactif, thèmes et permissions ;
- `ORF-EE-013` — événements UI typés, contexte d’entrée, focus et automatisation ;
- `ORF-EE-015` — API JSON runtime sûre, versionnée et bornée ;
- `ORF-EE-016` — snapshots d’entités avec composants, références, inventaire et blackboard.

## Résultat livré

- agrégat `ModuleRuntimeExtensionProfile` v1 avec UUID, provenance, ordre canonique et références UI
  validées ;
- cinq types de schémas de règles consommables sans recompilation du moteur ;
- service runtime en lecture par handles typés et diagnostic localisé pour une extension requise
  absente ;
- fenêtre Creator **Données** pour règles, JSON, documents/widgets/événements UI et politique de
  snapshot, reliée à une commande avec dry-run, undo et redo ;
- `RuntimeDeclarativeUi` qui reconstruit labels, boutons, progression et champs depuis le domaine,
  applique thème, binding et permission puis publie les événements ForgeScript typés ;
- `ForgeScriptJsonService` avec handles, JSON Pointer, contrôle de révision, permissions, quotas et
  validation `type`/`required` ;
- MCP en lecture, plan, application et réversion ;
- `.orpsave` 1.3 avec migration 1.1/1.2 et snapshots des transforms, combats, inventaires et
  blackboards sélectionnés ;
- Crypte des Cendres enrichie d’un HUD déclaratif, d’un document JSON et de quatre schémas de règles ;
- formats projet `0.6.17` et workspace `1.11.0`, avec migration reproductible des trois workspaces.

## Preuves

- `ModuleRuntimeExtensionProfileTests` couvre canonisation, références et quotas ;
- `SetRuntimeExtensionsHandlerTests` couvre prévisualisation, application, undo et redo ;
- `RuntimeExtensionRuntimeServiceTests` couvre handles et absence optionnelle/requise ;
- `RuntimeExtensionPersistenceTests` couvre round-trip et migration du format précédent ;
- `ForgeScriptJsonAndUiTests` couvre permissions, quotas, révisions et événements typés ;
- `RuntimeSaveStoreTests` couvre snapshots, validation et migration `.orpsave` ;
- `ForgeProjectAgentServiceTests` couvre le plan MCP réversible ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_G11O_EXTENSIONS_PASS` ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_G11O_EXTENSIONS_PASS` ;
- smokes save/reprise `OPENRPG_FORGE_RUNTIME_G11O_SAVE_PASS`.

## Limites explicites

- le validateur JSON v1 implémente un sous-ensemble sûr (`type`, `required`) et non l’intégralité de
  JSON Schema ;
- les règles supplémentaires sont disponibles sous forme de documents typés et de valeurs par
  défaut ; les écrans spécialisés classe/arme/VFX pourront ajouter des formulaires plus guidés ;
- l’UI runtime couvre les widgets v1 essentiels, pas encore une bibliothèque complète de layout ;
- les références d’entités sont validées et bornées ; la Crypte n’en persiste aucune dans son
  scénario mono-zone actuel ;
- ce lot ne remplace pas le chantier artistique de la carte 3D finale.

## Critère de passage

G11-O est fermé lorsque tests ciblés, smokes Toolset/runtime/save, migrations, tracker, graphe et
`scripts/Verify.ps1` sont verts sur le même état Git.
