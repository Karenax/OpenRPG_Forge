# P0 R018 — provenance, quarantaine et frontière IP

Lot stable : `R018` / `FC-CONTENT-P0-03` — quatre fonctions, aucune fonction hors lot.

## Périmètre et sources auditées

- `ORF-CONT-023` — Provenance d’asset.
- `ORF-CONT-024` — Quarantaine d’import.
- `ORF-CONT-030` — Credits and notices generation, déjà `Tested` et seulement reconfirmée.
- `ORF-CONT-031` — IP boundary enforcement.
- Source immuable relue intégralement : sections correspondantes de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/22_CONTENT.md`.

## Matrice d’audit avant implémentation

| Critère source | Présent | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|
| SPDX, auteur, URL source, permission, modifications, IA et hashes | Catalogue, manifestes, blueprints et reçus portent déjà licence, hash, source, auteur et provenance partielle | Aucun manifeste asset unifié ni preuve de permission/IA structurée | Domaine, persistance, UI, distribution et tests hostiles |
| Staging confiné, limites, scan malware, parsers durcis, approbation humaine | Import atomique et chemins confinés existants | L’import écrit avant revue, sans registre de quarantaine ni adapter de scan | Service de quarantaine réel, faux scanner testé, approbation explicite |
| NOTICE, licences UI, attribution par asset, échec si licence absente | Agrégation de crédits, NOTICE et blocage Shipping déjà `Tested` | Reconfirmer que les nouvelles décisions de gouvernance alimentent le même garde-fou | Tests existants plus scénario R018 |
| Refus NWN/CEP propriétaire, compatibilité isolée, revue légale | Politique documentaire seulement | Aucun contrat exécutable ni blocage distribution dédié | Politique IP canonique, diagnostics stables, test d’archive refusée |

## Contrat minimal de fermeture

- registre canonique par asset avec UUID, schéma, manifeste de package, SPDX, hash, provenance de
  modification, auteur, URL, preuve de permission, modifications et politique IA bornée ;
- quarantaine avant import canonique, chemin confiné, types et tailles bornés, adapter de scan,
  résultat de parser et approbation/rejet humains persistants ;
- frontière IP explicite qui refuse toute redistribution NWN/CEP propriétaire, isole les travaux de
  compatibilité et exige une référence de revue légale ;
- commandes dry-run/apply, diff, undo/redo, persistance exacte, UI avec états vide/erreur, handles
  ForgeScript sûrs et ressources/plans MCP filtrés ;
- distribution bloquée tant que provenance, licence, quarantaine ou frontière IP ne sont pas conformes.

## Risques et tests exacts

- sécurité : traversal, lien symbolique, type réel incohérent, dépassement, hash divergent et scan positif ;
- gouvernance : permission absente, divulgation IA incomplète, licence inconnue, NWN/CEP ou revue légale absente ;
- déterminisme : tri ordinal, UUID/hashes stables et JSON UTF-8 sans BOM atomique ;
- compatibilité : absence optionnelle ignorée au runtime, registre requis absent diagnostiqué ;
- tests ciblés Domain, Application, ModuleFormat et MCP, smokes Toolset/runtime, capture visible, puis
  tracker/backlog/docs/graphe et `scripts/Verify.ps1` avant R019.

## Non-objectifs

- service antivirus propriétaire ou cloud, acquisition distante et publication marketplace ;
- P1/P2, AV7–AV10, compatibilité ou redistribution de ressources propriétaires ;
- commit ou push Git.

## Preuves de fermeture

- `ORF-CONT-023`, `ORF-CONT-024` et `ORF-CONT-031` sont promues individuellement à `Tested` ;
  `ORF-CONT-030`, déjà `Tested`, est reconfirmée par les tests Shipping et NOTICE existants.
- Le registre `.forge/assets/asset-governance.json` est atomique, déterministe, UTF-8 sans BOM et porte
  UUID, schéma, manifeste package, SPDX, hash, auteur, URL, permission, modifications, divulgation IA,
  budget, décision de quarantaine et frontière IP.
- La quarantaine refuse traversal, reparse point, taille, type réel, hash, parser ou scanner hostiles et ne
  crée jamais `assets/imported` avant une décision humaine explicite.
- Deux tests d’export Shipping démontrent qu’une quarantaine en attente et une origine NWN/CEP propriétaire
  échouent avant toute écriture d’archive ; la génération NOTICE demeure couverte par le même scénario Shipping.
- Tests ciblés verts : Domain 3/3, Application 2/2, ModuleFormat 7/7 et MCP 1/1 ; build solution 27 projets,
  zéro avertissement et zéro erreur.
- Marqueurs intégrés : `OPENRPG_FORGE_TOOLSET_R018_ASSET_GOVERNANCE_PASS` et
  `OPENRPG_FORGE_RUNTIME_R018_ASSET_GOVERNANCE_PASS`.
- Preuve visible inspectée : `docs/aurora-parity/evidence/r018-asset-governance-ui.png` en 1240×780,
  montrant licence, hash, provenance, quarantaine, frontière IP et distribution bloquée avant validation.
- Portail global : tracker, backlog, documentation et graphe régénérés, puis `scripts/Verify.ps1` exécuté
  avant l’ouverture de R019.
