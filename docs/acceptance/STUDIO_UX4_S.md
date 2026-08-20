# Acceptation Studio — UX4-S Objets complets

État local au 20 août 2026 : **fonctionnel et qualifié** pour les 32 routes
`ORF-ITEM-001..032`. La page **Objets** expose une surface unique en cinq onglets et applique le
lot approuvé via `SetItemAuthoringBundleHandler` dans une transaction atomique.

## Parcours canonique

La sélection d’un objet existant ou la création explicite d’un nouvel objet précède toute édition.
Le brouillon couvre identité, localisation, type immuable, création, gameplay, opérations et profil
avancé. L’aperçu sec valide le projet candidat, affiche le patch et l’inverse, puis lie
l’approbation à la révision et aux empreintes binaires du projet, du brouillon et du candidat.
Appliquer, annuler, rétablir et enregistrer passent par l’historique canonique et le round-trip du
workspace.

### ORF-ITEM-001 — Assistant de création d’objet

Le parcours guidé regroupe les cinq sections sans exiger de JSON ni d’identifiant technique.

### ORF-ITEM-002 — Type de base immuable

Le changement de type est prévisualisé et exige une acceptation explicite des pertes éventuelles.

### ORF-ITEM-003 — Blueprint d’objet

Le blueprint conserve son identité stable et ses profils complets dans une seule commande.

### ORF-ITEM-004 — Identité et localisation

Slug, nom et textes localisés sont édités directement avec les variantes auteur complètes.

### ORF-ITEM-005 — Apparence personnalisable

Meshes, matériaux, sockets, icône, VFX et aperçu équipé sont authorés sans chargement d’asset.

### ORF-ITEM-006 — Poids et encombrement

Masse, volume, grille et facteurs de pile sont conservés avec leurs valeurs exactes.

### ORF-ITEM-007 — Valeur et coût

Qualité, niveau et valeur alimentent la projection économique déterministe du profil avancé.

### ORF-ITEM-008 — Piles

Maximum, clé de compatibilité et politique de fusion/séparation sont persistés.

### ORF-ITEM-009 — Charges et utilisations

Charges, usages, cooldown, repos et recharge restent cohérents avec le profil de création.

### ORF-ITEM-010 — Niveau ou Lore requis

Les prérequis du profil avancé sont validés avant toute mutation.

### ORF-ITEM-011 — Objet identifié

La politique d’identification et les informations masquées restent authorées et préservées.

### ORF-ITEM-012 — Objet volé

La propriété et le statut volé sont représentés par le profil avancé sans transaction live.

### ORF-ITEM-013 — Objet plot

Les protections plot sont éditées dans la politique gameplay canonique.

### ORF-ITEM-014 — Indroppable et transfert

Les destinations et permissions de transfert sont validées comme un ensemble.

### ORF-ITEM-015 — Propriétés magiques

Les modificateurs data-driven conservent type, valeur, effet, condition et stacking.

### ORF-ITEM-016 — Pouvoir unique

Le pouvoir, ses coûts, charges et cooldown restent liés au même objet.

### ORF-ITEM-017 — Effets On Hit

Les procs On Hit sont authorés et prévisualisés sans lancer de combat.

### ORF-ITEM-018 — Équipement et slots

Rig, slots, couches et conflits sont validés dans le profil gameplay.

### ORF-ITEM-019 — Armes et combat

Classe, cadence, mains, portée, animation et canaux de dégâts restent persistés.

### ORF-ITEM-020 — Armures et protection

Protection, encombrement, bruit, matériau, résistances et états de dommage sont validés.

### ORF-ITEM-021 — Munitions et projectiles

Tags, consommation, récupération et payload projectile sont authorés sans instanciation moteur.

### ORF-ITEM-022 — Usure et durabilité

Durabilité, usure, réparation, casse et seuils restent bornés par le validateur canonique.

### ORF-ITEM-023 — Conteneurs imbriqués

Capacité, profondeur, poids, tags et permissions sont préservés avec la détection de cycles.

### ORF-ITEM-024 — Loot tables

Groupes, poids, conditions, quantités et unicité sont édités et prévisualisés déterministement.

