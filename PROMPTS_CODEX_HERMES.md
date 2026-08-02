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
