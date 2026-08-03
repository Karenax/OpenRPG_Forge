# OpenRPG Forge — contexte autonome pour Codex/Hermes

Version de référence : 2 août 2026.

Ce fichier peut être transmis seul à un agent. Dans un dépôt réel, utiliser de préférence le paquet structuré et conserver `AGENTS.md` à la racine.



---

# MEGACONTEXT PRINCIPAL

# MEGACONTEXT — OpenRPG Forge

## 0. Instruction au nouvel agent

Tu rejoins un projet de moteur et de toolset de RPG 3D provisoirement nommé **OpenRPG Forge**.

Lis ce document comme la source autonome de contexte produit et architecture. Lis ensuite `AGENTS.md`, `STATUS.md`, `TASKS_MVP.md` et les ADR. La direction produit et graphique courante est tenue dans `docs/UX_AURORA_2026.md`. Le chantier post-MVP **Asset Vault et carte Forgotten Forest** est spécifié dans `docs/assets/OPENRPG_FORGE_BIBLIOTHEQUE_ASSETS_ET_MAP_REELLE.md` et doit être découpé en tâches bornées avant exécution. Ne tente pas de construire tout le produit en une seule opération. Transforme chaque étape en tâche bornée, avec critères d’acceptation, compilation, tests et mise à jour du statut.

Le mot « Aurora 2026 » décrit l’intention fonctionnelle. Il ne doit pas devenir le nom public du produit. Ne copie pas les actifs, marques, règles, scripts, données ou interfaces propriétaires de Neverwinter Nights, Aurora ou D&D.

---

## 1. Vision

OpenRPG Forge doit permettre à une personne non spécialiste des moteurs de jeu de créer un RPG 3D moderne comme on créait un module dans Aurora :

- construire des zones intérieures et extérieures ;
- placer des décors, portes, créatures, objets, déclencheurs et rencontres ;
- créer des PNJ, factions, inventaires et tables de butin ;
- créer des dialogues à embranchements ;
- créer des quêtes et états de journal ;
- définir sorts, pouvoirs, effets, classes et règles originales ;
- écrire des scripts proches du C sans recompiler le moteur ;
- lancer immédiatement le module depuis l’éditeur ;
- exporter un module autonome ;
- charger plusieurs modules dans un exécutable joueur ;
- jouer seul, en coopération et, plus tard, sur des mondes persistants ;
- administrer ou mettre en scène une partie avec des outils de maître de jeu à terme.

La différence majeure avec les anciens toolsets est l’intégration native de l’IA :

- génération assistée de plans de zones ;
- création de dialogues, quêtes, descriptions et variantes ;
- génération et correction de scripts ;
- orchestration de Blender pour produire ou adapter des assets ;
- validation des références et cohérence du lore ;
- tests automatisés du module ;
- analyse d’équilibrage et de performances.

L’IA reste facultative. Un créateur doit pouvoir tout faire manuellement et ouvrir un module des années plus tard sans dépendre d’un fournisseur.

---

## 2. Contraintes non négociables

### 2.1 Liberté

Le moteur, l’éditeur et les outils obligatoires doivent être utilisables sans abonnement, royalties ni licence propriétaire imposée. Les dépendances doivent avoir des licences compatibles avec un usage commercial et la redistribution du toolset.

Blender est utilisé comme processus externe libre. Les modèles IA sont des providers optionnels : aucun provider ne doit devenir une dépendance structurelle.

### 2.2 Plafond graphique

Le produit ne doit pas être condamné à un rendu rétro ou amateur. La cible est un RPG 3D AA moderne, en caméra 3/4 rotative et éventuellement troisième personne, avec :

- matériaux PBR ;
- éclairage indirect adapté aux zones créées par les utilisateurs ;
- lumière dynamique ;
- brouillard volumétrique ;
- decals ;
- réflexion et ambiance ;
- HDR ;
- mise à l’échelle temporelle ;
- LOD et occlusion ;
- animation, VFX et audio modernes.

La cible n’est pas de reproduire immédiatement Nanite et Lumen. La qualité des modèles, textures, animations, composition et éclairage est considérée comme la première limite artistique.

### 2.3 Modding profond

Les créateurs doivent pouvoir définir des comportements sans toucher au moteur. Les scripts sont écrits en **ForgeScript**, une couche fondée sur AngelScript avec une syntaxe C/C++ familière.

Les modules communautaires ne peuvent pas charger de code natif, DLL, assembly .NET ou script Godot arbitraire.

### 2.4 IA contrôlée

Toute modification IA doit être :

- contextualisée ;
- validée ;
- prévisualisable ;
- transactionnelle ;
- annulable ;
- journalisée ;
- testable.

### 2.5 Vibe coding maîtrisé

Le projet est principalement construit avec Codex et Hermes Agent. L’architecture doit donc minimiser :

- le nombre de langages ;
- les temps de compilation ;
- les dépendances natives ;
- les API implicites ;
- les refactorings transversaux ;
- les opérations manuelles non testables.

---

## 3. Résultat de la comparaison des moteurs

### 3.1 Décision

Le meilleur compromis libre pour ce projet est :

> **Godot 4.7.1 .NET + C# au centre, AngelScript via une petite GDExtension C++, Python pour Blender et les workers IA.**

La recommandation initiale Rust/Bevy a été abandonnée. Rust est puissant, mais Bevy ne fournit pas encore le niveau d’éditeur intégré nécessaire. Construire simultanément un moteur, un éditeur 3D, un toolset RPG et un runtime de modules serait une mauvaise allocation d’effort.

### 3.2 Pourquoi Godot

Godot fournit déjà les éléments coûteux :

- éditeur 3D ;
- scene tree ;
- inspecteur ;
- gizmos ;
- import d’assets ;
- animation ;
- physique ;
- navigation ;
- interface d’éditeur extensible ;
- profils permettant de masquer les fonctions génériques ;
- runtime client et headless ;
- rendu Forward+ moderne ;
- licence MIT.

Le projet commence comme plugin d’éditeur. Il réutilise le viewport et les outils 3D natifs. Les écrans spécifiques — dialogues, quêtes, scripts, IA, validation — sont ajoutés sous forme de main screens et de docks.

Aucun fork de Godot n’est autorisé avant qu’une tranche verticale complète ne prouve les limites réelles du plugin.

### 3.3 Pourquoi C# plutôt que Rust

C# devient le langage principal car il apporte :

- intégration officielle dans Godot .NET ;
- compilation et diagnostic simples pour les agents ;
- très bon outillage ;
- bibliothèques de tests matures ;
- sérialisation, ASP.NET et SDK MCP officiels ;
- partage direct des modèles de domaine entre éditeur, runtime, serveur et MCP ;
- réduction du nombre de frontières FFI ;
- possibilité de créer des bibliothèques pures indépendantes de Godot.

Rust reste une option future pour un composant isolé démontrant un besoin de sécurité ou performance, jamais par préférence abstraite.

### 3.4 Alternatives

#### Unreal Engine 5.8

C’est le raccourci technique le plus fort : rendu supérieur, outils de monde, multijoueur, écosystème, AngelScript Hazelight et MCP expérimental intégré. Il est néanmoins exclu de la décision principale parce qu’il n’est pas un moteur libre au sens du projet et impose une EULA ainsi que des conditions commerciales.

Il sert de benchmark pour :

- les sandboxes IA ;
- l’exposition MCP des assets et niveaux ;
- les workflows de création de monde ;
- la qualité de prévisualisation ;
- l’expérience de scripting.

#### O3DE

O3DE possède un plafond graphique et réseau élevé, un éditeur extensible, Atom, Lua/Script Canvas et des serveurs dédiés. Il est libre. Il est cependant très lourd, fortement C++/CMake/Qt, plus lent à compiler et plus difficile à maintenir par vibe coding. Il devient un plan B uniquement si une équipe C++ expérimentée est disponible et si le plafond graphique prime sur la vitesse de livraison.

#### Stride

Stride est une alternative sérieuse : MIT, C#/.NET, éditeur et rendu personnalisable. Il est proche de l’idéal linguistique. Ses faiblesses sont un éditeur actuellement Windows, une extensibilité de toolset moins mature/documentée que Godot et un écosystème plus petit. Il doit être réévalué si Godot empêche réellement une fonctionnalité essentielle après un prototype, pas sur hypothèse.

#### ezEngine

ezEngine est conceptuellement très proche d’Aurora : éditeur, plugins, AngelScript intégré, compilation rapide. Il est pénalisé par l’absence de système multijoueur, un niveau supérieur Windows-only, une équipe réduite et une base C++ plus difficile pour les agents. Il constitue une bonne source d’inspiration pour ForgeScript.

