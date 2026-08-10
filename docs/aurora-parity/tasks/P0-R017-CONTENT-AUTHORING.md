# P0 R017 — localisation et ressources visuelles

Lot stable : `R017` / `FC-CONTENT-P0-02` — cinq fonctions, aucune fonction hors lot.

## Périmètre et sources auditées

- `ORF-CONT-011` — Localisation.
- `ORF-CONT-013` — Textures.
- `ORF-CONT-014` — Modèles 3D.
- `ORF-CONT-019` — Resource namespaces.
- `ORF-CONT-020` — Content browser.
- Source immuable relue intégralement : sections correspondantes de
  `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/22_CONTENT.md`.
- Dépendances réévaluées : BUILD, EE, PALETTE et QA disposent déjà de parcours testés ; R016 fournit
  packages, manifestes, licences, hashes, priorités et compilation reproductible.

## Matrice d’audit avant implémentation

| Critère source | Présent | Limite réelle à fermer | Preuve attendue |
|---|---|---|---|
| Catalogue de localisation, replis, extraction, pseudo-localisation | Domaine, commande, persistance et atelier existants | ICU pluriel/genre et échange traducteur non canoniques ; ForgeScript/MCP absents | Tests résolution, bundle, handle, MCP et UI |
| PNG/TIFF/EXR/KTX2, PBR, compression, densité, licence | Profil d’import, validation, plan/cache, reçu et UI existants | Preuve groupée incomplète et absence d’identité namespacée/alias | Tests profil hostile, registre et navigateur |
| GLTF/GLB, rigs, animations, collisions, sockets, LOD | Profil, worker Blender, manifestes, QA et round-trip existants | Preuve groupée incomplète et références du navigateur non unifiées | Tests profil/manifest/registre et runtime |
| `package:type/path`, UUID et aliases | Namespace de package et ressources UUID de R016 | Aucun identifiant URI canonique ni résolution d’alias/collision | Tests domaine et round-trip projet |
| Browser unifié, facettes, preview, références, package/licence/statut/actions | Asset browser local, facettes, preview, import, manifests et MCP assets | Références et identités namespacées non exposées ; pas de recherche unifiée des ressources package/localisation | Tests application/MCP, UI et capture |

## Contrat minimal de fermeture

- résolution localisée déterministe avec langue exacte, langue parente, défaut, repli et expressions ICU
  `plural`/`select`, plus export/import traducteur borné et pseudo-localisation ;
- registre canonique de ressources dérivé des packages, avec adresse `package:type/path`, UUID backing,
  aliases collision-free, références résolues, licence/provenance/hash et budget plateforme ;
- navigateur unifié combinant ressources de package, localisation et Asset Vault, avec recherche et facettes
  type/package/licence/statut, preview, références et actions autorisées ;
- profils textures et modèles validés avant build et représentés dans le registre : formats modernes, canaux PBR,
  compression/densité, GLB, rigs/animations/collisions/sockets/LOD ;
- mutations via commandes canoniques, dry-run/diff, undo/redo, persistance exacte et diagnostics stables ;
- lectures runtime/ForgeScript par handles opaques, MCP filtré et sans accès direct au système de fichiers.

## Risques et tests exacts

- Compatibilité : les anciens projets sans aliases restent valides et produisent les mêmes adresses stables.
- Sécurité : aucun alias ambigu, traversal, hash/licence/provenance manquant ou référence non résolue.
- Déterminisme : tri ordinal, bundles traducteur et registres byte-stables, UUID inchangés au round-trip.
- Tests ciblés : Domain, Application, ModuleFormat et MCP, puis smokes Toolset/runtime R017 et capture visuelle.
- Portail final : tracker/backlog/docs/graphe régénérés et `scripts/Verify.ps1` avant R018.

## Preuves de fermeture

- `ORF-CONT-011`, `ORF-CONT-013`, `ORF-CONT-014`, `ORF-CONT-019` et `ORF-CONT-020` sont promues
  individuellement à `Tested` après audit complet des critères source.
- Tests ciblés verts : Domain 8/8, Application 13/13, ModuleFormat 13/13 et MCP 4/4 ; le transport
  MCP stdio officiel passe également avec les quatre nouveaux outils.
- Marqueurs intégrés : `OPENRPG_FORGE_TOOLSET_R017_CONTENT_AUTHORING_PASS` et
  `OPENRPG_FORGE_RUNTIME_R017_CONTENT_AUTHORING_PASS`.
- Preuve visible inspectée : `docs/aurora-parity/evidence/r017-content-authoring-ui.png` en 1240×780,
  montrant ressources projet/localisation, AssetId canonique, UUID, package, licence, alias et référence.
- Le prototype est byte-stable après migration additive du catalogue de localisation ; son hash est
  identique avant/après un smoke Toolset complet.
- Portail global pré-promotion puis post-promotion : 1 151 tests .NET, 6 tests Python généraux,
  33 tests asset, 27 projets sans avertissement, graphe déterministe sans diagnostic et marqueur final
  `OPENRPG_FORGE_VERIFICATION=PASS`.

## Non-objectifs

- acquisition réseau, quarantaine/provenance avancée de R018, external-edit/Blender bridge P1/P2 ;
- P1/P2, AV7–AV10, marketplace distante, commit ou push Git.
