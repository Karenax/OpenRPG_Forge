# G8 PLACEABLE-04 — Événements, assemblage et verbes

Le changement de verrou est un cycle contrôlé `LockStateChanging`/`LockStateChanged` avec méthode,
credential, acteur et veto explicite. Les changements de contenu portent un delta transactionnel,
leurs révisions et leur cause. Dégâts et destruction utilisent un payload d’entité uniforme.

Les sockets d’attachement valident type et capacité, appliquent le snap local et conservent les
prefabs composés avec patch inverse. Les verbes Inspect, Use, Open, Talk, Take, Push, Climb et custom
sont filtrés et ordonnés par contexte de manière déterministe.

R094 relie `LockStateChanging`, `LockStateChanged` et `ContainerContentsChanged` au profil projet
canonique, à l'UI transactionnelle, à ForgeScript/MCP et à un panneau runtime accessible. Le veto
reste contrôlé et l'événement de contenu conserve un delta atomique et ses révisions.

Preuves : `PlaceableEventServiceTests`, `PlaceableSecurityEventsRuntimeAdapterTests`,
`P1-R094-PLACEABLE-SECURITY-EVENTS-ACCEPTANCE.md` et
`r094-placeable-security-events-ui.png`.

R095 relie `DamageApplied`/`EntityDestroyed`, les sockets/attachements réversibles et les huit verbes
contextuels au profil canonique, au Studio transactionnel, à ForgeScript/MCP et au runtime accessible.
Le snap conserve le prefab composé et produit toujours un patch inverse.

Preuves R095 : `PlaceableCompositionEventsRuntimeAdapterTests`,
`P1-R095-PLACEABLE-COMPOSITION-EVENTS-ACCEPTANCE.md` et
`r095-placeable-composition-events-ui.png`.

La famille Placeable P1 est désormais fermée. La file P1 reprend au lot Trigger R096 ; aucune fonction
P2 n'est ouverte.
