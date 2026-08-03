# G8 DIALOGUE-01 — Graphe narratif et exécution

`DialogueGraph` contient participants multi-rôles, nœuds, liens, tokens typés et métadonnées
éditoriales. Le rendu échappe les valeurs, localise les tokens requis absents et résout le locuteur
par rôle, query, tag ou entité avec fallback.

FirstValid, AllValid, Weighted, RandomDeterministic et Manual sont explicites. Les expressions et
actions issues des builders visuels conservent leur source descriptive ; l'exécution trie les liens
avant sélection et emploie une seed pour les modes aléatoires. Notes, intention, émotion, source,
statut, TODO et auteur restent hors du texte joueur mais dans le document canonique.

Preuves : `DialogueGraphServiceTests`, suite RPG Kit et solution globale.

La prochaine tranche unique est **G8 DIALOGUE-02**.
