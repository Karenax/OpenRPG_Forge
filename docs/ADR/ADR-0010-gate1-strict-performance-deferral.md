# ADR-0010 — Report explicite des critères stricts de performance de Gate 1

Date : 2026-09-03
Statut : **accepté par le propriétaire — vérification différée, pas PASS technique**
Portée : branche `Rust`, critères quantitatifs de performance de Gate 1 uniquement
Complète : ADR-0008 ; ne remplace ni ADR-0005, ni ADR-0006, ni ADR-0009

## Autorisation et motif

Le propriétaire répond « oui je valide » à la demande : « M'autorises-tu à reporter les critères
stricts de performance de Gate 1, en les conservant explicitement dans la roadmap pour vérification
ultérieure ? » Cette décision borne le temps consacré aux audits de cadence et permet de reprendre
les livrables fonctionnels de la gate active.

Le dernier replay gouverné, source `6171388761e3c2f22903116cad537a2612679c7b`, arbre
`7fbcae7bf4acd33cb4d2f931e3a56047d462ddb7`, produit un vrai **FAIL de performance schema 13**,
et non un ABORT d'acquisition : 26,1661 FPS affichés, p95 131,956 ms et 1 % low 3,4183 FPS.
Les preuves et leurs hashes sont conservés dans
[R1-048](../evidence/gate1/R1_048_GAMEPLAY_STUTTER_PRESENTMON.md).
Ce résultat ne se réduit donc pas à l'ancienne frame isolée de 35,3837 ms. La cause et la
représentativité en usage normal restent non établies ; aucune amélioration de performance produit
n'a été démontrée par les dernières expériences. Les modifications expérimentales ont été retirées.

## Décision et dettes conservées

Les critères suivants deviennent **DIFFÉRÉS / NON BLOQUANTS ADMINISTRATIVEMENT POUR GATE 1**.
Leurs cases restent décochées et leurs résultats bruts inchangés.

| Dette | Critère conservé pour vérification ultérieure | Route / portée |
|---|---|---|
| G1-PERF-001 | 60 FPS moyens, 1920 × 1080 High, RTX 3060 ou RX 6600 | O2, `R1-041` |
| G1-PERF-002 | p95 ≤ 16,7 ms sur la machine de référence | O2, `R1-041` |
| G1-PERF-003 | 1 % low ≥ 45 FPS sur la machine de référence | O2, `R1-041` |
| G1-PERF-004 | qualification automatisée du stutter et répétabilité, seuils ADR-0008 inchangés | volet cadence de `R1-048` |

La campagne PresentMon, les rebuilds destinés uniquement à cette campagne et les micro-optimisations
exploratoires restent suspendus jusqu'à une nouvelle demande explicite de reprise. Le futur contrôle
réutilisera les objectifs et le protocole documentés sur un candidat exact, en publiant tous les
résultats, pas seulement le meilleur run. Aucun seuil de script, ledger, statut FAIL/ABORT ou
`qualified=false` n'est modifié pour matérialiser ce report.

## Hors dérogation

- La vidéo, le comportement du contrôleur et l'appréciation humaine de `R1-048` restent dus.
- Les revues humaines/comparaisons de Gate 1 ne sont pas reportées par cet accord ; la dérogation
  humaine ADR-0005 reste limitée à Gate 0.
- Les draw calls/triangles/instances `R1-037` et les preuves d'exécution GPU de l'eau, de la wetness
  et du post-traitement `R1-023/025/026` restent dus. La mutation hôte RenderDoc exige toujours
  l'autorisation séparée d'ADR-0009.
- Fonctionnement des profils, parité native/Studio, tests locaux pertinents, sécurité, provenance
  et interdiction des assets Cadle restent inchangés. La CI distante relève seulement d'ADR-0006.
- Aucun blocage manifeste de l'usage n'est réputé corrigé ou acceptable par cette décision.

## Conséquence sur la progression

Gate 1 reste **OUVERTE** : quatre conditions de passage sont satisfaites, quatre sont différées par
ce nouvel ADR et deux conditions visuelles restent ouvertes ; les routes non couvertes restent dues.
Les 42 routes cochées sur 55 ne changent pas. Cette décision n'autorise pas à elle seule Gate 2.
La suite porte sur les livrables et validations non différés de Gate 1, sans nouvelle boucle d'audit
de cadence. Les dettes restent visibles avant toute qualification ou annonce de performances ; cet
ADR ne qualifie ni une distribution ni les performances des gates suivantes.
