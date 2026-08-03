# G7-30 — Simulation gameplay Core

Le Core définit et simule désormais cinq familles : vol libre 3D avec accélération/altitude/lift/
atterrissage, projectile ballistique/homing/hitscan autoritaire, physique statique/cinématique/rigide,
ciblage self/entity/point/direction/area/cone/line/chain, et horloge monde avec calendrier,
domaines de pause, échelle locale et schedules.

Les fonctions sont pures et déterministes. Les profils peuvent être portés par les propriétés typées
des blueprints/instances, éditées par la commande existante et persistées. RPG Kit ajoutera les
écrans métier et règles de combat au-dessus de ces primitives Core.

Preuves : `GameplaySimulationTests`, tests d’overrides et suite globale.

La prochaine tranche unique est **G7-31 — sauvegarde et difficulté gameplay**.
