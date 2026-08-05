# G11-D — Distribution signée, delta et canaux

## Périmètre

- `ORF-CONT-028` — Signatures ;
- `ORF-CONT-029` — Delta updates ;
- `ORF-BUILD-016` — Build multi-plateforme ;
- `ORF-BUILD-017` — Packaging Windows ;
- `ORF-BUILD-024` — Release channels.

## Résultat livré

- configuration de release canonique v1, UUID stable, canaux Development/Preview/Stable et matrice
  Windows x64, Linux x64, macOS arm64 ;
- persistance dans le projet, commande transactionnelle existante, dry-run, diff, undo/redo et
  édition dans Validation & distribution ;
- politique de signature Disabled/Optional/Required avec référence de profil secret, empreintes
  SHA-256 approuvées, révocations et exception bornée pour le développement ;
- génération de clé hors projet, signature détachée ECDSA P-256 SHA-256, vérification
  cryptographique, confiance et révocation ;
- `.orpdelta` déterministe et borné : chunks content-addressed, déduplication, cache de reprise,
  reconstruction atomique, hash cible et rollback vers une base retenue ;
- bundle de release ZIP versionné avec manifeste, package, signature/delta optionnels et, sous
  Windows, scripts d'installation/désinstallation, détection déclarative des prérequis et journal ;
- CLI structuré : `keygen`, `sign`, `verify-signature`, `delta`, `apply-delta` et `bundle` ;
- Stable/Shipping est refusé sans signature obligatoire, delta reprenable/dédupliqué/réversible et
  cycle Windows sûr.

## Preuves

- tests domaine sur UUID/configuration, empreintes, chunks, règles Stable/Shipping et Windows ;
- tests de sérialisation sur le round-trip et la migration d'un projet antérieur sans release ;
- tests ModuleFormat sur signature, altération, révocation, delta modifié, reprise, reconstruction,
  rollback et bundle Windows reproductible ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_G713_DISTRIBUTION_PASS` avec Preview, deux plateformes et
  persistance ;
- smoke CLI `OPENRPG_FORGE_G11D_RELEASE_SMOKE_PASS` : clé éphémère dans `artifacts/`, package
  signé, delta appliqué bit pour bit, bundle extrait, installation puis désinstallation isolées ;
- aucune clé privée, aucun secret et aucun artefact de release ne sont versionnés.

## Limites explicites

- la tranche produit physiquement le bundle Windows de module ; les exports de l'application
  runtime/Toolset et les jobs CI Linux/macOS restent à brancher avant la distribution finale ;
- l'installeur applicatif complet et la signature Authenticode ne sont pas simulés ;
- les feeds distants, promotion de canal, rotation de clé et reprise HTTP/CDN restent des adaptateurs
  de publication ;
- les cinq exigences restent `Implemented` partiel et non `Tested` complet tant que ces adaptateurs
  et tests multi-plateformes ne sont pas livrés.

## Critère de passage

G11-D est fermé lorsque les tests ciblés, le smoke Toolset, le smoke release CLI, la sérialisation,
le tracker, le graphe et `scripts/Verify.ps1` sont verts sur le même état Git.
