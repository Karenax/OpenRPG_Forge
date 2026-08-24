# Acceptation Studio — UX4-H Capacités et inventaire de créature

État local au 17 août 2026 : **fonctionnel et qualifié** pour `ORF-CRE-021` et
`ORF-CRE-022`. La page **Créatures** expose une fiche spécialisée pour les capacités spéciales,
les conteneurs, les objets réels du catalogue et les emplacements d'équipement.

## Preuves communes

- portail incrémental réussi en **107,11 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-h-creature-capabilities-inventory.summary.json` ;
- build Studio sans avertissement et **18/18 tests** : Domain 4, Application 9, RPG Kit 4 et
  ModuleFormat 1 ;
- smoke Studio global réussi : sélection explicite, références d'objet inconnues refusées,
  invalidités localisées, dry-run, empreintes, stale draft/approval, commande canonique,
  transaction UI réelle, undo/redo, save/reopen, préservation, restauration de surface et
  libération du workspace temporaire ;
- capture Forward+ FHD du fallback inspectée dans
  `ux4-h-creature-capabilities-inventory-fhd.png`, puis trois captures de la vraie Crypte dans
  `ux4-h-abilities-real-crypt-fhd.png`, `ux4-h-inventory-real-crypt-fhd.png` et
  `ux4-h-preview-real-crypt-fhd.png` ;
- les trois onglets, les collections et le footer transactionnel complet restent lisibles à
  1920×1080 sans UUID, JSON ni clipping bloquant. La provenance distingue explicitement la vraie
  Crypte du fallback canonique temporaire.

### ORF-CRE-021 — Capacités spéciales

La surface édite la collection complète des capacités : clé, niveau, charges, cooldown,
conditions, tags d'IA et liaison script. Les identités internes restent stables, les valeurs
invalides sont refusées par le validateur canonique et l'aperçu lit la projection typée sans
exécuter la capacité.

### ORF-CRE-022 — Inventaire de créature

Les conteneurs conservent leurs limites de slots, poids et volume. Chaque entrée référence un
blueprint `Item` réellement présent dans le projet, avec quantité, poids et volume unitaires ; les
slots d'équipement pointent vers une entrée existante. Owner et persistance sont éditables, et la
validation bloque une référence absente ou une incohérence d'équipement avant toute application.

## Limites honnêtes

- l'aperçu résout les contrats Domain/Application en mémoire ; aucune capacité, liaison script,
  création d'objet, modification d'acteur, animation, physique ou réplication réseau n'est
  exécutée ;
- UX4-H remplace uniquement `Abilities` et `Inventory` dans le profil de règles. État initial,
  ressources, attributs, défenses, progression et tous les autres profils de créature sont
  préservés ;
- les captures emploient une copie temporaire de la vraie Crypte ou un fallback canonique isolé.
  Source, historique, état dirty, sélection, approbations, page et surface active sont restaurés ;
- le loot, le pickpocket et le cadavre sont qualifiés séparément par UX4-I ; cette preuve ne
  qualifie pas `ORF-CRE-025` à `ORF-CRE-031` ni `ORF-CRE-033` à `ORF-CRE-049`.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
& .\scripts\Verify-StudioIncrement.ps1 -Page creatures `
  -Evidence ux4-h-creature-capabilities-inventory -TestProject $projects `
  -TestFilter 'FullyQualifiedName~CreatureRulesComposition|FullyQualifiedName~CreatureRulesServiceTests|FullyQualifiedName~SetCreatureFoundation|FullyQualifiedName~RoundTripsCreatureFoundation' `
  -Capture artifacts/verification/studio-increment/ux4-h-creature-capabilities-inventory-fhd.png `
  -NoMatrixCheck

& .\scripts\Open-Studio.ps1 -Workspace apps/Forge.Runtime/content/crypt-of-ashes `
  -Page creatures -Evidence ux4-h-preview -Resolution 1920x1080 `
  -Capture artifacts/verification/studio-increment/ux4-h-preview-real-crypt-fhd.png `
  -Wait -SkipBuild
```
