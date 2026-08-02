# Conversations, dialogues et narration interactive

**Domaine : `DIALOGUE`**  
**Préfixe : `ORF-DLG`**  
**Couche dominante : Forge RPG Kit**  
**Nombre d’exigences : 32**  
**Sources principales : S01, S03, S02**

## Finalité

Graphes de dialogue, conditions, actions, multi-interlocuteurs, liens, localisation, voix, cinématiques, simulation et analyse.

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
| [ORF-DLG-001](#orf-dlg-001) | Fichier/graphe de conversation | P0 | Native Aurora |
| [ORF-DLG-002](#orf-dlg-002) | Nœuds PNJ et joueur | P0 | Native Aurora modernized |
| [ORF-DLG-003](#orf-dlg-003) | Nombre de branches non limité | P0 | Native Aurora |
| [ORF-DLG-004](#orf-dlg-004) | Liens vers un nœud existant | P0 | Native Aurora |
| [ORF-DLG-005](#orf-dlg-005) | Réordonnancement des réponses | P0 | Native Aurora |
| [ORF-DLG-006](#orf-dlg-006) | Texte localisé | P0 | Native Aurora |
| [ORF-DLG-007](#orf-dlg-007) | Tokens de texte | P1 | Native Aurora |
| [ORF-DLG-008](#orf-dlg-008) | Participants multiples | P1 | Native Aurora |
| [ORF-DLG-009](#orf-dlg-009) | Conditions d’apparition | P0 | Native Aurora |
| [ORF-DLG-010](#orf-dlg-010) | Évaluation dans l’ordre | P1 | Native Aurora modernized |
| [ORF-DLG-011](#orf-dlg-011) | Actions sur nœud | P0 | Native Aurora |
| [ORF-DLG-012](#orf-dlg-012) | Assistant de scripts de dialogue | P1 | Native Aurora |
| [ORF-DLG-013](#orf-dlg-013) | Commentaires de nœud | P1 | Native Aurora |
| [ORF-DLG-014](#orf-dlg-014) | Animations de dialogue | P2 | Native Aurora |
| [ORF-DLG-015](#orf-dlg-015) | Sons et voix | P2 | Native Aurora |
| [ORF-DLG-016](#orf-dlg-016) | Caméra de conversation | P2 | Native Aurora modernized |
| [ORF-DLG-017](#orf-dlg-017) | Scripts de fin et d’abandon | P1 | Native Aurora |
| [ORF-DLG-018](#orf-dlg-018) | Recherche dans les conversations | P1 | Native Aurora |
| [ORF-DLG-019](#orf-dlg-019) | Mise en évidence et filtres | P1 | Native Aurora |
| [ORF-DLG-020](#orf-dlg-020) | Bookmarks | P2 | Native Aurora |
| [ORF-DLG-021](#orf-dlg-021) | Mode test | P0 | Native Aurora modernized |
| [ORF-DLG-022](#orf-dlg-022) | Mémoire de conversation | P1 | Native Aurora modernized |
| [ORF-DLG-023](#orf-dlg-023) | Interjections de compagnons | P2 | Native Aurora ecosystem |
| [ORF-DLG-024](#orf-dlg-024) | Checks de compétence | P1 | Native Aurora adapted |
| [ORF-DLG-025](#orf-dlg-025) | Choix temporisés | P3 | Forge extension |
| [ORF-DLG-026](#orf-dlg-026) | Graphes imbriqués | P1 | Forge extension |
| [ORF-DLG-027](#orf-dlg-027) | Variantes procédurales | P2 | Forge extension |
| [ORF-DLG-028](#orf-dlg-028) | Relations et conséquences | P1 | Native Aurora modernized |
| [ORF-DLG-029](#orf-dlg-029) | Validation structurelle | P0 | Forge extension |
| [ORF-DLG-030](#orf-dlg-030) | Couverture de branches | P1 | Forge extension |
| [ORF-DLG-031](#orf-dlg-031) | Édition collaborative | P1 | Forge extension |
| [ORF-DLG-032](#orf-dlg-032) | Génération IA contrôlée | P1 | Forge extension |

## Exigences détaillées

<a id="orf-dlg-001"></a>

### ORF-DLG-001 — Fichier/graphe de conversation

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | BUILD |
| Sources | S01, S03 |
| Tags | `graph`, `dialogue`, `json` |

**Comportement Aurora de référence.** Aurora stocke chaque conversation sous forme d’arbre.

**Adaptation obligatoire pour OpenRPG Forge.** DialogueDocument JSON avec graphe UUID, métadonnées, participants, entrées, nœuds, arêtes et localisation.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Fichier/graphe de conversation » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-002"></a>

### ORF-DLG-002 — Nœuds PNJ et joueur

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | Aucune dépendance explicite; respecter les dépendances du domaine. |
| Sources | S03 |
| Tags | `node`, `role`, `conversation` |

**Comportement Aurora de référence.** L’arbre alterne répliques rouges/bleues selon locuteur.

**Adaptation obligatoire pour OpenRPG Forge.** NodeRole explicite: SpeakerLine, PlayerChoice, Narration, System, ActionOnly, Subgraph et End.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Nœuds PNJ et joueur » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-003"></a>

### ORF-DLG-003 — Nombre de branches non limité

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | QA |
| Sources | S03 |
| Tags | `branching`, `graph`, `scale` |

**Comportement Aurora de référence.** Aurora autorise une arborescence profonde.

**Adaptation obligatoire pour OpenRPG Forge.** Graphe sans limite arbitraire, avec pagination visuelle, sous-graphes et protection contre cycles accidentels.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Nombre de branches non limité » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-004"></a>

### ORF-DLG-004 — Liens vers un nœud existant

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | Aucune dépendance explicite; respecter les dépendances du domaine. |
| Sources | S03 |
| Tags | `links`, `reuse`, `graph` |

**Comportement Aurora de référence.** Aurora réutilise une branche avec des links.

**Adaptation obligatoire pour OpenRPG Forge.** Edge de type Reference vers un nœud canonique, affichage des entrées multiples et édition sûre.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Liens vers un nœud existant » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-005"></a>

### ORF-DLG-005 — Réordonnancement des réponses

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | Aucune dépendance explicite; respecter les dépendances du domaine. |
| Sources | S03 |
| Tags | `order`, `priority`, `choice` |

**Comportement Aurora de référence.** L’ordre des branches détermine leur évaluation et affichage.

**Adaptation obligatoire pour OpenRPG Forge.** Ordre stable par liste/priority, drag-drop, règles de tri et diff lisible.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Réordonnancement des réponses » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-006"></a>

### ORF-DLG-006 — Texte localisé

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core + RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | CONTENT |
| Sources | S01, S03 |
| Tags | `localization`, `text`, `language` |

**Comportement Aurora de référence.** Aurora utilise l’éditeur de chaînes multilingue.

**Adaptation obligatoire pour OpenRPG Forge.** TextKey + variantes par langue, genre/pluriel, fallback, pseudo-localisation et validation.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Texte localisé » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-007"></a>

### ORF-DLG-007 — Tokens de texte

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | SCRIPT |
| Sources | S03 |
| Tags | `token`, `template`, `text` |

**Comportement Aurora de référence.** Aurora possède tokens standard/custom pour noms et valeurs.

**Adaptation obligatoire pour OpenRPG Forge.** Template tokens typés, auto-complétion, aperçu par contexte, échappement et sécurité.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Tokens de texte » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-008"></a>

### ORF-DLG-008 — Participants multiples

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | CREATURE |
| Sources | S03 |
| Tags | `multi-speaker`, `binding`, `tag` |

**Comportement Aurora de référence.** Aurora permet de faire parler d’autres créatures par tag.

**Adaptation obligatoire pour OpenRPG Forge.** ParticipantBinding par rôle, query, tag ou entité; changement de locuteur par nœud et fallback.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Participants multiples » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-009"></a>

### ORF-DLG-009 — Conditions d’apparition

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | SCRIPT |
| Sources | S01, S03 |
| Tags | `condition`, `branch`, `script` |

**Comportement Aurora de référence.** Text Appears When exécute des conditions scriptées.

**Adaptation obligatoire pour OpenRPG Forge.** Condition AST typée ou ForgeScript pur, combinable AND/OR/NOT, avec preview et explication.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées
- version de l’API ForgeScript
- binding d’événement
- diagnostics structurés

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Conditions d’apparition » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-010"></a>

### ORF-DLG-010 — Évaluation dans l’ordre

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | QA |
| Sources | S03 |
| Tags | `evaluation`, `priority`, `determinism` |

**Comportement Aurora de référence.** Aurora choisit le premier nœud valide dans certaines branches.

**Adaptation obligatoire pour OpenRPG Forge.** Modes FirstValid, AllValid, Weighted, RandomDeterministic et Manual, explicitement visibles.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Évaluation dans l’ordre » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-011"></a>

### ORF-DLG-011 — Actions sur nœud

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | QUEST, ITEM, SCRIPT, AUDIO |
| Sources | S03 |
| Tags | `action`, `transaction`, `dialogue` |

**Comportement Aurora de référence.** Action Taken exécute scripts, journal, animation ou son.

**Adaptation obligatoire pour OpenRPG Forge.** ActionList typée et transactionnelle: variables, quêtes, items, relations, animation, audio, séquence et script.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Actions sur nœud » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-012"></a>

### ORF-DLG-012 — Assistant de scripts de dialogue

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | SCRIPT |
| Sources | S03 |
| Tags | `wizard`, `condition`, `action` |

**Comportement Aurora de référence.** Aurora propose Script Wizard pour conditions/actions fréquentes.

**Adaptation obligatoire pour OpenRPG Forge.** Builders visuels générant des expressions typées; jamais de code opaque sans source.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Assistant de scripts de dialogue » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-013"></a>

### ORF-DLG-013 — Commentaires de nœud

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | BUILD |
| Sources | S03 |
| Tags | `comments`, `authoring`, `metadata` |

**Comportement Aurora de référence.** L’auteur peut ajouter des commentaires non visibles.

**Adaptation obligatoire pour OpenRPG Forge.** Notes, intentions, émotion, source, statut éditorial, TODO et mention d’auteur.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Commentaires de nœud » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-014"></a>

### ORF-DLG-014 — Animations de dialogue

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | CREATURE |
| Sources | S03 |
| Tags | `animation`, `cue`, `dialogue` |

**Comportement Aurora de référence.** Aurora peut déclencher animations.

**Adaptation obligatoire pour OpenRPG Forge.** AnimationCue par participant, timing, blend, gestuelle, regard, interruption et fallback.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Animations de dialogue » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-015"></a>

### ORF-DLG-015 — Sons et voix

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core + RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | AUDIO, CONTENT |
| Sources | S03 |
| Tags | `voice`, `audio`, `subtitle` |

**Comportement Aurora de référence.** Aurora peut jouer sons et voice-over.

**Adaptation obligatoire pour OpenRPG Forge.** VoiceCue par langue, fichier, take, sous-titre, lipsync, émotion, durée et licence.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Sons et voix » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-016"></a>

### ORF-DLG-016 — Caméra de conversation

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | GAMEPLAY |
| Sources | S03 |
| Tags | `camera`, `cinematic`, `dialogue` |

**Comportement Aurora de référence.** Aurora possède zoom caméra et options d’arrêt.

**Adaptation obligatoire pour OpenRPG Forge.** Shot/camera rig par nœud, framing automatique, cinématique, priorité joueur et accessibilité.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Caméra de conversation » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-017"></a>

### ORF-DLG-017 — Scripts de fin et d’abandon

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | SCRIPT |
| Sources | S03 |
| Tags | `event`, `end`, `abort` |

**Comportement Aurora de référence.** Une conversation peut exécuter actions à la fin ou si interrompue.

**Adaptation obligatoire pour OpenRPG Forge.** OnDialogueCompleted/Aborted avec raison, rollback optionnel et état de reprise.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées
- version de l’API ForgeScript
- binding d’événement
- diagnostics structurés

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Scripts de fin et d’abandon » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-018"></a>

### ORF-DLG-018 — Recherche dans les conversations

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | BUILD |
| Sources | S03 |
| Tags | `search`, `replace`, `stats` |

**Comportement Aurora de référence.** Aurora propose find, replace et statistiques.

**Adaptation obligatoire pour OpenRPG Forge.** Recherche multi-dialogues, regex sécurisée, filtres par locuteur/condition/action, remplacement preview.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Recherche dans les conversations » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-019"></a>

### ORF-DLG-019 — Mise en évidence et filtres

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | QA |
| Sources | S03 |
| Tags | `highlight`, `filter`, `review` |

**Comportement Aurora de référence.** L’éditeur Aurora colore certains nœuds selon propriétés.

**Adaptation obligatoire pour OpenRPG Forge.** Badges condition/action/voice/localization/error/link, légende, filtres et mode review.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Mise en évidence et filtres » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-020"></a>

### ORF-DLG-020 — Bookmarks

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | BUILD |
| Sources | S03 |
| Tags | `bookmark`, `review`, `navigation` |

**Comportement Aurora de référence.** Aurora permet des signets dans une conversation.

**Adaptation obligatoire pour OpenRPG Forge.** Bookmarks personnels/partagés, commentaires de review et deep links.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Bookmarks » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-021"></a>

### ORF-DLG-021 — Mode test

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | QA, SCRIPT |
| Sources | S03 |
| Tags | `simulation`, `condition`, `test` |

**Comportement Aurora de référence.** Aurora prévisualise l’arbre mais n’exécute pas toutes les conditions.

**Adaptation obligatoire pour OpenRPG Forge.** Simulateur complet avec état fictif, conditions réellement évaluées en sandbox et journal des décisions.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Mode test » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-022"></a>

### ORF-DLG-022 — Mémoire de conversation

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | SCRIPT |
| Sources | S03 |
| Tags | `memory`, `state`, `scope` |

**Comportement Aurora de référence.** Les modules utilisent variables locales pour retenir des choix.

**Adaptation obligatoire pour OpenRPG Forge.** ConversationMemory typée par dialogue, participant, joueur, groupe ou monde avec durée explicite.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Mémoire de conversation » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-023"></a>

### ORF-DLG-023 — Interjections de compagnons

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora ecosystem |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | COMPANION |
| Sources | S03 |
| Tags | `companion`, `interjection`, `dialogue` |

**Comportement Aurora de référence.** Les extensions utilisent triggers et branches pour henchmen.

**Adaptation obligatoire pour OpenRPG Forge.** InterjectionRule liée à personnalité, présence, relation, priorité, cooldown et conflit.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Interjections de compagnons » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-024"></a>

### ORF-DLG-024 — Checks de compétence

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora adapted |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | CREATURE |
| Sources | S03 |
| Tags | `skill-check`, `condition`, `result` |

**Comportement Aurora de référence.** Les réponses peuvent dépendre de scripts de compétences.

**Adaptation obligatoire pour OpenRPG Forge.** SkillCheckNode exposant difficulté, opposant, avantage, résultat, visibilité et explication au joueur.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Checks de compétence » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-025"></a>

### ORF-DLG-025 — Choix temporisés

| Champ | Valeur |
|---|---|
| Priorité | **P3** |
| Couche | Forge RPG Kit |
| Origine | Forge extension |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | MULTI, QA |
| Sources | Conception Forge |
| Tags | `timed-choice`, `accessibility`, `network` |

**Comportement Aurora de référence.** Non central dans l’Aurora original.

**Adaptation obligatoire pour OpenRPG Forge.** Timer optionnel, choix par défaut, pause accessible, réseau et script de timeout.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Choix temporisés » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-026"></a>

### ORF-DLG-026 — Graphes imbriqués

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Forge extension |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | BUILD |
| Sources | Conception Forge |
| Tags | `subgraph`, `reuse`, `parameters` |

**Comportement Aurora de référence.** Les liens réutilisent des branches sans vraie modularité moderne.

**Adaptation obligatoire pour OpenRPG Forge.** SubDialogue avec paramètres d’entrée/sortie, contrat de variables, version et test isolé.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Graphes imbriqués » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-027"></a>

### ORF-DLG-027 — Variantes procédurales

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge RPG Kit |
| Origine | Forge extension |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `variant`, `random`, `determinism` |

**Comportement Aurora de référence.** Les textes historiques peuvent être sélectionnés par script.

**Adaptation obligatoire pour OpenRPG Forge.** VariantSet conditionnel/pondéré avec seed, anti-répétition et localisation cohérente.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Variantes procédurales » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-028"></a>

### ORF-DLG-028 — Relations et conséquences

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | QUEST, FACTION |
| Sources | S03 |
| Tags | `consequence`, `impact`, `reputation` |

**Comportement Aurora de référence.** Les actions peuvent modifier variables et réputation.

**Adaptation obligatoire pour OpenRPG Forge.** Conséquence typée affichée dans le graphe; analyse d’impact sur quêtes, factions, items et monde.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Relations et conséquences » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-029"></a>

### ORF-DLG-029 — Validation structurelle

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge RPG Kit |
| Origine | Forge extension |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `validation`, `graph`, `lint` |

**Comportement Aurora de référence.** Aurora peut contenir liens morts ou branches inatteignables.

**Adaptation obligatoire pour OpenRPG Forge.** Linter: entrée, nœuds orphelins, cycles, fins, types, références, conditions contradictoires et localisation.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Validation structurelle » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-030"></a>

### ORF-DLG-030 — Couverture de branches

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Forge extension |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `coverage`, `test`, `paths` |

**Comportement Aurora de référence.** Le Toolset ne mesure pas exhaustivement les parcours.

**Adaptation obligatoire pour OpenRPG Forge.** Générer chemins, états requis, branches impossibles et couverture par tests/sessions.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Couverture de branches » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-031"></a>

### ORF-DLG-031 — Édition collaborative

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Forge extension |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | BUILD |
| Sources | S17 |
| Tags | `collaboration`, `diff`, `merge` |

**Comportement Aurora de référence.** Les fichiers binaires historiques fusionnent mal.

**Adaptation obligatoire pour OpenRPG Forge.** JSON stable par nœud, IDs, diff sémantique, lock optionnel, commentaires et résolution de conflits.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Édition collaborative » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.


<a id="orf-dlg-032"></a>

### ORF-DLG-032 — Génération IA contrôlée

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge AI + RPG Kit |
| Origine | Forge extension |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime`, `Forge.McpHost` |
| Dépendances | AI |
| Sources | Conception Forge |
| Tags | `ai`, `generation`, `dialogue` |

**Comportement Aurora de référence.** Aurora n’intègre pas d’IA.

**Adaptation obligatoire pour OpenRPG Forge.** Créer synopsis ou graphe depuis un brief, citer contexte, preview, lint, style guide et transaction.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- nœuds/arêtes avec UUID
- ordre déterministe
- références typées
- transactionId
- planId
- semanticDiff
- niveau d’autorisation

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de graphe avec mini-carte
- panneau de validation et simulation

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
- nœuds accessibles
- conditions/actions typées
- clés de localisation présentes

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Génération IA contrôlée » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un test headless couvre au moins le chemin nominal et une branche d’échec.
