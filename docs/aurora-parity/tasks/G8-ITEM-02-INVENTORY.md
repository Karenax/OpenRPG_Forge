# G8 ITEM-02 — Apparence, inventaire et protection

`ItemVisualDefinition` découple meshes, matériaux, sockets, icône, VFX et aperçu équipé des règles.
`InventoryPolicy` calcule masse, volume, empreinte de grille et contribution des piles.

`StackPolicy` impose maximum et clé de compatibilité ; fusion et séparation sont atomiques,
révisionnées et signalent la réplication. `UsageResource` unifie charges, utilisations par repos,
cooldown, durabilité, consommation et recharge. `CriticalItemPolicy` protège les actions destructives
et journalise toute dérogation MJ.

Preuves : `ItemInventoryServiceTests`, suite RPG Kit et solution globale.

La prochaine tranche unique est **G8 ITEM-03**.
