# Acceptation Studio — UX4-I Loot, pickpocket et cadavre de créature

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-CRE-023`, `ORF-CRE-024` et
`ORF-CRE-032`. La page **Créatures** expose une fiche spécialisée pour les entrées droppables,
le vol à la tire et le cycle de cadavre.

## Preuves communes

- portail incrémental réussi en **119,64 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-i-creature-loot.summary.json` ;
- build Studio sans avertissement et **11/11 tests** : Domain 4, Application 4 et ModuleFormat 3 ;
- smoke Studio global réussi : sélection explicite, références inconnues refusées, invalidités
  localisées, dry-run, empreintes, stale draft/approval, commande canonique, transaction UI réelle,
  undo/redo, save/reopen, préservation, restauration de surface et libération du workspace
  temporaire ;
- capture Forward+ FHD du fallback inspectée dans `ux4-i-creature-loot-fhd.png`, puis quatre
  captures de la vraie Crypte dans `ux4-i-drops-real-crypt-fhd.png`,
  `ux4-i-pickpocket-real-crypt-fhd.png`, `ux4-i-corpse-real-crypt-fhd.png` et
  `ux4-i-preview-real-crypt-fhd.png` ;
- les quatre onglets, les collections et le footer transactionnel complet restent lisibles à
  1920×1080 sans UUID, JSON ni clipping bloquant. La provenance distingue explicitement la vraie
  Crypte du fallback canonique temporaire.

### ORF-CRE-023 — Objets droppables

La surface édite la collection complète des entrées de loot : objet issu de la table de combat,
slot optionnel, quantité minimale et maximale, probabilité, propriété, instanciation, drop au décès
et disponibilité au pickpocket. Les identités et l'ordre restent stables ; une référence absente
ou une quantité incohérente bloque l'aperçu avant toute écriture.

### ORF-CRE-024 — Pickpocket

Le vol à la tire référence des compétences réelles de progression pour le voleur et la perception.
La difficulté, la détection, le consentement serveur, le crime, le cooldown et l'audit sont
éditables. L'aperçu appelle le résolveur typé avec une transaction isolée, sans voler ni créer
d'objet dans le projet ou le moteur.

### ORF-CRE-032 — Cadavre et loot corpse

La surface couvre les modes visuel, conteneur, ragdoll et ragdoll avec conteneur, ainsi que durée,
profil ragdoll, résurrection, propriété, capacité et nettoyage. Le plan de décès est projeté en
mémoire ; aucun acteur, cadavre physique ou conteneur moteur n'est instancié par Studio.

## Limites honnêtes

- l'aperçu résout les contrats Domain/Application en mémoire ; aucune création d'objet, tentative
  de vol, modification d'acteur, physique de ragdoll, animation, script ou réplication réseau n'est
  exécutée ;
- UX4-I remplace uniquement `CreatureLootProfile`. Fondation, présentation, règles, progression,
  capacités, inventaire, combat loot et tous les blueprints voisins sont préservés ;
- les captures emploient une copie temporaire de la vraie Crypte ou un fallback canonique isolé.
  Source, historique, état dirty, sélection, approbations, page et surface active sont restaurés ;
- factions, perception, conversations et protections sont qualifiées séparément par UX4-J ; cette
  preuve ne qualifie pas `ORF-CRE-027`, `ORF-CRE-029`, `ORF-CRE-030` ni `ORF-CRE-033` à
  `ORF-CRE-049`, qui restent `planned`.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
& .\scripts\Verify-StudioIncrement.ps1 -Page creatures `
  -Evidence ux4-i-creature-loot -TestProject $projects `
  -TestFilter 'FullyQualifiedName~CreatureLoot' `
  -Capture artifacts/verification/studio-increment/ux4-i-creature-loot-fhd.png `
  -NoMatrixCheck

& .\scripts\Open-Studio.ps1 -Workspace apps/Forge.Runtime/content/crypt-of-ashes `
  -Page creatures -Evidence ux4-i-preview -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux4-i-preview-real-crypt-fhd.png `
  -Wait -SkipBuild
```
