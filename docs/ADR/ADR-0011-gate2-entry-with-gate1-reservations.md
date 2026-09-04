# ADR-0011 — Entrée en Gate 2 avec réserves explicites de Gate 1

Date : 2026-09-04
Statut : **accepté explicitement par le propriétaire**
Portée : branche `Rust`, autorisation de démarrer Gate 2 uniquement
Complète ADR-0005, ADR-0006 et ADR-0010 sans réécrire leurs preuves.

## Accord et décision

Après le candidat visuel `8b3e3e6` et ses preuves publiées à `28a2078`, le propriétaire constate :
« c'est mieux », mais demande une herbe plus naturelle, regroupée en zones denses, et un sol texturé.
À la question « Confirmes-tu cette dérogation pour démarrer Gate 2 avec ces réserves consignées ? »,
il répond **« oui »**. L'accord porte sur l'acceptation du rendu comme prototype, le maintien des
finitions à réaliser et le report des validations visuelles, de mouvement et comparatives manquantes.

**Gate 2 — Éditeur de monde devient la gate principale active.** Gate 1 reste techniquement
incomplète : 43/55 routes cochées, 12 ouvertes. Ce passage administratif sous réserves n'est ni un
PASS technique de Gate 1, ni un reçu de revue d'images, ni une qualification de distribution.

## Réserves conservées, non bloquantes pour l'entrée en Gate 2

| Réserve | Travail encore dû / routes |
|---|---|
| G1-VIS-001 | Herbe organisée en nappes irrégulières, cœur dense et bord progressif ; le scatter actuel augmente la quantité sans masque spatial de densité. |
| G1-VIS-002 | Sol détaillé/texturé et cohérent avec la couverture végétale ; palettes et bruit procédural actuels ne constituent pas cette finition. |
| G1-REV-001 | Vent, mouvement et jouabilité : `R1-014`, volet vidéo/humain de `R1-048`. |
| G1-REV-002 | Optique de l'eau, wetness, post-traitement et profils/native/Browser : compléments fonctionnels/visuels encore manquants de `R1-023/025/026`. Les preuves GPU existantes gardent leur portée exacte. |
| G1-REV-003 | Vues proche/lointaine, matériaux et composition : `R1-046/047/049/050`. |
| G1-REV-004 | Comparaisons WebGPU/Grassworks et Cadle : `R1-051/052`, avec les limites de source déjà consignées. |
| G1-PERF-001 à 004 | Dettes déjà différées sous ADR-0010 : `R1-041` et volet cadence de `R1-048`. Aucune nouvelle autorisation de campagne PresentMon. |

Ces réserves restent dans la roadmap. Elles ne disparaissent pas lorsque les pinceaux correspondants
de Gate 2 sont implémentés : un outil de peinture fonctionnel ne prouve pas une composition naturelle.
Leur fermeture demandera des preuves adaptées et, pour les jugements humains, un verdict explicite
sur les artefacts réellement présentés. Aucun `qualified=false`, FAIL ou ABORT historique n'est changé.

## Frontières

- Cette décision autorise le travail de Gate 2, pas son passage ni le démarrage anticipé de Gate 3.
- Les tests, la persistance, undo/redo, la parité des données et les critères de Gate 2 restent dus.
- Aucun défaut bloquant de fonctionnement, perte de données ou problème de provenance n'est accepté.
- La CI distante reste **NOT RUN — infrastructure billing** sous ADR-0006, pas PASS.
- Le renderer, le shell Tauri/Leptos, les documents moteur-neutres et les frontières de licences restent inchangés.
- Aucun asset Cadle n'est copié ; aucune publication supplémentaire n'est autorisée par cet ADR.
- La finition de Gate 1 n'est plus le travail automatique prioritaire ; elle doit être planifiée
  explicitement sans relancer les audits de cadence reportés.

## Références

- [Dernier rendu, preuves et limites](../evidence/gate1/VEGETATION_COVERAGE.md).
- [Roadmap et réserves](../planning/MIGRATION_ROADMAP.md).
- [Report des performances](ADR-0010-gate1-strict-performance-deferral.md).

L'objectif global C → 0 → 1 → 2 → 3 → 4 reste inchangé. L'autorisation de continuation ne signifie
pas que toutes ses exigences sont satisfaites.
