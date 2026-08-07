# G13-Q — Portes : réactivité et assemblages

Features : `ORF-DOOR-016`, `ORF-DOOR-017`, `ORF-DOOR-018`, `ORF-DOOR-020`.

## Livré

Les portes relient impacts de capacités, heartbeat et événement custom à des scripts, dialogue et
faction. Une assembly décrit porte simple, double, herse, ascenseur ou sas, ses panneaux, délais,
inversion, interlock, séquence et récupération d'échec.

## Preuves

- test : `G13Q_DoorReactiveComplex_ResolvesBindingsAndAssembly` ;
- Toolset/runtime : `G13Q_DOOR_REACTIVE_COMPLEX_PASS` et `G13Q_DOOR_ASSEMBLY_PASS`.

Limite : l'animation physique des panneaux est exécutée par l'adaptateur de scène.
