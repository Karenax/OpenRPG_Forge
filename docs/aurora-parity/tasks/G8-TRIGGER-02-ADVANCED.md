# G8 TRIGGER-02 — Pièges, requêtes, volumes gameplay et debug

`EntityQuery` filtre joueurs, tags, factions, composants, propriétaire, équipe et prédicat sandbox.
`TrapVolume` ajoute détection, désarmement, payload, reset, ownership et visualisation MJ.

La bibliothèque fournit Damage, Heal, Gravity, Water, Climb, NoBuild, Camera, Audio, Weather et
Checkpoint. L’overlay runtime expose occupants, derniers tirs, cooldowns, filtres et trace. La
validation croisée vérifie auto-intersections, filtres, actions, portail, cibles, scripts et accessibilité.

R096 raccorde désormais ces services au profil canonique `TriggerWorldProfile.Advanced`, à
l’interface auteur, à la validation pré-build, à la persistance, à ForgeScript/MCP et à un panneau
runtime accessible. Les données optionnelles restent sûres ; les données requises manquantes donnent
un diagnostic stable.

Preuves : `TriggerAdvancedServiceTests`, `TriggerAdvancedRuntimeAdapterTests`, marqueurs Toolset et
Runtime R096, et `docs/aurora-parity/evidence/r096-trigger-advanced-ui.png`.

La famille Trigger P1 est complète. Le programme séquentiel reprend à **R097 / FC-SCRIPT-P1-01** ;
aucune fonction Trigger P2/P3 n’est ouverte.