#### Bevy

Très bon framework Rust et renderer en progrès, mais le projet devrait construire son éditeur. Non retenu.

#### Fyrox

Éditeur intégré et Rust, mais plafond graphique et écosystème moins rassurants pour l’objectif. Non retenu.

#### Wicked Engine

Renderer moderne et libre, mais plus proche d’un framework graphique avec éditeur que d’une plateforme de création RPG complète. Trop de toolset à construire.

---

## 4. Pile technique de référence

```text
Moteur / éditeur                 Godot 4.7.1 .NET
Langage principal                C# / .NET
Version .NET cible               net10.0 à vérifier avec le SDK installé
Niveau C# prudent                C# 12 tant que la matrice Godot n’est pas validée
Domaine métier                   Bibliothèques C# pures
Interface toolset                Plugin Godot C#
Runtime joueur                   Godot exporté
Serveur initial                  Godot headless autoritaire
Scripts des modules              ForgeScript / AngelScript
Pont AngelScript                 C++ GDExtension minimale
MCP Host                         C# + SDK MCP officiel
IPC éditeur/MCP                  Loopback authentifié ou pipe local
Création 3D                      Blender + Python
Assets 3D                        glTF/GLB
Données                          JSON versionné + schémas
Module communautaire             Archive .orpmod
Tests C#                         xUnit ou NUnit
Tests intégration                Godot headless
CI                               GitHub Actions ou équivalent
```

La version de .NET doit être épinglée avec `global.json` après vérification locale. Le premier bootstrap ne doit pas inventer une combinaison qui ne compile pas.

---

## 5. Architecture générale

```text
                        ┌──────────────────────────┐
                        │ Codex / Hermes / LLM     │
                        └─────────────┬────────────┘
                                      │ MCP
                         ┌────────────▼────────────┐
                         │ Forge.McpHost (C#)      │
                         │ outils + ressources     │
                         └─────────────┬────────────┘
                                      │ RPC local authentifié
┌─────────────────────────────────────▼─────────────────────────────────────┐
│ Godot Editor + Forge.Editor plugin                                      │
│ World | Areas | Blueprints | Dialogues | Quests | Scripts | AI | Tests  │
└─────────────────────────────────────┬─────────────────────────────────────┘
                                      │ Command Bus
                         ┌────────────▼────────────┐
                         │ Forge.Application       │
                         │ commandes / validations │
                         └─────────────┬────────────┘
                                      │
                         ┌────────────▼────────────┐
                         │ Forge.Domain            │
                         │ modèle RPG pur          │
                         └───────┬─────────┬────────┘
                                 │         │
              ┌──────────────────▼─┐     ┌─▼────────────────────┐
              │ Persistence/Format │     │ Runtime adapters      │
              │ JSON / .orpmod     │     │ Godot client/server   │
              └────────────────────┘     └──────────┬────────────┘
                                                    │
                              ┌─────────────────────▼─────────────────────┐
                              │ ForgeScript Host / AngelScript GDExtension│
                              └────────────────────────────────────────────┘

Blender Worker (Python) reste un processus séparé appelé par le MCP Host ou l’éditeur.
```

---

## 6. Règle architecturale centrale : le domaine ne dépend pas de Godot

La source de vérité d’un module n’est pas une collection de scènes `.tscn`. C’est un modèle de domaine versionné.

Godot est un adapter de visualisation et d’exécution. Une zone doit pouvoir être :

- chargée depuis des données ;
- rendue dans l’éditeur ;
- reconstruite en runtime ;
- validée sans lancer Godot ;
- inspectée par MCP ;
- migrée vers une nouvelle version ;
- testée en headless.

Les couches `Forge.Domain` et `Forge.Application` n’importent aucune classe Godot.

### 6.1 Types de base

- `ProjectId`
- `ModuleId`
- `AreaId`
- `EntityId`
- `BlueprintId`
- `DialogueId`
- `QuestId`
- `ScriptId`
- `AssetId`
- `RevisionId`
- `Vector3Value`
- `QuaternionValue`
- `TransformValue`
- `LocalizedTextKey`
- `ContentHash`

Les identifiants sont opaques et stables. Les références par nom lisible sont interdites pour les liens persistants.

### 6.2 Définitions et instances

Une définition décrit un type réutilisable. Une instance est placée dans une zone.

Exemple :

```text
CreatureBlueprint "Ash Warden"
    stats
    appearance
    faction
    inventory
    script bindings

EntityInstance
    blueprint_id = Ash Warden
    area_id = Crypt
    transform
    local variable overrides
```

Cette séparation permet :

- le remplacement d’apparence ;
- les variantes ;
- la modification globale d’un blueprint ;
- les références stables ;
- l’IA qui raisonne sur des entités métier, pas sur des nœuds Godot.

### 6.3 Agrégats principaux

- `ProjectDefinition`
- `ModuleDefinition`
- `AreaDefinition`
- `LayerDefinition`
- `EntityInstance`
- `BlueprintDefinition`
- `CreatureBlueprint`
- `PlaceableBlueprint`
- `ItemBlueprint`
- `DoorBlueprint`
- `TriggerDefinition`
- `EncounterDefinition`
- `DialogueGraph`
- `QuestDefinition`
- `AbilityDefinition`
- `EffectDefinition`
- `FactionDefinition`
- `LootTableDefinition`
- `VariableDefinition`
- `ScriptBinding`
- `AssetDescriptor`
- `LocalizationCatalog`
- `TestScenario`

---

## 7. Bus de commandes et transactions

Toutes les modifications passent par `Forge.Application`.

Exemple conceptuel :

```csharp
public sealed record PlaceEntityCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    AreaId AreaId,
    BlueprintId BlueprintId,
    TransformValue Transform,
    bool DryRun);
```

Le résultat doit contenir :

```csharp
public sealed record CommandResult(
    bool Succeeded,
    ProjectRevision BaseRevision,
    ProjectRevision? NewRevision,
    IReadOnlyList<Diagnostic> Diagnostics,
    ProjectPatch? Patch,
    InversePatch? Inverse,
    AuditEntry Audit);
```

### 7.1 Raisons

- Undo/Redo unifié ;
- sécurité IA ;
- conflits de révision détectables ;
- tests unitaires ;
- historique ;
- collaboration future ;
- possibilité de rejouer ou analyser les changements.

### 7.2 Flux IA

```text
Lecture du contexte
→ proposition structurée
→ création d’un plan
→ validation de schéma et références
→ dry-run
→ diff visuel
→ approbation
→ application transactionnelle
→ validation du projet
→ test ciblé
→ commit dans l’historique
```

Un agent ne reçoit pas un outil « write_file » pour le contenu du module. Il reçoit des outils métier.

---

## 8. Toolset Godot

### 8.1 Stratégie

Le toolset commence comme addon C# dans Godot standard.

Réutiliser :

- viewport 3D ;
- sélection ;
- gizmos ;
- scene tree quand utile ;
- inspecteur ;
- asset browser ;
- Undo/Redo ;
- navigation ;
- animation ;
- import glTF ;
- play in editor.

Ajouter :

- dock de palette de blueprints ;
- placement avec grille et snapping ;
- peinture de kits modulaires ;
- validation ;
- bouton « Play from here » ;
- écrans principaux de dialogues, quêtes, scripts et IA ;
- export `.orpmod`.

### 8.2 Écrans visés

```text
World
Areas
Blueprints
Dialogues
Quests
Scripts
Assets
AI
Validation
Playtests
```

### 8.3 Construction de zones

Le MVP privilégie les kits modulaires plutôt qu’un terrain complexe :

- sols ;
- murs ;
- angles ;
- portes ;
- piliers ;
- escaliers ;
- plafonds ;
- variantes décoratives.

Fonctions :

- grille paramétrable ;
- alignement et rotation ;
- auto-connexion ;
- duplication ;
- placement par brosse ;
- suppression par brosse ;
- calques ;
- tags ;
- préfabriqués ;
- collision ;
- navmesh ;
- éclairage ;
- volumes audio ;
- transitions.

Le terrain libre, les routes, rivières et grandes zones extérieures arrivent après la tranche verticale.

### 8.4 Distribution future

Après stabilisation :

- Godot en mode self-contained ;
- addon préinstallé ;
- profil de fonctionnalités masquant les éléments inutiles ;
- thème et branding ;
- lanceur propre ;
- export templates contrôlés.

