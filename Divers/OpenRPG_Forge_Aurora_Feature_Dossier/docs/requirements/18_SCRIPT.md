# ForgeScript, événements et API de gameplay

**Domaine : `SCRIPT`**  
**Préfixe : `ORF-SCR`**  
**Couche dominante : Forge Core + Kits**  
**Nombre d’exigences : 41**  
**Sources principales : S01, S03, S05, S07, S08, S10, S12, S15, S18**

## Finalité

Langage C-like, événements, compilation, sandbox, API typée, tâches différées, débogage, hot reload, tests et versionnement.

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
| [ORF-SCR-001](#orf-scr-001) | Langage proche du C | P0 | Native Aurora concept |
| [ORF-SCR-002](#orf-scr-002) | Sources et bytecode | P0 | Native Aurora |
| [ORF-SCR-003](#orf-scr-003) | Compilateur intégré | P0 | Native Aurora |
| [ORF-SCR-004](#orf-scr-004) | Éditeur de scripts | P1 | Native Aurora modernized |
| [ORF-SCR-005](#orf-scr-005) | Script Wizard | P1 | Native Aurora |
| [ORF-SCR-006](#orf-scr-006) | Événements par objet | P0 | Native Aurora modernized |
| [ORF-SCR-007](#orf-scr-007) | Fonction d’entrée | P0 | Native Aurora modernized |
| [ORF-SCR-008](#orf-scr-008) | API d’actions différées | P1 | Native Aurora |
| [ORF-SCR-009](#orf-scr-009) | API zones et objets | P0 | Native Aurora |
| [ORF-SCR-010](#orf-scr-010) | API combat | P1 | Native Aurora adapted |
| [ORF-SCR-011](#orf-scr-011) | API effets | P1 | Native Aurora adapted |
| [ORF-SCR-012](#orf-scr-012) | API inventaire | P1 | Native Aurora |
| [ORF-SCR-013](#orf-scr-013) | API journal et quêtes | P1 | Native Aurora |
| [ORF-SCR-014](#orf-scr-014) | API dialogues | P1 | Native Aurora |
| [ORF-SCR-015](#orf-scr-015) | API factions et réputation | P1 | Native Aurora adapted |
| [ORF-SCR-016](#orf-scr-016) | API perception | P1 | Native Aurora modernized |
| [ORF-SCR-017](#orf-scr-017) | API mouvement | P0 | Native Aurora |
| [ORF-SCR-018](#orf-scr-018) | API caméra et cutscenes | P2 | Native Aurora |
| [ORF-SCR-019](#orf-scr-019) | API son et musique | P1 | Native Aurora |
| [ORF-SCR-020](#orf-scr-020) | API temps | P1 | Native Aurora |
| [ORF-SCR-021](#orf-scr-021) | API math et chaînes | P0 | Native Aurora modernized |
| [ORF-SCR-022](#orf-scr-022) | Variables locales | P0 | Native Aurora modernized |
| [ORF-SCR-023](#orf-scr-023) | Événements custom | P1 | Native Aurora modernized |
| [ORF-SCR-024](#orf-scr-024) | Tag-based scripting | P3 | Native Aurora ecosystem |
| [ORF-SCR-025](#orf-scr-025) | Module switches | P2 | Official expansion ecosystem |
| [ORF-SCR-026](#orf-scr-026) | AI behavior switches | P2 | Native Aurora ecosystem |
| [ORF-SCR-027](#orf-scr-027) | Sandbox sans fichier/réseau | P0 | Native Aurora concept |
| [ORF-SCR-028](#orf-scr-028) | Handles opaques | P0 | Native Aurora modernized |
| [ORF-SCR-029](#orf-scr-029) | Budget d’instructions | P0 | Forge extension |
| [ORF-SCR-030](#orf-scr-030) | Budget mémoire | P0 | Forge extension |
| [ORF-SCR-031](#orf-scr-031) | Déterminisme | P0 | Forge extension |
| [ORF-SCR-032](#orf-scr-032) | Hot reload | P1 | Forge extension |
| [ORF-SCR-033](#orf-scr-033) | Débogueur | P1 | Forge extension |
| [ORF-SCR-034](#orf-scr-034) | Tests unitaires ForgeScript | P1 | Forge extension |
| [ORF-SCR-035](#orf-scr-035) | Profilage de scripts | P1 | Enhanced Edition inspired |
| [ORF-SCR-036](#orf-scr-036) | Version d’API | P0 | Forge extension |
| [ORF-SCR-037](#orf-scr-037) | Documentation générée | P1 | Ecosystem inspired |
| [ORF-SCR-038](#orf-scr-038) | Analyse statique | P1 | Forge extension |
| [ORF-SCR-039](#orf-scr-039) | Permissions par module | P0 | Forge extension |
| [ORF-SCR-040](#orf-scr-040) | Sérialisation d’état script | P1 | Enhanced Edition inspired |
| [ORF-SCR-041](#orf-scr-041) | Interop éditeur/MCP | P1 | Forge extension |

## Exigences détaillées

<a id="orf-scr-001"></a>

### ORF-SCR-001 — Langage proche du C

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora concept |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | Aucune dépendance explicite; respecter les dépendances du domaine. |
| Sources | S01, S03, S18 |
| Tags | `language`, `c-like`, `angelscript` |

**Comportement Aurora de référence.** NWScript utilise une syntaxe dérivée du C et compile vers bytecode.

**Adaptation obligatoire pour OpenRPG Forge.** ForgeScript repose sur AngelScript non forké, typé statiquement, avec API Forge versionnée.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Langage proche du C » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-002"></a>

### ORF-SCR-002 — Sources et bytecode

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | BUILD |
| Sources | S03, S18 |
| Tags | `source`, `bytecode`, `cache` |

**Comportement Aurora de référence.** Aurora conserve .nss source et .ncs compilé.

**Adaptation obligatoire pour OpenRPG Forge.** `.as` canonique et bytecode cache; hash source/API/compiler; source distribuable selon policy.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Sources et bytecode » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-003"></a>

### ORF-SCR-003 — Compilateur intégré

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | BUILD, AI |
| Sources | S01, S03 |
| Tags | `compiler`, `diagnostics`, `incremental` |

**Comportement Aurora de référence.** Le Toolset compile scripts lors du build ou à la demande.

**Adaptation obligatoire pour OpenRPG Forge.** Compile service incremental, diagnostics structurés, parallel safe and CLI/editor/MCP integration.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Compilateur intégré » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-004"></a>

### ORF-SCR-004 — Éditeur de scripts

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | SHELL |
| Sources | S01, S03 |
| Tags | `editor`, `lsp`, `autocomplete` |

**Comportement Aurora de référence.** Aurora fournit un éditeur et accès au Script Wizard.

**Adaptation obligatoire pour OpenRPG Forge.** Editor with syntax highlighting, completion, signature help, docs, snippets, symbols and formatting.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Éditeur de scripts » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-005"></a>

### ORF-SCR-005 — Script Wizard

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core + RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | DIALOGUE, QUEST |
| Sources | S03 |
| Tags | `wizard`, `codegen`, `typed` |

**Comportement Aurora de référence.** Aurora génère conditions et actions courantes.

**Adaptation obligatoire pour OpenRPG Forge.** Visual builders produce typed AST or reviewed ForgeScript with round-trip where possible.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Script Wizard » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-006"></a>

### ORF-SCR-006 — Événements par objet

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | CREATURE, PLACEABLE, DOOR, TRIGGER |
| Sources | S03 |
| Tags | `event`, `binding`, `handler` |

**Comportement Aurora de référence.** Chaque type d’objet possède des slots de scripts.

**Adaptation obligatoire pour OpenRPG Forge.** EventBinding table per component, multiple handlers with priority, enable conditions and explicit authority.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Événements par objet » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-007"></a>

### ORF-SCR-007 — Fonction d’entrée

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | Aucune dépendance explicite; respecter les dépendances du domaine. |
| Sources | S03 |
| Tags | `entrypoint`, `event`, `payload` |

**Comportement Aurora de référence.** NWScript utilise `void main()` et événements injectent globals/functions.

**Adaptation obligatoire pour OpenRPG Forge.** Handlers typed by event payload, e.g. `void OnDamaged(const DamageEvent &in e)`.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Fonction d’entrée » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-008"></a>

### ORF-SCR-008 — API d’actions différées

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core + RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | GAMEPLAY |
| Sources | S15 |
| Tags | `action`, `queue`, `async` |

**Comportement Aurora de référence.** NWScript propose actions en file sur objets.

**Adaptation obligatoire pour OpenRPG Forge.** Task/Action API deterministic with queues, cancellation, priorities, completion and network authority.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « API d’actions différées » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-009"></a>

### ORF-SCR-009 — API zones et objets

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | AREA |
| Sources | S15 |
| Tags | `entity`, `area`, `query` |

**Comportement Aurora de référence.** NWScript crée, recherche et manipule zones/objects.

**Adaptation obligatoire pour OpenRPG Forge.** Entity queries, typed handles, transforms, spawn/destroy, component-safe operations and scopes.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « API zones et objets » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-010"></a>

### ORF-SCR-010 — API combat

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora adapted |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | CREATURE, ITEM |
| Sources | S15 |
| Tags | `combat`, `damage`, `attack` |

**Comportement Aurora de référence.** NWScript déclenche attaques, dégâts et lit informations de combat.

**Adaptation obligatoire pour OpenRPG Forge.** Combat service API using DamageSpec, AttackContext, effects, attribution and authoritative transactions.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « API combat » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-011"></a>

### ORF-SCR-011 — API effets

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora adapted |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | GAMEPLAY |
| Sources | S15 |
| Tags | `effect`, `status`, `stacking` |

**Comportement Aurora de référence.** NWScript crée/applique des effects.

**Adaptation obligatoire pour OpenRPG Forge.** EffectDefinition/Spec, stacking, duration, periodic, source, dispel and serialization.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « API effets » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-012"></a>

### ORF-SCR-012 — API inventaire

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | ITEM |
| Sources | S15 |
| Tags | `inventory`, `item`, `transaction` |

**Comportement Aurora de référence.** NWScript crée, détruit, équipe et recherche items.

**Adaptation obligatoire pour OpenRPG Forge.** Inventory transaction API with ownership, queries, capacity, rollback and audit.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « API inventaire » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-013"></a>

### ORF-SCR-013 — API journal et quêtes

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | QUEST |
| Sources | S15 |
| Tags | `quest`, `journal`, `state` |

**Comportement Aurora de référence.** NWScript ajoute et change des entrées de journal.

**Adaptation obligatoire pour OpenRPG Forge.** Quest service with state transitions, objectives, history and scoped instances.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « API journal et quêtes » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-014"></a>

### ORF-SCR-014 — API dialogues

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | DIALOGUE |
| Sources | S15 |
| Tags | `dialogue`, `conversation`, `script` |

**Comportement Aurora de référence.** NWScript démarre conversations et conditions.

**Adaptation obligatoire pour OpenRPG Forge.** StartDialogue, select graph, participant bindings, variables, outcome and safe interruption.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « API dialogues » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-015"></a>

### ORF-SCR-015 — API factions et réputation

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora adapted |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | FACTION |
| Sources | S15 |
| Tags | `faction`, `reputation`, `relationship` |

**Comportement Aurora de référence.** NWScript lit/modifie alignement et réputation.

**Adaptation obligatoire pour OpenRPG Forge.** Faction membership and relationship transactions with cause and scope.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « API factions et réputation » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-016"></a>

### ORF-SCR-016 — API perception

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | CREATURE, AREA |
| Sources | S15 |
| Tags | `perception`, `los`, `query` |

**Comportement Aurora de référence.** NWScript teste LOS, perception and nearby objects.

**Adaptation obligatoire pour OpenRPG Forge.** Sense queries, LOS, audibility, stimuli, confidence and deterministic spatial queries.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « API perception » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-017"></a>

### ORF-SCR-017 — API mouvement

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | GAMEPLAY, WAYPOINT |
| Sources | S15 |
| Tags | `movement`, `navigation`, `task` |

**Comportement Aurora de référence.** NWScript ordonne move, jump, face and follow.

**Adaptation obligatoire pour OpenRPG Forge.** Movement tasks, navigation destinations, teleports, movement modes, velocity and completion events.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « API mouvement » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-018"></a>

### ORF-SCR-018 — API caméra et cutscenes

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | GAMEPLAY |
| Sources | S15 |
| Tags | `camera`, `cutscene`, `sequence` |

**Comportement Aurora de référence.** NWScript contrôle caméra et cutscene mode.

**Adaptation obligatoire pour OpenRPG Forge.** Camera rig, shots, blends, player control locks, sequence orchestration and abort recovery.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « API caméra et cutscenes » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-019"></a>

### ORF-SCR-019 — API son et musique

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | AUDIO |
| Sources | S15 |
| Tags | `audio`, `music`, `script` |

**Comportement Aurora de référence.** NWScript joue sons et change musique.

**Adaptation obligatoire pour OpenRPG Forge.** Audio cues, state machine, fades, snapshots, listener policies and completion.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « API son et musique » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-020"></a>

### ORF-SCR-020 — API temps

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | PROJECT |
| Sources | S15 |
| Tags | `time`, `timer`, `schedule` |

**Comportement Aurora de référence.** NWScript lit/modifie heure and delays commands.

**Adaptation obligatoire pour OpenRPG Forge.** World clock, timers, schedules, simulation time, calendar and deterministic delay.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « API temps » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-021"></a>

### ORF-SCR-021 — API math et chaînes

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | S15 |
| Tags | `math`, `string`, `standard-library` |

**Comportement Aurora de référence.** NWScript fournit conversions, strings, vectors and math.

**Adaptation obligatoire pour OpenRPG Forge.** Safe standard library, Unicode, formatting, regex bounded, vectors/quaternions and deterministic math policy.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « API math et chaînes » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-022"></a>

### ORF-SCR-022 — Variables locales

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | PROJECT |
| Sources | S15 |
| Tags | `variables`, `blackboard`, `state` |

**Comportement Aurora de référence.** NWScript stocke int, float, string, object and location on objects.

**Adaptation obligatoire pour OpenRPG Forge.** Typed blackboards with schema, scopes, defaults, inspector, replication and persistence.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Variables locales » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-023"></a>

### ORF-SCR-023 — Événements custom

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | BUILD |
| Sources | S15, S23 |
| Tags | `event-bus`, `custom`, `payload` |

**Comportement Aurora de référence.** SignalEvent/OnUserDefined crée events génériques.

**Adaptation obligatoire pour OpenRPG Forge.** Namespaced event bus with registered payload schema, version, subscription and tracing.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Événements custom » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-024"></a>

### ORF-SCR-024 — Tag-based scripting

| Champ | Valeur |
|---|---|
| Priorité | **P3** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora ecosystem |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | BUILD |
| Sources | S03 |
| Tags | `tag`, `convention`, `routing` |

**Comportement Aurora de référence.** HotU/EE utilise scripts nommés d’après tags and switches.

**Adaptation obligatoire pour OpenRPG Forge.** Optional convention routing by blueprint/event, explicit in config and visible in editor; no hidden magic.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Tag-based scripting » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-025"></a>

### ORF-SCR-025 — Module switches

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Core + Kits |
| Origine | Official expansion ecosystem |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | PROJECT |
| Sources | S03 |
| Tags | `feature-flag`, `configuration`, `switch` |

**Comportement Aurora de référence.** x2_inc_switches active/désactive des sous-systèmes via variables.

**Adaptation obligatoire pour OpenRPG Forge.** FeatureFlag definitions with typed defaults, docs, scopes, migration and UI; replace magic hex bitmasks.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Module switches » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-026"></a>

### ORF-SCR-026 — AI behavior switches

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora ecosystem |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | CREATURE |
| Sources | S03 |
| Tags | `ai`, `policy`, `configuration` |

**Comportement Aurora de référence.** Variables/bitmasks modifient comportements creatures.

**Adaptation obligatoire pour OpenRPG Forge.** Named AI policy settings with schema, preview and no raw bitmask in authoring UI.

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
- transactionId
- planId
- semanticDiff
- niveau d’autorisation

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « AI behavior switches » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-027"></a>

### ORF-SCR-027 — Sandbox sans fichier/réseau

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora concept |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | S01 |
| Tags | `sandbox`, `security`, `capability` |

**Comportement Aurora de référence.** NWScript exposes only registered engine functions.

**Adaptation obligatoire pour OpenRPG Forge.** No filesystem, network, processes, native pointers, reflection, Godot or .NET access.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Sandbox sans fichier/réseau » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-028"></a>

### ORF-SCR-028 — Handles opaques

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | Aucune dépendance explicite; respecter les dépendances du domaine. |
| Sources | S15 |
| Tags | `handle`, `security`, `type` |

**Comportement Aurora de référence.** NWScript `object` is engine handle.

**Adaptation obligatoire pour OpenRPG Forge.** Generational typed EntityRef/AssetId/EffectHandle validated each call and never exposing pointers.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Handles opaques » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-029"></a>

### ORF-SCR-029 — Budget d’instructions

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `quota`, `timeout`, `security` |

**Comportement Aurora de référence.** Scripts historiques peuvent boucler ou lagger.

**Adaptation obligatoire pour OpenRPG Forge.** Instruction quota per handler/tick/module, timeout, cancellation and diagnostic without crashing runtime.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Budget d’instructions » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-030"></a>

### ORF-SCR-030 — Budget mémoire

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `memory`, `quota`, `sandbox` |

**Comportement Aurora de référence.** La VM doit limiter allocations.

**Adaptation obligatoire pour OpenRPG Forge.** Memory quota by context/module, array/string limits, allocation telemetry and fail-safe.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Budget mémoire » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-031"></a>

### ORF-SCR-031 — Déterminisme

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | MULTI, QA |
| Sources | Conception Forge |
| Tags | `determinism`, `rng`, `multiplayer` |

**Comportement Aurora de référence.** Persistent worlds/multiplayer need reproducible behavior.

**Adaptation obligatoire pour OpenRPG Forge.** Engine RNG, simulation clock, stable query ordering where required and server authority.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Déterminisme » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-032"></a>

### ORF-SCR-032 — Hot reload

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | BUILD |
| Sources | Conception Forge |
| Tags | `hot-reload`, `compiler`, `state` |

**Comportement Aurora de référence.** Aurora recompile scripts between builds; modern tools require live updates.

**Adaptation obligatoire pour OpenRPG Forge.** Safe reload at handler boundary, state migration hooks, dependency invalidation and fallback restart.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Hot reload » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-033"></a>

### ORF-SCR-033 — Débogueur

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core Tooling |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | QA, MULTI |
| Sources | Conception Forge |
| Tags | `debugger`, `breakpoint`, `remote` |

**Comportement Aurora de référence.** Toolset original has limited script debugging; ecosystem adds logs.

**Adaptation obligatoire pour OpenRPG Forge.** Breakpoints, step, stack, locals, watches, events, entity inspect and remote server debug with permissions.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Débogueur » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-034"></a>

### ORF-SCR-034 — Tests unitaires ForgeScript

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core Tooling |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `test`, `script`, `fixture` |

**Comportement Aurora de référence.** Aurora builders often test in game.

**Adaptation obligatoire pour OpenRPG Forge.** Test API creates deterministic TestWorld, fixtures, assertions, mocks and CI reporting.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Tests unitaires ForgeScript » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-035"></a>

### ORF-SCR-035 — Profilage de scripts

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core Tooling |
| Origine | Enhanced Edition inspired |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | QA |
| Sources | S13 |
| Tags | `profiler`, `script`, `performance` |

**Comportement Aurora de référence.** EE/current patches improve profiling/logging.

**Adaptation obligatoire pour OpenRPG Forge.** Per-handler CPU/instructions/allocations, hot spots, call graphs and threshold alerts.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Profilage de scripts » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-036"></a>

### ORF-SCR-036 — Version d’API

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | BUILD |
| Sources | Conception Forge |
| Tags | `api`, `version`, `compatibility` |

**Comportement Aurora de référence.** NWN patches add functions and content.

**Adaptation obligatoire pour OpenRPG Forge.** Semantic ForgeScript API versions, capability negotiation, deprecations, migration docs and compatibility tests.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Version d’API » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-037"></a>

### ORF-SCR-037 — Documentation générée

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core Tooling |
| Origine | Ecosystem inspired |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | AI |
| Sources | S15 |
| Tags | `docs`, `api`, `generation` |

**Comportement Aurora de référence.** Lexicon documents functions by category.

**Adaptation obligatoire pour OpenRPG Forge.** Generate API reference, examples, availability by kit/server/client and MCP resource from registration metadata.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Documentation générée » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-038"></a>

### ORF-SCR-038 — Analyse statique

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core Tooling |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `analyzer`, `lint`, `performance` |

**Comportement Aurora de référence.** L’ancien compilateur détecte syntax/type but little domain lint.

**Adaptation obligatoire pour OpenRPG Forge.** Lints for expensive heartbeat, unsafe query, missing null/valid checks, deprecated API and authority violations.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Analyse statique » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-039"></a>

### ORF-SCR-039 — Permissions par module

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | CONTENT, MULTI |
| Sources | Conception Forge |
| Tags | `permission`, `capability`, `module` |

**Comportement Aurora de référence.** Scripts téléchargés sont non fiables.

**Adaptation obligatoire pour OpenRPG Forge.** Capability manifest per module/kit, deny by default, server-enforced and signed distribution policies.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Permissions par module » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-040"></a>

### ORF-SCR-040 — Sérialisation d’état script

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core |
| Origine | Enhanced Edition inspired |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime` |
| Dépendances | GAMEPLAY, EE |
| Sources | S10 |
| Tags | `serialization`, `state`, `save` |

**Comportement Aurora de référence.** EE adds object serialization; scripts need persistent state.

**Adaptation obligatoire pour OpenRPG Forge.** Persist only registered blackboards/components, no raw VM pointers; migration and validation.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Sérialisation d’état script » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.


<a id="orf-scr-041"></a>

### ORF-SCR-041 — Interop éditeur/MCP

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge AI + Core |
| Origine | Forge extension |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.McpHost` |
| Dépendances | AI |
| Sources | Conception Forge |
| Tags | `mcp`, `script`, `compiler` |

**Comportement Aurora de référence.** IA doit compiler et tester les scripts.

**Adaptation obligatoire pour OpenRPG Forge.** Tools get API, create/update source, compile, diagnostics, run tests and preview bindings through transactions.

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

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- éditeur de code et diagnostics cliquables
- console de compilation/tests

#### ForgeScript

- La capacité fait partie de l’API ou de l’outillage ForgeScript.
- L’exposition exacte doit être versionnée et documentée automatiquement.

#### MCP et agents

- get_api, compile, diagnostics, run_tests et assign_event; aucune exécution hors sandbox.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- compilation sans erreur
- API autorisée
- quotas configurés
- tests pertinents exécutés

#### Undo, transactions et concurrence

Les modifications de source/configuration sont transactionnelles; les exécutions de test/build ne modifient pas la source.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Interop éditeur/MCP » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
- [ ] Un cas nominal compile et s’exécute dans la sandbox.
- [ ] Un cas abusif dépasse un quota et est interrompu sans arrêter le runtime.
