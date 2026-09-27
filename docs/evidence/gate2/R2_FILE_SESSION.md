# Gate 2 — session terrain sur disque

12 septembre 2026. Incrément local Rust, sans commit/push ni changement de carte.

## Implémenté

`forge_core::document_store` : adapter natif de stockage de bytes, dossier déjà
autorisé par l'application, noms fixes, verrou OS détenu pendant la session.
Chargement borné à 4 Mio ; sauvegarde avec hash attendu, staging créé exclusivement,
write/sync puis remplacement dans le même répertoire. Le hash est vérifié avant
écriture et juste avant remplacement. Aucun nouvel accès filesystem côté WASM.

`terrain_edit::file_session::FileTerrainSession` : création, ouverture, exécution
des mêmes commandes, sauvegarde de l'archive vérifiée, indicateur modifié. Une
sauvegarde refusée conserve l'état modifié et la retouche en mémoire.

## Exécuté

- Tests core/project : **92 PASS**, 14 + 78, zéro ignoré.
- Clippy strict core/project all-targets/all-features : PASS.
- Formatage workspace et diff check : PASS (avertissements CRLF préexistants).
- Journal : `E:/OpenRPG_Forge/.gate2-evidence/file-session-tests.log`.
- Fixtures réelles uniques conservées sous ce même dossier E:, quelques petits
  fichiers par test. Aucun processus graphique ou serveur lancé.

Tests : remplacement réel Windows et réouverture ; deuxième ouverture concurrente
refusée puis verrou libéré ; hash périmé refusé ; échec injecté avant remplacement
préservant l'ancien fichier ; changement externe durant la sauvegarde refusé ;
staging préexistant conservé ; entrée répertoire et taille excessive refusées.
Parcours intégré : retoucher, sauvegarder, fermer, rouvrir, retry sans duplication,
Undo restauré, conflit externe refusé avec état modifié conservé.

Premier contrôle d'intégration refusé pour visibilité de trois helpers de test ;
visibilité limitée au parent corrigée, aucun lint ou test neutralisé.

## Limites et prochaine étape

Le dossier doit être autorisé par l'application native : ce module n'est pas un
sandbox contre un processus hostile échangeant les répertoires/liens en course.
Le verrou protège les écrivains coopératifs ; un logiciel ignorant le verrou
peut toujours intervenir après le dernier contrôle. Les liens/répertoires de
fichiers connus sont refusés, pas une garantie complète de confinement hostile.

L'échec injecté n'est PAS un kill réel ni une panne électrique. Un staging laissé
par interruption n'est jamais écrasé/supprimé automatiquement ; le fichier validé
reste lisible, mais la récupération explicite du staging reste à implémenter.
La durabilité du répertoire après perte d'alimentation n'est pas qualifiée.

À faire : récupération explicite, acteur/audit d'application, raccord Studio et
playtest, tests de processus interrompu, contrôle workspace/WASM et parcours UI.
R2-038/039/040 restent partiels, **Gate 2 non validée**.
