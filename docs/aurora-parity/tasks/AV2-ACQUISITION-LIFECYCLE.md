# AV2 — Cycle de vie des acquisitions Asset Vault

Date : 5 août 2026.

## Résultat

- reprise HTTP par fichier `.part` et en-tête `Range` lorsque le serveur l’accepte ;
- cache de téléchargement adressé par SHA-256 sous `cache/downloads/sha256/` ;
- option `--update` pour Poly Haven, ambientCG et Kenney ;
- conservation d’un original différent sous `versions/<hash>/` au lieu de l’écraser ;
- versionnement conjoint des extractions ambientCG/Kenney lorsqu’une archive change ;
- plan déterministe comparant catalogues courant et entrant ;
- désinstallation locale en mode sec par défaut, refus des plans altérés ou devenus obsolètes ;
- déplacement récupérable des sources, normalisations et métadonnées sous
  `cache/uninstalled/<plan_id>/`, avec reçu et registre v1 ;
- rapport local des objets en cache, téléchargements reprenables et acquisitions désinstallées.

## Preuves

`tests/asset_pipeline/test_download_cc0_3d.py` couvre reprise, cache, version immuable et extraction
versionnée. `tests/asset_pipeline/test_asset_vault_lifecycle.py` couvre diff de catalogues, plan sec,
application récupérable, contenu modifié, traversal, plan altéré et rapport. La suite du pipeline
compte 20 tests.

## Limites conservées

La commande d’update interroge toujours les API officielles lorsqu’elle est réellement lancée ; le
gate local simule le transport et ne télécharge pas les 4,16 Gio du Vault. La purge définitive des
backups est volontairement absente : une politique de rétention explicite sera requise avant toute
suppression non récupérable.
