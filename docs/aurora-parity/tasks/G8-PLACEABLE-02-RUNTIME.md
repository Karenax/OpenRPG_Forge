# G8 PLACEABLE-02 — Conteneurs, états, dégâts et validation

`ContainerComponent` porte capacité, masse, ownership, permissions, persistance, loot, UI et
réplication. La machine d’états visuelle couvre les états standards ou personnalisés, transitions,
conditions et durée de prévisualisation.

`DamageableComponent` résout santé, armure, résistances, étapes visuelles, destruction, débris et
réparation. `InteractionExecuted` conserve verbe, acteur, point cible et résultat. La validation
croisée vérifie collision/navigation, animations, locks, traps, inventaire, scripts, physique et budgets.

R093 relie désormais ces contrats purs au profil projet canonique `PlaceableOperationsProfile`, au
Studio transactionnel, à ForgeScript/MCP, à la persistance et au runtime via
`PlaceableOperationsRuntimeAdapter`. Le panneau runtime expose un inventaire, l’état visuel, la santé,
le feedback et des commandes focusables d’ouverture, fermeture, dégâts et réparation.

Preuves : `PlaceableInteractionServiceTests`, `PlaceableOperationsRuntimeAdapterTests`,
`P1-R093-PLACEABLE-OPERATIONS-ACCEPTANCE.md` et `r093-placeable-operations-ui.png`.

La prochaine tranche unique de la file P1 est **R094 / FC-PLACEABLE-P1-02**.