Un fork n’est envisagé que si des limites précises, reproduites et documentées rendent cette distribution impossible.

---

## 9. Rendu et direction graphique

### 9.1 Renderer

Utiliser Forward+.

Pour les zones créées par les utilisateurs :

- SDFGI ou VoxelGI selon le profil ;
- lumière dynamique ;
- reflection probes ;
- SSIL/SSR si adaptés ;
- brouillard volumétrique ;
- decals ;
- TAA/FSR2 ;
- HDR configurable ;
- ombres avec niveaux de qualité.

Pour les zones de campagne maîtrisées :

- option de lightmap bake ;
- éclairage hybride ;
- variantes jour/nuit préparées ;
- budgets plus stricts.

### 9.2 Stratégie artistique

La qualité cible dépend d’une bibliothèque cohérente :

- échelle métrique ;
- texel density ;
- conventions PBR ;
- palettes ;
- silhouettes ;
- modularité ;
- LOD ;
- collisions ;
- points d’accroche ;
- animations ;
- VFX ;
- audio.

Le projet doit créer très tôt une scène benchmark représentative. Les budgets ne doivent pas être inventés avant mesure.

### 9.3 Ce qui n’est pas promis

- équivalent immédiat de Nanite ;
- Lumen complet ;
- photoréalisme AAA ;
- monde ouvert continu ;
- destruction globale ;
- ray tracing obligatoire.

### 9.4 Chantier Asset Vault et carte de référence

Le backlog post-MVP comprend un **Asset Vault** libre, traçable et extensible, ainsi qu’une carte
extérieure de référence nommée **Forgotten Forest**. La spécification complète est conservée dans
`docs/assets/OPENRPG_FORGE_BIBLIOTHEQUE_ASSETS_ET_MAP_REELLE.md`.

Ce chantier doit fournir progressivement :

- un catalogue local recherchable, des packs installables à la demande et une provenance juridique
  vérifiable ;
- une chaîne d’acquisition, validation, normalisation GLB, optimisation, prévisualisation,
  déduplication et génération de crédits ;
- une interface Assets intégrée au toolset, compatible avec le placement, le glisser-déposer et les
  futures brosses de dispersion ;
- une carte extérieure cohérente servant de démonstration, benchmark, exemple créateur et test du
  pipeline ;
- des commandes applicatives et, plus tard, MCP qui ne contournent ni le bus transactionnel ni les
  contrôles de licence.

Le noyau artistique redistribué avec Forge vise CC0. Les autres licences et dépendances restent
soumises à `docs/ASSET_LICENSES.md`, `docs/ASSET_CONVENTIONS.md`, aux ADR et aux règles de
`AGENTS.md`. En cas de conflit, ces sources de vérité du dépôt priment sur la spécification importée.
L’implémentation commence par l’audit demandé dans la spécification, réutilise les systèmes existants
et privilégie une petite sélection visuellement cohérente avant l’élargissement du catalogue.

La capture `docs/assets/screenshots/forge-forest-map-demo.png` prouve le scatter cellulaire et le
masque de peinture du prototype 128 × 128 m. Elle constitue un état fonctionnel intermédiaire, pas
la référence artistique finale : relief, matériaux normalisés, composition, navigation, eau,
éclairage et profils graphiques restent à construire et à valider.

Le relief extérieur doit reposer sur une **fondation de terrain générique et indépendante des
biomes**. Une zone possède d'abord son terrain ; forêt, prairie, désert, marais, neige ou autre biome
sont ensuite des couches et recettes réutilisables posées sur ce terrain. Dans les conventions
Godot/Forge, X/Z forment le plan horizontal et Y porte l'altitude. L'interface peut parler de
« hauteur » sans exposer cette convention au créateur.

La source de vérité du terrain appartient au domaine C# versionné : dimensions, découpage,
résolution, seed, données d'altitude, matériaux, masques de surface et trous. Godot reconstruit la
géométrie, les collisions, la navigation et les instances de biome par un adapter ; les nœuds
Terrain3D, meshes et MultiMesh générés restent jetables. Les biomes échantillonnent notamment
altitude, pente, matériau, eau, chemins et zones d'exclusion sans posséder ni dupliquer le terrain.

Un champ d'altitude couvre montagnes, collines, vallées et dépressions. Les grottes, tunnels,
surplombs et autres volumes impossibles à représenter avec une seule hauteur par coordonnée X/Z
emploient un trou de terrain et une géométrie 3D séparée. Sculpture, peinture et découpe doivent
passer par des commandes transactionnelles, annulables, validables et testables hors de Godot.

### 9.5 État actuel de l’expérience Creator

Le MVP solo est fermé et le gate **Creator Core G7** est ouvert. Les incréments G7-03 à G7-17 sont
livrés avec preuves automatisées et revues visuelles ciblées :

- le workspace **Blueprints** recherche par nom, slug ou type, crée et édite une définition par
  commandes transactionnelles, dérive un blueprint depuis une instance sans perdre ses overrides
  et recalcule les usages dans les entités et dialogues ;
- le workspace **Monde** accepte le glisser-déposer d’un blueprint sur le sol, projette et snappe le
  placement, refuse limites et cellules occupées avant mutation puis partage sélection et historique
  avec les autres surfaces ;
- le contexte graphique de l’interaction emploie un fantôme vert pour un placement valide, rouge
  avec diagnostic pour un refus, puis un gizmo Forge rouge/bleu pour la translation X/Z ;
- l’identité d’interface reste sombre, calme et lisible, avec accent braise et sans marque visible du
  moteur sous-jacent dans les surfaces contrôlées par Forge ; les champs numériques restent une
  alternative précise à la manipulation directe.
- la coque Creator relie neuf workspaces, la recherche globale, les palettes, la configuration du
  module, la caméra auteur, le build incrémental, la distribution, la navigation et les diagnostics ;
- l’espace **Assets** charge 605 entrées depuis le Vault externe avec UUID, hash, licence, provenance,
  recherche à facettes, aperçu local et prévisualisation d’import sans écriture ; les détails restent
  défilables à la taille nominale et l’action braise n’est active que pour un asset gouverné.
- l’espace **Assistant** extrait métadonnées, dialogues et quêtes dans un catalogue de localisation
  canonique, filtre par clé et langue, prévisualise replis et pseudo-localisation puis applique les
  traductions par commande transactionnelle annulable.
- le Monde édite l’échelle uniforme ou par axe avec valeurs précises, Ctrl + molette, contraintes,
  validation géométrique et commande atomique multi-entités.
- la navigation calcule des routes A* déterministes avec coûts, obstacles dynamiques, récupération,
  budget borné et invalidation incrémentale depuis ses surfaces canoniques.

Les contrats, critères et preuves sont détaillés dans `docs/aurora-parity/tasks/`,
`docs/UX_AURORA_2026.md` et `STATUS.md`. La prochaine tranche Creator est **G7-20 — productivité de
la coque Creator** pour les exigences Shell P1 restantes hors autosauvegarde.

---

## 10. ForgeScript

### 10.1 Objectif

ForgeScript donne aux créateurs une expérience proche d’un langage C simple, comme NWScript, mais avec :

- typage statique ;
- classes/structs contrôlées ;
- compilation rapide ;
- bytecode ;
- hot reload ;
- diagnostics ;
- autocomplétion ;
- debug ;
- API versionnée ;
- sandbox.

### 10.2 Intégration

AngelScript n’est pas enregistré comme langage natif de Godot dans le MVP. La GDExtension expose un service :

- `Compile`
- `LoadBytecode`
- `CreateContext`
- `InvokeEvent`
- `Suspend`
- `Abort`
- `InspectStack`
- `GetDiagnostics`

C# encapsule ce service dans `IForgeScriptRuntime`.

### 10.3 API de domaine

Exemple :

```cpp
void OnDamaged(EntityRef self, const DamageEvent &in event)
{
    if (HealthPercent(self) <= 0.50f && !GetBool(self, "phase_two"))
    {
        SetBool(self, "phase_two", true);
        Say(self, "La cendre se réveille.");
        CastAbility(self, AbilityId("ash_nova"));
        Spawn("ash_spirit", Position(self) + Vec3(2, 0, 0));
        Spawn("ash_spirit", Position(self) + Vec3(-2, 0, 0));
    }
}
```

Événements initiaux :

- `OnSpawn`
- `OnInteract`
- `OnEnterArea`
- `OnExitArea`
- `OnPerception`
- `OnEnterCombat`
- `OnDamaged`
- `OnDeath`
- `OnAbilityCast`
- `OnItemUsed`
- `OnDialogueStarted`
- `OnQuestChanged`
- `OnHeartbeat` limité