### ORF-ITEM-025 — Trésor par contexte

Sources, biome, faction, niveau, difficulté et tags restent des règles authorées, non exécutées.

### ORF-ITEM-026 — Objets uniques

La portée d’unicité est persistée sans réserver ni publier un objet dans un registre live.

### ORF-ITEM-027 — Butin instancié

Propriété, expiration et politique de distribution sont authorées sans transport réseau.

### ORF-ITEM-028 — Artisanat et matériaux

Recettes, ingrédients, station, compétence, qualité et sous-produits sont conservés.

### ORF-ITEM-029 — Génération procédurale d’objet

Seed, budget, pools, raretés, affixes et exclusions composent un plan reproductible local.

### ORF-ITEM-030 — Inspecteur d’inventaire

La surface présente les paramètres d’inventaire complets mais ne déplace aucun objet live.

### ORF-ITEM-031 — Événements d’objet

Les neuf événements et leurs handlers restent persistés sans exécution ForgeScript.

### ORF-ITEM-032 — Validation d’objet

Le candidat complet passe les validateurs projet et profils avant la commande canonique.

## Preuves

- portail incrémental réussi en **140,56 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-s-item-authoring.summary.json` ;
- build Studio sans avertissement et **42/42 tests ciblés** : Domain 19, RpgKit Domain 12,
  Application 7 et ModuleFormat 4 ;
- smoke Studio global réussi avec sortie native 0 : sélection explicite, création, identité,
  localisation, apparence, inventaire, usages, gameplay, équipement, combat, loot, artisanat,
  génération, événements, validation, dry-run, gardes source/approbation, commande canonique,
  transaction UI, undo/redo, save/reopen, voisins préservés, surface restaurée et workspace
  temporaire libéré ;
- capture Forward+ FHD inspectée dans
  `artifacts/verification/studio-increment/ux4-s-item-authoring-fhd.png` : cinq onglets, contexte
  isolé, résumé canonique, fermeture et cinq actions transactionnelles visibles à 1920×1080, sans
  UUID, JSON ni clipping bloquant ;
- la matrice atteint 313 routes `functional` et 404 `planned`. Le portail reste volontairement
  `full_gate=false` : il qualifie le lot Items, pas la totalité du produit.

## Préservation et limites honnêtes

- la preuve riche copie le module dans une session temporaire isolée et complète un objet seulement
  dans cet isolat ; projet, historique, état dirty, sélection et approbation source restent
  inchangés, puis le répertoire temporaire est libéré ;
- la transaction atomique préserve les blueprints voisins, les profils non ciblés et toutes les
  autres racines projet ; undo/redo et save/reopen restituent exactement les états attendus ;
- Studio ne charge aucun mesh, matériau, icône ou VFX, ne compile ni n’exécute ForgeScript, ne
  lance aucun projectile ou combat, ne déplace aucun inventaire live et n’effectue aucune
  transaction réseau ou économique.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~ItemFoundationProfileTests|' +
  'FullyQualifiedName~ItemCreationProfileTests|' +
  'FullyQualifiedName~ItemGameplayProfileTests|' +
  'FullyQualifiedName~ItemOperationsProfileTests|' +
  'FullyQualifiedName~ItemAdvancedProfileTests|' +
  'FullyQualifiedName~ItemFoundationRuntimeAdapterTests|' +
  'FullyQualifiedName~ItemGameplayRuntimeAdapterTests|' +
  'FullyQualifiedName~ItemOperationsRuntimeAdapterTests|' +
  'FullyQualifiedName~ItemInventoryServiceTests|' +
  'FullyQualifiedName~ItemGameplayServiceTests|' +
  'FullyQualifiedName~SetItemFoundationHandlerTests|' +
  'FullyQualifiedName~ItemAdvancedWorkflowTests|' +
  'FullyQualifiedName~SetItemAuthoringBundleHandlerTests|' +
  'FullyQualifiedName~ItemFoundationPersistenceTests|' +
  'FullyQualifiedName~QuestItemDepthPersistenceTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page items `
  -Evidence ux4-s-item-authoring -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux4-s-item-authoring-fhd.png `
  -NoMatrixCheck
```
