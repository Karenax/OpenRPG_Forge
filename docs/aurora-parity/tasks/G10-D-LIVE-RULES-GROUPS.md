# G10-D — Règles et groupes live

## Exigences

`ORF-DM-017`, `ORF-DM-019`, `ORF-DM-020`, `ORF-DM-021`, `ORF-DM-023`.

## Livré

- ajustement XP par service de règles injecté avec aperçu des niveaux;
- verrouillage, déverrouillage, ouverture forcée, réparation et reliaison de portail;
- simulation d'entrée/sortie de trigger avec acteur, payload, dry-run et trace de script;
- sélection multiple recherchable et bornée;
- déplacement en ligne, coin, cercle ou libre avec aperçu de chemin et rapport d'échec partiel.

## Preuves

- `LiveDirectorRulesGroupTests`: 3 scénarios;
- `Forge.LiveDirector.Tests`: 14/14;
- tracker G10-D: cinq lignes `Implemented` avec adaptateurs runtime explicitement reportés.