### 10.4 Sécurité

ForgeScript n’expose pas :

- fichiers ;
- réseau ;
- processus ;
- DLL ;
- threads ;
- API Godot ;
- mémoire brute ;
- reflection native ;
- horloge système non déterministe.

Le runtime impose :

- temps ou nombre d’instructions ;
- profondeur de pile ;
- mémoire ;
- nombre d’entités créées ;
- recursion d’événements ;
- quotas par tick.

Les scripts autoritaires tournent côté serveur. Les scripts visuels client sont séparés et ne peuvent modifier l’état de jeu autoritaire.

---

## 11. Format de module `.orpmod`

### 11.1 But

Ne pas exposer directement la structure du projet Godot aux créateurs et ne pas charger des PCK communautaires capables d’écraser les chemins du runtime.

Le MVP utilise une archive ZIP contrôlée avec extension `.orpmod`.

### 11.2 Structure

```text
manifest.json
content/
  project.json
  areas/*.json
  blueprints/*.json
  dialogues/*.json
  quests/*.json
  abilities/*.json
  tests/*.json
scripts/
  source/*.as
  bytecode/*.asbc
assets/
  models/*.glb
  textures/*
  audio/*
localization/
  fr.json
  en.json
provenance/
  assets.json
licenses/
  THIRD_PARTY_NOTICES.md
```

### 11.3 Manifest

```json
{
  "format_version": "0.1.0",
  "module_id": "uuid",
  "slug": "crypt-of-ashes",
  "title": "Crypte des Cendres",
  "module_version": "0.1.0",
  "minimum_runtime": "0.1.0",
  "forge_api": "1",
  "entry_area_id": "uuid",
  "dependencies": [],
  "content_hash": "sha256:...",
  "created_with": "OpenRPG Forge"
}
```

### 11.4 Sécurité

Validation avant extraction :

- chemin normalisé ;
- aucune traversée ;
- nombre de fichiers limité ;
- taille décompressée limitée ;
- ratio de compression limité ;
- extensions autorisées ;
- hashes ;
- schémas ;
- IDs uniques ;
- dépendances résolues ;
- aucune DLL/assembly/binaire natif ;
- aucun script Godot ;
- isolation de namespace.

Le runtime peut monter l’archive dans un VFS propre au module ou l’extraire dans un cache content-addressed.

---

## 12. Multijoueur

### 12.1 Principe

Le produit final vise le jeu solo, la coopération et des mondes persistants, mais le multijoueur ne doit pas bloquer le MVP.

Le modèle est autoritaire :

- le serveur possède l’état ;
- les scripts de règles s’exécutent sur le serveur ;
- les clients envoient des intentions ;
- le serveur valide et réplique ;
- les clients ne décident ni dégâts, ni butin, ni états de quête.

### 12.2 Premier serveur

Godot headless est utilisé afin de :

- partager les adapters ;
- éviter un second moteur de simulation ;
- ne pas dépendre du protocole réseau interne depuis un serveur Rust/C# externe ;
- livrer plus vite la tranche verticale.

Les services périphériques — comptes, persistance, liste de serveurs, télémétrie — pourront être séparés plus tard.

### 12.3 Préparation sans surconstruction

Dès le début :

- le domaine n’accède pas directement à l’input ;
- les commandes joueur sont explicites ;
- la notion d’autorité existe ;
- RNG et horloge sont injectables ;
- les scripts reçoivent un contexte serveur ;
- les sauvegardes sont versionnées.

Ne pas rechercher un déterminisme lockstep strict pour le MVP.

---

## 13. IA et MCP

### 13.1 Architecture

```text
Codex / Hermes / autre client MCP
            │
            ▼
Forge.McpHost (processus C# séparé)
            │
            ▼
Forge.EditorBridge (plugin Godot)
            │
            ▼
Forge.Application Command Bus
```

Le serveur MCP expose des ressources URI et des outils de haut niveau. Il ne possède pas de voie d’écriture directe vers les fichiers.

### 13.2 Ressources

- `forge://project/summary`
- `forge://project/revision`
- `forge://selection/current`
- `forge://world/lore`
- `forge://world/factions`
- `forge://module/{id}`
- `forge://area/{id}`
- `forge://dialogue/{id}`
- `forge://quest/{id}`
- `forge://script-api/1`
- `forge://diagnostics/current`
- `forge://audit/recent`
- `forge://tests/catalog`

### 13.3 Outils initiaux

Lecture :

- `forge_get_project_summary`
- `forge_get_selection`
- `forge_search_content`
- `forge_get_area`
- `forge_validate_project`
- `forge_list_diagnostics`

Planification :

- `forge_plan_area`
- `forge_plan_dialogue`
- `forge_plan_quest`
- `forge_plan_boss_encounter`

Application :

- `forge_apply_plan`
- `forge_revert_transaction`

Scripts :

- `forge_compile_script`
- `forge_run_script_tests`

Tests :

- `forge_run_headless_scenario`
- `forge_launch_playtest`
- `forge_collect_performance_report`

3D, après stabilisation :

- `forge_request_asset_draft`
- `forge_validate_asset`
- `forge_generate_lods`
- `forge_export_glb`

### 13.4 Niveaux d’autorisation

1. Lecture seule.
2. Proposer un plan.
3. Appliquer après approbation.
4. Autonomie dans sandbox temporaire.

Les mutations sont `dry_run` par défaut et utilisent la révision optimiste.

### 13.5 Providers IA

Prévoir des interfaces :

- `IChatModelProvider`
- `IImageGenerationProvider`
- `IModel3DProvider`
- `IVoiceProvider`
- `IEmbeddingProvider`

Les providers peuvent viser :

- serveur local compatible OpenAI ;
- LM Studio ;
- vLLM ;
- API distante ;
- aucun provider.

Le cœur ne dépend pas du nom d’un modèle.

---

## 14. Blender et création 3D

### 14.1 Rôle de Blender

Blender est le DCC et le worker de transformation :

- génération procédurale ;
- kitbash ;
- modificateurs ;
- nettoyage ;
- UV ;
- matériaux ;
- LOD ;
- collision ;
- rigging ;
- validation ;
- export GLB.

Les commandes sont exécutées en mode headless par scripts Python déterministes.

### 14.2 Pipeline d’asset assisté

```text
Brief structuré
→ concept ou référence
→ mesh brouillon
→ import Blender
→ contrôle d’échelle
→ nettoyage / retopologie
→ UV / PBR
→ LOD
→ collision
→ points d’accroche
→ validation
→ prévisualisation
→ approbation
→ export GLB
→ enregistrement de provenance
```

Un mesh IA brut n’entre jamais automatiquement dans un module publié.

### 14.3 Modèles optionnels

Des providers MIT tels que TRELLIS.2 ou TripoSR peuvent être expérimentés, mais ils restent remplaçables. Les licences des poids, dépendances, datasets et sorties doivent être revérifiées pour chaque version.

---

## 15. Cohérence narrative et contenu

Le contexte de monde est structuré, pas seulement vectorisé.

Objets :

- lore ;
- lieux ;
- personnages ;
- factions ;
- chronologie ;
- relations ;
- contraintes ;
- secrets ;
- tonalité ;
- lexique ;
- règles de canon.

L’IA produit un plan conforme à un schéma, puis le système valide :

- références absentes ;
- noms dupliqués ;
- branches de dialogue mortes ;
- conditions impossibles ;
- quête sans résolution ;
- boucle involontaire ;
- variable jamais définie ;
- conflit avec chronologie ;
- ligne non localisée.

Le RAG vient après la stabilité du modèle. Il ne remplace pas les IDs et les relations structurées.

---

## 16. Tests

### 16.1 Unitaires

- invariants du domaine ;
- commandes ;
- calculs de règles ;
- transitions de quête ;
- graphes de dialogue ;
- migrations ;
- sérialisation ;
- sécurité de chemins ;
- quotas de scripts.

### 16.2 Contrats

- golden files JSON ;
- compatibilité de version ;
- compilation ForgeScript ;
- round-trip `.orpmod` ;
- hash ;
- dépendances.

### 16.3 Intégration Godot

- reconstruction d’une zone ;
- placement ;
- sélection ;
- sauvegarde ;
- play in editor ;
- chargement runtime ;
- navmesh ;
- headless.

### 16.4 Scénarios

Un `TestScenario` décrit :

- personnage ;
- état initial ;
- zone ;
- séquence d’intentions ;
- assertions.

Exemples :

