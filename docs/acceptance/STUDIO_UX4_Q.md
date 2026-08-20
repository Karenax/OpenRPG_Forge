# Acceptation Studio — UX4-Q Voix localisée de créature

État local au 20 août 2026 : **fonctionnel et qualifié** pour `ORF-AUD-016`. La page
**Créatures** expose les profils `CreatureVoiceProfile` d’une zone par créature et les persiste via
la commande canonique `SetAreaExperienceProfileHandler`.

## ORF-AUD-016 — Voix de créature

La surface édite la locale par défaut et la collection complète des prises localisées : ressource
audio optionnelle, sous-titre, phonèmes, licence SPDX et SHA-256. Une prise sans ressource utilise
explicitement le fallback TTS ; plugin, caractère synthétique, autorisation, référence de
consentement et attribution du locuteur restent visibles et validés avant écriture.

L’aperçu appelle `AreaExperienceRuntimeService.ResolveVoice` pour résoudre la locale demandée et
le fallback canonique. Il ne charge aucun média et ne lance aucun moteur TTS.

## Preuves

- portail incrémental réussi en **116,00 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-q-area-voice.summary.json` ;
- build Studio sans avertissement et **14/14 tests ciblés** : Domain 7, Application 3 et
  ModuleFormat 4 ;
- smoke Studio global réussi : sélection explicite, prises localisées, sous-titres/phonèmes,
  fallback TTS, consentement, invalidités, dry-run, projection runtime, gardes source/approbation,
  commande canonique, transaction UI réelle, undo/redo, save/reopen, préservation des voisins,
  restauration de surface et libération du workspace temporaire ;
- capture Forward+ FHD inspectée dans
  `artifacts/verification/studio-increment/ux4-q-area-voice-fhd.png` : collection, gouvernance TTS,
  résumé canonique et cinq actions transactionnelles sont visibles à 1920×1080 sans UUID, JSON ni
  clipping bloquant ;
- la matrice atteint 280 routes `functional` et 437 `planned`. Le portail reste volontairement
  `full_gate=false` : il qualifie cette route, pas la totalité du produit.

## Préservation et limites honnêtes

- le remplacement cible uniquement `Area.RuntimePolicies.Experience.Voices` ; audio cues, scatter,
  patrouilles inter-zones, chargement, autres zones, blueprints et racines projet sont préservés ;
- le parcours normal accepte un état vide et n’invente aucune voix. La preuve riche vit dans une
  session temporaire isolée ; projet, historique, état dirty, sélection et approbation source
  restent inchangés puis le répertoire temporaire est libéré ;
- Studio résout la locale et le fallback en mémoire. Il ne lit aucun fichier audio, ne synthétise
  aucune voix, n’exécute aucun acteur, ForgeScript, moteur ou transport réseau ;
- `ORF-CRE-029` reste la voix/soundset de présentation de créature qualifiée par UX4-L ; cette
  route transverse qualifie les prises localisées et leur consentement, sans dupliquer ce profil.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~AreaExperienceProfileTests|' +
  'FullyQualifiedName~AreaExperienceRuntimeServiceTests|' +
  'FullyQualifiedName~SetAreaExperienceProfileHandlerTests|' +
  'FullyQualifiedName~AreaExperiencePersistenceTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page creatures `
  -Evidence ux4-q-area-voice -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux4-q-area-voice-fhd.png `
  -NoMatrixCheck
```
