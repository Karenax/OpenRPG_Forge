# G8 QUEST-01 — Objectifs, wizard, cast et récompenses

La première tranche G8 avait démontré ces contrats dans `Forge.RpgKit.Domain`, sans les rattacher au
document de quête principal. Elle ne constituait donc pas à elle seule la preuve d’acceptation Aurora.

`R071 / FC-QUEST-P1-01` relie désormais au modèle canonique `QuestDefinition` les huit types
d’objectifs, les récompenses atomiques, les notes et reviews, le cast et le Plot Wizard transparent.
Le même document passe par la commande `SetQuestHandler`, la persistance, ForgeScript, le MCP,
l’éditeur et des nœuds runtime réels.

Les participants déclarent rôle, blueprint/instance/query et conditions de présence. Le bundle de
récompense applique atomiquement progression, objets, monnaies, réputation, déverrouillages et choix,
avec rollback exact en cas d'invalidité. Notes de design, canon, propriétaire, statut, risques, TODO,
sources et historique de review restent canoniques.

Preuves de clôture : `P1-R071-QUEST-AUTHORING-ACCEPTANCE.md`, `QuestAuthoringProfileTests`,
`QuestJournalScriptServiceTests`, `QuestJournalPersistenceTests`, `ForgeProjectAgentServiceTests` et
`r071-quest-authoring-ui.png`.

Le programme P1 reprend séquentiellement avec `R072 / FC-QUEST-P1-02`.
