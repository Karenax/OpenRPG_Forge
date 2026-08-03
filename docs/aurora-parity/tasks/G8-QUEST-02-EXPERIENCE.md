# G8 QUEST-02 — Ressources, étapes, visibilité, historique et marqueurs

`QuestResource` référence items, placeables, portes, zones et assets avec ownership, conditions et
validation. Les templates d'étape deviennent objectifs/transitions à IDs déterministes, provenance
et version conservées, tout en restant modifiables après génération.

La visibilité progresse Hidden, Rumor, Discovered, Tracked puis SecretCompleted par conditions
ordonnées. `QuestHistoryEvent` conserve transitions, causes, acteurs, date, contexte debug et résumé
joueur. Les marqueurs ciblent zone, entité, query ou waypoint avec visibilité, rayon de précision,
fallback et suivi.

Preuves : `QuestExperienceServiceTests`, suite RPG Kit et solution globale.

La prochaine tranche unique est **G8 QUEST-03**.
