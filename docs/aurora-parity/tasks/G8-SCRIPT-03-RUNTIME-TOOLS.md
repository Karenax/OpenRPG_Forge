# G8 SCRIPT-03 — Services runtime ForgeScript

`R099 / FC-SCRIPT-P1-03` est clos. `ForgeScriptRuntimeTools` fournit les cues audio, fades, snapshots et listeners, une horloge de
simulation avec calendrier et timers déterministes, et un bus d'événements custom namespacé dont les
payloads sont typés et versionnés.

Le même contrat passe par des handles natifs opaques, MCP, l'éditeur ForgeScript et un panneau runtime
accessible. Le hot reload n'est appliqué qu'à une frontière de handler, exige une migration valide, invalide les
dépendances et prescrit un redémarrage sûr en repli. Le débogueur protège l'accès par autorité et
expose pause, pas, pile, locales, watches, événements et inspection des entités.

La preuve de clôture est `P1-R099-SCRIPT-RUNTIME-TOOLS-ACCEPTANCE.md`. La prochaine tranche canonique
est `R100 / FC-SCRIPT-P1-04`.
