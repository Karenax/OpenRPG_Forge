# Placements canoniques consommés par le prototype natif

`building_placements.rs` applique les placements du head sauvegardé aux morceaux
du village. Chaque référence est appariée au descripteur hashé et à sa clé source.
Le delta entre repère source et transformation auteur est appliqué aux morceaux ;
la pose source exacte conserve la matrice originale sans inversion numérique.
Un placement absent retire ses morceaux. Plusieurs placements du même asset
produisent plusieurs instances. Ces deux derniers parcours restent à qualifier
visuellement. Les nœuds portent l'EntityId mais le picking n'est pas raccordé.

Les passes visibles et les ombres consomment le même `village.json` effectif en
mémoire. Les payloads géométriques restent ceux de la fixture vérifiée.
Le probe d'ombres autonome garde sa route sur disque.

## Exécution native

Racine : `E:/OpenRPG_Forge/.tidewater-integration/`.

- Fixture `building-placement-scene-001` : scène séparée plus catalogue et
  23 descripteurs hashés, préparée par `prepare-building-placement-scene.py`.
- `workspace-buildings-initial-001`, adopté par les commandes workspace :
  capture `native-buildings-initial-001.png` inspectée, exactement identique
  aux pixels de `native-merged-village-current-001.png` (zéro pixel différent).
- Premier essai `native-buildings-moved-001.png` sans changement visible :
  le premier ID trié n'est pas une cible utile pour cette caméra. Ce résultat
  n'est pas une preuve de déplacement visuel.
- L'exemple de placements cible désormais `building:16`, la maison au premier
  plan. Archives `building-placement-states-002` puis adoption du moved dans
  `workspace-buildings-moved-002`.
- `native-buildings-moved-002.png` inspectée : maison déplacée de 12 m en X,
  ombre déplacée avec elle. 284419 pixels changent, maximum de canal 141/255.
  Ce nombre décrit la différence entre états auteur, pas une erreur Tidewater.
- Compilation et Clippy strict des probes scène/ombres réussis ; les deux
  exécutions de capture terminent avec code 0.

## Limites

Undo est qualifié dans les archives, pas encore par une capture du rendu de ce
lot. Rotation, échelle, duplication, suppression visuelle, collisions et Studio
restent ouverts. Le déplacement peut croiser chemins/herbe/décor résiduel : les
objets indépendants ne sont pas déplacés automatiquement avec la maison.
Catalogue et dépendances restent dans la fixture, et ne constituent pas encore
un package workspace autonome. Aucune parité globale n'est revendiquée.
