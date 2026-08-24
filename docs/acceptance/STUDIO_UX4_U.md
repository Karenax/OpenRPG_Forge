# Acceptation Studio — UX4-U Compagnons complets

État local au 20 août 2026 : **fonctionnel et qualifié** pour les 24 routes
`ORF-CMP-001..024`. La page **Contenu social** expose une surface Compagnons en cinq onglets et
remplace uniquement le profil ciblé via `SetWorldFeatureCatalogHandler`, dans une transaction
canonique annulable.

## Parcours canonique

La sélection d’un compagnon existant ou sa création explicite précède toute édition. L’aperçu sec
croise créatures, dialogues, quête, objets, zones et scripts réels, puis résout les contrats
Foundation, Control, Experience et Validation. L’approbation est liée à la révision et aux
empreintes du projet, du brouillon et du candidat. Appliquer, annuler, rétablir et enregistrer
passent par l’historique et le round-trip du workspace.

### ORF-CMP-001 — Créature recrutable

Le compagnon référence une créature existante et conserve une identité stable sans exposer son
UUID dans la surface.

### ORF-CMP-002 — Conversation avant recrutement

Dialogue, contexte, conditions, nœuds d’offre/refus et conséquences sont authorés ensemble.

### ORF-CMP-003 — Conversation avec son maître

Le contexte propriétaire expose explicitement commandes, relation, équipement, progression et départ.

### ORF-CMP-004 — Conversation avec non-maître

Les comportements propriétaire, groupe, tiers, maître de jeu et hostile sont séparés et persistés.

### ORF-CMP-005 — Recrutement et renvoi

Capacité, propriété, inventaire, position de renvoi et persistance forment une transaction atomique.

### ORF-CMP-006 — Plusieurs compagnons

Les capacités compagnons, invocations, familiers, alliés, serveur et limites de rôle sont validées.

### ORF-CMP-007 — Propriété du compagnon

Type de propriété, contrôleurs, transfert, cibles autorisées et révision sont éditables.

### ORF-CMP-008 — Ordres de déplacement

Six ordres de déplacement, formation, requête waypoint et priorités sont authorés.

### ORF-CMP-009 — Ordres de combat

Six ordres tactiques, quatre postures, capacités et priorités composent la politique de combat.

### ORF-CMP-010 — Comportement autonome

Autonomie, risque, ressources, tirs alliés, rôle, distance et autorisation produisent un plan pur.

### ORF-CMP-011 — Formation

Identité, place, espacement, adaptation aux obstacles, intérieur et synchronisation sont persistés.

### ORF-CMP-012 — Équipement du compagnon

Permissions, comparaison, propriétaire d’inventaire, loadout humain, verrous et contrat de slots
sont cohérents.

### ORF-CMP-013 — Progression et level-up

Mode, niveau courant/maximal, aperçu, respecialisation et jalons sont conservés.

### ORF-CMP-014 — Relation et loyauté

Affinité, confiance, peur, loyauté, tags et historique sont bornés et prévisualisés.

### ORF-CMP-015 — Banter et interjections

Participants réels, priorité, cooldown, lieux, interruption et dialogue sont authorés sans lecture live.

### ORF-CMP-016 — Quêtes personnelles

Quête existante, disponibilité, état de départ, loyauté minimale et conséquence sont liés.

### ORF-CMP-017 — Mort, inconscience et résurrection

Mode, durée, ressource, remplaçant, mémorial et inventaire couvrent le cycle persistant.

### ORF-CMP-018 — Téléportation de rattrapage

Seuils navigation/portail/téléportation, suivi des portails et ancres sûres sont validés.

### ORF-CMP-019 — Gestion des pièges

Détection, désarmement, risque, sens, compétence et ordre joueur restent data-driven.

### ORF-CMP-020 — Utilisation de sorts/capacités

Capacités autorisées/réservées, seuil de santé, cible et override joueur sont persistés.

### ORF-CMP-021 — Contrôle direct

Possession, changement de personnage, retour IA et permission multijoueur sont explicités.

### ORF-CMP-022 — Interface de groupe

Portraits, santé, ressources, statuts, ordres, réordre, clavier, manette, lecteur d’écran et
contraste composent un HUD accessible.

### ORF-CMP-023 — Séparation et réunion du groupe

Suivi du propriétaire, regroupement, instance, synchronisation des quêtes et délai sont authorés.

### ORF-CMP-024 — Validation compagnon

Recrutement, propriété, commandes, équipement, mort, dialogues et navigation inter-zones passent
le validateur canonique avant toute mutation.

## Preuves

- portail incrémental réussi en **132,09 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-u-companion-authoring.summary.json` ;
- build Studio sans avertissement et **66/66 tests ciblés** : Domain 2, RpgKit Domain 22,
  Application 27 et ModuleFormat 15 ;
- smoke Studio global réussi avec sortie native 0 : sélection explicite, recrutement/dialogues,
  groupe, propriété/déplacement, tactique/IA/formation, équipement/progression,
  relation/banter/quête, mort/cohésion, pièges/capacités/contrôle direct, HUD/séparation,
  validation, dry-run, gardes source/approbation, commande canonique, transaction UI, undo/redo,
  save/reopen, voisins préservés, surface restaurée et workspace temporaire libéré ;
- capture Forward+ FHD inspectée dans
  `artifacts/verification/studio-increment/ux4-u-companion-authoring-fhd.png` : cinq onglets,
  sélection humaine, fallback canonique isolé, aperçu et cinq actions transactionnelles visibles à
  1920×1080, sans UUID, JSON ni clipping bloquant ;
- la matrice atteint 356 routes `functional` et 361 `planned`. UX4 est close sur ses 140 routes.
  Le résumé incrémental reste volontairement `full_gate=false` ; le portail complet clôt le jalon.

## Préservation et limites honnêtes

- Crypte des Cendres n’authore aucun compagnon dans son document source ; la preuve riche ajoute
  donc Maëlys uniquement dans une copie temporaire explicitement étiquetée et y copie les vrais
  scripts `.forge`, puis prouve projet, historique, dirty, sélection et approbation source inchangés ;
- la transaction remplace seulement le compagnon ciblé et préserve compagnons voisins, toutes les
  autres familles WorldFeatures et toutes les racines projet ;
- Studio n’instancie ni ne déplace aucun acteur, ne joue aucun dialogue, n’exécute aucun script,
  n’ouvre aucune quête ou interface runtime et n’effectue aucun transfert réseau. Les résultats
  affichés sont des validations et simulations déterministes en mémoire.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~Companion|' +
  'FullyQualifiedName~WorldFeatureCatalogWorkflowTests|' +
  'FullyQualifiedName~WorldFeaturePersistenceTests|' +
  'FullyQualifiedName~CryptOfAshesSampleTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page social-content `
  -Evidence ux4-u-companion-authoring `
  -Workspace apps/Forge.Runtime/content/crypt-of-ashes `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux4-u-companion-authoring-fhd.png `
  -NoMatrixCheck
```
