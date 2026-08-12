# G8 FACTION-02 — Réputation et réactions

`FactionReputationService` compose la relation de base avec les transactions monde, serveur,
campagne, groupe ou joueur et les surcharges personnelles temporaires causées. Chaque transaction
est validée et fournit son inverse.

Les tags sémantiques sont interrogeables en mode tous/au moins un. La heatmap peut être capturée,
comparée, simulée pour une rencontre et importée/exportée avec son historique. Le résolveur de
réaction applique dans un ordre stable réputation, déguisement, crimes, dialogue, règle de zone,
puis décision MJ prioritaire, tout en exposant son explication.

Preuves : `FactionReputationTests`, suite RPG Kit et solution globale.

`R086 / FC-FACTION-P1-01` persiste désormais les surcharges personnelles par acteur, joueur ou
groupe avec UUID, schéma, provenance, expiration et cause. Elles sont authorables, lisibles par
handle typé et MCP filtré, puis matérialisées par le runtime sans déplacer les transactions globales
du service RPG Kit.

`R087 / FC-FACTION-P1-02` ferme la réputation globale authorable aux scopes monde, serveur,
campagne, groupe et joueur, les tags sémantiques validés, l'historique de heatmap comparable et
importable/exportable, ainsi que la politique de réaction réputation/déguisement/crime/dialogue/
zone/ordre MJ. Le tout partage la commande faction transactionnelle, ForgeScript typé, MCP filtré
et les nœuds runtime du profil canonique.

La prochaine tranche unique est **R088 / FC-ITEM-P1-01** après portail R087 vert.
