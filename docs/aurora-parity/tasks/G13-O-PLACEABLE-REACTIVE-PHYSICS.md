# G13-O — Placeables : événements et physique

Features : `ORF-PLC-021`, `ORF-PLC-022`, `ORF-PLC-023`, `ORF-PLC-024`.

## Livré

Les événements attaque physique, impact de capacité, heartbeat et custom namespacé utilisent des
bindings typés. Cadence, budget, suspension et autorité sont explicites. La physique décrit corps,
masse, contraintes, réplication, sommeil et conversion statique sans posséder de nœud moteur.

## Preuves

- test : `G13O_PlaceableReactivePhysics_BudgetsEventsAndKeepsAuthority` ;
- Toolset/runtime : marqueurs `G13O_PLACEABLE_REACTIVE_PHYSICS_PASS` et
  `G13O_PLACEABLE_REACTIVE_PASS`.

Limite : l'adaptateur visuel choisit encore le corps 3D concret.
