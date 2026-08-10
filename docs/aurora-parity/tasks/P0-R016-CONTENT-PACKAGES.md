# P0 R016 — packages de contenu

Lot stable : `R016` / `FC-CONTENT-P0-01` — cinq fonctions, aucune fonction hors lot.

## Périmètre et sources auditées

- `ORF-CONT-001` — Package de contenu versionné.
- `ORF-CONT-002` — Ordre de priorité.
- `ORF-CONT-003` — Dépendances.
- `ORF-CONT-006` — Sources textuelles.
- `ORF-CONT-007` — Compilation de package.
- Source immuable relue intégralement :
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/22_CONTENT.md`.
- Dépendances réévaluées : R001–R002 fournissent build reproductible, CLI et gouvernance ; R012–R015
  fournissent compilation ForgeScript, sandbox et permissions. Les fonctions CONTENT P1/P2 restent hors lot.

## Matrice d’audit avant implémentation

| Critère source | Présent | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|
| Package/version/namespace/licence/hash | Manifest `.orpmod`, SemVer et conformité distribution | Aucun catalogue canonique de packages de contenu | Tests domaine et round-trip projet |
| Priorité/override/conflit/scellé | Fusion de tables et ordre déterministe générique | Aucun graphe explicite ni prévisualisation de résolution | Tests conflit, priorité et ressource scellée |
| Dépendances required/optional/peer | Dépendances module tierces | Aucun constraint resolver ni lockfile hors ligne | Tests resolver et lockfile déterministe |
| Sources textuelles Git-friendly | Projet/aires JSON et scripts texte | Aucun manifeste « une ressource logique par fichier » | Validation chemins/formats/hash et package |
| `forge pack` complet | Validation, hash, compression et ZIP reproductible | Ne compile/résout pas un catalogue de contenu | Tests export invalide, compilation et reproductibilité |
| Auteur/commande/undo | `ContentWorkflowWindow` et `SetContentWorkspaceHandler` transactionnels | Aucun écran package/dépendance/source/preview | Smoke Toolset et capture inspectée |
| ForgeScript/MCP | Services bornés et MCP projet existants | Aucune lecture sûre/filtrée ni dry-run de catalogue | Tests services ForgeScript et MCP |
| Runtime optionnel/requis | Infrastructure runtime générale | Aucune résolution de package au chargement | Smoke runtime avec absence optionnelle/requise |

## Contrat minimal de fermeture

- catalogue canonique UUID/schéma/provenance contenant packages namespace+SemVer, licence SPDX, hash, priorité,
  ressources scellées, dépendances required/optional/peer et sources texte typées avec budget plateforme ;
- validation localisée par codes stables, chemins et UUID, contraintes SemVer, références/cycles, licences, hashes,
  chemins sûrs et unicité « une ressource logique par fichier » ;
- résolution hors ligne déterministe avec lockfile, dépendance optionnelle absente ignorée, requise absente en erreur,
  preview des conflits, priorité explicite et interdiction d’écraser une ressource scellée ;
- compilation avant packaging : validation, résolution, contrôle/import des sources, compilation ForgeScript, hashes,
  manifestes générés en mémoire, compression et `.orpmod` reproductible ;
- interface auteur réutilisant la commande canonique, états vide/erreur, preview, application/undo/redo ;
- lecture ForgeScript par handle opaque et MCP filtré avec mutation dry-run et diff sémantique ;
- tests nominaux, hostiles, round-trip exact, parité commande/UI, runtime et packaging.

## Risques et tests exacts

- Compatibilité des anciens `project.json` : le catalogue absent doit produire un catalogue vide déterministe.
- Sécurité : aucun chemin absolu, traversal, accès fichier ForgeScript ou ressource sans licence/provenance/hash.
- Déterminisme : ordre ordinal, timestamps normalisés, lock/manifest JSON stables et deux exports byte-identiques.
- Tests ciblés : `Forge.Domain.Tests`, `Forge.Application.Tests`, `Forge.ModuleFormat.Tests`,
  `Forge.Scripting.Tests`, `Forge.Mcp.Tests`, puis smokes Toolset/runtime R016.
- Portail final du lot : régénération tracker/backlog/graphe et `scripts/Verify.ps1` avant R017.

## Non-objectifs

- localisation, textures, modèles 3D, navigateur de contenu ou provenance avancée des lots R017–R018 ;
- P1/P2, marketplace distante, téléchargement réseau, AV7–AV10, commit ou push Git.

## Fermeture vérifiée

- Domaine canonique : packages UUID/schéma/provenance, namespace/SemVer, licences et hashes, priorité, ressources
  scellées, dépendances required/optional/peer, sources textuelles et actifs binaires gouvernés.
- Compilation et distribution : résolution hors ligne, lockfile et manifeste déterministes, compilation ForgeScript,
  contrôles de fichiers et de hashes, puis export `.orpmod` byte-identique par la commande canonique `forge pack`.
- Surfaces : éditeur dédié avec états vide/erreur, prévisualisation, application/undo/redo ; lecture ForgeScript par handle
  opaque ; MCP filtré et mutation dry-run sans écriture ; comportement runtime requis/optionnel vérifié.
- Preuve visuelle inspectée : `docs/aurora-parity/evidence/r016-content-packages-ui.png` (1420 × 900).
- Tests ciblés : 14 réussis sur Domain, Application, ModuleFormat et MCP ; compilation solution Debug : 27 projets,
  zéro avertissement et zéro erreur ; fumées Toolset, runtime et CLI réussies.
- Portail intégré avant promotion : 1 141 tests .NET, 6 tests Python généraux, 33 tests pipeline assets,
  graphe Roslyn frais et déterministe (5 629 nœuds, 33 638 relations, zéro diagnostic), marqueur final
  `OPENRPG_FORGE_VERIFICATION=PASS`.
- Promotion limitée aux cinq IDs du lot : `ORF-CONT-001`, `ORF-CONT-002`, `ORF-CONT-003`, `ORF-CONT-006`,
  `ORF-CONT-007`. Aucun P1/P2 ni AV7–AV10 n’a été modifié.
