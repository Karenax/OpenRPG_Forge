# G8 ITEM-01 — Fondation des objets

`ItemDefinition` sépare le blueprint versionné de `ItemInstance` et de son état mutable. L’identité
localisée couvre nom, descriptions, texte inconnu, label de type et notes ; l’asset conserve licence
et SHA-256.

Le wizard transactionnel construit un objet depuis un archétype, type, apparence, slots, qualité,
niveau, propriétés et provenance. Un changement de type passe par un aperçu des slots, propriétés et
rig incompatibles avant migration explicite. La validation localisée couvre identité, type, slots,
effets, scripts, localisation, asset, licence et économie.

Preuves : `ItemFoundationTests`, suite RPG Kit et solution globale.

La prochaine tranche unique est **G8 ITEM-02**.
