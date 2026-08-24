# Studio UX6-Z — création assistée de capacité

Qualification Studio de **ORF-AI-020** sur la page `abilities`. Cette dernière route clôt la vague
Gameplay UX6 à **84/84** sans créer une seconde autorité de capacité : la proposition, les cinq
scénarios de combat, l'équilibrage et l'application réutilisent les services RPG Kit existants.

## ORF-AI-020

La surface « Assistant de création de pouvoirs » impose la sélection explicite d'un porteur de
combat et sépare le parcours en trois onglets humains : Design de combat, Dépendances & gouvernance,
et Tests & équilibrage. Elle expose coût, recharge, ciblage, effets ordonnés, références VFX/audio,
script réel, objet optionnel, tags IA, intention d'équilibrage, citations, auteur, style et seed.

La proposition est produite hors ligne par `AssistedAbilityCreationService`. Elle traverse les cinq
scénarios réels de `CombatResolver`, le bilan d'équilibrage et la construction du document runtime
stable. Aucun fournisseur distant, modèle d'IA, ForgeScript, VFX, audio ou combat live n'est lancé.

L'application passe uniquement par `ApplyAssistedAbilityHandler` après approbation et permission
explicites. Le dry-run est rejoué juste avant l'écriture ; révision, empreinte SHA-256 du projet et
empreinte binaire typée du brouillon protègent contre les changements concurrents.

## Transaction et préservation

- effets complets ordonnés, avec ajout/remplacement, retrait et déplacement sans accès par UUID ;
- diagnostics humains et valeurs numériques exactes, sans clamp silencieux ;
- dry-run pur, application atomique, undo/redo et save/reopen canonique ;
- conservation du porteur, de ses autres capacités, des autres blueprints et des voisins du module ;
- preuve UI dans un workspace temporaire isolé dérivé de Crypte des Cendres ;
- projet, historique, dirty state, sélection, approbations et surface source restaurés exactement ;
- nettoyage borné des scripts et du workspace temporaires.

## Validation locale

- build Studio Debug : **0 avertissement, 0 erreur** ;
- RPG Kit Domain : **5/5** tests `AssistedAbilityCreationServiceTests` ;
- MCP : **1/1** test assisté de mutation/revert canonique ;
- smoke Studio complet : `OPENRPG_FORGE_STUDIO_UX6_Z_PASS`, approbations et shell global PASS ;
- capture Forward+ **1920×1080** inspectée sans clipping, UUID technique ni action masquée :
  `artifacts/verification/studio-increment/ux6-z-assisted-ability-forward-plus-fhd.png` ;
- portail incrémental : **146,31 s**, build, 6/6 tests, smoke, capture FHD et matrice ;
- résumé machine :
  `artifacts/verification/studio-increment/ux6-z-assisted-ability.summary.json` ;
- `full_gate=false` reste explicite : cette preuve locale ne remplace pas le portail transversal.

## Limites honnêtes

Le fournisseur utilisé est déterministe, local et gratuit ; il ne prouve aucun service d'IA distant.
Studio valide une proposition et applique une définition canonique, mais n'exécute ni ForgeScript,
objet, VFX, audio, combat moteur, acteur Godot, serveur ou transport réseau. Le runtime et la
plateforme restent les autorités live.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.Mcp.Tests/Forge.Mcp.Tests.csproj'
)
& .\scripts\Verify-StudioIncrement.ps1 -Page abilities `
  -Evidence ux6-z-assisted-ability `
  -Workspace apps/Forge.Runtime/content/crypt-of-ashes `
  -TestProject $projects -TestFilter 'FullyQualifiedName~AssistedAbility' `
  -Capture artifacts/verification/studio-increment/ux6-z-assisted-ability-forward-plus-fhd.png
```
