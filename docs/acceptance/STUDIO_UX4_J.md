# Acceptation Studio — UX4-J Interactions de créature

État local au 20 août 2026 : **fonctionnel et qualifié** pour `ORF-CRE-025`, `ORF-CRE-026`,
`ORF-CRE-028`, `ORF-CRE-031` et `ORF-CRE-034`. La page **Créatures** expose une fiche spécialisée
pour les factions, la perception, les conversations associées, leur handler serveur et les
protections narratives.

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
- complément OnConversation réussi en **135,91 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-j-onconversation.summary.json` : build sans
  avertissement, **9/9 tests ciblés**, smoke `on_conversation=true`, matrice 279/438 et capture
  Forward+ FHD inspectée dans `ux4-j-onconversation-fhd.png`. Ce portail incrémental reste
  explicitement `full_gate=false`.

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

### ORF-CRE-034 — OnConversation

Le handler `creature.interaction_requested` est édité directement sans remplacer l'UUID, le schéma,
la version ForgeScript, le nom d'événement ni l'autorité serveur du binding. L'aperçu vérifie la
projection Application et RPG Kit, le dry-run canonique et la valeur persistée, mais n'exécute ni
ForgeScript, ni dialogue, ni acteur moteur. Le smoke exige explicitement
`on_conversation=true` après apply, undo/redo et save/reopen.

## Limites honnêtes

- l'aperçu résout les contrats Domain/Application/RPG Kit en mémoire ; aucune perception moteur,
  conversation live, modification d'acteur, action maître de jeu, ForgeScript ou réplication réseau
  n'est exécutée ;
- UX4-J remplace uniquement `CreatureInteractionCompositionProfile`. Dans `OnConversation`, seul le
  handler est modifié ; identité, schéma, provenance, API, événement et autorité restent préservés.
  Mouvement, présentation, voix, règles, progression, loot, événements et tous les blueprints
  voisins sont inchangés ;
- les captures emploient une copie temporaire de la vraie Crypte ou un fallback canonique isolé.
  Source, historique, état dirty, sélection, approbations, page et surface active sont restaurés ;
- les autres routes créature sont qualifiées dans UX4-D à UX4-P ; cette preuve ne qualifie pas les
  routes item, marchand, compagnon, voix transverse ou assistant de boss restantes d'UX4.

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
