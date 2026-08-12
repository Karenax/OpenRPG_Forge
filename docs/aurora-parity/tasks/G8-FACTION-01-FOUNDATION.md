# G8 FACTION-01 — Fondation factionnelle

`FactionCatalog` porte des définitions versionnées, leurs membres, une matrice relationnelle
directionnelle et des bandes de disposition configurables. Les relations A→B et B→A restent
indépendantes, expliquées et bornées entre -100 et 100.

`FactionValidator` détecte les cycles d’héritage, parents absents, membres orphelins et bandes qui
se chevauchent à priorité égale. `FactionAuthoringService` applique création, archivage et suppression
avec contrôle de révision, aperçu à blanc, patch inverse et migration obligatoire des membres.

Preuves : `FactionFoundationTests`, suite RPG Kit et solution globale.

`R086 / FC-FACTION-P1-01` ferme la fondation authorable : état archivé, matrice asymétrique, bandes
prioritaires et suppression avec migration des membres legacy/avancés partagent désormais la
commande transactionnelle du projet. Le Studio affiche la heatmap et les états résultants ;
ForgeScript, MCP et le runtime lisent le même catalogue validé.

La réputation et les réactions du lot R087 sont fermées dans `G8-FACTION-02-REPUTATION.md`.
La prochaine tranche unique est **R088 / FC-ITEM-P1-01** après portail R087 vert.