- parler au PNJ ouvre la bonne branche ;
- réussir une condition avance la quête ;
- tuer le boss déverrouille la porte ;
- le boss entre en phase 2 sous 50 % de vie ;
- le module se termine sans référence manquante.

### 16.5 Performance

Créer une scène benchmark tôt et suivre :

- temps de chargement ;
- frame time CPU/GPU ;
- mémoire ;
- draw calls ;
- triangles visibles ;
- temps de navigation bake ;
- temps de compilation scripts ;
- latence d’ouverture éditeur.

Ne fixer des budgets qu’après mesures.

---

## 17. Sécurité

Menaces :

- zip bomb ;
- path traversal ;
- asset malformé ;
- shader ou ressource détournée ;
- script infini ;
- récursion d’événements ;
- création massive d’entités ;
- bytecode incompatible ;
- remplacement de ressource runtime ;
- prompt injection dans contenu ;
- agent autorisé à appeler un outil destructif ;
- fuite de secrets provider.

Mesures :

- processus séparés ;
- validation ;
- quotas ;
- allowlists ;
- aucune extension native communautaire ;
- jeton de session local pour l’éditeur ;
- permissions MCP ;
- audit ;
- sandbox ;
- secrets hors dépôt ;
- provenance ;
- signatures optionnelles.

---

## 18. Structure cible du dépôt

```text
OpenRPGForge/
├── AGENTS.md
├── MEGACONTEXT.md
├── STATUS.md
├── TASKS_MVP.md
├── README.md
├── global.json
├── Directory.Build.props
├── OpenRPGForge.sln
├── apps/
│   ├── Forge.Toolset/              # projet Godot .NET + addon éditeur
│   └── Forge.Runtime/              # projet Godot client/headless
├── src/
│   ├── Forge.Domain/
│   ├── Forge.Application/
│   ├── Forge.ModuleFormat/
│   ├── Forge.Persistence/
│   ├── Forge.GodotAdapter/
│   ├── Forge.Scripting.Abstractions/
│   ├── Forge.McpHost/
│   └── Forge.EditorBridge/
├── native/
│   └── ForgeScript.GDExtension/
├── tools/
│   └── blender_worker/
├── tests/
│   ├── Forge.Domain.Tests/
│   ├── Forge.Application.Tests/
│   ├── Forge.ModuleFormat.Tests/
│   ├── Forge.Scripting.Tests/
│   └── integration/
├── samples/
│   └── crypt-of-ashes/
├── schemas/
├── docs/
│   └── ADR/
└── third_party/
    └── notices/
```

Le bootstrap peut réduire cette structure. Ne pas créer dix projets vides sans besoin immédiat. Commencer par Domain, Application, ModuleFormat, Toolset et tests.

---

## 19. Tranche verticale : Crypte des Cendres

### 19.1 Contenu

- zone modulaire approximativement 20 × 20 cases ;
- entrée ;
- salle du PNJ ;
- porte verrouillée ;
- salle de combat ;
- salle de boss ;
- joueur ;
- PNJ ;
- ennemi standard ;
- boss Gardien des Cendres ;
- objet clé ;
- pouvoir Nova de Cendre ;
- dialogue avec condition ;
- quête à trois états ;
- trigger de transition ;
- butin final.

### 19.2 Script du boss

- phase 1 normale ;
- à 50 %, invoque deux esprits ;
- lance une nova ;
- change de résistance ;
- à sa mort, déverrouille la porte et complète la quête.

### 19.3 Critères de réussite

- éditer la zone ;
- enregistrer ;
- fermer/réouvrir ;
- jouer depuis l’éditeur ;
- exporter `.orpmod` ;
- charger dans Runtime ;
- compléter la quête ;
- sauvegarder/reprendre ;
- compiler et exécuter le script ;
- headless test vert ;
- demander à une IA de proposer une variante de dialogue ;
- prévisualiser le diff ;
- appliquer ;
- annuler.

---

## 20. Phasage

### Phase 0 — Fondations

- dépôt ;
- versions ;
- solution ;
- bibliothèques pures ;
- tests ;
- plugin Godot minimal ;
- CI ;
- ADR.

### Phase 1 — Monde minimal

- modèle Area/Blueprint/Entity ;
- sérialisation ;
- reconstruction Godot ;
- placement ;
- save/load ;
- runtime séparé.

### Phase 2 — Gameplay vertical

- joueur ;
- interaction ;
- dialogue ;
- quête ;
- combat simple ;
- objet ;
- pouvoir ;
- sauvegarde.

### Phase 3 — ForgeScript

- spike AngelScript ;
- API minimale ;
- compilation ;
- hot reload ;
- quotas ;
- boss.

### Phase 4 — Module

- export ;
- validation ;
- runtime loader ;
- cache ;
- migrations ;
- provenance.

### Phase 5 — IA/MCP

- ressources read-only ;
- validation ;
- plan/dry-run ;
- apply/revert ;
- dialogue ;
- script ;
- tests.

### Phase 6 — 3D assistée

- Blender worker ;
- validation ;
- LOD/collision ;
- provider abstrait ;
- génération brouillon.

### Phase 6A — Asset Vault et Forgotten Forest

- audit de l’existant et plan d’intégration ;
- politique de licence et schéma de métadonnées ;
- catalogue local, stockage adressé par contenu et packs à la demande ;
- adaptateurs de sources, téléchargements contrôlés et normalisation ;
- interface Assets, placement et dispersion ;
- fondation de terrain générique, sculptable et indépendante des biomes ;
- carte Forgotten Forest jouable, profilée et exportable ;
- tests, crédits, documentation et reproduction depuis un clone propre.

La liste opérationnelle et les critères détaillés sont dans `TASKS_MVP.md` et
`docs/assets/OPENRPG_FORGE_BIBLIOTHEQUE_ASSETS_ET_MAP_REELLE.md`.

### Phase 7 — Multijoueur

- headless autoritaire ;
- intentions ;
- réplication ;
- co-op ;
- persistance ;
- outils maître de jeu.

Chaque phase doit laisser un produit exécutable et testable.

---

## 21. Workflow Codex et Hermes

### 21.1 Codex

Utiliser Codex pour :

- initialiser le dépôt ;
- implémenter une tâche bornée ;
- créer les tests ;
- corriger compilation/CI ;
- refactorer avec filet de tests ;
- revoir un diff ;
- maintenir les ADR.

Utiliser `AGENTS.md` comme règles persistantes. Mettre les détails lourds dans `docs/`. Utiliser des worktrees pour les tâches parallèles réellement indépendantes.

### 21.2 Hermes

Créer un profil dédié. Utiliser Hermes pour :

- mémoire du projet ;
- cohérence des décisions ;
- préparation du backlog ;
- documentation ;
- suivi du lore ;
- création de skills ;
- orchestration de workers ;
- délégation d’une tâche de code à Codex via MCP si configuré.

Hermes et Codex ne modifient pas la même branche simultanément.

### 21.3 Début de session

1. lire AGENTS ;
2. lire STATUS ;
3. lire la tâche ;
4. inspecter Git ;
5. exécuter les tests de base ;
6. proposer un plan limité ;
7. coder ;
8. tester ;
9. mettre à jour statut et tâches ;
10. résumer les preuves.

### 21.4 Fin de session

Le résumé doit contenir :

- fichiers modifiés ;
- comportement créé ;
- tests exécutés et résultat ;
- décisions prises ;
- limites ;
- prochaine tâche exacte ;
- état du dépôt.

---

## 22. Ce qu’il ne faut pas faire maintenant

- recréer le renderer ;
- créer un éditeur 3D complet indépendant ;
- forker Godot ;
- écrire un langage depuis zéro ;
- créer un serveur Rust ;
- intégrer dix modèles IA ;
- développer un monde ouvert ;
- copier le système D20 ;
- développer la marketplace ;
- prévoir toutes les classes RPG ;
- créer un visual scripting universel ;
- générer des milliers d’assets ;
- commencer par le multijoueur persistant ;
- laisser l’IA modifier les fichiers sans commande.

---

## 23. Mission courante à donner à Codex

> Lis `AGENTS.md`, `MEGACONTEXT.md`, `STATUS.md`, `TASKS_MVP.md`, le plan Aurora et les ADR
> applicables. Le bootstrap, le MVP et G7 sont validés : ne les recommence pas.
> Réalise **G8 CREATURE-02** pour `ORF-CRE-036`, `043`, `049`, `001` et `004` : combat, événements, validation et assistant.
> Conserve le modèle de
> domaine comme source de vérité, passe les mutations par les commandes, teste chaque couche touchée,
> régénère le graphe et mets le statut à jour uniquement avec les preuves obtenues.

