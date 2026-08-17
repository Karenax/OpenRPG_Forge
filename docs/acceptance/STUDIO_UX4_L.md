# Acceptation Studio — UX4-L Voix et soundset

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-CRE-029`. La page
**Créatures** expose une fiche spécialisée pour le profil voix et le soundset.

## Preuves communes

- portail incrémental réussi en **120,40 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-l-creature-voice.summary.json` ;
- build Studio sans avertissement et **6/6 tests** : Domain 3, Application 2 et ModuleFormat 1 ;
- smoke Studio global réussi : sélection explicite, banques/émotions, lipsync/variantes,
  gouvernance, retrait optionnel, invalidité et doublon refusés, projection typée, dry-run, stale
  draft/approval, commande canonique, transaction UI, undo/redo, save/reopen, préservation,
  restauration de surface et libération du workspace temporaire ;
- capture Forward+ FHD inspectée dans `ux4-l-creature-voice-fhd.png` ;
- les huit champs, l'aperçu et le footer transactionnel restent lisibles à 1920×1080 sans UUID,
  JSON ni clipping.

## ORF-CRE-029 — Voix et soundset

La surface édite le profil voix optionnel : identifiant, langue, banques audio, émotions, lipsync,
variantes, licence SPDX et provenance des droits. Les collections sont ordonnées canoniquement,
mais les doublons ne sont pas masqués : ils restent visibles au validateur et bloquent l'aperçu.

La mutation remplace uniquement `CreaturePresentationProfile.Voice` via
`SetCreaturePresentationHandler`. `CreaturePresentationValidator` et
`CreaturePresentationRuntimeResolver` produisent le verdict canonique avant application. Le profil
peut être retiré explicitement sans supprimer le reste de la présentation.

## Limites honnêtes

- Studio ne charge ni ne joue de banque audio, n'exécute aucun lipsync, TTS, acteur ou réseau et
  n'importe aucun média ; la licence et la provenance sont des métadonnées authorées ;
- la preuve riche utilise un fallback canonique temporaire parce que le workspace ouvert ne porte
  pas de profil voix produit qualifiable. Elle ne prétend donc pas être une écoute réelle ;
- genre, pronoms, portraits, modèle, rig, parties du corps, couleurs, attachements et blueprints
  voisins sont préservés ;
- `ORF-AUD-016` reste distinct : ses prises localisées, sous-titres, phonèmes, TTS et consentement
  vivent dans `AreaExperienceProfile` et seront qualifiés dans une transaction séparée ;
- cette preuve ne qualifie pas `ORF-CRE-033` à `ORF-CRE-046` ni `ORF-CRE-049` ;
  `ORF-CRE-030`, `ORF-CRE-047` et `ORF-CRE-048` sont qualifiées séparément par UX4-M.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~CreaturePresentationProfileTests|' +
  'FullyQualifiedName~SetCreaturePresentationHandlerTests|' +
  'FullyQualifiedName=Forge.ModuleFormat.Tests.Serialization.ProjectJsonSerializerTests.' +
  'SerializeAndDeserialize_RoundTripsCreaturePresentation'
& .\scripts\Verify-StudioIncrement.ps1 -Page creatures `
  -Evidence ux4-l-creature-voice -TestProject $projects `
  -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux4-l-creature-voice-fhd.png `
  -NoMatrixCheck
```
