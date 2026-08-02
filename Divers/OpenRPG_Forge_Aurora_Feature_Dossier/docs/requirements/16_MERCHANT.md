# Marchands, magasins et économie

**Domaine : `MERCHANT`**  
**Préfixe : `ORF-MER`**  
**Couche dominante : Forge RPG Kit**  
**Nombre d’exigences : 19**  
**Sources principales : S03, S02, S15**

## Finalité

Magasins indépendants des PNJ, inventaire, prix, restrictions, trésorerie, biens volés, monnaies et réapprovisionnement.

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
| [ORF-MER-001](#orf-mer-001) | Objet magasin indépendant | P1 | Native Aurora |
| [ORF-MER-002](#orf-mer-002) | Store Setup Wizard | P1 | Official expansion |
| [ORF-MER-003](#orf-mer-003) | Inventaire du magasin | P1 | Native Aurora |
| [ORF-MER-004](#orf-mer-004) | Markup et markdown | P1 | Native Aurora modernized |
| [ORF-MER-005](#orf-mer-005) | Trésorerie | P2 | Native Aurora modernized |
| [ORF-MER-006](#orf-mer-006) | Prix maximal d’achat | P2 | Native Aurora |
| [ORF-MER-007](#orf-mer-007) | Biens volés | P2 | Native Aurora modernized |
| [ORF-MER-008](#orf-mer-008) | Restrictions whitelist/blacklist | P1 | Native Aurora modernized |
| [ORF-MER-009](#orf-mer-009) | Stock infini | P1 | Native Aurora |
| [ORF-MER-010](#orf-mer-010) | Réapprovisionnement | P2 | Native Aurora modernized |
| [ORF-MER-011](#orf-mer-011) | OnOpen et OnClose | P1 | Native Aurora |
| [ORF-MER-012](#orf-mer-012) | Transactions atomiques | P0 | Native Aurora modernized |
| [ORF-MER-013](#orf-mer-013) | Monnaies multiples | P2 | Forge extension |
| [ORF-MER-014](#orf-mer-014) | Négociation | P3 | Forge extension |
| [ORF-MER-015](#orf-mer-015) | Marchés dynamiques | P3 | Forge extension |
| [ORF-MER-016](#orf-mer-016) | Services | P2 | Forge extension |
| [ORF-MER-017](#orf-mer-017) | Interface de boutique | P1 | Native Aurora modernized |
| [ORF-MER-018](#orf-mer-018) | Audit économique | P2 | Ecosystem pattern |
| [ORF-MER-019](#orf-mer-019) | Validation marchand | P0 | Forge extension |

## Exigences détaillées

<a id="orf-mer-001"></a>

### ORF-MER-001 — Objet magasin indépendant

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | PALETTE |
| Sources | S03 |
| Tags | `store`, `definition`, `merchant` |

**Comportement Aurora de référence.** Aurora place un merchant invisible séparé du PNJ.

**Adaptation obligatoire pour OpenRPG Forge.** StoreDefinition indépendant de l’acteur; plusieurs vendeurs, interfaces et canaux peuvent le référencer.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- formulaire à sections
- aperçu visuel et résumé des règles

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
- règles du kit satisfaites
- valeurs dans les plages
- assets et bindings compatibles

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Objet magasin indépendant » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-mer-002"></a>

### ORF-MER-002 — Store Setup Wizard

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Official expansion |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | DIALOGUE, SCRIPT |
| Sources | S02 |
| Tags | `wizard`, `store`, `dialogue` |

**Comportement Aurora de référence.** SoU crée conversation, scripts et lien marchand.

**Adaptation obligatoire pour OpenRPG Forge.** Wizard crée StoreDefinition, vendor binding, dialogue choices, interaction and tests.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- formulaire à sections
- aperçu visuel et résumé des règles

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
- règles du kit satisfaites
- valeurs dans les plages
- assets et bindings compatibles

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Store Setup Wizard » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-mer-003"></a>

### ORF-MER-003 — Inventaire du magasin

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | ITEM |
| Sources | S03 |
| Tags | `stock`, `inventory`, `store` |

**Comportement Aurora de référence.** Aurora édite le stock dans un inventaire.

**Adaptation obligatoire pour OpenRPG Forge.** StockEntries finite/infinite, quantities, variants, supplier, restock, display order and hidden conditions.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- formulaire à sections
- aperçu visuel et résumé des règles

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
- règles du kit satisfaites
- valeurs dans les plages
- assets et bindings compatibles

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Inventaire du magasin » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-mer-004"></a>

### ORF-MER-004 — Markup et markdown

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | FACTION |
| Sources | S03 |
| Tags | `price`, `markup`, `economy` |

**Comportement Aurora de référence.** Aurora règle pourcentage de vente/achat.

**Adaptation obligatoire pour OpenRPG Forge.** PricingPolicy with buy/sell curves, currencies, reputation, scarcity, condition and negotiation.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- formulaire à sections
- aperçu visuel et résumé des règles

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
- règles du kit satisfaites
- valeurs dans les plages
- assets et bindings compatibles

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Markup et markdown » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-mer-005"></a>

### ORF-MER-005 — Trésorerie

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | MULTI |
| Sources | S03 |
| Tags | `treasury`, `currency`, `ledger` |

**Comportement Aurora de référence.** Aurora configure une quantité d’or disponible.

**Adaptation obligatoire pour OpenRPG Forge.** Treasury accounts per currency, credit, debt, replenishment, caps and transaction ledger.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- formulaire à sections
- aperçu visuel et résumé des règles

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
- règles du kit satisfaites
- valeurs dans les plages
- assets et bindings compatibles

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Trésorerie » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-mer-006"></a>

### ORF-MER-006 — Prix maximal d’achat

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | ITEM |
| Sources | S03 |
| Tags | `purchase`, `cap`, `policy` |

**Comportement Aurora de référence.** Un marchand peut refuser les objets trop chers.

**Adaptation obligatoire pour OpenRPG Forge.** PurchasePolicy caps by item, category, transaction, treasury and buyer relationship.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- formulaire à sections
- aperçu visuel et résumé des règles

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
- règles du kit satisfaites
- valeurs dans les plages
- assets et bindings compatibles

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Prix maximal d’achat » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-mer-007"></a>

### ORF-MER-007 — Biens volés

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | ITEM, FACTION |
| Sources | S03 |
| Tags | `stolen`, `fence`, `crime` |

**Comportement Aurora de référence.** Le magasin accepte ou refuse les objets volés.

**Adaptation obligatoire pour OpenRPG Forge.** StolenGoodsPolicy by jurisdiction, proof, fence role, risk premium and crime event.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- formulaire à sections
- aperçu visuel et résumé des règles

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
- règles du kit satisfaites
- valeurs dans les plages
- assets et bindings compatibles

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Biens volés » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-mer-008"></a>

### ORF-MER-008 — Restrictions whitelist/blacklist

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | SCRIPT |
| Sources | S03 |
| Tags | `restriction`, `filter`, `item` |

**Comportement Aurora de référence.** Aurora limite catégories achetées/vendues.

**Adaptation obligatoire pour OpenRPG Forge.** ItemQuery filters on type, tags, quality, provenance, condition, owner and custom rule.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- formulaire à sections
- aperçu visuel et résumé des règles

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
- règles du kit satisfaites
- valeurs dans les plages
- assets et bindings compatibles

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Restrictions whitelist/blacklist » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-mer-009"></a>

### ORF-MER-009 — Stock infini

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | MULTI |
| Sources | S03 |
| Tags | `infinite`, `stock`, `supply` |

**Comportement Aurora de référence.** Certains items peuvent être achetés sans limite.

**Adaptation obligatoire pour OpenRPG Forge.** SupplyPolicy infinite, finite, generated, crafted, shared-server or personal.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- formulaire à sections
- aperçu visuel et résumé des règles

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
- règles du kit satisfaites
- valeurs dans les plages
- assets et bindings compatibles

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Stock infini » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-mer-010"></a>

### ORF-MER-010 — Réapprovisionnement

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | PROJECT, SCRIPT |
| Sources | S03, S02, S15 |
| Tags | `restock`, `schedule`, `economy` |

**Comportement Aurora de référence.** Souvent géré par scripts.

**Adaptation obligatoire pour OpenRPG Forge.** RestockSchedule based on world time, events, supplier routes, demand and server persistence.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- formulaire à sections
- aperçu visuel et résumé des règles

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
- règles du kit satisfaites
- valeurs dans les plages
- assets et bindings compatibles

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Réapprovisionnement » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-mer-011"></a>

### ORF-MER-011 — OnOpen et OnClose

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | SCRIPT |
| Sources | S03 |
| Tags | `event`, `store`, `session` |

**Comportement Aurora de référence.** Merchant object expose événements.

**Adaptation obligatoire pour OpenRPG Forge.** StoreSessionOpened/Closed with customer, vendor, price context and transaction summary.

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
- formulaire à sections
- aperçu visuel et résumé des règles

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
- règles du kit satisfaites
- valeurs dans les plages
- assets et bindings compatibles

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « OnOpen et OnClose » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-mer-012"></a>

### ORF-MER-012 — Transactions atomiques

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | ITEM, MULTI |
| Sources | S15 |
| Tags | `transaction`, `trade`, `atomic` |

**Comportement Aurora de référence.** Aurora effectue achat/vente via UI moteur.

**Adaptation obligatoire pour OpenRPG Forge.** Buy/Sell/Barter transaction validates funds, ownership, stock, capacity and rollback.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- formulaire à sections
- aperçu visuel et résumé des règles

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
- règles du kit satisfaites
- valeurs dans les plages
- assets et bindings compatibles

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Transactions atomiques » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-mer-013"></a>

### ORF-MER-013 — Monnaies multiples

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge RPG Kit |
| Origine | Forge extension |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | PROJECT |
| Sources | Conception Forge |
| Tags | `currency`, `wallet`, `exchange` |

**Comportement Aurora de référence.** Aurora utilise surtout gold.

**Adaptation obligatoire pour OpenRPG Forge.** CurrencyDefinition and wallets supporting coins, tokens, reputation, barter and exchange rates.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- formulaire à sections
- aperçu visuel et résumé des règles

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
- règles du kit satisfaites
- valeurs dans les plages
- assets et bindings compatibles

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Monnaies multiples » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-mer-014"></a>

### ORF-MER-014 — Négociation

| Champ | Valeur |
|---|---|
| Priorité | **P3** |
| Couche | Forge RPG Kit |
| Origine | Forge extension |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | CREATURE, DIALOGUE |
| Sources | Conception Forge |
| Tags | `negotiation`, `skill`, `price` |

**Comportement Aurora de référence.** Les prix peuvent être modifiés par compétences/scripts.

**Adaptation obligatoire pour OpenRPG Forge.** NegotiationCheck, offers, limits, cooldown, relationship effects and multiplayer authority.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- formulaire à sections
- aperçu visuel et résumé des règles

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
- règles du kit satisfaites
- valeurs dans les plages
- assets et bindings compatibles

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Négociation » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-mer-015"></a>

### ORF-MER-015 — Marchés dynamiques

| Champ | Valeur |
|---|---|
| Priorité | **P3** |
| Couche | Forge RPG Kit |
| Origine | Forge extension |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | MULTI |
| Sources | Conception Forge |
| Tags | `market`, `dynamic`, `economy` |

**Comportement Aurora de référence.** Non natif central.

**Adaptation obligatoire pour OpenRPG Forge.** SupplyDemand model optional, regional prices, events, production, sinks and admin controls.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- formulaire à sections
- aperçu visuel et résumé des règles

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
- règles du kit satisfaites
- valeurs dans les plages
- assets et bindings compatibles

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Marchés dynamiques » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-mer-016"></a>

### ORF-MER-016 — Services

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge RPG Kit |
| Origine | Forge extension |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | SCRIPT |
| Sources | Conception Forge |
| Tags | `service`, `merchant`, `action` |

**Comportement Aurora de référence.** Les marchands historiques vendent surtout des items.

**Adaptation obligatoire pour OpenRPG Forge.** ServiceEntry for repair, identification, travel, crafting, training, healing and custom actions.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- formulaire à sections
- aperçu visuel et résumé des règles

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
- règles du kit satisfaites
- valeurs dans les plages
- assets et bindings compatibles

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Services » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-mer-017"></a>

### ORF-MER-017 — Interface de boutique

| Champ | Valeur |
|---|---|
| Priorité | **P1** |
| Couche | Forge RPG Kit |
| Origine | Native Aurora modernized |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | EE, QA |
| Sources | S10 |
| Tags | `ui`, `store`, `accessibility` |

**Comportement Aurora de référence.** Aurora fournit UI standard.

**Adaptation obligatoire pour OpenRPG Forge.** Store UI configurable, keyboard/gamepad, compare, filters, cart, confirmation and accessibility.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- formulaire à sections
- aperçu visuel et résumé des règles

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
- règles du kit satisfaites
- valeurs dans les plages
- assets et bindings compatibles

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Interface de boutique » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-mer-018"></a>

### ORF-MER-018 — Audit économique

| Champ | Valeur |
|---|---|
| Priorité | **P2** |
| Couche | Forge Network Kit + RPG Kit |
| Origine | Ecosystem pattern |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime`, `Forge.Network`, `Forge.Server` |
| Dépendances | MULTI, QA |
| Sources | S16 |
| Tags | `audit`, `economy`, `server` |

**Comportement Aurora de référence.** Les persistent worlds nécessitent suivi externe.

**Adaptation obligatoire pour OpenRPG Forge.** Ledger with source/sink, actor, store, item, price, currency, timestamp and anomaly detection.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- formulaire à sections
- aperçu visuel et résumé des règles

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
- règles du kit satisfaites
- valeurs dans les plages
- assets et bindings compatibles

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Audit économique » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.


<a id="orf-mer-019"></a>

### ORF-MER-019 — Validation marchand

| Champ | Valeur |
|---|---|
| Priorité | **P0** |
| Couche | Forge RPG Kit |
| Origine | Forge extension |
| Composants probables | `Forge.RpgKit.Domain`, `Forge.RpgKit.Editor`, `Forge.RpgKit.Runtime` |
| Dépendances | QA |
| Sources | Conception Forge |
| Tags | `validation`, `merchant`, `economy` |

**Comportement Aurora de référence.** Un stock ou filtre incohérent peut rendre le shop inutilisable.

**Adaptation obligatoire pour OpenRPG Forge.** Verify vendor binding, currencies, stock, prices, restrictions, treasury, localization and scripts.

#### Contrat fonctionnel

- La capacité est modélisée dans le domaine canonique avant sa représentation Godot.
- L’interface ne doit pas créer une voie de mutation parallèle: elle appelle une commande de domaine.
- Le runtime ignore proprement une extension optionnelle absente et produit un diagnostic si elle est requise.
- Les erreurs sont identifiées par un code stable, un chemin de document et, lorsque possible, un UUID.

#### Données minimales

- UUID stable
- schemaVersion
- provenance de modification

#### Interface d’auteur

- propriété visible dans l’inspecteur ou l’écran spécialisé
- état vide et erreur explicites
- formulaire à sections
- aperçu visuel et résumé des règles

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
- règles du kit satisfaites
- valeurs dans les plages
- assets et bindings compatibles

#### Undo, transactions et concurrence

Une mutation d’édition doit être atomique, reliée à EditorUndoRedoManager et restaurer le document canonique, pas seulement la scène Godot.

#### Critères d’acceptation

- [ ] Un auteur peut configurer « Validation marchand » sans modifier manuellement un fichier généré.
- [ ] Sauvegarder, fermer et recharger conserve exactement la configuration canonique.
- [ ] Une configuration invalide est refusée ou signalée avant le build avec un diagnostic localisé.
- [ ] Une modification via interface et la même modification via commande de domaine produisent le même résultat.
- [ ] Undo puis redo restaurent successivement l’état précédent et l’état modifié.