---

## 24. Résumé décisionnel

Le projet est faisable à condition de ne pas confondre vision et premier lot.

Le socle retenu n’est pas « tout Rust ». Il est :

> **Godot 4.7.1 .NET + C# pour la majorité du produit, AngelScript pour les créateurs, C++ uniquement comme pont, Python pour Blender, MCP en C# et IA interchangeable.**

Le point le plus important n’est pas le choix du renderer. C’est la séparation entre :

- le modèle RPG ;
- sa représentation Godot ;
- les modules ;
- le langage de script ;
- le bus de commandes ;
- les outils IA.

Cette séparation permet de construire par petites tranches, de tester avec des agents, de changer de provider IA, de sécuriser les modules et de conserver un toolset libre.


---

# RÈGLES AGENT

# AGENTS.md — OpenRPG Forge

## Mission

Construire un moteur et un toolset de RPG 3D libre, moderne et orienté modules, inspiré par la promesse d’Aurora sans reprendre les marques, contenus, règles ou actifs de Neverwinter Nights/D&D.

Le produit doit rester utilisable sans IA, sans abonnement et sans outil propriétaire obligatoire.

## Sources de vérité

Avant toute modification importante, lire :

1. `MEGACONTEXT.md`
2. `STATUS.md`
3. `TASKS_MVP.md`
4. les ADR applicables dans `docs/ADR/`
5. la documentation spécialisée du sous-système concerné

Une décision enregistrée dans un ADR ne doit pas être renversée silencieusement. Proposer un nouvel ADR de remplacement.

## Pile imposée pour le MVP

- Godot **4.7.1 .NET** comme moteur, runtime et base d’éditeur.
- C#/.NET comme langage principal.
- Bibliothèques de domaine C# sans dépendance Godot.
- Plugin d’éditeur Godot, sans fork du moteur au départ.
- AngelScript encapsulé dans une GDExtension C++ minimale pour `ForgeScript`.
- Python uniquement pour Blender, traitement d’assets et workers IA.
- MCP Host en C# avec le SDK MCP officiel.
- JSON versionné pour les données ; GLB/glTF pour les assets 3D.
- Format de module propre `.orpmod` ; ne pas utiliser un PCK Godot non fiable comme format communautaire.
- Git et tests automatisés dès le premier lot.

Rust est autorisé uniquement après mesure ou besoin isolé clairement documenté. Ne pas introduire Rust dans le cœur du MVP.

## Architecture obligatoire

Les couches de domaine et d’application ne doivent jamais référencer Godot.

Flux autorisé :

`Editor/Runtime/MCP -> Application Commands -> Domain -> Persistence/Adapters`

L’IA, les plugins et les interfaces ne modifient jamais directement les fichiers du projet. Toute mutation passe par le bus de commandes transactionnel et produit :

- un identifiant de commande ;
- une révision de base ;
- une validation ;
- un diff prévisualisable ;
- une opération inverse ou un snapshot ;
- un journal d’audit ;
- un résultat de test si applicable.

## Discipline de développement

- Une tâche bornée par branche ou worktree.
- Aucun « grand refactoring » mêlé à une fonctionnalité.
- Avant de coder, identifier les fichiers touchés et les critères d’acceptation.
- Après modification, exécuter les tests pertinents et rapporter exactement les résultats.
- Ne jamais annoncer qu’une fonction est terminée sans preuve de compilation ou de test.
- Ne jamais masquer une erreur avec un `try/catch` vide, une valeur par défaut silencieuse ou la désactivation d’un test.
- Ne pas ajouter de dépendance de production sans vérifier sa licence, sa maintenance et son utilité.
- Préférer les bibliothèques officielles ou les dépendances minimales.
- Les formats persistants et API publiques doivent être versionnés.
- Ajouter ou mettre à jour un ADR lorsqu’une décision structurelle est prise.
- Mettre `STATUS.md` et `TASKS_MVP.md` à jour à la fin d’une session ayant modifié l’état réel.

## Conventions C#

- Identifiants, namespaces et API en anglais.
- Nullable reference types activés.
- Warnings as errors pour le code propre au projet.
- Types du domaine petits et testables.
- Préférer les records immuables pour les commandes, événements et DTO.
- Un type public principal par fichier.
- Pas de singleton global pour le domaine.
- Pas de `Godot.Node`, `Vector3`, `Resource`, `Variant` ou signal Godot dans `Forge.Domain` et `Forge.Application`.
- Utiliser les types propres `Vector3Value`, `QuaternionValue`, `EntityId`, `AssetId`, etc., puis convertir dans les adapters.
- Toute sérialisation doit être explicitement versionnée et couverte par un test.
- Les horloges, UUID et hasard doivent être injectables dans les tests.
- Les règles autoritaires s’exécutent côté serveur.

## Conventions Godot

- Réutiliser le viewport 3D, l’inspecteur, les gizmos et l’Undo/Redo natifs avant de recréer un éditeur.
- Ajouter des écrans spécialisés uniquement pour les domaines qui en ont besoin : dialogues, quêtes, scripts, IA, validation.
- Les nœuds Godot sont des représentations, pas la source de vérité métier.
- Les scènes générées doivent pouvoir être reconstruites depuis le modèle de domaine.
- Tout code d’éditeur doit être isolé du runtime exporté.
- Le toolset doit fonctionner dans l’éditeur Godot standard avant toute tentative de fork ou de rebranding.

## ForgeScript

- ForgeScript est AngelScript avec une API RPG contrôlée.
- Ne pas exposer directement l’API Godot.
- Interdire fichiers, réseau, processus, threads, chargement natif et accès mémoire.
- Exposer des handles opaques et une API versionnée.
- Prévoir quotas CPU, profondeur d’appel, mémoire et récursion d’événements.
- Compiler et tester les scripts avant inclusion dans un module.
- Conserver source et bytecode avec versions de VM/API.
- Les scripts de gameplay autoritaires s’exécutent sur le serveur.

## IA et MCP

- L’IA ne doit pas écrire directement dans le dépôt ou les modules par l’intermédiaire des outils Forge.
- Les outils de lecture sont séparés des outils de mutation.
- Les mutations sont `dry_run` par défaut.
- Une application exige un `plan_id` lié à une révision de projet.
- Les outils doivent être de haut niveau : créer une zone, ajouter un dialogue, compiler un script, lancer un test.
- Éviter les outils génériques du type « exécuter n’importe quelle commande » dans le MCP livré aux créateurs.
- Commencer avec peu d’outils ; élargir seulement lorsqu’un workflow manuel réel est stabilisé.
- Les modèles IA et fournisseurs 3D doivent rester interchangeables.
- Tout asset généré conserve sa provenance et sa licence.

## Sécurité des modules

- Refuser les chemins absolus et les traversées `..`.
- Limiter tailles, nombre de fichiers et taux de décompression.
- Vérifier hash, type MIME réel et schéma.
- Isoler l’espace de noms de chaque module.
- Ne pas charger de DLL ou script natif provenant d’un module communautaire.
- Ne pas autoriser un module communautaire à remplacer les ressources du runtime.
- Utiliser PCK seulement pour du contenu de confiance contrôlé par l’équipe.

## Définition de terminé

Une tâche est terminée uniquement si :

- les critères d’acceptation sont satisfaits ;
- le code compile ;
- les tests pertinents passent ;
- les erreurs et limites connues sont documentées ;
- aucune dépendance non justifiée n’a été ajoutée ;
- le statut et la documentation sont cohérents ;
- le diff reste centré sur la tâche.

## Interdictions du MVP

Ne pas démarrer avant validation explicite :

- fork de Godot ;
- renderer personnalisé ;
- monde ouvert ou streaming massif ;
- serveur Rust séparé ;
- multijoueur public persistant ;
- marketplace ;
- génération 3D imposée par un fournisseur unique ;
- système de règles copié de D&D ;
- éditeur visuel universel remplaçant tout code ;
- réécriture générale pour « préparer le futur » sans besoin concret.


---

# ÉTAT INITIAL

> **Archive historique du paquet de démarrage.** La section ci-dessous décrit l’état du 1er août
> 2026 avant initialisation ; elle ne doit pas servir de gate courant. Utiliser `STATUS.md`, la
> section 9.5 de ce document et le plan Aurora pour l’état réel.

# État du projet — OpenRPG Forge

Mise à jour initiale : 1 août 2026.

## État réel

