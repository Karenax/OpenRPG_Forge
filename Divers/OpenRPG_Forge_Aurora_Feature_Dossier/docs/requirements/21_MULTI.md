# Multijoueur, serveur et mondes persistants

**Domaine : `MULTI`**  
**Préfixe : `ORF-MP`**  
**Couche dominante : Forge Network Kit**  
**Nombre d’exigences : 36**  
**Sources principales : S07, S09, S10, S11, S13, S14, S16, S21, S23**

## Finalité

Lobby, sessions, serveur autoritaire, permissions, synchronisation de contenu, persistance, modération, observabilité et extensibilité.

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
| [ORF-MP-001](#orf-mp-001) | Serveur dédié | P1 | Native runtime capability |
| [ORF-MP-002](#orf-mp-002) | Serveur autoritaire | P1 | Native runtime capability |
| [ORF-MP-003](#orf-mp-003) | Découverte et connexion | P2 | Native runtime capability |
| [ORF-MP-004](#orf-mp-004) | Lobby et sélection de personnage | P2 | Native runtime capability |
| [ORF-MP-005](#orf-mp-005) | Création de personnage | P2 | Native runtime capability |
| [ORF-MP-006](#orf-mp-006) | Personnages locaux ou serveur | P2 | Native runtime modernized |
| [ORF-MP-007](#orf-mp-007) | Parties et groupes | P1 | Native runtime capability |
| [ORF-MP-008](#orf-mp-008) | PvP et friendly fire | P2 | Native Aurora adapted |
| [ORF-MP-009](#orf-mp-009) | Instances de zones | P2 | Enhanced/ecosystem inspired |
| [ORF-MP-010](#orf-mp-010) | Création dynamique de zone | P2 | Enhanced Edition |
| [ORF-MP-011](#orf-mp-011) | Réplication d’entités | P1 | Native runtime modernized |
| [ORF-MP-012](#orf-mp-012) | Interest management | P1 | Forge extension |
| [ORF-MP-013](#orf-mp-013) | Synchronisation de contenu | P1 | Enhanced Edition |
| [ORF-MP-014](#orf-mp-014) | Curated content | P3 | Enhanced Edition |
| [ORF-MP-015](#orf-mp-015) | Version locking | P1 | Enhanced Edition modernized |
| [ORF-MP-016](#orf-mp-016) | Authentification et comptes | P2 | Native runtime modernized |
| [ORF-MP-017](#orf-mp-017) | Rôles et permissions | P1 | Native Aurora modernized |
| [ORF-MP-018](#orf-mp-018) | Administration à distance | P2 | Ecosystem inspired |
| [ORF-MP-019](#orf-mp-019) | Modération | P2 | Native runtime modernized |
| [ORF-MP-020](#orf-mp-020) | Chat | P1 | Native runtime capability |
| [ORF-MP-021](#orf-mp-021) | Voix | P3 | Forge extension |
| [ORF-MP-022](#orf-mp-022) | Persistance de monde | P2 | Ecosystem inspired |
| [ORF-MP-023](#orf-mp-023) | SQL sécurisé | P2 | Enhanced Edition + ecosystem |
| [ORF-MP-024](#orf-mp-024) | Sharding et zones serveur | P4 | Forge extension |
| [ORF-MP-025](#orf-mp-025) | Hot reload de contenu serveur | P3 | Ecosystem inspired |
| [ORF-MP-026](#orf-mp-026) | Extensibilité serveur | P3 | Ecosystem inspired |
| [ORF-MP-027](#orf-mp-027) | Bus d’événements serveur | P1 | Ecosystem inspired |
| [ORF-MP-028](#orf-mp-028) | Métriques et health checks | P2 | Enhanced/current inspired |
| [ORF-MP-029](#orf-mp-029) | Logs structurés | P1 | Enhanced Edition inspired |
| [ORF-MP-030](#orf-mp-030) | Profiler serveur | P2 | Enhanced Edition inspired |
| [ORF-MP-031](#orf-mp-031) | Anti-cheat par conception | P1 | Forge extension |
| [ORF-MP-032](#orf-mp-032) | Reconnexion | P2 | Forge extension |
| [ORF-MP-033](#orf-mp-033) | Sauvegarde/rollback serveur | P2 | Ecosystem inspired |
| [ORF-MP-034](#orf-mp-034) | Capacité et files d’attente | P2 | Native runtime modernized |
| [ORF-MP-035](#orf-mp-035) | Cross-platform protocol | P3 | Enhanced Edition |
| [ORF-MP-036](#orf-mp-036) | Tests réseau déterministes | P1 | Forge extension |

## Exigences détaillées

<a id="orf-mp-001"></a>

### ORF-MP-001 — Serveur dédié

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Network Kit |
| Origine | Native runtime capability |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | BUILD |
| Sources | S21 |
| Tags | `server`, `dedicated`, `headless` |

**Comportement Aurora de référence.** NWN supports hosted/dedicated multiplayer modules.

**Adaptation obligatoire pour OpenRPG Forge.** Headless authoritative server binary with config files, service mode, health checks and no editor dependency.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Serveur dédié » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-002"></a>

### ORF-MP-002 — Serveur autoritaire

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Network Kit |
| Origine | Native runtime capability |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | SCRIPT |
| Sources | S21 |
| Tags | `authority`, `server`, `security` |

**Comportement Aurora de référence.** NWN server controls module state and scripts.

**Adaptation obligatoire pour OpenRPG Forge.** All gameplay mutations validated server-side; clients send intents and receive replicated state.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Serveur autoritaire » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-003"></a>

### ORF-MP-003 — Découverte et connexion

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Network Kit |
| Origine | Native runtime capability |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | CONTENT |
| Sources | S21 |
| Tags | `discovery`, `connect`, `lobby` |

**Comportement Aurora de référence.** NWN provides server browser/direct connect.

**Adaptation obligatoire pour OpenRPG Forge.** Direct address, LAN discovery, curated directory optional, favorites, recent and invite links.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Découverte et connexion » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-004"></a>

### ORF-MP-004 — Lobby et sélection de personnage

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Network Kit |
| Origine | Native runtime capability |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | CREATURE |
| Sources | S21 |
| Tags | `lobby`, `character`, `ready` |

**Comportement Aurora de référence.** Players connect and choose/create characters.

**Adaptation obligatoire pour OpenRPG Forge.** Session lobby with rules, content status, slots, teams, characters, permissions and ready state.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Lobby et sélection de personnage » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-005"></a>

### ORF-MP-005 — Création de personnage

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge RPG Kit + Network |
| Origine | Native runtime capability |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime`, `Forge.Network`, `Forge.Server` |
| Dépendances | CREATURE |
| Sources | S21 |
| Tags | `character-creation`, `rules`, `server` |

**Comportement Aurora de référence.** NWN supports character creation based on module rules.

**Adaptation obligatoire pour OpenRPG Forge.** Ruleset-driven CharacterBuilder with templates, validation, preview, import policy and server approval.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Création de personnage » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-006"></a>

### ORF-MP-006 — Personnages locaux ou serveur

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Network Kit |
| Origine | Native runtime modernized |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | CONTENT |
| Sources | S07, S09, S10, S11, S13, S14, S16, S21, S23 |
| Tags | `character`, `vault`, `authority` |

**Comportement Aurora de référence.** NWN historically supports local/server vault concepts.

**Adaptation obligatoire pour OpenRPG Forge.** CharacterAuthority local, signed upload, server-managed or account-bound; security and migration policies.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Personnages locaux ou serveur » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-007"></a>

### ORF-MP-007 — Parties et groupes

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Network Kit + RPG |
| Origine | Native runtime capability |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime`, `Forge.Network`, `Forge.Server` |
| Dépendances | COMPANION, QUEST |
| Sources | S10, S21 |
| Tags | `party`, `group`, `multiplayer` |

**Comportement Aurora de référence.** NWN supports parties and party chat/control.

**Adaptation obligatoire pour OpenRPG Forge.** Party service invitations, roles, leader, loot, quest sharing, markers and separation policy.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Parties et groupes » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-008"></a>

### ORF-MP-008 — PvP et friendly fire

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Network Kit |
| Origine | Native Aurora adapted |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | AREA, FACTION |
| Sources | S03 |
| Tags | `pvp`, `friendly-fire`, `policy` |

**Comportement Aurora de référence.** NWN offers PvP modes and module/area settings.

**Adaptation obligatoire pour OpenRPG Forge.** CombatPolicy global/area/party, consent, duel, crime, safe zones and server overrides.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « PvP et friendly fire » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-009"></a>

### ORF-MP-009 — Instances de zones

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Network Kit |
| Origine | Enhanced/ecosystem inspired |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | AREA |
| Sources | S10, S16 |
| Tags | `instance`, `area`, `server` |

**Comportement Aurora de référence.** Persistent worlds often need separate copies via extensions/scripts.

**Adaptation obligatoire pour OpenRPG Forge.** AreaInstance service with lifecycle, party allocation, persistence, capacity, templates and garbage collection.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Instances de zones » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-010"></a>

### ORF-MP-010 — Création dynamique de zone

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Network Kit |
| Origine | Enhanced Edition |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | EE |
| Sources | S10 |
| Tags | `dynamic-area`, `instance`, `runtime` |

**Comportement Aurora de référence.** EE adds CreateArea and object copying.

**Adaptation obligatoire pour OpenRPG Forge.** Server can instantiate AreaTemplate/clone with validated overrides and bounded resources.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Création dynamique de zone » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-011"></a>

### ORF-MP-011 — Réplication d’entités

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Network Kit |
| Origine | Native runtime modernized |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | GAMEPLAY |
| Sources | S07, S09, S10, S11, S13, S14, S16, S21, S23 |
| Tags | `replication`, `entity`, `network` |

**Comportement Aurora de référence.** Engine synchronizes creatures/items/doors and effects.

**Adaptation obligatoire pour OpenRPG Forge.** Component replication policies, relevance, delta compression, ownership, prediction and reconciliation.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Réplication d’entités » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-012"></a>

### ORF-MP-012 — Interest management

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Network Kit |
| Origine | Forge extension |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | AREA |
| Sources | Conception Forge |
| Tags | `interest`, `scalability`, `network` |

**Comportement Aurora de référence.** Large worlds cannot send all entities to all clients.

**Adaptation obligatoire pour OpenRPG Forge.** Relevance by area, distance, visibility, party, ownership and explicit subscriptions.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Interest management » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-013"></a>

### ORF-MP-013 — Synchronisation de contenu

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Network Kit |
| Origine | Enhanced Edition |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | CONTENT |
| Sources | S09, S11, S14 |
| Tags | `content-sync`, `manifest`, `download` |

**Comportement Aurora de référence.** NWSync distributes required custom content to players.

**Adaptation obligatoire pour OpenRPG Forge.** Content repository manifests, chunks, hashes, cache, resumable download, delta and trust policy.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Synchronisation de contenu » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-014"></a>

### ORF-MP-014 — Curated content

| Champ | Valeur |
|---|---|
| Priorité | **P3** |
| Couche | Forge Distribution |
| Origine | Enhanced Edition |
| Composants probables | `Forge.Domain` |
| Dépendances | CONTENT |
| Sources | S08, S09 |
| Tags | `catalog`, `curated`, `content` |

**Comportement Aurora de référence.** EE launcher offers curated community content.

**Adaptation obligatoire pour OpenRPG Forge.** Optional signed catalog with metadata, dependencies, ratings, compatibility and moderation; not required for direct packages.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Curated content » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-015"></a>

### ORF-MP-015 — Version locking

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Network Kit |
| Origine | Enhanced Edition modernized |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | CONTENT |
| Sources | S14 |
| Tags | `version`, `lock`, `compatibility` |

**Comportement Aurora de référence.** Servers require exact content versions.

**Adaptation obligatoire pour OpenRPG Forge.** Lockfile and content fingerprint negotiated before join; human-readable mismatch report.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Version locking » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-016"></a>

### ORF-MP-016 — Authentification et comptes

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Network Kit |
| Origine | Native runtime modernized |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | QA |
| Sources | S07, S09, S10, S11, S13, S14, S16, S21, S23 |
| Tags | `auth`, `account`, `identity` |

**Comportement Aurora de référence.** NWN servers use CD keys/accounts/passwords depending era.

**Adaptation obligatoire pour OpenRPG Forge.** Pluggable auth local, OAuth/OIDC, platform or anonymous; stable account ID and privacy.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Authentification et comptes » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-017"></a>

### ORF-MP-017 — Rôles et permissions

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Network Kit |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | DM |
| Sources | S01 |
| Tags | `role`, `permission`, `security` |

**Comportement Aurora de référence.** Server/DM/player distinctions exist.

**Adaptation obligatoire pour OpenRPG Forge.** RBAC/ABAC roles Player, Moderator, Builder, GM, Admin, Spectator plus custom capabilities.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Rôles et permissions » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-018"></a>

### ORF-MP-018 — Administration à distance

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Network Kit |
| Origine | Ecosystem inspired |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | DM, QA |
| Sources | S16 |
| Tags | `admin`, `remote`, `api` |

**Comportement Aurora de référence.** Persistent worlds use console/NWNX tooling.

**Adaptation obligatoire pour OpenRPG Forge.** Authenticated admin API/CLI with rate limits, audit, health, kick/ban, config and safe commands.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Administration à distance » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-019"></a>

### ORF-MP-019 — Modération

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Network Kit |
| Origine | Native runtime modernized |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | DM |
| Sources | S21 |
| Tags | `moderation`, `ban`, `chat` |

**Comportement Aurora de référence.** Multiplayer requires kick/ban/chat tools.

**Adaptation obligatoire pour OpenRPG Forge.** Account/IP/device policy adapters, mute, timeout, reports, evidence, appeal notes and privacy retention.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Modération » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-020"></a>

### ORF-MP-020 — Chat

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Network Kit |
| Origine | Native runtime capability |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | DM |
| Sources | S21 |
| Tags | `chat`, `channel`, `text` |

**Comportement Aurora de référence.** NWN supports local, party, tell, shout, DM channels.

**Adaptation obligatoire pour OpenRPG Forge.** Typed channels, proximity, language filters, permissions, logs, accessibility and bridge adapters.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Chat » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-021"></a>

### ORF-MP-021 — Voix

| Champ | Valeur |
|---|---|
| Priorité | **P3** |
| Couche | Forge Network Kit |
| Origine | Forge extension |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | AUDIO |
| Sources | Conception Forge |
| Tags | `voice`, `proximity`, `privacy` |

**Comportement Aurora de référence.** Not core Aurora authoring feature.

**Adaptation obligatoire pour OpenRPG Forge.** Optional voice service adapter with proximity/party/GM, consent, mute, indicators and recording policy.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Voix » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-022"></a>

### ORF-MP-022 — Persistance de monde

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Network Kit |
| Origine | Ecosystem inspired |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | EE |
| Sources | S16 |
| Tags | `persistence`, `database`, `world` |

**Comportement Aurora de référence.** Persistent worlds rely on databases and server extensions.

**Adaptation obligatoire pour OpenRPG Forge.** Persistence provider interface for relational/embedded stores, transactions, migrations, backups and failover.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Persistance de monde » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-023"></a>

### ORF-MP-023 — SQL sécurisé

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Network Kit |
| Origine | Enhanced Edition + ecosystem |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | EE, SCRIPT |
| Sources | S10, S16 |
| Tags | `sql`, `database`, `security` |

**Comportement Aurora de référence.** EE adds SQL/database functions; NWNX expands DB access.

**Adaptation obligatoire pour OpenRPG Forge.** Server-only repository APIs and parameterized queries; module scripts receive domain services, not raw unrestricted SQL by default.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « SQL sécurisé » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-024"></a>

### ORF-MP-024 — Sharding et zones serveur

| Champ | Valeur |
|---|---|
| Priorité | **P4** |
| Couche | Forge Network Kit |
| Origine | Forge extension |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | Aucune dépendance explicite; respecter les dépendances du domaine. |
| Sources | Conception Forge |
| Tags | `shard`, `scale`, `distributed` |

**Comportement Aurora de référence.** Very large PWs use custom architectures.

**Adaptation obligatoire pour OpenRPG Forge.** Future service boundaries by world/zone, transfer tokens, consistency rules and observability; not MVP.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Sharding et zones serveur » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-025"></a>

### ORF-MP-025 — Hot reload de contenu serveur

| Champ | Valeur |
|---|---|
| Priorité | **P3** |
| Couche | Forge Network Kit |
| Origine | Ecosystem inspired |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | BUILD, CONTENT |
| Sources | S16, S17 |
| Tags | `deploy`, `hot-reload`, `server` |

**Comportement Aurora de référence.** Builders often update live persistent worlds carefully.

**Adaptation obligatoire pour OpenRPG Forge.** Staged package deployment, compatibility checks, drain/reload, rollback and migration hooks.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Hot reload de contenu serveur » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-026"></a>

### ORF-MP-026 — Extensibilité serveur

| Champ | Valeur |
|---|---|
| Priorité | **P3** |
| Couche | Forge Network Kit |
| Origine | Ecosystem inspired |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | SCRIPT |
| Sources | S16 |
| Tags | `plugin`, `server`, `extension` |

**Comportement Aurora de référence.** NWNX exposes plugins and hooks.

**Adaptation obligatoire pour OpenRPG Forge.** Stable server plugin SDK with capabilities, process isolation option, versioning and observability.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Extensibilité serveur » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-027"></a>

### ORF-MP-027 — Bus d’événements serveur

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core + Network |
| Origine | Ecosystem inspired |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Network`, `Forge.Server` |
| Dépendances | SCRIPT |
| Sources | S23 |
| Tags | `event-bus`, `server`, `trace` |

**Comportement Aurora de référence.** Community frameworks centralize NWN events.

**Adaptation obligatoire pour OpenRPG Forge.** Typed event bus shared with gameplay, subscriptions, priorities, tracing, replay and backpressure.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Bus d’événements serveur » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-028"></a>

### ORF-MP-028 — Métriques et health checks

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Network Kit |
| Origine | Enhanced/current inspired |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | QA |
| Sources | S13 |
| Tags | `metrics`, `health`, `observability` |

**Comportement Aurora de référence.** Modern server operation needs telemetry.

**Adaptation obligatoire pour OpenRPG Forge.** Prometheus/OpenTelemetry adapters, TPS, tick time, memory, connections, queues, script hot spots and endpoints.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Métriques et health checks » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-029"></a>

### ORF-MP-029 — Logs structurés

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Core + Network |
| Origine | Enhanced Edition inspired |
| Composants probables | `Forge.Domain`, `Forge.Editor`, `Forge.Runtime`, `Forge.Network`, `Forge.Server` |
| Dépendances | QA |
| Sources | S13 |
| Tags | `logging`, `structured`, `server` |

**Comportement Aurora de référence.** EE patch line improves logging.

**Adaptation obligatoire pour OpenRPG Forge.** JSON/text logs with categories, correlation IDs, privacy filters, rotation and remote sinks.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Logs structurés » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-030"></a>

### ORF-MP-030 — Profiler serveur

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Network Kit |
| Origine | Enhanced Edition inspired |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | QA |
| Sources | S13 |
| Tags | `profiler`, `server`, `performance` |

**Comportement Aurora de référence.** Current patches add Tracy profiling.

**Adaptation obligatoire pour OpenRPG Forge.** Sampling/instrumentation hooks, trace capture, frame/tick breakdown and script/DB/network spans.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Profiler serveur » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-031"></a>

### ORF-MP-031 — Anti-cheat par conception

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Network Kit |
| Origine | Forge extension |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `anti-cheat`, `security`, `authority` |

**Comportement Aurora de référence.** NWN server authority mitigates client cheats.

**Adaptation obligatoire pour OpenRPG Forge.** Validate intents, rate limits, signed content, authoritative RNG, anomaly detection and no trust in client saves.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Anti-cheat par conception » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-032"></a>

### ORF-MP-032 — Reconnexion

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Network Kit |
| Origine | Forge extension |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | GAMEPLAY |
| Sources | Conception Forge |
| Tags | `reconnect`, `session`, `recovery` |

**Comportement Aurora de référence.** Network interruptions require recovery.

**Adaptation obligatoire pour OpenRPG Forge.** Session tokens, grace period, entity handoff, party state, pending transactions and safe fallback.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Reconnexion » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-033"></a>

### ORF-MP-033 — Sauvegarde/rollback serveur

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Network Kit |
| Origine | Ecosystem inspired |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | QA |
| Sources | S16 |
| Tags | `backup`, `rollback`, `persistence` |

**Comportement Aurora de référence.** Persistent worlds need backups.

**Adaptation obligatoire pour OpenRPG Forge.** Consistent snapshots, WAL/backups, restore rehearsal, migrations and point-in-time recovery.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Sauvegarde/rollback serveur » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-034"></a>

### ORF-MP-034 — Capacité et files d’attente

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Network Kit |
| Origine | Native runtime modernized |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | QA |
| Sources | S21 |
| Tags | `capacity`, `queue`, `scalability` |

**Comportement Aurora de référence.** Promoted NWN PWs can host large populations but capacity varies.

**Adaptation obligatoire pour OpenRPG Forge.** Configurable caps, admission queue, reserved slots, load shedding and metrics; no fixed player-count promise.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Capacité et files d’attente » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-035"></a>

### ORF-MP-035 — Cross-platform protocol

| Champ | Valeur |
|---|---|
| Priorité | **P3** |
| Couche | Forge Network Kit |
| Origine | Enhanced Edition |
| Composants probables | `Forge.Network`, `Forge.Server` |
| Dépendances | BUILD |
| Sources | S21 |
| Tags | `cross-platform`, `protocol`, `compatibility` |

**Comportement Aurora de référence.** NWN EE promotes cross-platform multiplayer.

**Adaptation obligatoire pour OpenRPG Forge.** Versioned protocol and platform abstraction; compatibility matrix, endianness/tests and staged rollout.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Cross-platform protocol » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.


<a id="orf-mp-036"></a>

### ORF-MP-036 — Tests réseau déterministes

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge Network Kit Tooling |
| Origine | Forge extension |
| Composants probables | `Forge.Network`, `Forge.Server`, `Forge.Cli`, `Forge.Tooling` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `network`, `test`, `simulation` |

**Comportement Aurora de référence.** Network bugs are hard to reproduce.

**Adaptation obligatoire pour OpenRPG Forge.** Simulated clients, latency/loss/reorder, replay, soak tests and authoritative state assertions.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification
- scope d’autorité
- révision serveur
- identité/audit

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- permissions visibles
- confirmation des actions destructrices
- journal d’audit

#### ForgeScript

- Lecture par handle/type sûr lorsque la donnée doit être consultable au runtime.
- Mutation uniquement via un service/commande validé; aucun accès direct aux fichiers ou aux nœuds Godot.
- Événements avant/après uniquement lorsque la capacité possède un cycle de vie utile; éviter les hooks redondants.

#### MCP et agents

- Ressource de lecture filtrée par ID ou recherche.
- Commande de mutation disponible en dry-run avec diff sémantique.
- Exiger identité, rôle, portée serveur et journal d’audit.

#### Validation

- champs obligatoires
- UUID uniques
- références résolues
- version de schéma supportée
- permission suffisante
- scope borné
- révision non obsolète
- audit généré

#### Undo, transactions et concurrence

Prévisualisation obligatoire; undo seulement si l’état serveur permet une compensation sûre, sinon commande compensatoire et audit.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Tests réseau déterministes » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Les permissions et l’audit sont vérifiés par un test d’intégration serveur.
