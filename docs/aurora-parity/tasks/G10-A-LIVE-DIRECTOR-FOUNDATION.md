# G10-A — Fondation Live Director

## Exigences

`ORF-DM-001`, `ORF-DM-002`, `ORF-DM-003`, `ORF-DM-004`, `ORF-DM-005`.

## Livré

- kit `Forge.LiveDirector` séparé du moteur graphique et construit sur `Forge.Network`;
- authentification GM par rôle administrateur, second facteur optionnel, expiration, révocation et audit;
- connexion d'un client Director distinct avec identité et session attribuées;
- catalogue live filtré par permission, allowlist serveur, zone, type, coût, texte et package;
- création single/formation/scatter prévisualisable puis atomique, bornée à 64 objets, révisionnée et anti-rejeu;
- chooser déterministe par zone, type, nom, tag, joueur, état et diagnostics.

## Critères de passage

- tests ciblés `Forge.LiveDirector.Tests` verts;
- tracker G10-A à cinq lignes `Implemented`, preuves et limites explicites;
- solution complète compilable et graphe déterministe frais.

## Limites reportées

La surface visuelle dédiée, les coordonnées de formation, l'undo sûr et le branchement sur une session Network Beta complète seront traités après stabilisation des six lots de contrats G10.
