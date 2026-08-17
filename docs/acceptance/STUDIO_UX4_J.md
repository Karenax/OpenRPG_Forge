# Acceptation Studio — UX4-J Interactions de créature

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-CRE-025`, `ORF-CRE-026`,
`ORF-CRE-028` et `ORF-CRE-031`. La page **Créatures** expose une fiche spécialisée pour les
factions, la perception, les conversations associées et les protections narratives.

## Preuves communes

- portail incrémental réussi en **115,01 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-j-creature-interaction.summary.json` ;
- build Studio sans avertissement et **9/9 tests** : Domain 4, Application 2, RPG Kit 2 et
  ModuleFormat 1 ;
- smoke Studio global réussi : sélection explicite, références faction/dialogue absentes refusées,
  validation localisée, projection typée, dry-run, empreintes, stale draft/approval, commande
  canonique, transaction UI réelle, undo/redo, save/reopen, préservation structurelle, restauration
  de surface et libération du workspace temporaire ;
- capture Forward+ FHD du fallback inspectée dans `ux4-j-creature-interaction-fhd.png`, puis cinq
  captures de la vraie Crypte dans `ux4-j-factions-real-crypt-fhd.png`,
  `ux4-j-perception-real-crypt-fhd.png`, `ux4-j-dialogues-real-crypt-fhd.png`,
  `ux4-j-protection-real-crypt-fhd.png` et `ux4-j-preview-real-crypt-fhd.png` ;
- les cinq onglets, leurs collections et le footer transactionnel complet restent lisibles à
  1920×1080 sans UUID, JSON ni clipping bloquant. La provenance distingue explicitement la vraie
  Crypte du fallback canonique temporaire.

### ORF-CRE-025 — Faction de créature

La surface édite la faction principale et toutes les appartenances : faction active du module,
poids, rang, réputation personnelle et héritage. Les identités et l'ordre restent stables ; une
faction absente, un doublon ou une pondération hors contrat bloque l'aperçu avant toute écriture.

### ORF-CRE-026 — Perception

Vision, audition, odorat et perception magique sont édités comme une collection complète avec
portée, cône, occlusion, stimuli et mémoire. L'aperçu appelle la projection RPG Kit typée mais ne
raycast aucun volume, ne détecte aucun acteur et ne modifie pas le runtime.

### ORF-CRE-028 — Conversation associée

Chaque liaison référence un dialogue réel et conserve contexte, priorité, interlocuteur, état
requis et fallback. La sélection de dialogue reste humaine ; une référence orpheline est refusée
par la même commande canonique `SetCreatureFoundationHandler`.

### ORF-CRE-031 — Plot, immortel et interruptibilité

La protection couvre invulnérabilité, mort narrative, interruption de dialogue et permissions
maître de jeu. La décision d'interruption est projetée en mémoire ; aucun dialogue, script, acteur,
dégât ou transport réseau n'est réellement exécuté par Studio.

## Limites honnêtes

- l'aperçu résout les contrats Domain/Application/RPG Kit en mémoire ; aucune perception moteur,
  conversation live, modification d'acteur, action maître de jeu, ForgeScript ou réplication réseau
  n'est exécutée ;
- UX4-J remplace uniquement `CreatureInteractionCompositionProfile`. `OnConversation`, mouvement,
  présentation, voix, règles, progression, loot, événements et tous les blueprints voisins sont
  préservés ;
- les captures emploient une copie temporaire de la vraie Crypte ou un fallback canonique isolé.
  Source, historique, état dirty, sélection, approbations, page et surface active sont restaurés ;
- cette preuve ne qualifie pas `ORF-CRE-029`, `ORF-CRE-030` ni `ORF-CRE-033` à
  `ORF-CRE-049`, qui restent `planned` ; `ORF-CRE-027` est qualifiée séparément par UX4-K.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~CreatureInteractionComposition|' +
  'FullyQualifiedName~SetCreatureFoundationHandlerTests.InteractionComposition|' +
  'FullyQualifiedName~ProjectJsonSerializerTests.SerializeAndDeserialize_RoundTripsCreatureFoundation'
& .\scripts\Verify-StudioIncrement.ps1 -Page creatures `
  -Evidence ux4-j-creature-interaction -TestProject $projects `
  -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux4-j-creature-interaction-fhd.png `
  -NoMatrixCheck

& .\scripts\Open-Studio.ps1 -Workspace apps/Forge.Runtime/content/crypt-of-ashes `
  -Page creatures -Evidence ux4-j-preview -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux4-j-preview-real-crypt-fhd.png `
  -Wait -SkipBuild
```
