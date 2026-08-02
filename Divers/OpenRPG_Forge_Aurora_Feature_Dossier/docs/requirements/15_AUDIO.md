# Audio, musique, ambiance et voix

**Domaine : `AUDIO`**  
**Préfixe : `ORF-AUD`**  
**Couche dominante : Forge Core**  
**Nombre d’exigences : 20**  
**Sources principales : S03**

## Finalité

Objets sonores, playlists, spatialisation, boucles, variation, ambiances de zone, musique de combat, voix et mixage.

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
| [ORF-AUD-001](#orf-aud-001) | Objet sonore | P0 | Native Aurora |
| [ORF-AUD-002](#orf-aud-002) | Liste de clips | P0 | Native Aurora |
| [ORF-AUD-003](#orf-aud-003) | Recherche et aperçu des ressources | P1 | Native Aurora modernized |
| [ORF-AUD-004](#orf-aud-004) | Ordre séquentiel ou aléatoire | P1 | Native Aurora |
| [ORF-AUD-005](#orf-aud-005) | Lecture unique ou répétée | P0 | Native Aurora |
| [ORF-AUD-006](#orf-aud-006) | Intervalle et variation | P1 | Native Aurora |
| [ORF-AUD-007](#orf-aud-007) | Volume relatif | P1 | Native Aurora modernized |
| [ORF-AUD-008](#orf-aud-008) | Position spécifique | P0 | Native Aurora modernized |
| [ORF-AUD-009](#orf-aud-009) | Son partout | P1 | Native Aurora |
| [ORF-AUD-010](#orf-aud-010) | Position aléatoire | P2 | Native Aurora modernized |
| [ORF-AUD-011](#orf-aud-011) | Portée audible | P1 | Native Aurora |
| [ORF-AUD-012](#orf-aud-012) | Activation par script | P1 | Native Aurora |
| [ORF-AUD-013](#orf-aud-013) | Ambiance jour/nuit | P1 | Native Aurora modernized |
| [ORF-AUD-014](#orf-aud-014) | Musique jour/nuit/combat | P1 | Native Aurora modernized |
| [ORF-AUD-015](#orf-aud-015) | Réverbération | P2 | Native Aurora modernized |
| [ORF-AUD-016](#orf-aud-016) | Voix de créature | P2 | Native Aurora modernized |
| [ORF-AUD-017](#orf-aud-017) | Événements audio | P2 | Native Aurora modernized |
| [ORF-AUD-018](#orf-aud-018) | Mixage et bus | P1 | Forge extension |
| [ORF-AUD-019](#orf-aud-019) | Sous-titres de sons importants | P1 | Forge extension |
| [ORF-AUD-020](#orf-aud-020) | Validation audio | P0 | Forge extension |

## Exigences détaillées

<a id="orf-aud-001"></a>

### ORF-AUD-001 — Objet sonore

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | CONTENT |
| Sources | S03 |
| Tags | `sound`, `emitter`, `audio` |

**Comportement Aurora de référence.** Aurora place un objet invisible avec une liste de sons.

**Adaptation obligatoire pour OpenRPG Forge.** AudioEmitterDefinition/Instance avec clips, mode, spatialisation, timing, mix bus et scripts.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

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
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Objet sonore » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-aud-002"></a>

### ORF-AUD-002 — Liste de clips

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | CONTENT |
| Sources | S03 |
| Tags | `clips`, `playlist`, `variant` |

**Comportement Aurora de référence.** Un sound object contient un ou plusieurs sons.

**Adaptation obligatoire pour OpenRPG Forge.** ClipSet ordonné/pondéré avec tags, variants, langues, loudness metadata et preview.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

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
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Liste de clips » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-aud-003"></a>

### ORF-AUD-003 — Recherche et aperçu des ressources

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | PALETTE |
| Sources | S03 |
| Tags | `browser`, `preview`, `audio` |

**Comportement Aurora de référence.** Aurora filtre et joue les sons dans une fenêtre.

**Adaptation obligatoire pour OpenRPG Forge.** Browser waveform, audition, metadata, tags, transcript, licence, loudness et batch preview.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

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
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Recherche et aperçu des ressources » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-aud-004"></a>

### ORF-AUD-004 — Ordre séquentiel ou aléatoire

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | S03 |
| Tags | `random`, `sequence`, `playback` |

**Comportement Aurora de référence.** Aurora joue la liste en ordre ou random.

**Adaptation obligatoire pour OpenRPG Forge.** PlaybackSelection sequential, shuffle bag, weighted, deterministic seed et no-repeat window.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

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
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Ordre séquentiel ou aléatoire » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-aud-005"></a>

### ORF-AUD-005 — Lecture unique ou répétée

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | Aucune dépendance explicite; respecter les dépendances du domaine. |
| Sources | S03 |
| Tags | `loop`, `playback`, `audio` |

**Comportement Aurora de référence.** Sounds peuvent être once, repeat ou seamless loop.

**Adaptation obligatoire pour OpenRPG Forge.** LoopPolicy one-shot, loop, seamless region, finite count, event-driven et crossfade.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

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
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Lecture unique ou répétée » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-aud-006"></a>

### ORF-AUD-006 — Intervalle et variation

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | S03 |
| Tags | `interval`, `pitch`, `volume` |

**Comportement Aurora de référence.** Aurora règle interval, pitch et volume variation.

**Adaptation obligatoire pour OpenRPG Forge.** Modulation ranges, distributions, curves, seed, tempo sync et preview statistique.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

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
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Intervalle et variation » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-aud-007"></a>

### ORF-AUD-007 — Volume relatif

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | S03 |
| Tags | `volume`, `mix`, `loudness` |

**Comportement Aurora de référence.** Aurora règle volume relatif au mix joueur.

**Adaptation obligatoire pour OpenRPG Forge.** Loudness-normalized gain, bus routing, ducking, distance, accessibility cap et meter.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

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
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Volume relatif » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-aud-008"></a>

### ORF-AUD-008 — Position spécifique

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | AREA |
| Sources | S03 |
| Tags | `spatial`, `3d`, `attenuation` |

**Comportement Aurora de référence.** Un son peut être localisé à un point.

**Adaptation obligatoire pour OpenRPG Forge.** Emitter 3D avec transform, attenuation curve, cone, occlusion, height et virtualization.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

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
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Position spécifique » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-aud-009"></a>

### ORF-AUD-009 — Son partout

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | AREA |
| Sources | S03 |
| Tags | `ambient`, `2d`, `area` |

**Comportement Aurora de référence.** Aurora peut jouer un son non positionnel dans la zone.

**Adaptation obligatoire pour OpenRPG Forge.** 2D/ambient bus with area scope, listener policies et multiplayer personalization.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

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
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Son partout » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-aud-010"></a>

### ORF-AUD-010 — Position aléatoire

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | AREA |
| Sources | S03 |
| Tags | `scatter`, `random`, `spatial` |

**Comportement Aurora de référence.** Aurora peut émettre aléatoirement autour de la zone.

**Adaptation obligatoire pour OpenRPG Forge.** ScatterEmitter dans volume/surface avec exclusion, nav/biome filters et deterministic seed.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

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
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Position aléatoire » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-aud-011"></a>

### ORF-AUD-011 — Portée audible

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | Aucune dépendance explicite; respecter les dépendances du domaine. |
| Sources | S03 |
| Tags | `range`, `attenuation`, `debug` |

**Comportement Aurora de référence.** Aurora définit max/min distance.

**Adaptation obligatoire pour OpenRPG Forge.** Attenuation asset with inner/outer radius, curve, air absorption et debug spheres.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

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
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Portée audible » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-aud-012"></a>

### ORF-AUD-012 — Activation par script

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | SCRIPT |
| Sources | S03 |
| Tags | `script`, `playback`, `control` |

**Comportement Aurora de référence.** Un sound peut être inactif puis PlaySound/StopSound.

**Adaptation obligatoire pour OpenRPG Forge.** Runtime handle, Start/Stop/Pause/Fade, instance policies, authority et event callbacks.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- version de l’API ForgeScript
- binding d’événement
- diagnostics structurés
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

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
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Activation par script » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-aud-013"></a>

### ORF-AUD-013 — Ambiance jour/nuit

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | AREA, PROJECT |
| Sources | S03 |
| Tags | `ambience`, `day-night`, `adaptive` |

**Comportement Aurora de référence.** Area properties choisissent ambient day/night.

**Adaptation obligatoire pour OpenRPG Forge.** Adaptive ambience layers driven by time, weather, location, danger and indoor volumes.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

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
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Ambiance jour/nuit » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-aud-014"></a>

### ORF-AUD-014 — Musique jour/nuit/combat

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core + RPG Kit |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | GAMEPLAY |
| Sources | S03 |
| Tags | `music`, `state`, `combat` |

**Comportement Aurora de référence.** Aurora choisit morceaux et délai de replay.

**Adaptation obligatoire pour OpenRPG Forge.** MusicStateMachine exploration, tension, combat, boss, victory; stems, transitions and cooldown.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

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
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Musique jour/nuit/combat » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-aud-015"></a>

### ORF-AUD-015 — Réverbération

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | AREA |
| Sources | S03 |
| Tags | `reverb`, `zone`, `mix` |

**Comportement Aurora de référence.** Aurora sélectionne reverb environment.

**Adaptation obligatoire pour OpenRPG Forge.** AudioZone snapshots, impulse/convolution option, sends, blends et preview.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

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
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Réverbération » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-aud-016"></a>

### ORF-AUD-016 — Voix de créature

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core + RPG Kit |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | CREATURE, DIALOGUE, CONTENT |
| Sources | S03 |
| Tags | `voice`, `lipsync`, `subtitle` |

**Comportement Aurora de référence.** Aurora utilise soundsets et voice-over de conversation.

**Adaptation obligatoire pour OpenRPG Forge.** VoiceProfile, localized takes, phonemes/lipsync, subtitles, fallback TTS plugin and consent metadata.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

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
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Voix de créature » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-aud-017"></a>

### ORF-AUD-017 — Événements audio

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | SCRIPT |
| Sources | S15 |
| Tags | `event`, `audio`, `script` |

**Comportement Aurora de référence.** Scripts jouent musiques, sons et cues.

**Adaptation obligatoire pour OpenRPG Forge.** AudioCueStarted/Completed/Interrupted plus API PlayCue, SetMusicState and PushSnapshot.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- version de l’API ForgeScript
- binding d’événement
- diagnostics structurés
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

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
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Événements audio » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-aud-018"></a>

### ORF-AUD-018 — Mixage et bus

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `mix`, `bus`, `accessibility` |

**Comportement Aurora de référence.** Aurora expose surtout propriétés par objet/zone.

**Adaptation obligatoire pour OpenRPG Forge.** Hierarchical mix buses, snapshots, platform profiles, dynamic range, accessibility and monitoring.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

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
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Mixage et bus » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-aud-019"></a>

### ORF-AUD-019 — Sous-titres de sons importants

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `captions`, `accessibility`, `audio` |

**Comportement Aurora de référence.** Non systématique historiquement.

**Adaptation obligatoire pour OpenRPG Forge.** Caption metadata for dialogue, effects and ambient cues, direction indicator and localization.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

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
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Sous-titres de sons importants » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.


<a id="orf-aud-020"></a>

### ORF-AUD-020 — Validation audio

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `validation`, `audio`, `budget` |

**Comportement Aurora de référence.** Ressources manquantes ou boucles incorrectes apparaissent tard.

**Adaptation obligatoire pour OpenRPG Forge.** Vérifier fichiers, licences, loudness, loop points, localization, bus, range and memory budgets.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- manifeste de package
- licence SPDX
- hash de contenu

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites

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
- licence/provenance connues
- hash et format valides
- budget plateforme respecté

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Validation audio » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un asset sans licence/provenance requise est bloqué pour une build de distribution.
