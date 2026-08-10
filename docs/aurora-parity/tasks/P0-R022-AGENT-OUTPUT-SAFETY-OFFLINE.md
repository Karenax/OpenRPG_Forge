# P0 R022 — sorties sûres et indépendance hors ligne

Lot stable : `R022` / `FC-AI-P0-03` — quatre fonctions, aucune fonction hors lot.

## Périmètre et sources auditées

- `ORF-AI-023` — Pas d'exécution Python libre, déjà `Tested`, preuve réauditée sans promotion.
- `ORF-AI-031` — Résultats structurés.
- `ORF-AI-032` — Protection contre prompt injection.
- `ORF-AI-033` — Mode hors ligne.
- Source immuable relue intégralement : sections correspondantes de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/24_AI.md`.

## Matrice d'audit avant implémentation

| Critère source | Présent | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|
| Aucun Python/exec/eval/shell public et opérations Blender allowlistées | Politique, store, UI, worker et MCP G12-L déjà `Tested` | Aucune limite rouverte ; vérifier que le nouveau pipeline ne contourne pas cette frontière | Régression domaine/worker/MCP et catalogue sémantique sans outil arbitraire |
| JSON Schema ou appels d'outil, validation, réparation syntaxique bornée, refus ambigu | Les prompts portent un schéma JSON et les transports demandent JSON | Le contenu retourné n'est pas validé contre le schéma ; aucune réparation bornée ni détection de mutation ambiguë | Validateur déterministe de sous-ensemble JSON Schema, retrait borné d'une seule fence, tool calls allowlistés, ambiguïté refusée |
| Contenu projet traité comme données avec provenance et aucune escalade cachée | Contexte R020 étiqueté, outils/permissions R021 bornés | Le rendu de prompt interpole encore directement les valeurs projet ; aucune frontière explicite ni diagnostic d'injection | Enveloppes de données encodées et étiquetées, scan de signaux hostiles, permission non modifiable par contenu et confirmation risquée |
| Core manuel complet, providers locaux optionnels, metadata IA non bloquante | Provider déterministe offline par défaut ; profil absent migre ; build/runtime ne requièrent pas le profil | L'indépendance n'est pas un contrat canonique ni une preuve intégrée explicite | Politique offline v1, état UI/readiness, test projet sans metadata/réseau et runtime optionnel/requis |
| Configuration UI, round-trip, diagnostics, même commande, undo/redo | Atelier assistant et commande de profil déjà canoniques | Les nouvelles politiques ne sont pas configurables/persistées ni fingerprintées | Contrôles visibles, legacy sûr, preview/apply/undo/redo, capture et smokes |

## Contrat minimal de fermeture

- profil de sécurité v1 attaché au workflow avec politiques de sortie structurée, frontière
  d'injection et mode hors ligne ;
- validateur applicatif sans dépendance réseau : taille bornée, JSON strict, réparation limitée à
  une fence Markdown, sous-ensemble de schéma explicite, identités et outils vérifiés ;
- contenu non fiable enveloppé comme données JSON échappées avec clé et provenance, signaux
  d'injection signalés sans devenir des instructions ;
- aucune élévation de permission depuis la réponse ; mutation ambiguë ou outil absent de l'allowlist
  refusé avant toute transaction ;
- mode offline par défaut et réseau désactivé, toutes les fonctions manuelles conservées, metadata IA
  optionnelle sans blocage du build/runtime ;
- configuration via le profil/commande existants, round-trip exact, historique, UI et MCP de lecture/
  validation, smokes et preuve visible.

## Risques et tests exacts

- JSON tronqué, fence multiple, commentaire, propriété requise absente, type incorrect, clé inconnue,
  dépassement de taille et tool call hostile ;
- données contenant fermeture de balise, « ignore previous instructions », fausse consigne système,
  secret ou demande shell ;
- plan/transaction/permission falsifiés, semantic diff non vide sans appels d'outil ;
- profil absent, réseau refusé, provider local optionnel, extension offline optionnelle/requise ;
- ancien profil sans sécurité migré par défaut, JSON déterministe, commande fingerprintée ;
- tests Domain/Application/ModuleFormat/MCP, régression G12-L, marqueurs Toolset/runtime, capture et portail.

## Non-objectifs

- nouveau fournisseur P1, exécution d'outil arbitraire, réponse en langage libre, apprentissage/RAG ;
- P1/P2, AV7–AV10, commit ou push Git.

## Fermeture et preuves

Les critères source ont été relus intégralement. `ORF-AI-023`, déjà `Tested`, a été réaudité sans
promotion ; `ORF-AI-031`, `ORF-AI-032` et `ORF-AI-033` ont été promus individuellement à `Tested`.

- sorties : JSON Schema validé, taille et appels d'outil bornés, une seule fence réparable,
  propriétés inconnues et mutation ambiguë refusées, plan/transaction/permission immuables ;
- injection : valeurs projet encodées et étiquetées par clé/provenance, frontière système explicite,
  signaux hostiles diagnostiqués, allowlist d'outils et gouvernance R021 conservées ;
- hors ligne : édition manuelle, build et runtime indépendants des métadonnées IA, réseau non requis,
  provider déterministe et migration sûre d'un profil absent ou antérieur ;
- Python libre : politique, commande, persistance, requête worker, worker Python et MCP réexécutés,
  sans nouveau point d'exécution arbitraire.

Preuves reproductibles :

- tests R022 ciblés : Domain 2/2, Application 6/6, ModuleFormat 7/7 et MCP 1/1 ;
- régression `ORF-AI-023` : Domain 3/3, Application 3/3, ModuleFormat 7/7,
  MCP 1/1 et worker Python 18/18 ;
- builds Toolset, Runtime et MCP Host sans avertissement ni erreur ;
- smokes intégrés `OPENRPG_FORGE_TOOLSET_R022_AGENT_SAFETY_PASS` et
  `OPENRPG_FORGE_RUNTIME_R022_AGENT_SAFETY_PASS` ;
- capture inspectée : `docs/aurora-parity/evidence/r022-agent-output-safety-offline-ui.png`,
  1600×960, protections, limites, contexte, état vide et actions visibles ;
- tracker/backlog/docs régénérés : 300 fonctions `Tested`, 98 P0 non closes dans 28 lots ;
- graphe déterministe : 5 833 nœuds, 35 065 relations, zéro diagnostic ;
- portail intégré : 27 projets construits sans avertissement ni erreur, 1 194 tests .NET,
  6 tests Python généraux, 33 tests asset et `OPENRPG_FORGE_VERIFICATION=PASS`.
