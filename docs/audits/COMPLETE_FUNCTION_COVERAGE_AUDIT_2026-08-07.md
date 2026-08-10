# Audit complet de couverture fonctionnelle — 7 août 2026

## Objet

Établir ce qui est réellement fait, distinguer les jalons verticaux des fonctions complètement
prouvées et garantir qu'aucune des fonctions initialement prévues ne disparaît du backlog actif.
Cet audit ne change aucun statut fonctionnel du tracker et n'implémente aucun comportement produit.

## Sources contrôlées

- catalogue clean-room immuable `Divers/OpenRPG_Forge_Aurora_Feature_Dossier/feature_catalog.csv` ;
- tracker projet généré `tracking/project/AURORA_PARITY_COVERAGE.csv` ;
- 168 fiches existantes sous `docs/aurora-parity/tasks/` ;
- `MEGACONTEXT.md`, `STATUS.md`, `TASKS_MVP.md`, la roadmap et le plan de livraison Aurora ;
- backlog et état Asset Vault/Forgotten Forest ;
- code, tests et preuves uniquement par les liens déjà portés par le tracker et le portail intégré.

Le catalogue source et le tracker contiennent chacun 717 IDs uniques, sans manque ni ajout.

## Ce qui est livré

- les lots historiques T0 à T9 et le MVP Crypte des Cendres ;
- les gates verticaux G0 à G13, leurs démonstrations Creator, RPG, réseau, Live Director, contenu
  moderne et création assistée ;
- deux modules jouables, l'export `.orpmod`, les sauvegardes, ForgeScript, MCP et le runtime 3D ;
- Forgotten Forest 512 × 512, personnages animés, navigation, assets gouvernés et Asset Atlas ;
- un portail local intégré avec build, tests, smokes, graphes, assets, réseau et exports.

Ces résultats prouvent des parcours utilisables. Ils ne prouvent pas automatiquement tous les
critères détaillés des 717 fonctions.

## Couverture feature par feature observée

| Statut | Total | Interprétation stricte |
|---|---:|---|
| `Tested` | 207 | fonction close avec preuve liée |
| `Implemented` | 432 | comportement présent mais critères ou preuves encore partiels |
| `Designed` | 1 | conception présente, livraison à faire |
| `Researched` | 77 | exigence cataloguée, livraison à concevoir et prouver |
| **Total** | **717** | **510 fonctions non closes** |

Par priorité, il reste 191 P0, 257 P1 et 62 P2 non `Tested`. Les 32 P3 et l'unique P4 sont `Tested`.
Le tracker contient 432 notes portant explicitement une mention `Partial`. Une preuve de fichier ou un
test générique ne suffit donc pas à considérer une ligne `Implemented` comme terminée.

Les domaines ayant le plus de fonctions non closes sont `SCRIPT` (37), `AREA` (32), `DM` (32),
`MULTI` (30), `CREATURE` (28), `CONTENT` (27) et `DIALOGUE` (25). Le détail exhaustif est dans
`../planning/COMPLETE_FUNCTION_BACKLOG.md`.

## Écart du découpage antérieur

Les 168 fiches historiques respectent la limite de cinq feature IDs, mais elles ne mentionnent que
340 IDs uniques. Avant reformalisation, 377 fonctions n'avaient donc aucune fiche de petit lot :
276 `Implemented`, 76 `Researched`, 24 `Tested` et 1 `Designed`.

Le backlog humain ne contenait qu'une case ouverte regroupant huit archives manuelles, alors que les
documents de statut décrivaient encore de nombreux chantiers non numérotés. Plusieurs sources actives
se contredisaient : G7, G11-T, G12-K ou G13 étaient encore présentés comme « prochaine tranche »
alors que leurs journaux de clôture les déclaraient livrés.

## Reformalisation retenue

- génération déterministe de 187 lots `FC-*`, chacun limité à cinq fonctions d'un même domaine et
  d'une même priorité ;
- couverture de chacun des 717 IDs exactement une fois ;
- 53 lots `MAINTAIN` clos, 113 lots `COMPLETE` à terminer/prouver et 21 lots `DELIVER` à livrer ;
- aucun lot actif automatiquement et aucune invention d'un gate G14 ;
- création d'une fiche détaillée seulement lors de l'activation explicite d'un lot ;
- 25 lots `AV7-M*`, `AV8-*`, `AV9-*` et `AV10-*` pour les engagements Asset/Map hors catalogue ;
- anciens récits de livraison conservés mais étiquetés comme historiques ;
- contrôle de fraîcheur du plan complet ajouté au portail `scripts/Verify.ps1`.

## Définition de la cible 100 %

La cible initiale est entièrement couverte par le plan dès maintenant. Elle ne sera entièrement
livrée que lorsque :

1. les 717 lignes seront `Tested` avec preuves spécifiques ;
2. les 187 lots `FC-*` seront `MAINTAIN` ;
3. les lots Asset/Map explicitement retenus seront clos ou différés par décision documentée ;
4. tracker, plan généré, contexte, statut et portail intégré resteront cohérents ;
5. les parcours visibles et les performances promises auront une preuve reproductible adaptée.

## Limites de cet audit

- les 717 critères source n'ont pas été réimplémentés ni rejoués individuellement ;
- les 432 lignes `Implemented` n'ont pas été promues, car leurs notes déclarent encore des limites ;
- les liens de preuves ont été contrôlés structurellement par le tracker, pas réévalués
  sémantiquement un par un contre chaque phrase des 26 dossiers d'exigences ;
- la fermeture des 25 lots Asset/Map dépendra de choix produit, de revues visuelles et, pour huit
  lots, d'archives déposées manuellement ;
- l'ordre exact entre lots de même priorité doit encore être décidé à partir des dépendances et de la
  valeur de démonstration, sans ouvrir plusieurs grands chantiers.

## Corrections issues du contre-audit

- le comptage `Partial` a été corrigé de 433 à 432, exactement les 432 lignes `Implemented` ;
- chaque lot affiche désormais sa progression `x/n Tested`, ce qui rend visibles les 35 fonctions
  déjà testées présentes dans des lots encore ouverts ;
- la file `R001` à `R134` expose gates, parcours et dépendances déclarées, avec contrôle obligatoire
  des cycles avant activation ;
- le programme P0 possède un protocole d'audit sémantique avant toute promotion ;
- les journaux longs sont archivés et les documents actifs condensés ;
- les 25 lots Asset/Map restants sont explicitement `OPTIONAL_POST_DEMO` et ne bloquent ni les 717
  fonctions Aurora ni le programme P0.
