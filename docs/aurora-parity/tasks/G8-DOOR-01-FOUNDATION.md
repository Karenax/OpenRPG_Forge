# G8 DOOR-01 — Fondation des portes et portails

`DoorDefinition` fonctionne sur socket architectural ou en placement libre via adaptateur de kit.
Sa machine d’états couvre open, closed, opening, closing, locked, jammed, broken, disabled et custom,
avec animations et collision par état.

L’interaction contrôle portée, côté, clearance, autorité, file et feedback. `PortalComponent` porte
destination typée, ancre, orientation, préchargement et permissions. L’éditeur de liens crée une paire
bidirectionnelle atomique ou un lien one-way explicite, avec validation du retour.

Preuves : `DoorFoundationTests`, suite RPG Kit et solution globale.

La prochaine tranche unique est **G8 DOOR-02**.
