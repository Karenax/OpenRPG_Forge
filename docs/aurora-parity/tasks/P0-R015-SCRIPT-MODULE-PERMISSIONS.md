# P0 R015 — permissions ForgeScript par module

Lot stable : `R015` / `FC-SCRIPT-P0-04` — une fonction, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-SCR-039` — Permissions par module.
- Source immuable relue intégralement :
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/18_SCRIPT.md#orf-scr-039`.
- Dépendances réévaluées : CONTENT fournit l’identité et le package du module ; MULTI fournit l’autorité
  serveur et la confiance du signataire ; R012–R014 fournissent source, bindings, capacités, sandbox,
  politique d’exécution, SemVer et diagnostics.

## Audit avant implémentation

Présent :

- allowlist globale versionnée, capacités requises par source et refus des capacités hôte ;
- autorité serveur des événements et sandbox bornée ;
- configuration de signature et vérification cryptographique des distributions `.orpmod` ;
- APIs MCP ForgeScript et éditeur transactionnel existants.

Limite réelle :

- aucune politique canonique par UUID de module/kit/événement ;
- aucune intersection deny-by-default entre capacités globales, module, kit et binding ;
- aucune décision runtime liant autorité serveur et confiance de signature ;
- aucune persistance canonique empaquetée, commande preview/apply/undo/redo ou interface dédiée ;
- aucun diagnostic requis/optionnel localisé, test de round-trip exact ou preuve d’abus/récupération.

## Contrat minimal de fermeture

- document canonique UUID/schéma/provenance/API/module avec deny-by-default, enforcement serveur,
  signature requise, exception développement explicite, capacités module, grants kit et bindings événement ;
- validation des identités, références, capacités connues, sous-ensembles module/kit/binding et invariants de sécurité ;
- autorisation runtime pure et déterministe : extension optionnelle absente ignorée sans exécution, requise absente
  diagnostiquée, signature/autorité/capacité refusées par codes stables ;
- service compilation/exécution qui autorise avant compilation et conserve les quotas R014 ;
- stockage atomique sous `scripts/forge-script-permissions.json`, donc inclus dans le package signé ;
- interface auteur sans JSON avec états vide/erreur, capacités, signature, binding, diagnostics, console et commandes
  preview/apply/undo/redo ; MCP expose le contrat et la validation sans exécution hors sandbox ;
- tests nominaux, abusifs, round-trip, parité UI/commande, packaging et reprise saine.

## Fermeture vérifiée

- Statut : `ORF-SCR-039` promu seul de `Researched` à `Tested` après audit intégral des critères source.
- Domaine : manifeste canonique `scripts/forge-script-permissions.json`, UUID/schéma/provenance/API/module,
  refus par défaut, grants par kit et liaisons événement/handler avec intersection stricte des capacités.
- Runtime : autorité serveur et confiance du signataire vérifiées avant compilation ; absence optionnelle ignorée,
  absence requise diagnostiquée, dépassement de quota interrompu et exécution suivante saine.
- Packaging : manifeste validé avant build, identité du module vérifiée et fichier conservé dans le `.orpmod`.
- Auteur : fenêtre dédiée sans JSON, états vide/erreur, capacités explicites, signature, diagnostics, console,
  prévisualisation/application/annulation/rétablissement et test autorité+sandbox.
- MCP : chemin, schéma et invariants de permission publiés par `get_api` ; exécution conservée dans le sandbox.
- Preuve visuelle inspectée : `docs/aurora-parity/evidence/r015-forgescript-module-permissions-ui.png`
  (1280×760, trois colonnes lisibles, aucun rognage).
- Tests ciblés : 57 tests `Forge.Scripting.Tests`, test packaging R015 et 4 tests MCP ForgeScript verts.
- Portail pré-promotion : 1 127 tests .NET, 6 tests Python généraux, 33 tests Python asset, tous les smokes,
  graphe 5 589 nœuds / 33 319 relations / 0 diagnostic et `OPENRPG_FORGE_VERIFICATION=PASS`.

## Non-objectifs

- permissions P1/P2, marketplace, PKI distante, gestion de comptes ou nouvelles capacités réseau/fichier ;
- AV7–AV10, autre lot P0, commit ou push Git.
