# Rapport de performance — MVP Crypte des Cendres

## Cible provisoire

Le MVP vise une station Windows x64 de création avec 8 Go de RAM, processeur quatre cœurs et GPU
compatible avec le renderer de compatibilité. Pour la petite zone de démonstration, le gate exige :

- démarrage headless jusqu'à un monde jouable en moins de 1 000 ms ;
- 16 entités métier et 10 collisions statiques reconstruites sans erreur ;
- scénario complet, ForgeScript et sauvegarde/reprise avec code de sortie 0 ;
- archive `.orpmod` de la Crypte inférieure à 1 Mio tant qu'elle ne contient que des primitives.

## Mesure reproductible

`scripts/Measure-RuntimePerformance.ps1` lance cinq fois le package exporté avec le moteur épinglé
4.7.1 stable Mono, en headless. Le marqueur `OPENRPG_FORGE_RUNTIME_PERF` mesure le temps écoulé entre
l'entrée de `_Ready` et la fin de reconstruction/HUD. Chaque itération doit aussi terminer le smoke
gameplay complet.

La mesure du 2 août 2026 sera actualisée à chaque modification matérielle de la zone. Le JSON brut,
les valeurs min/moyenne/max et les logs par itération sont conservés dans
`artifacts/performance/`. Cette métrique est un temps de démarrage fonctionnel, pas une mesure de FPS
GPU ; une passe interactive frametime/GPU deviendra obligatoire lorsque des assets 3D externes,
ombres nombreuses ou post-traitements seront ajoutés.

## Résultat de référence — 2 août 2026

Sur cinq lancements successifs de la zone finale exportée :

| Mesure | Résultat |
|---|---:|
| Minimum | 165,398 ms |
| Moyenne | 190,903 ms |
| Maximum | 225,207 ms |
| Nœuds reconstruits | 98 |
| Entités / collisions | 16 / 10 |

Le maximum reste à 22,6 % du budget de 1 000 ms. Les cinq scénarios ont terminé avec le marqueur de
succès gameplay et un code de sortie nul. Mesure brute :
`artifacts/performance/runtime-performance.json`.
