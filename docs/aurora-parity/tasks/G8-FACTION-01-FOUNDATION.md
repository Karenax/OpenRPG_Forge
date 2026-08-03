# G8 FACTION-01 — Fondation factionnelle

`FactionCatalog` porte des définitions versionnées, leurs membres, une matrice relationnelle
directionnelle et des bandes de disposition configurables. Les relations A→B et B→A restent
indépendantes, expliquées et bornées entre -100 et 100.

`FactionValidator` détecte les cycles d’héritage, parents absents, membres orphelins et bandes qui
se chevauchent à priorité égale. `FactionAuthoringService` applique création, archivage et suppression
avec contrôle de révision, aperçu à blanc, patch inverse et migration obligatoire des membres.

Preuves : `FactionFoundationTests`, suite RPG Kit et solution globale.

La prochaine tranche unique est **G8 FACTION-02 — réputation, tags, heatmap et réactions**.
