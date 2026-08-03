# G8 SCRIPT-05 — Interop éditeur et MCP

`ForgeScriptInteropService` présente la même façade versionnée aux surfaces éditeur et MCP : lecture
de l'API, création/mise à jour de sources `.as`, compilation, diagnostics, tests sandboxés et aperçu
des bindings. Les mutations exigent transaction, révision de base, provenance et compilation valide ;
un conflit ou une source invalide ne change ni le document ni la révision.

`ForgeScriptInteropServiceTests` vérifie le parcours complet et les refus stale/invalide. La famille
SCRIPT sélectionnée pour G8 est complète.

La prochaine tranche unique est **G8 AUTHOR-UI-01** : exposer les services RPG Kit dans les écrans
d'auteur sans édition JSON ni voie de mutation parallèle.
