# ADR-0018 — Validation propriétaire de Gate 2 et ouverture de Gate 3 sur Tidewater

Date : 2026-10-01
Statut : accepté par instruction explicite du propriétaire dans la conversation
Portée : branche `Rust`, progression locale Gate 2 → Gate 3

## Décision propriétaire

Après présentation des réserves R2-050, R2-052 et de qualification finale, le
propriétaire demande de considérer ces points validés, puis ordonne :
« lance le travail, objectif, clôturer la Gate 3, utilise la map tidewater pour ça ».

Gate 2 est donc clôturée par validation propriétaire pour la poursuite locale.
R2-050, R2-052 et les conditions de passage sont acceptés par cette décision.
Les preuves automatiques historiques conservent leurs résultats et limites :
aucun test non exécuté, temps humain non mesuré ou rendu non observé n'est
transformé en PASS. Le bilan `GATE2_CLOSURE_WORK_20260923.md` reste une preuve
partielle du candidat qu'il décrit, et non une nouvelle qualification machine.

Gate 3 est ouverte avec pour objectif sa clôture sur la carte Tidewater Rust.
La carte et ses sources restent celles du module, conformément à ADR-0017.
Le moteur et les commandes génériques ne dépendent d'aucun slug Tidewater.

## Portée d'exécution

- Réaliser les critères R3-A, R3-B et R3-P01 à R3-P05 de la roadmap.
- Réutiliser le contrôleur, Rapier et les commandes d'auteur existants.
- Préserver les modifications présentes dans le workspace Rust.
- Préparer puis valider les parcours Studio → jeu → sauvegarde/reprise.
- Documenter séparément implementation, tests, exécution graphique et revue humaine.

La demande autorise le travail et le passage à Gate 3 ; elle n'autorise aucune
publication Git. Au moment de cette décision, la CI distante était `NOT RUN`
tant qu'elle n'avait pas été réexécutée. Depuis le 3 octobre, les jobs ont
réellement démarré : les conditions de facturation ADR-0006 ne couvrent plus
leurs erreurs de code. Voir l'actualisation d'ADR-0006 et le contexte canonique.

## Références

- [Roadmap](../planning/MIGRATION_ROADMAP.md)
- [Plan Gate 3](../planning/GATE3_PREPARATION_PLAN.md)
- [Preuves Gate 2](../evidence/gate2/GATE2_CLOSURE_WORK_20260923.md)
- [Tidewater](ADR-0017-tidewater-fidelity-integration.md)
