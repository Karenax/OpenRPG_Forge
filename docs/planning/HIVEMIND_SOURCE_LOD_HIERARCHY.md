# Hivemind — conservation des groupes LOD source

## Preuve locale du 11 octobre 2026

Le lot Pirate v951 a produit 22 fiches illustrées et 44 vues, avec 15 contrôles
LOD0. Les sources sont inchangées et le lecteur courant de Forge a vérifié
recherche, filtres, détails, licences et images. La livraison v957 ne qualifie
ni conversion, ni placement, ni rendu OpenRPG, ni GUI Studio.

Deux demandes LOD3 ont été refusées par Unity : `SM_BarrelTable` et
`SM_Chandelier`. L'audit v959 a relu les prefabs conservés au Vault et les copies
privées, vérifié leurs SHA, résolu les objets et transforms parents, puis mesuré
les groupes LOD et leurs références renderer.

| Assemblage | Groupes | Niveaux déclarés par groupe | Groupes sans LOD3 |
|---|---:|---|---:|
| BarrelTable | 5 | 3 ou 4 | 3 |
| Chandelier | 9 | 3 ou 4 | 8 |

L'index `original_lod_level=3` d'une fiche d'assemblage ne prouve donc pas
l'existence d'un état LOD3 uniforme de tous ses composants. Ne pas remplacer
silencieusement les composants absents par LOD0, leur dernier niveau, ou leur
suppression. Les deux refus restent conservés, avec leurs fiches et sources.

## Contraintes pour la conversion et le runtime

La représentation dérivée doit conserver l'identité et la hiérarchie de chaque
groupe, ses transforms composés, son point de référence, sa taille, ses seuils,
son mode de transition, ses références renderer et les slots vides explicites.
Un niveau global appliqué à l'assemblage ne suffit pas à représenter ces données.
Les groupes doivent pouvoir évoluer indépendamment selon leur configuration
source. La capture d'un état fixe ne prouve pas ces transitions dans le runtime.

Les 159 fiches Pirate restant sans image lors de v959 correspondent à 75
prefabs. Pour 145 fiches, l'index demandé existe dans tous les groupes déclarés.
Les 14 autres correspondent aux deux assemblages ci-dessus et à 12 fiches
LOD0 sans groupe LOD. L'absence de groupe ne rend pas un prefab invalide : elle
demande une capture de l'objet source sans sélection forcée de niveau. Un prefab
animé doit également conserver et qualifier son profil d'animation ; son image
fixe ne constitue pas une conversion de l'animation.

Le routage v960 retient les deux assemblages et les refus de matériaux déjà
documentés. Il trouve 67 fiches LOD3+ candidates au prochain lot et 62 fiches
retenues. Ce routage est un précontrôle, pas une admission de conversion.

## Reçus

- `F:/OpenRPG_ImportWork/pirate-upper-lod-images-delivery-v957/RESULT.v1.json`
- `F:/OpenRPG_ImportWork/remaining-source-images-v958/RESULT.v1.json`
- `F:/OpenRPG_ImportWork/pirate-source-lod-hierarchy-v959/RESULT.v1.json`
- `F:/OpenRPG_ImportWork/pirate-upper-lod-frontier-v960/RESULT.v1.json`

Les compteurs décrivent ces snapshots. Ne pas les employer comme verdict courant
sans vérifier le pointeur catalogue et les preuves des lots ultérieurs.
