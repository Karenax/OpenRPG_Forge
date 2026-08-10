# P0 R021 — transactions et gouvernance des agents

Lot stable : `R021` / `FC-AI-P0-02` — cinq fonctions, aucune fonction hors lot.

## Périmètre et sources auditées

- `ORF-AI-008` — Transaction atomique.
- `ORF-AI-009` — Diff sémantique.
- `ORF-AI-010` — Niveaux d’autorisation.
- `ORF-AI-011` — Audit IA.
- `ORF-AI-013` — Budget et confidentialité.
- Source immuable relue intégralement : sections correspondantes de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/24_AI.md`.

## Matrice d’audit avant implémentation

| Critère source | Présent | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|
| Begin/preview/commit/rollback atomiques, révision optimiste, limites et audit | Les plans spécialisés MCP et `ProjectCommandHistory` savent prévisualiser, appliquer et annuler une commande canonique | Pas de cycle transversal identifié par transaction/plan ni de batch agent prouvé sans état partiel | Transaction v1 bornée, prévisualisée avant commit, commit canonique unique et rollback de la dernière révision |
| Diff ajouté/supprimé/modifié pour objets, références, états, assets et fichiers générés | `ProjectPatch` et le dry-run R020 fournissent une ligne descriptive | Pas de diff structuré par catégorie ni de hash avant/après | Entrées de diff UUID ordonnées, catégories et changements before/after avec hashes stables |
| Lecture, proposition, mutation confirmée et autonomie bornée | L’enum existant couvre lecture, proposition, configuration et plan approuvé | Pas de niveau autonome ni de bornes outil/scope/compte/chemin/temps communes | Politique canonique refusant par défaut et autorisation évaluée sur toutes les bornes |
| Client, provider/modèle optionnels, utilisateur, plan, commandes, hashes, tests, provenance et rollback ID | Les audits spécialisés enregistrent action/révision/date | Pas de journal IA transversal complet, exportable et sans secret | Entrée d’audit immuable, ordonnée et expurgée, créée à chaque étape, succès comme refus |
| Politique provider, plafonds tokens/coût, redaction, classification, opt-in et aucun secret loggé | Le profil provider possède limites/coûts et opt-in de partage ; le nom de variable du secret est persisté, jamais sa valeur | Pas de classification projet, budget par requête, champs sensibles/redaction ni validation combinée | Politique v1 par provider, classification, consentement, budget calculé et redaction déterministe |
| Configuration UI, round-trip, diagnostic, même commande, undo/redo | L’atelier assistant R020 persiste déjà le profil par commande avec preview/undo/redo | Les nouvelles politiques ne sont ni configurables ni incluses dans le fingerprint/persistence/UI | Extension optionnelle sûre du profil, contrôles visibles, round-trip exact et tests de commande/historique |

## Contrat minimal de fermeture

- gouvernance canonique versionnée attachée à `AgentSemanticWorkflow` : transaction, diff,
  autorisation, audit, budgets et confidentialité ;
- quatre niveaux d’autorisation, le niveau autonome restant désactivé par défaut et soumis à des
  limites cumulatives d’outil, cible, nombre, chemin et durée ;
- cycle MCP `begin → preview → commit → rollback` à révision optimiste pour un batch sémantique
  exécutable ; une erreur de commande ou de sauvegarde ne laisse aucun état partiel ;
- première exécution transversale bornée de `project.set_variable`, agrégée en une commande de
  domaine atomique ; les autres commandes conservent leurs API métier spécialisées et sont refusées
  explicitement par ce coordinateur tant qu’elles n’ont pas d’adaptateur atomique ;
- diff structuré déterministe, hashes avant/après, audit complet et expurgé, rollback ID ;
- budget et confidentialité évalués avant toute transaction, sans persister ni journaliser la valeur
  d’un secret ;
- UI de gouvernance visible, états vide/erreur, plan/diff, provenance/coût, appliquer/annuler,
  persistance exacte et historique undo/redo.

## Risques et tests exacts

- transaction : plan absent/non prévisualisé, révision obsolète, double commit, rollback non terminal,
  commande invalide au milieu d’un batch et échec de sauvegarde ;
- autorisation : outil/cible/chemin hors allowlist, compte ou durée dépassé, confirmation manquante,
  autonomie désactivée ;
- confidentialité : cloud sans opt-in, classification interdite, budget tokens/coût dépassé, clé ou
  valeur sensible présente dans arguments/audit ;
- audit/diff : ordre instable, UUID/hash vide, catégorie inconnue, rollback non corrélé ;
- persistance : ancien profil sans gouvernance migré par défaut, JSON déterministe UTF-8 sans BOM ;
- tests Domain/Application/ModuleFormat/MCP, marqueurs Toolset/runtime, capture inspectée et portail.

## Non-objectifs

- fournisseurs interchangeables P1, prompt injection et mode hors ligne de R022 ;
- adaptateur générique pour commandes arbitraires, Python/shell, P1/P2, AV7–AV10, commit ou push Git.

## Fermeture et preuves

Les cinq fonctions du lot ont été auditées et promues individuellement à `Tested` :

- `ORF-AI-008` : cycle `begin → preview → commit → rollback` à révision optimiste, batch agrégé
  en une commande canonique et absence d'état partiel sur une commande hostile ;
- `ORF-AI-009` : diff v1 UUID et hashé, ordonné, typé par catégorie et changement, avec valeurs
  sensibles expurgées ;
- `ORF-AI-010` : lecture, proposition, mutation confirmée et autonomie bornée, avec refus par défaut
  sur permission, outil, cible, compte, chemin ou durée ;
- `ORF-AI-011` : audit persistant client/utilisateur/provider/modèle/plan/commandes/hashes/tests/
  provenance/rollback, sans arguments ni secrets ;
- `ORF-AI-013` : politiques provider, plafonds tokens/coût, classification projet, opt-in cloud,
  redaction et migration sûre d'un profil antérieur.

Preuves reproductibles :

- tests ciblés : Domain 10/10, Application 3/3, ModuleFormat 6/6 et MCP 2/2 ;
- build MCP Host sans avertissement ni erreur ;
- smokes Toolset et runtime : `OPENRPG_FORGE_TOOLSET_R021_AGENT_GOVERNANCE_PASS` et
  `OPENRPG_FORGE_RUNTIME_R021_AGENT_GOVERNANCE_PASS` ;
- capture inspectée : `docs/aurora-parity/evidence/r021-agent-transaction-governance-ui.png`,
  1600×960, gouvernance complète, état vide et actions visibles sans rognage ;
- tracker/backlog/docs régénérés : 297 fonctions `Tested`, 29 lots P0 ouverts ;
- graphe déterministe : 5 812 nœuds, 34 899 relations, zéro diagnostic ;
- portail intégré : 27 projets construits sans avertissement ni erreur, 1 184 tests .NET,
  6 tests Python généraux, 33 tests asset et `OPENRPG_FORGE_VERIFICATION=PASS`.
