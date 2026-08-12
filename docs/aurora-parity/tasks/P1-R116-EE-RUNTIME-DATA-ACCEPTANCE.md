# R116 — Acceptation EE données et chargement runtime

## Périmètre

`R116 / FC-EE-P1-03` couvre exactement cinq fonctions P1 :

- `ORF-EE-015` — JSON runtime, déjà `Tested` et reconfirmée ;
- `ORF-EE-016` — sérialisation d’objet avec état, déjà `Tested` et reconfirmée ;
- `ORF-EE-019` — copie dynamique d’objet, déjà `Tested` et reconfirmée ;
- `ORF-EE-022` — indicateur de zone d’effet, déjà `Tested` et reconfirmée ;
- `ORF-EE-023` — chargement de zone amélioré.

Les critères source ont été lus intégralement dans
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/23_EE.md`, lignes 1010–1146,
1284–1350 et 1485–1618.

## Matrice d’écarts auditée

| Exigence source | Présent avant R116 | Limite réelle | Fermeture/preuve |
|---|---|---|---|
| JSON runtime typé, validé et borné | documents versionnés, validation de schéma sûre, quotas, JSON Pointer et API ForgeScript | aucune limite source réelle | preuves G11-O maintenues et smoke R116 de non-régression |
| Snapshots d’entités avec composants, références, inventaire, blackboard et version de schéma | sauvegarde runtime 1.3, migrations, quotas et restauration de transforms | aucune limite source réelle | preuves G11-O maintenues et smoke R116 de non-régression |
| Copie runtime transactionnelle et indicateur AOE confirmé par le serveur | nouveaux UUID, ownership, remap de références, état allowlisté, formes, cibles, couverture, hauteur et palettes daltoniennes | aucune limite source réelle | preuves G11-P maintenues et smoke R116 de non-régression |
| Chargement asynchrone, prélecture, progression, cache, annulation et gate de benchmark | coordinateur borné, concurrence, cache LRU, progression, durée et cible persistée | les options `AsyncStreaming` et `CancellationEnabled` n’étaient pas appliquées ; un dépassement de cible ne produisait pas de diagnostic stable | exécution séquentielle de repli, annulation configurable et `AreaStreamingBenchmarkGate` avec `area.loading.target_exceeded` |

## Contrat fermé

- Le chargement utilise la concurrence configurée en mode asynchrone et une seule opération en mode
  séquentiel de repli.
- Un profil qui désactive l’annulation ignore le jeton appelant ; un profil qui l’active propage
  l’annulation jusque dans le chargeur de ressource.
- La prélecture des dépendances, la progression, le cache à budget, les ressources requises et la
  mesure de durée restent déterministes et bornés.
- `AreaStreamingBenchmarkGate` compare la mesure réelle à la cible du profil et retourne le diagnostic
  localisé `area.loading.target_exceeded` avec chemin canonique et UUID du profil.
- JSON, snapshots, copie et AOE conservent leurs contrats, interfaces, commandes, persistance et
  replis runtime déjà testés.

## Preuves obtenues avant portail global

- 4/4 tests `AreaStreamingCoordinatorTests` ;
- build Toolset sans avertissement ni erreur ;
- `.artifacts/r116-toolset-smoke.log` contient le marqueur exact
  `OPENRPG_FORGE_TOOLSET_R116_EE_RUNTIME_DATA_PASS`.

## Portail consolidé

- tracker/backlog régénérés : 654 `Tested`, 62 `Implemented`, 1 `Researched`, 166 lots clos ;
- documentation générée puis vérifiée sans dérive ;
- graphe Roslyn déterministe : 7 697 nœuds, 49 444 relations, zéro diagnostic ;
- 1 767 tests .NET et 39 tests Python passent ;
- `artifacts/verification/toolset.log` contient le marqueur exact R116 ;
- `.artifacts/r116-full-verify-final.log` termine sur `OPENRPG_FORGE_VERIFICATION=PASS`.

## Non-objectifs

- monde ouvert massif ou streaming réseau, absents du contrat source de `ORF-EE-023` ;
- moteur de stockage JSON généraliste ou JSON Schema complet ;
- P2, Asset/Map AV7–AV10, commit ou push.
