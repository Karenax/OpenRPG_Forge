# G8 ITEM-02 — Apparence, inventaire et protection

`ItemVisualDefinition` découple meshes, matériaux, sockets, icône, VFX et aperçu équipé des règles.
`InventoryPolicy` calcule masse, volume, empreinte de grille et contribution des piles.

`StackPolicy` impose maximum et clé de compatibilité ; fusion et séparation sont atomiques,
révisionnées et signalent la réplication. `UsageResource` unifie charges, utilisations par repos,
cooldown, durabilité, consommation et recharge. `CriticalItemPolicy` protège les actions destructives
et journalise toute dérogation MJ.

R088 persiste ces quatre politiques dans le blueprint canonique, les rend éditables dans le même Studio
transactionnel et adapte le runtime au service RPG Kit existant. L’état optionnel/requis, la persistance,
ForgeScript et MCP sont désormais couverts sans second magasin.

Preuves : `ItemInventoryServiceTests`, `ItemFoundationRuntimeAdapterTests`,
`P1-R088-ITEM-CREATION-ACCEPTANCE.md`, capture 1440×920 et portail intégré.

R089 persiste les six protections d'objet plot et l'autorisation de dérogation MJ auditée. Les décisions
de destruction, vente, drop, vol, consommation et nettoyage restent résolues par le service RPG Kit
existant, à partir du document canonique validé.

Preuves R089 : `ItemGameplayRuntimeAdapterTests`, `P1-R089-ITEM-GAMEPLAY-ACCEPTANCE.md`, capture
1440×920 et portail intégré.

La prochaine tranche ITEM canonique est **R090 / FC-ITEM-P1-03**.
