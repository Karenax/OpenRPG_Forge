# G8 ENCOUNTER-01 — Fondation des rencontres

`EncounterDefinition` porte une identité canonique, un volume d'activation 2D/3D, un pool de
créatures pondéré et un profil de difficulté piloté par ruleset. Le volume ajoute filtres, ligne de
vue, rayon de menace et script d'activation ; chaque candidat conserve rôle, coût, conditions,
variantes et limite d'instances.

`EncounterAuthoringService` prévisualise l'assistant sans mutation, produit un diff sémantique,
génère un UUID déterministe puis applique uniquement une définition valide. La validation localise
les pools vides, références de zone/créature/blueprint/script/ancre/récompense, géométries, budgets et
caps réseau invalides.

Preuves : `EncounterAuthoringServiceTests`, suite RPG Kit et solution globale.

La prochaine tranche unique est **G8 ENCOUNTER-02**.
