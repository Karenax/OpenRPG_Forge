# P1 R059 — Productivité et profils de la coque Creator

## Périmètre

- `ORF-SHELL-008` — filtres d’affichage couvrant calques, volumes, types, tags, auteur, état,
  isolation, mode fantôme et profils mémorisés.
- `ORF-SHELL-009` — placement répété avec pinceau, répétition, distributions, alignement de surface,
  scatter déterministe et annulation groupée.
- `ORF-SHELL-012` — onglets multi-zones, vue partagée, comparaison, copie inter-zones, contexte actif
  explicite et protection contre la mutation du mauvais document.
- `ORF-SHELL-021` — documentation intégrée structurée, exemples ForgeScript, propriétés,
  dépendances et aide assistée obligatoirement sourcée.
- `ORF-SHELL-023` — sept profils Builder, Writer, Scripter, Artist, GM, Tester et Beginner avec
  panneaux et permissions adaptés, persistants hors contenu canonique du module.
- Un seul lot actif ; P2, AV7–AV10, commit et push restent hors périmètre.

## Audit intégral des critères source

| Critère source | État avant R059 | Limite réelle à fermer et preuve attendue |
|---|---|---|
| Domaine canonique UUID/schéma/provenance | Partiel | remplacer les booléens implicites par des profils versionnés validés |
| Filtres type/tag/auteur/état/isolation/ghost | Présent | conserver le comportement et ajouter calques nommés et familles de volumes |
| Placement répété atomique | Présent | conserver ligne/grille/scatter et ajouter pinceau, alignement et plan typé persistant |
| Onglets et contexte actif | Présent | conserver les huit onglets et ajouter split view et copie inter-zones protégée |
| Comparaison | Partiel | exposer deux documents simultanément avec état vide/erreur et résumé déterministe |
| Aide locale | Partiel | catalogue recherché par contexte, propriétés, exemples et liens de dépendance |
| Aide assistée | Manquant | réponse locale bornée portant citations ; toute réponse non sourcée est refusée |
| Sept profils métier | Partiel | panneaux et permissions explicites, sélection et collection de profils persistantes |
| Interface sans JSON | Présent à étendre | contrôles visibles pour chaque nouvelle donnée, preview et erreurs localisées |
| Commande et historique | Partiel | même résultat UI/commande, dry-run, patch sémantique, undo/redo canonique |
| Persistance exacte | Partiel | migration de l’ancien profil, catalogue multi-profils et round-trip sans perte |
| MCP et agents | Manquant | lecture filtrée du profil/contexte/aide et plan/apply/revert sans pilotage UI |
| Validation pré-build | Partiel | références de zones/calques, rôles, permissions et citations invalides refusés |
| Runtime optionnel/requis | Partiel | absence optionnelle ignorée et profil requis absent diagnostiqué |

## Non-objectifs

- collaboration réseau simultanée sur deux documents ou verrou distribué ;
- génération distante de documentation, navigation web ou exécution arbitraire de scripts ;
- peinture terrain, édition de maillage ou rendu de deux simulations runtime complètes ;
- fonctions P2, raccourcis avancés et lots Asset/Map optionnels.

## Validations prévues

- tests Domain/Application/ModuleFormat/MCP ciblés puis suites complètes ;
- smoke Toolset R059 couvrant filtres, placement, split/copie, aide, profils, historique et persistance ;
- smoke runtime optionnel/requis sans modifier le module de démonstration ;
- capture UI R059 inspectée avec les cinq capacités et leurs états explicites ;
- tracker/backlog/docs/graphe frais puis `scripts/Verify.ps1` avant R060.

## Résultat de clôture

- Les cinq fonctions du lot sont `Tested` individuellement, sans promotion hors R059.
- Le filtre persistant combine calques nommés, familles de volumes, types, tags, auteur, état,
  isolation et mode fantôme, puis applique exactement ce profil au viewport.
- Le placement couvre ligne, grille, scatter déterministe, pinceau et trait continu avec alignement
  grille, surface ou socket ; une opération entière reste une transaction annulable.
- Le contexte multi-zones conserve huit onglets, une zone active, une comparaison protégée, deux
  viewports 3D simultanés et une copie inter-zones par clipboard sémantique sans mutation source.
- L’aide locale recherche propriétés, exemples ForgeScript et dépendances ; la synthèse assistée
  sans citation est refusée. Les sept profils métier contraignent panneaux et permissions et sont
  persistés dans `.forge/editor-profiles.json`, hors module exporté.
- Domain, Application, sérialisation, UI, MCP et runtime optionnel/requis convergent vers les mêmes
  profils UUID/schéma/provenance avec preview/apply/revert, undo/redo et round-trip exact.
- La preuve visuelle inspectée est `docs/aurora-parity/evidence/r059-shell-productivity-ui.png` en
  1760×900 ; les cinq capacités, leurs états et leurs actions y sont visibles sans contenu coupé.
- Les suites complètes passent à 1 425 tests .NET et 39 tests Python. Le graphe Roslyn est frais et
  déterministe à 6 521 nœuds / 40 857 relations.
- Le portail intégré termine sur `OPENRPG_FORGE_VERIFICATION=PASS` après smokes Toolset/runtime,
  MCP, Blender, assets, packaging, réseau et sauvegarde/reprise.