Le dépôt n’est pas encore initialisé. Aucun code de production n’est considéré comme existant.

Le présent paquet contient uniquement :

- l’analyse de choix technologique ;
- l’architecture de référence ;
- les décisions initiales ;
- le format cible du MVP ;
- le backlog de la tranche verticale ;
- les prompts pour Codex et Hermes.

## Décisions prises

- Godot 4.7.1 .NET est le moteur retenu.
- C# est le langage principal.
- Rust n’est pas dans le cœur du MVP.
- Le toolset commence comme plugin de l’éditeur Godot standard.
- Le domaine est indépendant de Godot.
- ForgeScript repose sur AngelScript via une GDExtension C++ minimale.
- Les modules communautaires utilisent un format propre `.orpmod`.
- L’IA passe par MCP et un bus de commandes transactionnel.
- Le MVP est la tranche verticale « Crypte des Cendres ».
- Le premier runtime serveur sera Godot headless, pas un serveur réécrit dans un autre langage.

## Décisions encore ouvertes

- Nom commercial définitif.
- Licence du code propre : recommandation provisoire `Apache-2.0` ou double licence `MIT OR Apache-2.0`.
- Charte graphique.
- Vue prioritaire du MVP : recommandation `3/4 isométrique avec caméra rotative`, tout en gardant une architecture compatible troisième personne.
- Niveau de règles RPG initial : recommandation originale et minimale, non dérivée de D&D.
- Cible matérielle minimale à mesurer sur la première scène benchmark.
- Stratégie exacte de navigation dynamique et de rebake partiel.
- Choix d’un langage serveur de persistance externe, seulement après le MVP solo.

## Risques principaux

1. Taille du projet et dérive de périmètre.
2. Construction d’un éditeur parallèle au lieu d’exploiter Godot.
3. Couplage prématuré aux nœuds/scènes Godot.
4. Fausse sécurité des scripts communautaires.
5. Qualité des assets 3D et animations, plus limitante que le renderer.
6. Agents qui effectuent des refactorings massifs sans tests.
7. Multijoueur introduit avant la stabilité du modèle de domaine.
8. IA qui produit des contenus incohérents ou non licenciables.
9. Formats persistants modifiés sans migration.
10. Fork moteur trop précoce.

## Prochaine action autorisée

Exécuter uniquement le **Prompt 1 — Initialisation contrôlée** de `PROMPTS_CODEX_HERMES.md`, puis vérifier manuellement :

- le contenu généré ;
- les versions réellement installées ;
- la compilation ;
- les tests ;
- l’ouverture du projet dans Godot 4.7.1 .NET.

Ne pas commencer AngelScript, MCP ou Blender avant que les fondations C# et le plugin minimal soient validés.


---

# BACKLOG MVP

> **Archive historique du backlog initial.** Le MVP est désormais fermé. Les cases ci-dessous
> conservent le plan d’origine ; l’avancement prouvé et le backlog actif sont dans `TASKS_MVP.md`.

# Backlog MVP — Crypte des Cendres

## Règles

- Exécuter dans l’ordre sauf dépendance clairement justifiée.
- Une tâche = un résultat testable.
- Ne pas ouvrir simultanément plusieurs grands chantiers.
- Chaque tâche doit préciser les tests.

## Lot 0 — Bootstrap

### T0.1 Vérifier l’environnement

- [ ] Godot 4.7.1 .NET installé.
- [ ] SDK .NET compatible détecté.
- [ ] `global.json` épingle une version présente.
- [ ] Git disponible.
- [ ] CMake/compilateur natif notés mais non obligatoires au premier build.
- [ ] Blender noté mais non obligatoire.

Acceptation : rapport de versions exact, sans installation automatique silencieuse.

### T0.2 Créer la solution minimale

- [ ] `Forge.Domain`
- [ ] `Forge.Application`
- [ ] `Forge.ModuleFormat`
- [ ] `Forge.Domain.Tests`
- [ ] solution
- [ ] `Directory.Build.props`
- [ ] nullable
- [ ] warnings as errors pour projets propres
- [ ] test trivial réel d’un value object

Acceptation : `dotnet test` vert.

### T0.3 Créer le projet Godot Toolset

- [ ] projet Godot .NET.
- [ ] référence aux bibliothèques.
- [ ] addon C# activable.
- [ ] main screen « Forge » vide.
- [ ] chargement sans erreur.

Acceptation : ouverture et capture/log de succès.

### T0.4 CI

- [ ] restore.
- [ ] build.
- [ ] test.
- [ ] validation format minimale.
- [ ] cache raisonnable.

## Lot 1 — Domaine monde

### T1.1 IDs et valeurs

- [ ] IDs opaques.
- [ ] Vector/Quaternion/Transform propres.
- [ ] JSON converters.
- [ ] equality.
- [ ] tests.

### T1.2 Project/Module/Area

- [ ] agrégats.
- [ ] invariants.
- [ ] create commands.
- [ ] serializers.
- [ ] round-trip tests.

### T1.3 Blueprint/EntityInstance

- [ ] définition.
- [ ] instance.
- [ ] transform.
- [ ] tags.
- [ ] overrides.
- [ ] validation de référence.

### T1.4 Révision et command bus

- [ ] base revision.
- [ ] command ID.
- [ ] handler.
- [ ] diagnostics.
- [ ] patch.
- [ ] inverse.
- [ ] conflit de révision test.

## Lot 2 — Editeur zone

### T2.1 Session projet

- [ ] ouvrir dossier Forge.
- [ ] manifest de travail.
- [ ] chargement.
- [ ] sauvegarde atomique.
- [ ] diagnostics.

### T2.2 Reconstruction Godot

- [ ] Area vers Node3D.
- [ ] EntityInstance vers représentation.
- [ ] conversion transform.
- [ ] destruction/rebuild propre.

### T2.3 Placement

- [ ] palette de blueprints.
- [ ] placer.
- [ ] déplacer.
- [ ] tourner.
- [ ] supprimer.
- [ ] grille.
- [ ] undo/redo via commands.

### T2.4 Kit Crypte

- [ ] assets libres/provisoires.
- [ ] sol.
- [ ] mur.
- [ ] porte.
- [ ] pilier.
- [ ] collision.
- [ ] conventions.

## Lot 3 — Runtime

### T3.1 Projet runtime

- [ ] charge snapshot.
- [ ] instancie area.
- [ ] caméra 3/4.
- [ ] joueur placeholder.
- [ ] déplacement.
- [ ] interaction.

### T3.2 Play from here

- [ ] sélection spawn.
- [ ] snapshot temporaire.
- [ ] lancement runtime.
- [ ] retour logs.

### T3.3 Headless

- [ ] démarre sans rendu.
- [ ] charge area.
- [ ] exécute un scénario.
- [ ] retourne code de sortie.

## Lot 4 — Dialogue et quête

### T4.1 Dialogue domain

- [ ] nodes.
- [ ] choices.
- [ ] conditions.
- [ ] actions.
- [ ] validation.
- [ ] parcours test.

### T4.2 Dialogue editor minimal

- [ ] liste ou graphe simple.
- [ ] ajout node.
- [ ] ajout choice.
- [ ] condition.
- [ ] localisation.
- [ ] preview.

### T4.3 Quest state machine

- [ ] states.
- [ ] transitions.
- [ ] journal.
- [ ] actions.
- [ ] invalid transition test.

### T4.4 PNJ Crypte

- [ ] dialogue.
- [ ] condition objet/variable.
- [ ] quête.
- [ ] porte.

## Lot 5 — Combat data-driven

### T5.1 Stats minimales

- [ ] health.
- [ ] resource.
- [ ] offense/defense simples.
- [ ] faction.
- [ ] team.

### T5.2 Ability

- [ ] cost.
- [ ] cooldown.
- [ ] targeting.
- [ ] effect.
- [ ] visual cue séparé.

### T5.3 Combat

- [ ] attack.
- [ ] damage.
- [ ] death.
- [ ] loot.
- [ ] test simulation.

## Lot 6 — ForgeScript spike

### T6.1 Native build

- [ ] AngelScript version épinglée.
- [ ] licence/notices.
- [ ] GDExtension compile.
- [ ] C# wrapper.
- [ ] smoke test.

### T6.2 Compiler

- [ ] source.
- [ ] diagnostics.
- [ ] bytecode.
- [ ] load.
- [ ] version metadata.

### T6.3 API minimale

- [ ] EntityRef.
- [ ] health.
- [ ] variable.
- [ ] say.
- [ ] cast.
- [ ] spawn.
- [ ] quest.
- [ ] unlock.

