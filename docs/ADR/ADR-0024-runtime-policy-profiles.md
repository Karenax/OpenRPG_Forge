# ADR-0024 — Profils canoniques pour rythme, repos et restauration de séquence

## Décision

Les politiques périodiques et de récupération appartiennent aux documents de module et de zone,
pas aux nœuds du moteur 3D. Le module porte timers globaux, cycle de repos et règle d'abandon ; la
zone porte timers locaux, permission de repos et volumes prioritaires.

Les modifications module/zone sont appliquées par une commande combinée unique. Le runtime emploie
un scheduler déterministe avec budgets et rattrapage borné. Le repos émet quatre événements serveur
et l'abandon restaure un instantané explicite selon des drapeaux versionnés.

## Conséquences

- sérialisation, validation, dry-run, undo/redo et MCP partagent la même source de vérité ;
- une zone inactive peut suspendre son rythme sans perdre le contrat auteur ;
- les handlers ForgeScript restent des références versionnées et devront passer par le sandbox ;
- le lecteur de séquence peut évoluer en G11-K sans déplacer les politiques dans l'adapter moteur.
