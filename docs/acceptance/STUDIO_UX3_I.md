# Studio UX3-I — assets gouvernés et génération média planifiée

Qualification Studio de `ORF-CONT-023`, `ORF-CONT-024` et `ORF-AI-024` sur la
page `assets`. La surface « Provenance, quarantaine et médias gouvernés » sépare deux
historiques réels : le registre local d’asset et la politique projet de génération média.
Elle n’annonce donc aucune transaction atomique fictive entre ces agrégats.

<a id="orf-cont-023"></a>
## ORF-CONT-023

L’auteur choisit explicitement un asset local PNG, JPEG, GLB ou glTF, puis renseigne licence
SPDX, auteur, URL source, preuve de permission, modifications, redistribution, frontière de
compatibilité propriétaire, origine, budget et revue légale. Le cas IA conserve en plus le modèle
et le SHA-256 de politique de prompt. Le dry-run vérifie la source sélectionnée et le contrat de
distribution avant toute écriture ; l’application passe par `SetAssetGovernanceHandler`, avec
empreinte du brouillon, undo/redo et persistance dans `AssetGovernanceStore`.

<a id="orf-cont-024"></a>
## ORF-CONT-024

La quarantaine copie l’asset dans un espace confiné, vérifie son SHA-256, son format et le verdict
du scanner, puis exige une décision humaine nominative. Une approbation conserve uniquement la
copie confinée ; un refus la supprime immédiatement. Le produit réel tente Microsoft Defender et
échoue fermé lorsque le scanner système n’est pas disponible. La preuve automatisée n’usurpe pas ce
verdict : elle utilise un scanner déterministe explicitement limité au workspace temporaire isolé.

L’import canonique du projet n’est jamais touché par cette surface. Les opérations undo/redo
réconcilient le registre et la copie de quarantaine ; toute divergence de hash invalide l’ancien
reçu au lieu d’écraser un fichier modifié.

<a id="orf-ai-024"></a>
## ORF-AI-024

La politique projet expose activation, types autorisés Image/Audio/VFX, fournisseurs allowlistés,
quota de tâches, budget d’octets et fallback manuel. Un candidat indique type, fournisseur,
licence, droits, taille attendue et disponibilité. Le dry-run canonique passe par
`SetRuntimeExtensionsHandler`, puis `PostLaunchRuntimeService.PlanMediaGeneration` produit seulement
un plan gouverné.

Aucun fournisseur n’est appelé, aucun octet média n’est généré et aucun candidat n’est importé.
Le plan est une décision éditoriale réversible ; il ne constitue pas une exécution de modèle ni une
preuve de disponibilité distante.

## Transaction, isolation et préservation

Les deux commandes utilisent révision, empreinte binaire length-prefixed du projet/source et
empreinte du brouillon approuvé. L’application refait le dry-run avant mutation. Le projet conserve
tous les voisins de `RuntimeExtensions`, tandis que le registre conserve les autres assets et leur
ordre. Les deux parcours couvrent apply, undo, redo, save et reopen.

La preuve clone le module et un asset réel dans un workspace temporaire borné. Projet, historique,
révision, journal, dirty state, zone active, sélection et approbations de la session source restent
inchangés ; les fichiers de quarantaine et le workspace de preuve sont supprimés à la fermeture.

## Preuves

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests ciblés : **12/12** — Domain 3, Application 4, ModuleFormat 5 ;
- smoke `OPENRPG_FORGE_STUDIO_UX3_I_PASS` : sélections explicites, provenance, quarantaine,
  décision humaine, politique média, plan sans fournisseur, commandes canoniques, gardes stale,
  undo/redo, round-trip, préservation projet, transaction UI, cleanup et restauration source à
  `true` ;
- portail incrémental : build, tests, smoke, capture et matrice en **171,71 s** ;
- quatre captures Forward+ **1920×1080** inspectées : provenance, quarantaine, génération média et
  capture agrégée ; aucune troncature bloquante, footer transactionnel complet et contexte isolé
  explicite ;
- résumé machine :
  `artifacts/verification/studio-increment/ux3-i-governed-assets.summary.json` ;
- captures :
  `artifacts/verification/studio-increment/ux3-i-governed-assets-current-forward-plus-fhd.png`,
  `ux3-i-provenance-current-forward-plus-fhd.png`,
  `ux3-i-quarantine-current-forward-plus-fhd.png` et
  `ux3-i-media-generation-current-forward-plus-fhd.png` ;
- `full_gate=false` et `graph_check=false` restent explicites pour ce portail incrémental ; le
  graphe Roslyn est régénéré et vérifié séparément après promotion.

## Limites honnêtes

Studio ne télécharge, ne convertit, ne signe, ne publie et n’importe aucun asset dans Godot. Le scan
automatisé de preuve ne remplace jamais l’antivirus système du produit. La génération média reste un
plan local : aucun modèle, fournisseur, API distante, rendu, playback ou VFX live n’est exécuté.
Les formats propriétaires et contenus dont les droits sont insuffisants restent bloqués.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~AssetGovernanceTests|' +
  'FullyQualifiedName~AssetGovernanceCommandTests|' +
  'FullyQualifiedName~PostLaunchRuntimeServiceTests|' +
  'FullyQualifiedName~AssetGovernanceStoreTests|' +
  'FullyQualifiedName~ShippingExport_BlocksPendingQuarantineBeforeWritingArchive|' +
  'FullyQualifiedName~ShippingExport_NeverRedistributesNwnOrCepProprietaryAssets'
& .\scripts\Verify-StudioIncrement.ps1 -Page assets `
  -Evidence ux3-i-governed-assets `
  -Workspace apps/Forge.Runtime/content/crypt-of-ashes `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux3-i-governed-assets-current-forward-plus-fhd.png

foreach ($mode in 'ux3-i-provenance', 'ux3-i-quarantine', 'ux3-i-media-generation') {
  & .\scripts\Open-Studio.ps1 -Page assets -Evidence $mode `
    -Workspace apps/Forge.Runtime/content/crypt-of-ashes `
    -Resolution 1920x1080 -SkipBuild -Wait `
    -Capture "artifacts/verification/studio-increment/$mode-current-forward-plus-fhd.png"
}
```
