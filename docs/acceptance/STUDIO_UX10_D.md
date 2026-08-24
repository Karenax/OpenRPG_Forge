# Studio UX10-D — Création 3D gouvernée et cohérence narrative

Qualification locale du 23 août 2026. Les trois routes sont accessibles depuis **3D assistée** avec
nom technique, brief humain, aperçu, approbation et exécution en sandbox.

## ORF-AI-021 — Création 3D via Blender

La recette typée utilise les primitives, PBR, UV, LOD et collision autorisés, génère une requête de
worker sans code exécutable libre, passe cinq tests puis restaure tous les fichiers appliqués.

## ORF-AI-022 — Fournisseurs text/image-to-3D

Le fournisseur déterministe hors ligne produit une proposition CC0, un plan borné, une application
approuvée et un rollback complet avec reçu et provenance.

## ORF-AI-029 — Contrôle de cohérence narrative

Le contrôle local couvre dialogues, quêtes et contacts, fournit des constats sourcés et ne réécrit
silencieusement aucun contenu.

## Validation

- build Studio Debug : 0 avertissement, 0 erreur ;
- smoke global : Blender typé, text-to-3D hors ligne, cohérence narrative, rollback, audit,
  préservation source et suppression de sandbox verts ;
- tests canoniques UX10 cumulés : 66/66 ;
- capture Forward+ Full HD inspectée :
  `artifacts/verification/studio-increment/ux10-d-creative-forward-plus-fhd.png` ;
- portail UX10 partagé : PASS en 524,12 s ; graphe Roslyn 11 566 nœuds / 90 143 relations /
  0 diagnostic.

## Limites

La preuve n’exécute ni Blender, ni GPU génératif, ni modèle distant. Elle qualifie les recettes,
politiques, plans, fichiers et rollbacks canoniques sur des fournisseurs déterministes locaux ; la
qualité artistique d’une génération externe reste une qualification séparée.
