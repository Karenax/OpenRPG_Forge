# G8 FACTION-02 — Réputation et réactions

`FactionReputationService` compose la relation de base avec les transactions monde, serveur,
campagne, groupe ou joueur et les surcharges personnelles temporaires causées. Chaque transaction
est validée et fournit son inverse.

Les tags sémantiques sont interrogeables en mode tous/au moins un. La heatmap peut être capturée,
comparée, simulée pour une rencontre et importée/exportée avec son historique. Le résolveur de
réaction applique dans un ordre stable réputation, déguisement, crimes, dialogue, règle de zone,
puis décision MJ prioritaire, tout en exposant son explication.

Preuves : `FactionReputationTests`, suite RPG Kit et solution globale.

La famille Faction G8 est complète. La prochaine tranche unique est **G8 ITEM-01**.
