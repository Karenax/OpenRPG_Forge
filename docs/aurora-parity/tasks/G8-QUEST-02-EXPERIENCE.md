# G8 QUEST-02 — Ressources, étapes, visibilité, historique et marqueurs

Cette fiche historique décrivait le noyau isolé `Forge.RpgKit.Domain/QuestExperienceService`.
Avant R072, il n'était ni attaché au document `QuestDefinition`, ni exposé dans le journal principal,
ni persisté par une commande projet, ni adapté au runtime moteur. Il ne constituait donc pas une
preuve complète des critères source.

R072 ferme cette limite avec `Forge.Domain.Quests.QuestExperienceProfile`. Les ressources item,
placeable, porte, zone et asset portent ownership, conditions et validation projet des références
requises. Les templates d'étape deviennent objectifs/transitions à IDs déterministes, provenance
et version conservées, tout en restant modifiables après génération.

La visibilité progresse Hidden, Rumor, Discovered, Tracked puis SecretCompleted par conditions
ordonnées. `QuestExperienceHistoryEvent` conserve transitions, causes, acteurs, date, contexte debug
et résumé joueur localisé. Les marqueurs ciblent zone, entité, query ou waypoint avec visibilité,
rayon de précision, fallback spatial et suivi.

Preuves de clôture : `P1-R072-QUEST-EXPERIENCE-ACCEPTANCE.md`, capture R072, tests Domain,
Application, ModuleFormat et MCP, smokes Toolset/runtime et portail intégré.

La prochaine tranche unique est **R073 / FC-QUEST-P1-03**.
