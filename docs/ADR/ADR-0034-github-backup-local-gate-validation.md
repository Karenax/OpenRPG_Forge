# ADR-0034 — GitHub comme sauvegarde distante, validation locale des gates

Date : 22 octobre 2026. Statut : accepté par instruction explicite du propriétaire.

## Décision

Le propriétaire demande : « non mais il faut arreter les controle de gith on
s'en fou, c'est juste une sauvegarde distante ».

GitHub sert de sauvegarde distante et de dépôt de collaboration. Les contrôles
GitHub Actions ne sont plus un critère bloquant de qualification ou de clôture
des gates. Leur suivi et leurs relances automatiques sont arrêtés. Cette décision
remplace l'attente de décision CI de Gate 5 ; elle ne dépend pas de l'ancienne
exception ADR-0006 limitée aux gates 0–4.

Les validations locales restent exigées : tests, formatage, analyses statiques,
constructions, parcours Studio/jeu et preuves proportionnées aux critères.
Aucun échec réel n'est renommé en succès et aucun test local n'est supprimé.
Les résultats distants historiques restent conservés avec leur verdict exact.

## Application

Le workflow distant `Rust gate` (ID `346133112`) est désactivé et son état
`disabled_manually` est vérifié. La seule définition de workflow du dépôt est
concernée. Dans le checkout, ses déclencheurs `push`/`pull_request` sont remplacés
par `workflow_dispatch` : une sauvegarde Git future ne programme plus de contrôle
automatique. Les tâches sont conservées pour leur historique ; aucune exécution
manuelle n'est demandée. Ce changement local n'autorise ni commit ni push.

## Gate 5

La qualification locale des treize domaines et R5-P01/P02 est rapprochée dans
[le bilan du 21 octobre](../evidence/gate5/GATE5_LOCAL_QUALIFICATION_20261021.md).
La réserve administrative de CI est levée par cette décision. La clôture locale
du 22 octobre est consignée dans
[le rapport final](../evidence/gate5/GATE5_CLOSURE_20261022.md), avec archive
vérifiée et limites maintenues. Aucune acceptation esthétique, validation Linux,
Windows vierge ou distribution publique n'est déduite de cette décision.
Gate 6 et la gate bonus restent non ouvertes.
