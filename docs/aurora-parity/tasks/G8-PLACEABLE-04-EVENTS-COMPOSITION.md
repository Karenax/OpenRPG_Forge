# G8 PLACEABLE-04 — Événements, assemblage et verbes

Le changement de verrou est un cycle contrôlé `LockStateChanging`/`LockStateChanged` avec méthode,
credential, acteur et veto explicite. Les changements de contenu portent un delta transactionnel,
leurs révisions et leur cause. Dégâts et destruction utilisent un payload d’entité uniforme.

Les sockets d’attachement valident type et capacité, appliquent le snap local et conservent les
prefabs composés avec patch inverse. Les verbes Inspect, Use, Open, Talk, Take, Push, Climb et custom
sont filtrés et ordonnés par contexte de manière déterministe.

Preuves : `PlaceableEventServiceTests`, suite RPG Kit et solution globale.

La famille Placeable G8 est complète. La prochaine tranche unique est **G8 DOOR-01**.
