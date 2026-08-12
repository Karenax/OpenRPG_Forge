# G8 ITEM-04 — Inspecteur d’inventaire et événements

`InventoryAuthoringService` fournit la même commande canonique aux interactions drag-drop et aux
agents : aperçu à blanc, contrôle de révision, validation de destination, multi-edit, patch inverse,
filtres et comparaison. Les placements conservent leur aperçu visuel.

Le cycle de vie typé couvre `ItemCreated`, `Acquired`, `Transferred`, `Equipped`, `Unequipped`,
`Used`, `Consumed`, `Broken` et `Destroyed`, avec identité, provenance, source, cible et contexte.

Preuves : `InventoryAuthoringServiceTests`, suite RPG Kit et solution globale.

R090 persiste le document d'inspection et les neuf bindings dans le blueprint objet canonique. Le Studio
réunit placements, drag-drop prévisualisé, filtres, comparaison multi-edit, assets d'aperçu, handlers,
autorité et API ForgeScript 1.0. La commande, la persistance, ForgeScript, MCP et le runtime utilisent
le même profil optionnel/requis via `ItemOperationsRuntimeAdapter`.

Preuves R090 : `ItemOperationsRuntimeAdapterTests`, `P1-R090-ITEM-OPERATIONS-ACCEPTANCE.md`, capture
1440×920 et portail intégré.

La famille Item G8 est complète. La prochaine tranche séquentielle est **R091 / FC-MERCHANT-P1-01**.
