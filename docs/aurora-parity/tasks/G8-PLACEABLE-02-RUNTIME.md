# G8 PLACEABLE-02 — Conteneurs, états, dégâts et validation

`ContainerComponent` porte capacité, masse, ownership, permissions, persistance, loot, UI et
réplication. La machine d’états visuelle couvre les états standards ou personnalisés, transitions,
conditions et durée de prévisualisation.

`DamageableComponent` résout santé, armure, résistances, étapes visuelles, destruction, débris et
réparation. `InteractionExecuted` conserve verbe, acteur, point cible et résultat. La validation
croisée vérifie collision/navigation, animations, locks, traps, inventaire, scripts, physique et budgets.

Preuves : `PlaceableInteractionServiceTests`, suite RPG Kit et solution globale.

La prochaine tranche unique est **G8 PLACEABLE-03**.
