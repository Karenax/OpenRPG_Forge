# G9-B — Personnages, groupes, combat et instances

## Exigences

`ORF-MP-006`, `ORF-MP-007`, `ORF-MP-008`, `ORF-MP-009`, `ORF-MP-010`.

## Livré

- registre de personnages locaux ou serveur, propriétaire et fingerprint obligatoires;
- groupes autoritaires avec chef, adhésion révisionnée et requête d'alliance;
- décision serveur PvP/friendly fire avec audit stable;
- modèles d'instances persistantes, de session ou éphémères;
- création dynamique bornée par gabarit et entrée isolée par groupe.

## Preuves ciblées

`Forge.Network.Tests` contient 13 tests, dont six couvrent directement G9-B : deux scopes de personnage, permissions/revision de groupe, politique de dégâts, isolation/capacité de zone et diagnostics localisés.

