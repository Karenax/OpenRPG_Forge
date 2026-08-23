# Studio UX10-B — Plans, transactions et autorisations

Qualification locale du 23 août 2026. Les cinq routes sont accessibles dans la page **Plans** avec
une variable de validation nommée, un aperçu sec et une approbation humaine obligatoire.

## ORF-AI-006 — Plan avant mutation

Le plan structuré exige objectif, commande sémantique, hypothèse, objet affecté, risque et test. Un
plan incomplet est refusé avant toute transaction.

## ORF-AI-007 — Dry-run

Le dry-run canonique conserve révision et empreinte et produit une ligne de diff lisible sans
écriture.

## ORF-AI-008 — Transaction atomique

La sandbox exerce `begin → preview → commit → rollback`. Après rollback, la liste complète des
variables persistées est comparée à l’état initial, puis le workspace temporaire est supprimé.

## ORF-AI-009 — Diff sémantique

Le diff v1 contient catégories typées, valeurs avant/après et hashes stables de 64 caractères.

## ORF-AI-010 — Niveaux d’autorisation

Lecture, proposition, configuration, application d’un plan approuvé et autonomie bornée restent
visibles et séparées ; une transaction sans confirmation humaine est refusée.

## Validation

- build Studio Debug : 0 avertissement, 0 erreur ;
- smoke global : cycle transactionnel, rollback, audit, préservation source et restauration UI verts ;
- tests canoniques UX10 cumulés : 66/66 ;
- capture Forward+ Full HD inspectée :
  `artifacts/verification/studio-increment/ux10-b-plans-forward-plus-fhd.png` ;
- portail UX10 partagé : PASS en 524,12 s ; graphe Roslyn 11 566 nœuds / 90 143 relations /
  0 diagnostic.

## Limites

La transaction cible une copie confinée du module Forgotten Forest. Cette preuve ne revendique ni
mutation autonome du module source, ni résolution automatique de conflits entre auteurs, ni
publication Git.
