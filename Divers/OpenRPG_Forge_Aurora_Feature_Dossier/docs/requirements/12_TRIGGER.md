# Triggers, volumes, secrets et zones d’interaction

**Domaine : `TRIGGER`**  
**Préfixe : `ORF-TRG`**  
**Couche dominante : Forge Core + RPG Kit**  
**Nombre d’exigences : 20**  
**Sources principales : S03**

## Finalité

Polygones et volumes 3D, entrée/sortie/clic, transitions, pièges, repos, découverte, pistes et interjections.

## Règles de domaine

- Le domaine respecte les décisions architecturales du MEGACONTEXT OpenRPG Forge.
- Une propriété historique liée à D&D est reformulée comme donnée de ruleset ou option de kit.
- Les noms historiques sont utilisés uniquement pour expliquer la référence.
- Les données canoniques sont textuelles, versionnées et indépendantes des ressources Godot.
- Les opérations d’édition passent par commandes, validation et undo/redo.
- Les opérations runtime sensibles sont autoritaires côté serveur lorsqu’un Network Kit est actif.
- Les opérations IA utilisent les mêmes commandes via MCP.

## Index des exigences

| ID | Fonctionnalité | Priorité | Origine |
|---|---|---:|---|
| [ORF-TRG-001](#orf-trg-001) | Création polygonale | P0 | Native Aurora modernized |
| [ORF-TRG-002](#orf-trg-002) | Trigger générique | P0 | Native Aurora |
| [ORF-TRG-003](#orf-trg-003) | Trigger de transition | P0 | Native Aurora |
| [ORF-TRG-004](#orf-trg-004) | Trigger de piège | P1 | Native Aurora |
| [ORF-TRG-005](#orf-trg-005) | Trigger secret | P2 | Native Aurora ecosystem |
| [ORF-TRG-006](#orf-trg-006) | OnEnter | P0 | Native Aurora |
| [ORF-TRG-007](#orf-trg-007) | OnExit | P0 | Native Aurora |
| [ORF-TRG-008](#orf-trg-008) | OnClick | P2 | Native Aurora |
| [ORF-TRG-009](#orf-trg-009) | OnHeartbeat | P2 | Native Aurora modernized |
| [ORF-TRG-010](#orf-trg-010) | OnUserDefined | P2 | Native Aurora modernized |
| [ORF-TRG-011](#orf-trg-011) | Filtre de cible | P1 | Native Aurora modernized |
| [ORF-TRG-012](#orf-trg-012) | Clé requise | P2 | Native Aurora |
| [ORF-TRG-013](#orf-trg-013) | Curseur et surbrillance | P2 | Native Aurora |
| [ORF-TRG-014](#orf-trg-014) | Trigger de repos sûr | P2 | Official expansion ecosystem |
| [ORF-TRG-015](#orf-trg-015) | Trigger de pistes | P3 | Official expansion ecosystem |
| [ORF-TRG-016](#orf-trg-016) | Trigger d’interjection | P2 | Official expansion ecosystem |
| [ORF-TRG-017](#orf-trg-017) | Volumes de gameplay | P1 | Native Aurora modernized |
| [ORF-TRG-018](#orf-trg-018) | Déclenchement manuel par GM | P2 | Native Aurora |
| [ORF-TRG-019](#orf-trg-019) | Visualisation d’occupation | P1 | Forge extension |
| [ORF-TRG-020](#orf-trg-020) | Validation de trigger | P0 | Forge extension |

## Exigences détaillées

<a id="orf-trg-001"></a>

### ORF-TRG-001 — Création polygonale

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | AREA |
| Sources | S03 |
| Tags | `volume`, `polygon`, `editor` |

**Comportement Aurora de référence.** Aurora dessine un polygone au sol.

**Adaptation obligatoire pour OpenRPG Forge.** Outil volume 2D extrudé ou forme 3D, édition de sommets, snap, trous, hauteur et preview runtime.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Création polygonale » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-trg-002"></a>

### ORF-TRG-002 — Trigger générique

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | SCRIPT |
| Sources | S03 |
| Tags | `trigger`, `volume`, `event` |

**Comportement Aurora de référence.** Aurora possède generic trigger avec événements.

**Adaptation obligatoire pour OpenRPG Forge.** TriggerVolume à filtres, conditions, cooldown, fire policy et actions typées.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites
- version de l’API ForgeScript
- binding d’événement
- diagnostics structurés

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Trigger générique » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-trg-003"></a>

### ORF-TRG-003 — Trigger de transition

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | AREA, WAYPOINT |
| Sources | S03 |
| Tags | `transition`, `trigger`, `portal` |

**Comportement Aurora de référence.** Un trigger peut relier des zones.

**Adaptation obligatoire pour OpenRPG Forge.** PortalVolume partage PortalComponent avec portes, destination et preloading.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Trigger de transition » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-trg-004"></a>

### ORF-TRG-004 — Trigger de piège

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | SCRIPT |
| Sources | S03 |
| Tags | `trap`, `trigger`, `detect` |

**Comportement Aurora de référence.** Aurora crée des trap triggers.

**Adaptation obligatoire pour OpenRPG Forge.** TrapVolume avec detection, disarm, payload, reset, ownership et visualization GM.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Trigger de piège » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-trg-005"></a>

### ORF-TRG-005 — Trigger secret

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora ecosystem |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | CREATURE |
| Sources | S03 |
| Tags | `secret`, `discovery`, `perception` |

**Comportement Aurora de référence.** Les Secret Object Triggers révèlent contenu selon recherche.

**Adaptation obligatoire pour OpenRPG Forge.** DiscoveryVolume avec senses, difficulté, reveal target, feedback, permanence et scope joueur/parti.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Trigger secret » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-trg-006"></a>

### ORF-TRG-006 — OnEnter

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | SCRIPT |
| Sources | S03 |
| Tags | `event`, `enter`, `volume` |

**Comportement Aurora de référence.** Événement lorsqu’une créature entre.

**Adaptation obligatoire pour OpenRPG Forge.** VolumeEntered avec entité, point, direction, layers et state.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites
- version de l’API ForgeScript
- binding d’événement
- diagnostics structurés

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « OnEnter » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-trg-007"></a>

### ORF-TRG-007 — OnExit

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | SCRIPT |
| Sources | S03 |
| Tags | `event`, `exit`, `volume` |

**Comportement Aurora de référence.** Événement lorsqu’une créature sort.

**Adaptation obligatoire pour OpenRPG Forge.** VolumeExited avec durée passée et raison.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites
- version de l’API ForgeScript
- binding d’événement
- diagnostics structurés

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « OnExit » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-trg-008"></a>

### ORF-TRG-008 — OnClick

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | SCRIPT |
| Sources | S03 |
| Tags | `event`, `click`, `interaction` |

**Comportement Aurora de référence.** Certains triggers peuvent être cliquables.

**Adaptation obligatoire pour OpenRPG Forge.** VolumeInteracted avec hit point, verb et acteur.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites
- version de l’API ForgeScript
- binding d’événement
- diagnostics structurés

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « OnClick » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-trg-009"></a>

### ORF-TRG-009 — OnHeartbeat

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | SCRIPT, QA |
| Sources | S03 |
| Tags | `heartbeat`, `occupied`, `performance` |

**Comportement Aurora de référence.** Trigger peut exécuter périodiquement.

**Adaptation obligatoire pour OpenRPG Forge.** WhileOccupied scheduler événementiel, cadence, budget et agrégation des occupants.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « OnHeartbeat » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-trg-010"></a>

### ORF-TRG-010 — OnUserDefined

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | SCRIPT |
| Sources | S03 |
| Tags | `event`, `custom` |

**Comportement Aurora de référence.** Extension custom.

**Adaptation obligatoire pour OpenRPG Forge.** CustomEvent namespacé au trigger.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites
- version de l’API ForgeScript
- binding d’événement
- diagnostics structurés

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « OnUserDefined » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-trg-011"></a>

### ORF-TRG-011 — Filtre de cible

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | FACTION |
| Sources | S03 |
| Tags | `filter`, `query`, `target` |

**Comportement Aurora de référence.** Aurora utilise faction et player-only.

**Adaptation obligatoire pour OpenRPG Forge.** EntityQuery typée: joueurs, tags, factions, composants, propriétaire, équipe et prédicat sandbox.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Filtre de cible » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-trg-012"></a>

### ORF-TRG-012 — Clé requise

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | ITEM |
| Sources | S03 |
| Tags | `key`, `access`, `trigger` |

**Comportement Aurora de référence.** Certains triggers utilisent une clé.

**Adaptation obligatoire pour OpenRPG Forge.** AccessCondition utilisant credentials sans consommation automatique.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Clé requise » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-trg-013"></a>

### ORF-TRG-013 — Curseur et surbrillance

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | S03 |
| Tags | `cursor`, `highlight`, `ui` |

**Comportement Aurora de référence.** Aurora configure cursor et highlight.

**Adaptation obligatoire pour OpenRPG Forge.** InteractionPresentation: icon, outline, prompt, visibility et permission.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Curseur et surbrillance » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-trg-014"></a>

### ORF-TRG-014 — Trigger de repos sûr

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge RPG Kit |
| Origine | Official expansion ecosystem |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | PROJECT |
| Sources | S03 |
| Tags | `rest`, `safe`, `volume` |

**Comportement Aurora de référence.** X0_SAFEREST marque des zones sûres.

**Adaptation obligatoire pour OpenRPG Forge.** RestVolume avec policy, danger query, party checks, camp setup et messages.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Trigger de repos sûr » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-trg-015"></a>

### ORF-TRG-015 — Trigger de pistes

| Champ | Valeur |
|---|---|
| Priorité | **P3** |
| Couche | Forge RPG Kit |
| Origine | Official expansion ecosystem |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | QUEST |
| Sources | S03 |
| Tags | `tracks`, `clue`, `skill` |

**Comportement Aurora de référence.** Tracks trigger révèle indices.

**Adaptation obligatoire pour OpenRPG Forge.** ClueVolume avec skill checks, traces, direction, âge, faux indices et journal.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Trigger de pistes » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-trg-016"></a>

### ORF-TRG-016 — Trigger d’interjection

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge RPG Kit |
| Origine | Official expansion ecosystem |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | DIALOGUE, COMPANION |
| Sources | S03 |
| Tags | `interjection`, `narrative`, `companion` |

**Comportement Aurora de référence.** XP2 utilise volumes pour one-liners et interjections.

**Adaptation obligatoire pour OpenRPG Forge.** NarrativeCueVolume avec participants, priority, once/repeat, cooldown et multiplayer policy.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Trigger d’interjection » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-trg-017"></a>

### ORF-TRG-017 — Volumes de gameplay

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core + Kits |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | GAMEPLAY |
| Sources | S15 |
| Tags | `volume`, `gameplay`, `library` |

**Comportement Aurora de référence.** Aurora générique permet de nombreux usages par script.

**Adaptation obligatoire pour OpenRPG Forge.** Bibliothèque Damage, Heal, Gravity, Water, Climb, NoBuild, Camera, Audio, Weather et Checkpoint volumes.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Volumes de gameplay » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-trg-018"></a>

### ORF-TRG-018 — Déclenchement manuel par GM

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Live Director Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.LiveDirector` |
| Dépendances | DM |
| Sources | S01 |
| Tags | `dm`, `trigger`, `manual` |

**Comportement Aurora de référence.** Le DM peut déclencher des scripts ou événements.

**Adaptation obligatoire pour OpenRPG Forge.** Live Director peut simuler enter/exit/fire avec cible et audit.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Déclenchement manuel par GM » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-trg-019"></a>

### ORF-TRG-019 — Visualisation d’occupation

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `debug`, `volume`, `trace` |

**Comportement Aurora de référence.** Le Toolset montre la forme mais pas toujours l’état runtime.

**Adaptation obligatoire pour OpenRPG Forge.** Debug overlay occupants, derniers fires, cooldowns, filters et event trace.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Visualisation d’occupation » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-trg-020"></a>

### ORF-TRG-020 — Validation de trigger

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `validation`, `trigger`, `geometry` |

**Comportement Aurora de référence.** Les volumes peuvent être auto-intersectés ou sans destination.

**Adaptation obligatoire pour OpenRPG Forge.** Vérifier géométrie, filters, actions, portal, target, cooldown, scripts et accessibilité.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- TransformData indépendant de Godot
- AssetId/BlueprintId
- overrides explicites

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- gizmo/overlay dans le viewport
- prévisualisation avant application

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- géométrie valide
- collision/navigation cohérentes
- transformations finies et dans les limites

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Validation de trigger » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
