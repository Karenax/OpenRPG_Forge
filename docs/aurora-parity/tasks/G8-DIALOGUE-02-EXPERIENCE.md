# G8 DIALOGUE-02 — Cycle, recherche, mémoire et checks

Les événements Completed/Aborted portent joueur, raison, rollback optionnel et état de reprise avec
mémoire et seed. La recherche traverse plusieurs dialogues avec filtres locuteur/condition/action,
regex bornée par timeout et remplacement uniquement prévisualisé.

Le mode review calcule badges condition, action, voix, localisation, erreur et lien. La mémoire
typée couvre dialogue, participant, joueur, groupe et monde avec cinq durées et expiration explicite.
`SkillCheckNode` reste data-driven et expose difficulté, opposant, avantage, branche choisie,
visibilité et explication reproductible.

Preuves : `DialogueExperienceServiceTests`, suite RPG Kit et solution globale.

La prochaine tranche unique est **G8 DIALOGUE-03**.
