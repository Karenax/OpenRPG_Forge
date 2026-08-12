# G8 ITEM-01 — Fondation des objets

`ItemDefinition` sépare le blueprint versionné de `ItemInstance` et de son état mutable. L’identité
localisée couvre nom, descriptions, texte inconnu, label de type et notes ; l’asset conserve licence
et SHA-256.

Le wizard transactionnel construit un objet depuis un archétype, type, apparence, slots, qualité,
niveau, propriétés et provenance. Un changement de type passe par un aperçu des slots, propriétés et
rig incompatibles avant migration explicite. La validation localisée couvre identité, type, slots,
effets, scripts, localisation, asset, licence et économie.

R088 relie désormais ce wizard au document canonique `ItemFoundationProfile.Creation`, avec règles de
génération, commande unique, aperçu live, ForgeScript typé, ressource MCP filtrée et résolution runtime
optionnelle/requise.

Preuves : `ItemFoundationTests`, `ItemCreationProfileTests`,
`P1-R088-ITEM-CREATION-ACCEPTANCE.md`, suite RPG Kit et portail intégré.

La prochaine tranche ITEM canonique est **R089 / FC-ITEM-P1-02**.
