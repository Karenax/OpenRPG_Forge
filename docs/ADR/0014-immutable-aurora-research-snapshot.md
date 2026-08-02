# ADR 0014 — Snapshot Aurora immuable et tracker projet généré

## Statut

Accepté le 2 août 2026.

## Contexte

Le dossier de recherche Aurora apporte 717 capacités, leurs sources, leurs critères d’acceptation et
un tracker initialisé à `Researched`. Ce tracker décrit la recherche et ne doit pas être confondu
avec la couverture réelle d’OpenRPG Forge. Modifier directement le dossier rendrait également son
manifeste SHA-256 inutilisable et ferait perdre la distinction entre source reçue et décisions du
projet.

## Décision

- `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/` reste un snapshot immuable de recherche.
- Son intégrité est vérifiée contre `MANIFEST_SHA256.txt` par les tests du dépôt.
- Le tracker opérationnel est généré dans
  `tracking/project/AURORA_PARITY_COVERAGE.csv` à partir du catalogue source et de l’audit projet.
- L’audit manuel des 76 P0 Core est conservé dans
  `tracking/project/AURORA_CORE_P0_AUDIT.csv` ; chaque statut `Tested` exige une preuve.
- `tracking/project/AURORA_GATE_MATRIX.csv` affecte chaque feature à un gate et un jalon.
- `scripts/Update-AuroraParityTracker.ps1` est l’unique générateur de ces deux artefacts. Son mode
  `-Check` échoue si les artefacts sont absents ou périmés.
- Les futures modifications de statut portent uniquement sur l’audit ou sur une source projet
  dédiée, jamais sur le snapshot.

## Conséquences

- La recherche reste reproductible et vérifiable byte par byte.
- Le projet peut faire évoluer ses preuves sans falsifier le dossier reçu.
- Les 717 capacités ont un gate cible, mais un gate n’implique jamais qu’une capacité est conçue ou
  implémentée.
- Toute nouvelle vague de recherche devra être importée comme une nouvelle version de snapshot et
  comparée explicitement avant régénération.
