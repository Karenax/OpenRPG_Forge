# Programme de fermeture P3 et P4

## Objectif borné

Réauditer et fermer proprement les 32 fonctions P3 et la fonction P4 du catalogue Aurora, sans
confondre un contrat fonctionnel testé avec le déploiement d'un service public à grande échelle.
Le programme a été exécuté le 12 août 2026 sur les 17 lots stables du registre : 16 lots P3 et un
lot P4, chacun limité à une à cinq fonctions.

## Protocole appliqué

1. Relire les critères source de chacune des 33 fonctions dans le dossier de recherche immuable.
2. Vérifier les chemins de conception, d'implémentation, de tests et de fiche G13 liés par le
   tracker, sans accepter le statut `Tested` comme preuve suffisante.
3. Vérifier que les marqueurs Toolset/runtime cités sont réellement émis par le produit et observés
   dans les journaux du portail intégré.
4. Rejouer un ensemble de tests ciblé pour chacun des 17 lots.
5. Ne modifier le code que lorsqu'un écart fonctionnel réel est constaté.
6. Régénérer tracker, backlog, documentation et graphe, puis passer `scripts/Verify.ps1`.

## Matrice de clôture

| Lot | Fonctions | Preuve ciblée rejouée | Résultat |
|---|---:|---|---|
| `FC-AI-P3-01` | 1 | `PostLaunchRuntimeServiceTests` | 2/2 |
| `FC-CONTENT-P3-01` | 1 | `G13AD` et `CuratedAssetPackInstallerTests` | 3/3 |
| `FC-CREATURE-P3-01` | 4 | progression, loot/pickpocket, rôle et événements | 16/16 |
| `FC-DIALOGUE-P3-01` | 1 | présentation adaptative, workflow et persistance | 10/10 |
| `FC-DM-P3-01` | 5 | résolveur `G13AE/G13AF` et `LiveProductionServiceTests` | 4/4 |
| `FC-DOOR-P3-01` | 1 | scénario `G13Q` | 1/1 |
| `FC-ENCOUNTER-P3-01` | 1 | scénario `G13T` | 1/1 |
| `FC-FACTION-P3-01` | 1 | profil avancé et service ForgeScript | 7/7 |
| `FC-GAMEPLAY-P3-01` | 4 | scénarios mobilité et achievements `G13AC/G13AD` | 2/2 |
| `FC-ITEM-P3-01` | 2 | profil avancé et workflow applicatif | 10/10 |
| `FC-MERCHANT-P3-01` | 2 | scénario `G13X` | 1/1 |
| `FC-MULTI-P3-01` | 5 | scénarios `G13AG/G13AH/G13AI` et production réseau | 5/5 |
| `FC-PLACEABLE-P3-01` | 1 | scénario `G13O` | 1/1 |
| `FC-SCRIPT-P3-01` | 1 | routage `G13AA` et sandbox native | 14/14 |
| `FC-TRIGGER-P3-01` | 1 | scénario `G13S` | 1/1 |
| `FC-WAYPOINT-P3-01` | 1 | scénario `G13V` et persistance des world features | 6/6 |
| `FC-MULTI-P4-01` | 1 | plan de shard `G13AH` et reload réseau atomique | 2/2 |

Total ciblé : 17/17 lots et 86/86 tests réussis.

## Résultat de l'audit

Les 33 sections source, tous les chemins d'implémentation et de tests, et toutes les fiches G13
liées existent. Les marqueurs cités sont présents dans `GameRoot` ou `ForgeEditorPlugin` et ont été
observés dans les journaux runtime/Toolset du portail. Aucun manque fonctionnel supplémentaire n'a
été constaté pendant cette réouverture ; le travail nécessaire était une clôture explicite et
reproductible, pas une promotion de masse ni une extension du produit.

## Limites assumées

- Les capacités P3/P4 sont prouvées au niveau des contrats auteur, domaine, application, runtime,
  réseau et outillage présents dans OpenRPG Forge.
- Le sharding est un contrat serveur borné avec planification, capacité, identité de zone, bascule
  atomique et rollback ; ce n'est pas la preuve d'un hébergement mondial déjà exploité.
- La voix, la génération média et les packs curatés restent optionnels, gouvernés par capacité,
  consentement, licence, provenance et fournisseur.
- Les lots Asset/Map AV7–AV10 restent `OPTIONAL_POST_DEMO` et ne font pas partie des 717 fonctions.

## Critère de fin

Le programme est clos lorsque les 33 fonctions et les 17 lots restent `Tested`, les 86 tests ciblés
et le portail intégré passent, les autorités générées sont fraîches et la publication distante est
vérifiée par hash.