### T6.4 Quotas

- [ ] timeout.
- [ ] recursion.
- [ ] spawn cap.
- [ ] failure diagnostic.
- [ ] serveur continue.

### T6.5 Boss

- [ ] phase 2.
- [ ] deux esprits.
- [ ] nova.
- [ ] mort.
- [ ] quête.
- [ ] test.

## Lot 7 — Module

### T7.1 Manifest/schema

- [ ] schema.
- [ ] validator.
- [ ] tests invalides.

### T7.2 Packager

- [ ] dossier travail vers `.orpmod`.
- [ ] hash.
- [ ] atomic export.
- [ ] round-trip.

### T7.3 Loader

- [ ] validation archive.
- [ ] safe paths.
- [ ] cache.
- [ ] domain load.
- [ ] runtime.

### T7.4 Sécurité

- [ ] path traversal fixture.
- [ ] zip bomb ratio fixture.
- [ ] DLL refusée.
- [ ] duplicate IDs.
- [ ] hash mismatch.

## Lot 8 — IA/MCP minimal

### T8.1 MCP Host read-only

- [ ] SDK C# officiel.
- [ ] stdio.
- [ ] project summary.
- [ ] current revision.
- [ ] diagnostics.
- [ ] tests.

### T8.2 Editor bridge

- [ ] discovery.
- [ ] loopback/pipes.
- [ ] token session.
- [ ] reconnect.
- [ ] no external bind.

### T8.3 Dialogue plan

- [ ] schema.
- [ ] validation.
- [ ] dry-run.
- [ ] diff.
- [ ] apply.
- [ ] inverse.
- [ ] audit.

### T8.4 Script workflow

- [ ] get API.
- [ ] compile.
- [ ] diagnostics.
- [ ] sandbox test.
- [ ] bind plan.

### T8.5 Test agent

- [ ] lancer scénario.
- [ ] résultat structuré.
- [ ] diagnostics.
- [ ] artifact log.

## Lot 9 — Démonstration

- [ ] zone finale.
- [ ] PNJ.
- [ ] quête.
- [ ] boss.
- [ ] export.
- [ ] runtime.
- [ ] save/load.
- [ ] IA dialogue.
- [ ] undo.
- [ ] headless vert.
- [ ] rapport performance.
- [ ] licences assets.
- [ ] guide créateur minimal.


---

# PROMPTS CODEX ET HERMES

# Prompts de lancement

## Prompt 1 — Initialisation contrôlée avec Codex

```text
Tu initialises le projet OpenRPG Forge dans ce dépôt.

Avant toute modification, lis intégralement :
- AGENTS.md
- MEGACONTEXT.md
- STATUS.md
- TASKS_MVP.md
- docs/ADR/0001-godot-dotnet.md
- docs/ADR/0002-domain-model.md
- docs/ADR/0003-forgescript.md
- docs/ADR/0004-ai-command-bus.md
- docs/ADR/0005-custom-module-format.md

Périmètre strict de cette session :
1. inspecter l’environnement réellement disponible ;
2. produire un rapport court des versions Godot .NET, .NET SDK, Git et OS ;
3. créer la solution minimale du lot T0.2 ;
4. créer un projet Godot .NET minimal et un addon C# affichant un main screen nommé « Forge » ;
5. ajouter un test réel d’un type de domaine ;
6. ajouter les commandes de build/test dans le README ;
7. exécuter la compilation et les tests ;
8. mettre STATUS.md et TASKS_MVP.md à jour avec uniquement ce qui est prouvé.

Contraintes :
- n’installe rien silencieusement ;
- ne commence ni AngelScript, ni MCP, ni Blender, ni gameplay ;
- ne crée pas de dizaines de projets vides ;
- ne référence pas Godot depuis Forge.Domain ou Forge.Application ;
- n’invente pas une version de SDK : utilise une version installée et compatible ;
- garde un diff limité ;
- signale exactement toute étape non testable ;
- ne marque pas le plugin comme fonctionnel si Godot n’a pas été lancé ou validé.

Avant de coder, donne un plan des fichiers qui seront créés.
Après le travail, donne :
- fichiers modifiés ;
- commandes exécutées ;
- résultats ;
- erreurs restantes ;
- prochaine tâche unique recommandée.
```

## Prompt 2 — Première zone

```text
Lis AGENTS.md, STATUS.md, TASKS_MVP.md et l’architecture.

Implémente uniquement le prochain sous-lot nécessaire pour créer, sérialiser et recharger une AreaDefinition contenant une EntityInstance référant un BlueprintDefinition.

Exigences :
- domaine pur sans Godot ;
- IDs opaques ;
- invariants ;
- JSON versionné ;
- round-trip ;
- référence manquante détectée ;
- tests unitaires ;
- aucun éditeur 3D dans ce lot ;
- mise à jour du statut.

Propose d’abord les contrats C# et les tests, puis implémente.
```

## Prompt 3 — Plugin Godot et reconstruction

```text
Implémente la reconstruction en lecture seule d’une AreaDefinition dans le projet Godot Toolset.

Périmètre :
- adapter de TransformValue vers Godot.Transform3D ;
- registry de représentation placeholder ;
- création d’un Node3D par EntityInstance ;
- rebuild idempotent ;
- nettoyage sans fuite ;
- diagnostic si Blueprint absent ;
- test des conversions hors Godot quand possible ;
- scène de démonstration minimale.

Ne crée pas encore le placement interactif.
Ne déplace pas la source de vérité dans les Nodes.
```

## Prompt 4 — Audit de fin de session Codex

```text
Arrête toute nouvelle implémentation.

Inspecte le diff, exécute les tests pertinents et vérifie la cohérence avec AGENTS.md et les ADR.

Produis :
1. comportement réellement ajouté ;
2. preuves de compilation/test ;
3. risques ou dettes ;
4. fichiers de documentation à mettre à jour ;
5. prochaine tâche unique, bornée et dépendante du résultat actuel.

Corrige seulement les défauts directement liés au diff. Ne lance pas de refactoring général.
```

## Prompt Hermes — Profil architecte/orchestrateur

```text
Tu es l’agent architecte et coordinateur du projet OpenRPG Forge.

Charge AGENTS.md, MEGACONTEXT.md, STATUS.md, TASKS_MVP.md et les ADR. Ta mission principale n’est pas de produire de grands changements de code directement. Tu dois :
- préserver la cohérence des décisions ;
- préparer des tâches petites avec critères d’acceptation ;
- vérifier que le statut reflète les preuves ;
- maintenir le backlog ;
- repérer les dérives de périmètre ;
- proposer un ADR avant tout changement structurel ;
- conserver le contexte produit et le lore ;
- déléguer à Codex les implémentations complexes si Codex est disponible via MCP ;
- ne jamais laisser deux agents modifier la même branche.

Commence par résumer :
1. le gate actuel ;
2. les décisions verrouillées ;
3. la prochaine tâche unique ;
4. les fichiers qu’elle devrait toucher ;
5. les tests attendus.
```

## Prompt Hermes → Codex délégué

```text
Délègue à Codex uniquement la tâche suivante : [COLLER UNE TÂCHE BORNÉE].

Transmets à Codex :
- les fichiers de contexte applicables ;
- les critères d’acceptation ;
- les interdictions ;
- les commandes de test ;
- le fait qu’il travaille dans une branche/worktree dédiée.

À son retour, ne fusionne pas automatiquement. Examine le diff, les tests et les écarts avec les ADR, puis prépare un rapport de revue.
```

## Prompt de création d’un skill

```text
À partir du workflow validé durant cette session, crée ou mets à jour un skill nommé openrpg-forge-workflow.

Le skill doit rester procédural et court. Il doit :
- charger AGENTS.md et STATUS.md ;
- vérifier le gate courant ;
- imposer une tâche bornée ;
- exiger compilation/tests ;
- mettre à jour STATUS/TASKS ;
- interdire les changements d’architecture sans ADR ;
- ne pas inventer de commandes ou d’outils.
```

## Prompt de revue moteur

À utiliser seulement en cas de blocage prouvé :

```text
Nous avons un blocage mesuré avec Godot. Analyse le cas reproductible et compare uniquement ce cas avec Stride et O3DE.

Ne recommence pas une comparaison générale. Fournis :
- le blocage précis ;
- les tentatives ;
- les mesures ;
- le coût d’un workaround Godot ;
- le coût d’un spike Stride ;
- le coût d’un spike O3DE ;
- le risque de migration ;
- une recommandation ADR.

Aucun changement de moteur sans prototype comparable.
```
