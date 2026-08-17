# Acceptation Studio — UX4-K Déplacement de créature

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-CRE-027`. La page
**Créatures** expose une fiche spécialisée pour la vitesse et les profils de déplacement.

## Preuves communes

- portail incrémental réussi en **117,80 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-k-creature-movement.summary.json` ;
- build Studio sans avertissement et **28/28 tests** : Domain 10, Application 9, RPG Kit 8 et
  ModuleFormat 1 ;
- smoke Studio global réussi : sélection explicite, six modes, valeurs complètes, invalidités
  numériques et altitude de vol refusées, projection typée, dry-run, stale draft/approval,
  commande canonique, transaction UI, undo/redo, save/reopen, préservation structurelle,
  restauration de surface et libération du workspace temporaire ;
- captures Forward+ FHD inspectées dans `ux4-k-creature-movement-fhd.png` et
  `ux4-k-creature-movement-real-crypt-fhd.png` ;
- les six champs, l'aperçu et le footer transactionnel complet restent lisibles à 1920×1080 sans
  UUID, JSON ni clipping. La provenance distingue explicitement la vraie Crypte du fallback
  canonique temporaire.

## ORF-CRE-027 — Vitesse et profils de déplacement

La surface édite les six modes canoniques — au sol, vol, nage, escalade, véhicule et zéro gravité —
ainsi que vitesse maximale, accélération, décélération, altitudes minimale/maximale et pente
maximale. Les nombres sont relus tels qu'écrits, sans grille ni clamp silencieux. Le validateur
refuse les valeurs non finies ou hors bornes et exige une plage d'altitude non vide pour le vol.

L'aperçu passe par `CreatureFoundationValidator`, `SetCreatureFoundationHandler`,
`CreatureFoundationScriptService` et `CreatureFoundationRuntimeResolver`. Il projette aussi une
accélération et un freinage arithmétiques en mémoire afin de rendre le résultat lisible, sans
modifier ni instancier un acteur.

## Limites honnêtes

- Studio ne déplace aucun acteur, ne calcule aucune collision, surface grimpable, flottabilité,
  gravité, conduite ou réplication réseau ; le runtime reste l'autorité du mouvement réel ;
- UX4-K remplace uniquement `CreatureFoundationProfile.Movement`. Ressources, références,
  transformations, compositions UX4-D à UX4-J et blueprints voisins sont préservés ;
- les captures utilisent une copie temporaire du profil réel de la Crypte ou un fallback canonique
  isolé. Source, historique, état dirty, sélection, approbations, page et surface sont restaurés ;
- cette preuve ne qualifie pas `ORF-CRE-029`, `ORF-CRE-030` ni `ORF-CRE-033` à
  `ORF-CRE-049`, qui restent `planned`.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~CreatureFoundationProfileTests|' +
  'FullyQualifiedName~SetCreatureFoundationHandlerTests|' +
  'FullyQualifiedName~CreatureFactoryTests|' +
  'FullyQualifiedName=Forge.ModuleFormat.Tests.Serialization.ProjectJsonSerializerTests.' +
  'SerializeAndDeserialize_RoundTripsCreatureFoundation'
& .\scripts\Verify-StudioIncrement.ps1 -Page creatures `
  -Evidence ux4-k-creature-movement -TestProject $projects `
  -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux4-k-creature-movement-fhd.png `
  -NoMatrixCheck

& .\scripts\Open-Studio.ps1 -Workspace apps/Forge.Runtime/content/crypt-of-ashes `
  -Page creatures -Evidence ux4-k-creature-movement -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux4-k-creature-movement-real-crypt-fhd.png `
  -Wait -SkipBuild
```
