# ADR-0013 — Décisions du propriétaire pour la clôture de Gate 1

Date : 2026-09-12
Statut : ACCEPTED — instruction explicite du propriétaire.
Portée : Gate 1 uniquement, qualification locale ; aucune publication.

## Instruction

« les sacades pendant le changement de qualité n'ont pas a etre tester, on s'en fiche,
fait un dérogation. pour le point 2 on verra plus tard, dérogation. 3 c'est validé.
fait le point 4 »

Les numéros renvoient aux quatre points de la revue de clôture du 12 septembre.

## Décisions

1. Les saccades pendant les changements de qualité sont WAIVED / ACCEPTED.
   Aucune nouvelle mesure ou correction de ces transitions n'est exigée pour Gate 1.
   Les pics historiques restent documentés, sans devenir un PASS de fluidité.
   Cette décision ne couvre pas une régression indépendante en jeu à qualité fixe,
   une erreur de shader, un crash ou le tramage persistant déjà corrigé.
2. Le point kit est WAIVED / DEFERRED : retouche humaine, export/réimport après cette
   retouche et finalisation des revues humaines LOD, pivots, collisions et provenance.
   La retouche n'a pas eu lieu ; les preuves structurelles existantes sont conservées.
   Le kit reste internal_gate_proof_only, sans droits de distribution nouveaux.
   Réexaminer ce point avant promotion du kit ou qualification du parcours d'édition.
3. Le jugement comparatif Godot/Cadle est ACCEPTED BY OWNER sur le dossier présenté.
   L'acceptation du rendu intégré et du ciel reste acquise. Cette décision artistique
   ne transforme pas les captures de cadrages différents en benchmark A/B équivalent.
4. Constituer la révision locale propre, vérifier les preuves et exécuter les contrôles
   affectés selon ADR-0006, puis enregistrer le verdict de Gate 1 si ceux-ci passent.
   La copie de travail et les travaux non liés sont préservés ; aucun push ni travail
   de Gate 2 n'est demandé.

## Limites maintenues

ADR-0011, ADR-0012 et la dérogation de performance restent bornés à leurs périmètres.
CI distante NOT RUN sous ADR-0006. Aucun test échoué n'est couvert par cette décision.
Le rapport final doit identifier commit, arbre, environnement, commandes et hashes.
Les mentions antérieures de décisions humaines attendues sont historiques à compter
de cette instruction ; elles ne doivent pas provoquer une nouvelle demande identique.
