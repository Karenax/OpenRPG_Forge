# G8 SCRIPT-03 — Services runtime ForgeScript

`ForgeScriptRuntimeTools` fournit les cues audio, fades, snapshots et listeners, une horloge de
simulation avec calendrier et timers déterministes, et un bus d'événements custom namespacé dont les
payloads sont typés et versionnés.

Le hot reload n'est appliqué qu'à une frontière de handler, exige une migration valide, invalide les
dépendances et prescrit un redémarrage sûr en repli. Le débogueur protège l'accès par autorité et
expose pause, pas, pile, locales, watches, événements et inspection des entités.

La prochaine tranche unique est **G8 SCRIPT-04**.
