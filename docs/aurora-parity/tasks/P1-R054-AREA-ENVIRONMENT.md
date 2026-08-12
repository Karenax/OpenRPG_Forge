# P1 R054 — Copie, classification, lumière et brouillard de zone

Lot stable : `R054` / `FC-AREA-P1-02` — cinq fonctions, aucune fonction hors lot.

## Périmètre et source auditée

- `ORF-AREA-012` — copie complète ou template de zone ;
- `ORF-AREA-013` — classification intérieure/extérieure extensible ;
- `ORF-AREA-014` — cycle jour/nuit ;
- `ORF-AREA-015` — lumière ambiante et directionnelle ;
- `ORF-AREA-016` — brouillard de zone.

Source immuable relue intégralement : sections correspondantes de
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/03_AREA.md`.

## Audit avant fermeture

| Critère source | Présent avant R054 | Limite réelle fermée par R054 |
|---|---|---|
| copie de zone | clone complet/template, nouveaux UUID d'aire/entités/transitions, remappage interne, aperçu et politique de transitions externes | nouvelle identité d'environnement et UUID enfants, copie explicite terrain/navigation/éclairage, références assets partagées comptées et diff sémantique |
| classification | intérieur, extérieur, naturel, artificiel, souterrain et tags | clé extensible par pack pilotant rendu, audio, navigation, météo, caméra et gameplay |
| jour/nuit | horloge, toujours jour/nuit, durée et transitions | courbes soleil/lune, événements horaires ForgeScript et overrides locaux par volume |
| lumière | intensités, température, HDR, ombres et contrôles généraux | presets physiques, couleurs, mode simple/avancé et probes de réflexion avec transforms/overrides |
| brouillard | modes distance/volumétrique, densité, hauteur, début et fin | couleurs jour/nuit, bruit, volumes locaux et profils qualité |
| intégration auteur | écran environnement et commande transactionnelle existaient | onglet R054 visible avec états vide/erreur/aperçu, même commande UI/application et handle ForgeScript typé |
| MCP/runtime/build | environnement persistant consommé par le runtime | lecture filtrée, plan dry-run/apply/revert, fallback optionnel/requis et refus pré-build localisé |

## Contrat livré

- `AreaEnvironmentAuthoringProfile` canonique UUID/schéma/provenance ;
- classification extensible et six capacités pilotées explicitement ;
- courbes soleil/lune, événements horaires et volumes d'override ;
- presets physiques, couleurs ambiante/directionnelle, probes et contrôles simple/avancé ;
- brouillard distance/volumétrique avec couleurs, bruit, volumes et qualité ;
- clone de zone avec identités nouvelles, références partagées explicites et aperçu enrichi ;
- onglet Toolset dédié relié à `SetAreaEnvironmentCommand`, undo/redo et round-trip ;
- snapshot ForgeScript sûr et MCP filtré à diff sémantique réversible ;
- absence optionnelle sûre et absence requise diagnostiquée avant package.

## Preuves produites

- huit tests ciblés R054 : Domain 3, Application 2, ModuleFormat 2 et MCP 1 ;
- capture 1320×820 inspectée `docs/aurora-parity/evidence/r054-area-environment-ui.png` ;
- marqueurs Toolset et runtime `R054_AREA_ENVIRONMENT_PASS` obtenus avec l'exécutable Mono non-console ;
- round-trip projet/workspace, refus pré-build et portail global reproductible.

## Non-objectifs

- fonctions AREA P1 du lot R055 et suivants ;
- fonctions P2, lots Asset/Map AV7–AV10, commit ou push.
