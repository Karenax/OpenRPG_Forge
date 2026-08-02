# Zones, construction 3D et environnement

**Domaine : `AREA`**  
**Préfixe : `ORF-AREA`**  
**Couche dominante : Forge Core**  
**Nombre d’exigences : 37**  
**Sources principales : S01, S03, S04, S06, S08, S12**

## Finalité

Création de zones, kits modulaires, placement libre, environnement, navigation, cartes, transitions, performances et édition multi-zones.

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
| [ORF-AREA-001](#orf-area-001) | Assistant de création de zone | P0 | Native Aurora |
| [ORF-AREA-002](#orf-area-002) | Document de zone indépendant | P0 | Native Aurora modernized |
| [ORF-AREA-003](#orf-area-003) | Construction par tuiles | P0 | Native Aurora |
| [ORF-AREA-004](#orf-area-004) | Terrain de base | P2 | Native Aurora modernized |
| [ORF-AREA-005](#orf-area-005) | Features de tileset | P2 | Native Aurora |
| [ORF-AREA-006](#orf-area-006) | Groupes de tuiles | P1 | Native Aurora |
| [ORF-AREA-007](#orf-area-007) | Effaceur et remplacement | P1 | Native Aurora |
| [ORF-AREA-008](#orf-area-008) | Rotation et variantes de tuile | P0 | Native Aurora |
| [ORF-AREA-009](#orf-area-009) | Ajustement aléatoire compatible | P1 | Native Aurora |
| [ORF-AREA-010](#orf-area-010) | Propriétés de tuile | P1 | Native Aurora |
| [ORF-AREA-011](#orf-area-011) | Redimensionnement de zone | P1 | Native Aurora |
| [ORF-AREA-012](#orf-area-012) | Copie de zone | P1 | Native Aurora |
| [ORF-AREA-013](#orf-area-013) | Zones intérieures/extérieures | P1 | Native Aurora |
| [ORF-AREA-014](#orf-area-014) | Cycle jour/nuit | P1 | Native Aurora |
| [ORF-AREA-015](#orf-area-015) | Lumière ambiante et directionnelle | P1 | Native Aurora modernized |
| [ORF-AREA-016](#orf-area-016) | Brouillard de zone | P1 | Native Aurora modernized |
| [ORF-AREA-017](#orf-area-017) | Vent | P2 | Native Aurora modernized |
| [ORF-AREA-018](#orf-area-018) | Météo | P2 | Native Aurora modernized |
| [ORF-AREA-019](#orf-area-019) | Skybox et ciel | P2 | Native Aurora modernized |
| [ORF-AREA-020](#orf-area-020) | Eau | P2 | Enhanced Edition inspired |
| [ORF-AREA-021](#orf-area-021) | Audio de zone | P1 | Native Aurora |
| [ORF-AREA-022](#orf-area-022) | Événements d’entrée/sortie | P0 | Native Aurora |
| [ORF-AREA-023](#orf-area-023) | Événement heartbeat de zone | P2 | Native Aurora |
| [ORF-AREA-024](#orf-area-024) | Événement utilisateur de zone | P1 | Native Aurora modernized |
| [ORF-AREA-025](#orf-area-025) | Modificateurs de perception | P2 | Native Aurora adapted |
| [ORF-AREA-026](#orf-area-026) | Interdiction de repos | P2 | Native Aurora |
| [ORF-AREA-027](#orf-area-027) | Mode PvP de zone | P2 | Native Aurora adapted |
| [ORF-AREA-028](#orf-area-028) | Écran de chargement | P2 | Native Aurora |
| [ORF-AREA-029](#orf-area-029) | Carte et brouillard de guerre | P1 | Native Aurora |
| [ORF-AREA-030](#orf-area-030) | Transitions de zone | P0 | Native Aurora |
| [ORF-AREA-031](#orf-area-031) | Navigation de surface | P0 | Native Aurora modernized |
| [ORF-AREA-032](#orf-area-032) | Navigation volumétrique | P2 | Forge extension |
| [ORF-AREA-033](#orf-area-033) | Occlusion et visibilité | P1 | Forge extension |
| [ORF-AREA-034](#orf-area-034) | LOD par zone et asset | P1 | Enhanced Edition |
| [ORF-AREA-035](#orf-area-035) | Couches et groupes d’édition | P1 | Forge extension |
| [ORF-AREA-036](#orf-area-036) | Play From Here | P0 | Forge extension |
| [ORF-AREA-037](#orf-area-037) | Validation de zone | P0 | Forge extension |

## Exigences détaillées

<a id="orf-area-001"></a>

### ORF-AREA-001 — Assistant de création de zone

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | CONTENT |
| Sources | S01, S03 |
| Tags | `wizard`, `area`, `template` |

**Comportement Aurora de référence.** L’Area Wizard choisit nom, tileset et dimensions.

**Adaptation obligatoire pour OpenRPG Forge.** Wizard de zone choisissant type, kit, dimensions, terrain, éclairage, navigation, streaming, biome et template.

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

- [ ] Un auteur peut configurer « Assistant de création de zone » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-002"></a>

### ORF-AREA-002 — Document de zone indépendant

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | BUILD |
| Sources | S17, S18 |
| Tags | `data`, `json`, `area` |

**Comportement Aurora de référence.** Aurora stocke la zone dans les formats du moteur.

**Adaptation obligatoire pour OpenRPG Forge.** AreaDocument JSON canonique avec UUID, métadonnées, environnement, instances, volumes, navigation et dépendances.

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

- [ ] Un auteur peut configurer « Document de zone indépendant » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-003"></a>

### ORF-AREA-003 — Construction par tuiles

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | PALETTE |
| Sources | S01, S03 |
| Tags | `tiles`, `modular`, `sockets` |

**Comportement Aurora de référence.** Aurora peint terrain, features et groupes d’un tileset.

**Adaptation obligatoire pour OpenRPG Forge.** Système de kits modulaires avec grille, sockets, règles de raccord, variantes, groupes et pinceaux.

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

- [ ] Un auteur peut configurer « Construction par tuiles » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-004"></a>

### ORF-AREA-004 — Terrain de base

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | CONTENT |
| Sources | S03 |
| Tags | `terrain`, `provider`, `landscape` |

**Comportement Aurora de référence.** Aurora change des tuiles de terrain dans une grille.

**Adaptation obligatoire pour OpenRPG Forge.** Interface TerrainProvider pour heightmap, voxels ou mesh; couches, trous, falaises, routes et sérialisation.

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

- [ ] Un auteur peut configurer « Terrain de base » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-005"></a>

### ORF-AREA-005 — Features de tileset

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | PALETTE |
| Sources | S03 |
| Tags | `features`, `splines`, `procedural` |

**Comportement Aurora de référence.** Aurora ajoute routes, rivières, bâtiments et détails compatibles.

**Adaptation obligatoire pour OpenRPG Forge.** Règles paramétriques de features, splines, intersections, ponts, murs et validation de compatibilité.

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

- [ ] Un auteur peut configurer « Features de tileset » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-006"></a>

### ORF-AREA-006 — Groupes de tuiles

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | PALETTE |
| Sources | S03 |
| Tags | `groups`, `prefab`, `placement` |

**Comportement Aurora de référence.** Aurora place des ensembles préconstruits.

**Adaptation obligatoire pour OpenRPG Forge.** Prefab de zone composé, paramètres exposés, empreinte, sockets, variantes et conversion en instances éditables.

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

- [ ] Un auteur peut configurer « Groupes de tuiles » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-007"></a>

### ORF-AREA-007 — Effaceur et remplacement

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | Aucune dépendance explicite; respecter les dépendances du domaine. |
| Sources | S03 |
| Tags | `erase`, `replace`, `editing` |

**Comportement Aurora de référence.** Le pinceau peut effacer ou remplacer une tuile.

**Adaptation obligatoire pour OpenRPG Forge.** Outils supprimer, remplacer, remapper kit, remplir, sélectionner par type et préserver objets attachés.

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

- [ ] Un auteur peut configurer « Effaceur et remplacement » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-008"></a>

### ORF-AREA-008 — Rotation et variantes de tuile

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | Aucune dépendance explicite; respecter les dépendances du domaine. |
| Sources | S03 |
| Tags | `rotate`, `variant`, `tiles` |

**Comportement Aurora de référence.** Les tuiles peuvent être tournées et avoir des états.

**Adaptation obligatoire pour OpenRPG Forge.** Rotation discrète ou libre selon kit, symétrie, variante sémantique et prévisualisation de raccord.

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

- [ ] Un auteur peut configurer « Rotation et variantes de tuile » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-009"></a>

### ORF-AREA-009 — Ajustement aléatoire compatible

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | S03 |
| Tags | `autotile`, `determinism`, `sockets` |

**Comportement Aurora de référence.** Aurora peut choisir automatiquement une tuile qui s’adapte.

**Adaptation obligatoire pour OpenRPG Forge.** Auto-tiling déterministe fondé sur sockets et seed, avec verrouillage manuel et rapport des ambiguïtés.

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

- [ ] Un auteur peut configurer « Ajustement aléatoire compatible » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-010"></a>

### ORF-AREA-010 — Propriétés de tuile

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | PALETTE |
| Sources | S03 |
| Tags | `tile`, `properties`, `override` |

**Comportement Aurora de référence.** Aurora permet animation de boucle et lumières par tuile.

**Adaptation obligatoire pour OpenRPG Forge.** TileInstance expose état, animation, matériaux, lumières, collision, navigation, sockets et overrides.

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

- [ ] Un auteur peut configurer « Propriétés de tuile » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-011"></a>

### ORF-AREA-011 — Redimensionnement de zone

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | BUILD |
| Sources | S03 |
| Tags | `resize`, `area`, `migration` |

**Comportement Aurora de référence.** Les zones historiques sont agrandies/réduites en grille.

**Adaptation obligatoire pour OpenRPG Forge.** Redimensionnement avec origine choisie, aperçu des pertes, migration d’instances, limites et undo atomique.

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

- [ ] Un auteur peut configurer « Redimensionnement de zone » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-012"></a>

### ORF-AREA-012 — Copie de zone

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | BUILD |
| Sources | S03 |
| Tags | `clone`, `area`, `references` |

**Comportement Aurora de référence.** Aurora permet de copier une zone.

**Adaptation obligatoire pour OpenRPG Forge.** Clone complet ou template, nouveaux UUID, remapping des transitions, choix des références partagées et diff.

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

- [ ] Un auteur peut configurer « Copie de zone » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-013"></a>

### ORF-AREA-013 — Zones intérieures/extérieures

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | GAMEPLAY |
| Sources | S03 |
| Tags | `classification`, `environment`, `tags` |

**Comportement Aurora de référence.** Aurora marque intérieur, extérieur, naturel, artificiel ou souterrain.

**Adaptation obligatoire pour OpenRPG Forge.** AreaClassification extensible pilotant rendu, audio, navigation, météo, caméra et règles de gameplay.

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

- [ ] Un auteur peut configurer « Zones intérieures/extérieures » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-014"></a>

### ORF-AREA-014 — Cycle jour/nuit

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | PROJECT |
| Sources | S03 |
| Tags | `day-night`, `lighting`, `time` |

**Comportement Aurora de référence.** Aurora propose cycle normal, toujours clair ou toujours sombre.

**Adaptation obligatoire pour OpenRPG Forge.** LightingProfile avec horloge, courbes soleil/lune, transitions, événements et overrides par volume.

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

- [ ] Un auteur peut configurer « Cycle jour/nuit » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-015"></a>

### ORF-AREA-015 — Lumière ambiante et directionnelle

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | EE |
| Sources | S03, S06 |
| Tags | `lighting`, `environment`, `hdr` |

**Comportement Aurora de référence.** Aurora règle couleurs ambiantes, diffuse soleil/lune et ombres.

**Adaptation obligatoire pour OpenRPG Forge.** Presets physiques, température, intensité, HDR, ombres, probes et paramètres simplifiés/avancés.

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

- [ ] Un auteur peut configurer « Lumière ambiante et directionnelle » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-016"></a>

### ORF-AREA-016 — Brouillard de zone

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | EE |
| Sources | S03, S06 |
| Tags | `fog`, `volume`, `quality` |

**Comportement Aurora de référence.** Aurora configure fog distance et couleur jour/nuit.

**Adaptation obligatoire pour OpenRPG Forge.** FogProfile volumétrique ou distance, densité, hauteur, couleur, bruit, volumes locaux et profils qualité.

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

- [ ] Un auteur peut configurer « Brouillard de zone » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-017"></a>

### ORF-AREA-017 — Vent

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | AUDIO |
| Sources | S03 |
| Tags | `wind`, `environment`, `physics` |

**Comportement Aurora de référence.** Aurora possède un niveau de vent.

**Adaptation obligatoire pour OpenRPG Forge.** Champ de vent global/local avec direction, rafales, influence végétation, particules, audio et gameplay.

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

- [ ] Un auteur peut configurer « Vent » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-018"></a>

### ORF-AREA-018 — Météo

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | SCRIPT, AUDIO, MULTI |
| Sources | S03 |
| Tags | `weather`, `vfx`, `audio` |

**Comportement Aurora de référence.** Aurora active pluie, neige et éclairs selon tileset.

**Adaptation obligatoire pour OpenRPG Forge.** WeatherSystem data-driven avec transitions, intensité, humidité, surface, audio, foudre, réseau et script.

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

- [ ] Un auteur peut configurer « Météo » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-019"></a>

### ORF-AREA-019 — Skybox et ciel

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | CONTENT |
| Sources | S03, S06 |
| Tags | `sky`, `hdr`, `environment` |

**Comportement Aurora de référence.** Aurora sélectionne une skybox.

**Adaptation obligatoire pour OpenRPG Forge.** SkyProfile: ciel physique, HDRI, étoiles, nuages, lune, horizon, exposition et transition.

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

- [ ] Un auteur peut configurer « Skybox et ciel » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-020"></a>

### ORF-AREA-020 — Eau

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core |
| Origine | Enhanced Edition inspired |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | GAMEPLAY, EE |
| Sources | S06, S08 |
| Tags | `water`, `shader`, `physics` |

**Comportement Aurora de référence.** EE améliore rendu de l’eau.

**Adaptation obligatoire pour OpenRPG Forge.** WaterProvider pour plans, rivières et volumes; shader, profondeur, flottabilité, navigation, son et qualité.

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

- [ ] Un auteur peut configurer « Eau » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-021"></a>

### ORF-AREA-021 — Audio de zone

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | AUDIO |
| Sources | S03 |
| Tags | `audio`, `music`, `reverb` |

**Comportement Aurora de référence.** Aurora choisit musique jour/nuit/combat, ambiance et reverb.

**Adaptation obligatoire pour OpenRPG Forge.** AreaAudioProfile avec snapshots de mixage, couches adaptatives, zones de réverbération et transitions.

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

- [ ] Un auteur peut configurer « Audio de zone » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-022"></a>

### ORF-AREA-022 — Événements d’entrée/sortie

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | SCRIPT |
| Sources | S03 |
| Tags | `event`, `area`, `script` |

**Comportement Aurora de référence.** Aurora expose OnEnter et OnExit.

**Adaptation obligatoire pour OpenRPG Forge.** AreaEntered/AreaExited avec acteur, transition, cause, ancien/nouveau contexte et autorité.

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

- [ ] Un auteur peut configurer « Événements d’entrée/sortie » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-023"></a>

### ORF-AREA-023 — Événement heartbeat de zone

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | SCRIPT, QA |
| Sources | S03 |
| Tags | `heartbeat`, `scheduler`, `performance` |

**Comportement Aurora de référence.** Aurora peut exécuter une routine périodique par zone.

**Adaptation obligatoire pour OpenRPG Forge.** Scheduler local optionnel avec cadence, budget, suspension hors ligne et avertissement de performance.

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

- [ ] Un auteur peut configurer « Événement heartbeat de zone » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-024"></a>

### ORF-AREA-024 — Événement utilisateur de zone

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | SCRIPT |
| Sources | S03 |
| Tags | `event`, `custom`, `area` |

**Comportement Aurora de référence.** OnUserDefined reçoit des signaux personnalisés.

**Adaptation obligatoire pour OpenRPG Forge.** Bus d’événements namespacés au scope zone, payload validé et abonnements ForgeScript.

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

- [ ] Un auteur peut configurer « Événement utilisateur de zone » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-025"></a>

### ORF-AREA-025 — Modificateurs de perception

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora adapted |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | CREATURE |
| Sources | S03 |
| Tags | `perception`, `modifier`, `rules` |

**Comportement Aurora de référence.** Aurora fournit Listen/Spot modifiers.

**Adaptation obligatoire pour OpenRPG Forge.** EnvironmentPerceptionModifiers génériques par sens et profil de règles, sans compétences D&D codées en dur.

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

- [ ] Un auteur peut configurer « Modificateurs de perception » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-026"></a>

### ORF-AREA-026 — Interdiction de repos

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | TRIGGER, SCRIPT |
| Sources | S03 |
| Tags | `rest`, `policy`, `area` |

**Comportement Aurora de référence.** Une zone peut empêcher le repos.

**Adaptation obligatoire pour OpenRPG Forge.** RestPolicy par zone/volume avec raisons, conditions, cooldown, sécurité et UI explicative.

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

- [ ] Un auteur peut configurer « Interdiction de repos » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-027"></a>

### ORF-AREA-027 — Mode PvP de zone

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Network Kit |
| Origine | Native Aurora adapted |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | MULTI, FACTION |
| Sources | S03 |
| Tags | `pvp`, `combat`, `policy` |

**Comportement Aurora de référence.** Aurora possède des paramètres PvP.

**Adaptation obligatoire pour OpenRPG Forge.** CombatPolicy par zone: friendly fire, duel, crime, aggro, dégâts environnementaux et autorité serveur.

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

- [ ] Un auteur peut configurer « Mode PvP de zone » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-028"></a>

### ORF-AREA-028 — Écran de chargement

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | CONTENT |
| Sources | S03 |
| Tags | `loading`, `streaming`, `ux` |

**Comportement Aurora de référence.** Aurora associe un loading screen à une zone.

**Adaptation obligatoire pour OpenRPG Forge.** LoadingPresentation avec image/vidéo/astuce/progression, fallback accessible et préchargement réel.

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

- [ ] Un auteur peut configurer « Écran de chargement » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-029"></a>

### ORF-AREA-029 — Carte et brouillard de guerre

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core + RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | WAYPOINT, SCRIPT |
| Sources | S03 |
| Tags | `map`, `fog-of-war`, `player-state` |

**Comportement Aurora de référence.** Aurora produit une minimap et permet de révéler la carte par script.

**Adaptation obligatoire pour OpenRPG Forge.** Carte 2D/3D générée, exploration par joueur/parti, pins, étages, fog of war et API de révélation.

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

- [ ] Un auteur peut configurer « Carte et brouillard de guerre » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-030"></a>

### ORF-AREA-030 — Transitions de zone

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | DOOR, TRIGGER, WAYPOINT |
| Sources | S03 |
| Tags | `transition`, `portal`, `streaming` |

**Comportement Aurora de référence.** Aurora relie portes, triggers et waypoints.

**Adaptation obligatoire pour OpenRPG Forge.** PortalLink typé, bidirectionnel ou non, validation de destination, orientation, préchargement et hooks.

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

- [ ] Un auteur peut configurer « Transitions de zone » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-031"></a>

### ORF-AREA-031 — Navigation de surface

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | S07, S08, S11 |
| Tags | `navmesh`, `pathfinding`, `agents` |

**Comportement Aurora de référence.** Aurora utilise walkmeshes.

**Adaptation obligatoire pour OpenRPG Forge.** Navmesh par zone, agents de tailles multiples, liens hors-mesh, rebuild partiel et visualisation des erreurs.

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

- [ ] Un auteur peut configurer « Navigation de surface » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-032"></a>

### ORF-AREA-032 — Navigation volumétrique

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | GAMEPLAY, QA |
| Sources | Conception Forge |
| Tags | `flight`, `3d-navigation`, `voxel` |

**Comportement Aurora de référence.** Aurora est centré sur surfaces marchables.

**Adaptation obligatoire pour OpenRPG Forge.** Navigation 3D optionnelle pour vol/nage/espace: grille voxel ou graphes, coûts, évitement et debug.

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

- [ ] Un auteur peut configurer « Navigation volumétrique » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-033"></a>

### ORF-AREA-033 — Occlusion et visibilité

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | EE, QA |
| Sources | S05, S12 |
| Tags | `performance`, `occlusion`, `hlod` |

**Comportement Aurora de référence.** Les zones anciennes reposent sur des tilesets limités.

**Adaptation obligatoire pour OpenRPG Forge.** Portails, occlusion culling, HLOD, rooms, distance streaming et visualiseur de cellules.

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

- [ ] Un auteur peut configurer « Occlusion et visibilité » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-034"></a>

### ORF-AREA-034 — LOD par zone et asset

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Enhanced Edition |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | CONTENT, QA |
| Sources | S05 |
| Tags | `lod`, `hlod`, `performance` |

**Comportement Aurora de référence.** EE ajoute du LOD.

**Adaptation obligatoire pour OpenRPG Forge.** Politique LOD/HLOD mesurable, génération ou import, seuils, budgets et tests de popping.

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

- [ ] Un auteur peut configurer « LOD par zone et asset » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-035"></a>

### ORF-AREA-035 — Couches et groupes d’édition

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | BUILD |
| Sources | Conception Forge |
| Tags | `layers`, `groups`, `collaboration` |

**Comportement Aurora de référence.** Aurora filtre catégories sans vraie organisation moderne.

**Adaptation obligatoire pour OpenRPG Forge.** Layers imbriqués, verrouillage, visibilité, export partiel, ownership et sélection de groupe.

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

- [ ] Un auteur peut configurer « Couches et groupes d’édition » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-036"></a>

### ORF-AREA-036 — Play From Here

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | GAMEPLAY, QA |
| Sources | Conception Forge |
| Tags | `playtest`, `spawn`, `debug` |

**Comportement Aurora de référence.** Aurora impose build/test plus global.

**Adaptation obligatoire pour OpenRPG Forge.** Démarrer à la caméra ou waypoint avec personnage/test profile et état de quête injecté.

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

- [ ] Un auteur peut configurer « Play From Here » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-area-037"></a>

### ORF-AREA-037 — Validation de zone

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `validation`, `diagnostics`, `area` |

**Comportement Aurora de référence.** Les erreurs historiques apparaissent souvent au runtime.

**Adaptation obligatoire pour OpenRPG Forge.** Vérifier raccords, collisions, navmesh, transitions, références, points de spawn, éclairage, audio et budgets.

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

- [ ] Un auteur peut configurer « Validation de zone » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
