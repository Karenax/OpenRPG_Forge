# R101 — Acceptation interop éditeur/MCP ForgeScript

## Périmètre

`R101 / FC-SCRIPT-P1-05` ferme exactement une fonction P1 :

- `ORF-SCR-041` — interop éditeur/MCP.

Les critères source ont été lus intégralement dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/18_SCRIPT.md`, lignes 3117–3188.

## Réduction des dépendances

AI et les outils d’auteur consomment la même façade MCP versionnée ; le compilateur, la sandbox et
le document ForgeScript restent les autorités techniques. R101 n’ajoute ni second éditeur, ni voie
de mutation directe, ni exécution arbitraire d’agent : sources, bindings et configuration passent
par les transactions du service d’interop existant.

## Matrice d’écarts auditée

| Exigence source | Présent avant R101 | Limite réelle fermée par R101 | Preuve |
|---|---|---|---|
| `get_api`, create/update, compile, diagnostics, tests et assignation d’événement | Façade compacte et trois tests | Contrats auteur incomplets, redo absent et diagnostics appauvris | 6 tests `ForgeScriptInteropServiceTests`, Toolset et MCP host |
| UUID, schéma, provenance, API et binding canonique | Identité portée partiellement par sources et plans | Aucun profil interop validé/persisté ni contrôle global d’unicité | profil, validateur, historique, store et 2 tests dédiés |
| Champs requis, références, compilation, API, quotas et tests | Validation locale des plans | Quotas de sources/octets/tests et diagnostic localisé incomplets | tests invalides, doublons, dépassement et récupération |
| Preview/apply/reject, Undo/Redo et roundtrip | Apply/revert révisionnés | Pas de redo ni snapshot exact de l’espace interop | tests de plans source/binding et snapshot JSON |
| Parité éditeur/commande, états vide/erreur, console et diagnostics cliquables | Éditeur ForgeScript existant | Aucun panneau ni preuve visuelle propre à l’interop | capture R101 1680×1000 et marqueur Toolset |
| Extension runtime optionnelle/requise et handle obsolète | Aucun adaptateur interop | Fallback optionnel, diagnostic requis et rejet stale absents | 2 tests adaptateur et marqueur runtime |
| Sandbox nominale, interruption sur quota et reprise | Exécution native commune | Aucun parcours d’abus/reprise prouvé pour l’interop | test MCP de quota puis exécution saine |

## Acceptation partagée

- `ForgeScriptInteropProfile` conserve UUID stable, schéma v1, provenance, version API, binding et
  quotas ; le validateur produit des codes, chemins et UUID localisés.
- `ForgeScriptInteropProfileHistory` est la frontière de configuration preview/apply/reject avec
  Undo/Redo ; `ForgeScriptInteropProfileStore` effectue un roundtrip JSON atomique.
- Sources et bindings sont planifiés puis appliqués avec révision de base ; Undo/Redo et le snapshot
  complet restaurent exactement sources, bindings, profil et révision.
- MCP expose les outils versionnés sans accès fichier, réseau, processus, objet moteur ou .NET
  arbitraire ; compilation et tests restent dans la sandbox.
- Une extension absente est ignorée lorsqu’elle est optionnelle et produit
  `FORGESCRIPT_INTEROP_REQUIRED_MISSING` lorsqu’elle est requise ; un handle obsolète est refusé.
- Un cas nominal compile et s’exécute ; un script abusif est interrompu par quota, puis une exécution
  saine réussit dans le même runtime.

## Changements bornés livrés

- Profil interop canonique, validation, historique transactionnel et stockage atomique.
- Validation des quotas de sources, octets et tests, UUID uniques et diagnostics enrichis.
- Redo des plans source/binding et snapshot exact de l’espace d’interop.
- Deux outils MCP de redo complétant la façade commune.
- Résumés interop dans l’éditeur ForgeScript et panneau runtime accessible avec états vide/erreur.
- Adaptateur runtime unique réutilisant le compilateur et la sandbox natifs.

## Preuves reproductibles

- Tests ciblés : 2/2 profil, 6/6 service MCP et 2/2 adaptateur runtime.
- Marqueur Toolset : `OPENRPG_FORGE_TOOLSET_R101_SCRIPT_INTEROP_PASS`.
- Marqueur runtime : `OPENRPG_FORGE_RUNTIME_R101_SCRIPT_INTEROP_PASS`.
- Marqueur visuel : `OPENRPG_FORGE_R101_SCRIPT_INTEROP_UI_CAPTURE_PASS`.
- Preuve visuelle : `docs/aurora-parity/evidence/r101-script-interop-ui.png`, inspectée à 1680×1000.
  Sources, identité, budgets, API, six outils, transactions, événements, console et diagnostic sont
  lisibles sans chevauchement.
- Transcript du portail intégré : `.artifacts/r101-full-verify-final.log`, avec 1 715 tests .NET,
  39 tests Python et `OPENRPG_FORGE_VERIFICATION=PASS`. Le portail valide les marqueurs Toolset et
  runtime R101 exacts dans ses journaux de scénario.

## Non-objectifs

- `FC-MULTI-P1-01` et les lots P1 suivants avant le portail R101 vert ;
- toutes les fonctions P2 et Asset/Map AV7–AV10 ;
- commit ou push.
