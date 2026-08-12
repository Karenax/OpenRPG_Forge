# P2 R140 — Routes aériennes runtime

`ORF-WPT-011` disposait déjà des documents, courbes et overlays. Le palier ajoute le contrôleur
runtime déterministe : échantillonnage linéaire, Catmull-Rom ou Bézier, vitesse interpolée entre
nœuds, orientation avant et inclinaison animée. Les politiques d'obstacle évitent, arrêtent ou
ignorent explicitement ; les routes ouvertes terminent et les routes fermées bouclent.

Tests ciblés : `FlightRouteControllerTests` (3/3). Preuve Toolset :
`OPENRPG_FORGE_TOOLSET_R140_FLIGHT_ROUTE_PASS`.

P3/P4, AV7–AV10, commit et push sont exclus.
