# G8 ITEM-04 — Inspecteur d’inventaire et événements

`InventoryAuthoringService` fournit la même commande canonique aux interactions drag-drop et aux
agents : aperçu à blanc, contrôle de révision, validation de destination, multi-edit, patch inverse,
filtres et comparaison. Les placements conservent leur aperçu visuel.

Le cycle de vie typé couvre `ItemCreated`, `Acquired`, `Transferred`, `Equipped`, `Unequipped`,
`Used`, `Consumed`, `Broken` et `Destroyed`, avec identité, provenance, source, cible et contexte.

Preuves : `InventoryAuthoringServiceTests`, suite RPG Kit et solution globale.

La famille Item G8 est complète. La prochaine tranche unique est **G8 PLACEABLE-01**.
