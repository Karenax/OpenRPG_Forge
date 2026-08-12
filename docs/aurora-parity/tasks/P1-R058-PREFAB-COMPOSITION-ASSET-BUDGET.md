# P1 R058 — Variantes, compositions et budgets de palette

## Périmètre

- `ORF-PAL-013` — variantes héritées avec overrides de composants, matériaux, stats et scripts,
  sans duplication complète du blueprint de base.
- `ORF-PAL-014` — prefabs composés avec hiérarchie, sockets, règles, paramètres et scripts
  d’assemblage, plus collections auteur persistantes et placement groupé.
- `ORF-PAL-018` — métriques polycount, matériaux, textures, mémoire, animations, LOD et collision,
  comparées à des budgets explicites par plateforme.
- Dépendances traversantes minimales : `BUILD` pour manifeste/licence/hash et préflight, `AREA` pour
  la résolution/placement d’une composition, `QA` pour l’évaluation des budgets.
- Un seul lot actif ; P2, AV7–AV10, commit et push restent hors périmètre.

## Audit intégral des critères source

| Critère source | État avant R058 | Limite réelle à fermer et preuve attendue |
|---|---|---|
| Domaine canonique avant moteur | Partiel | profils versionnés de variante, composition et budget sur le blueprint |
| Variante héritée sans duplication | Partiel | base référencée par UUID et deltas typés composant/matériau/stat/script |
| Composition hiérarchique | Partiel | nœuds UUID, parents, sockets, transforms, règles, paramètres et scripts |
| Collections et placement groupé | Partiel | collections auteur persistantes et plan de placement atomique dans une zone |
| Budget moderne par plateforme | Partiel | métriques 7 axes, limites par cible, états pass/warn/fail et diagnostic stable |
| Données minimales et gouvernance | Présent à traverser | schéma, provenance, manifeste, SPDX, SHA-256 et budget validés ensemble |
| Interface auteur sans JSON | Manquant | écran visible avec états vide/erreur/résumé et commande unique |
| Commande, dry-run et diff | Manquant | preview/apply atomiques, patch sémantique, historique undo/redo |
| Persistance exacte | Manquant | round-trip ancien format vide et profil configuré sans perte |
| ForgeScript | Manquant | lecture par handle sûr et résolution runtime pure, mutation par commande seulement |
| MCP et agents | Manquant | lecture filtrée, plan/apply/revert sans shell avec diff |
| Validation pré-build | Partiel | UUID/références/cycles/schema/gouvernance/budgets refusés avec chemin localisé |
| Runtime optionnel/requis | Manquant | absence optionnelle ignorée et profil requis absent diagnostiqué |
| Distribution | Présent à traverser | licence/provenance/hash invalides ou budget bloquant refusés avant écriture |

## Non-objectifs

- téléchargement de modèles, génération IA ou retouche destructive des assets ;
- éditeur visuel de graphe généraliste pour les compositions ;
- streaming distant de collections ou synchronisation multi-utilisateur ;
- métriques GPU réelles spécifiques à un pilote, au-delà du contrat canonique mesuré/importé.

## Validations prévues

- tests Domain/Application/ModuleFormat/MCP ciblés puis suites complètes ;
- smoke Toolset R058 avec preview/apply/undo/redo, persistance, placement et préflight ;
- smoke runtime optionnel/requis et résolution de variante/composition ;
- capture UI R058 inspectée avec variante, hiérarchie et matrice de budgets visibles ;
- tracker/backlog/docs/graphe frais puis `scripts/Verify.ps1` avant R059.

## Résultat de clôture

- Les trois fonctions du lot sont `Tested` individuellement, sans promotion hors R058.
- Les variantes référencent un blueprint de base par UUID et ne stockent que des deltas typés de
  composants, matériaux, statistiques et scripts ; la résolution reste déterministe et bornée.
- Les compositions portent nœuds, parents, sockets, transforms, règles, paramètres et scripts
  d’assemblage ; les collections auteur et le placement groupé utilisent la même transaction.
- Les sept axes de métriques sont comparés à des budgets explicites par plateforme avec états
  conforme, avertissement et bloquant, diagnostics stables et validation avant distribution.
- Domain, Application, sérialisation, UI, ForgeScript, MCP et runtime optionnel/requis convergent
  sur le même profil canonique avec preview/apply/revert, undo/redo et round-trip exact.
- La preuve visuelle inspectée est `docs/aurora-parity/evidence/r058-prefab-composition-budget-ui.png`
  en 1760×900 ; variantes, composition, deux plateformes et résultats de budget y sont visibles.
- Les suites complètes passent à 1 420 tests .NET et 39 tests Python. Le graphe Roslyn est frais et
  déterministe à 6 479 nœuds / 40 525 relations.
- Le portail intégré termine sur `OPENRPG_FORGE_VERIFICATION=PASS` après smokes Toolset/runtime,
  MCP, Blender, assets, packaging, réseau et sauvegarde/reprise.
