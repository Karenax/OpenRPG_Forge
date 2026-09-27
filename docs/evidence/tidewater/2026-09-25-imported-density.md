# Tidewater — densité peinte sur la forêt importée

`ImportedPopulation::authored_view` applique les exclusions/origines promues puis
les masques de densité explicitement écrits dans les corrections de zone. Les
instances de base restent autoritaires : leurs coordonnées, orientations,
échelles et paramètres ne sont pas recalculés par une recette procédurale.

Le poids du masque est interpolé en f64 dans le domaine de la carte. La décision
de conservation compare ce poids au rang déterministe de l'identité d'entité :
premiers 32 bits little-endian du SHA-256 de `imported-density-v1/{entity_id}`,
avec milieu de case `(u32 + 0.5) / 2^32`. Zéro retire tout, 255 conserve tout.
La baisse des poids produit des sous-ensembles imbriqués ; elle ne déplace pas
les survivants. Les verrous sont toujours gouvernés par les commandes existantes.

## Preuves

Rapport : `E:/OpenRPG_Forge/.tidewater-integration/imported-density-verification-001.json`.
Après l'exclusion circulaire des 669 plantes :

| Masque constant sur les dix couches | Instances conservées |
|---|---:|
| Aucun masque | 64714 |
| 128 / 255 | 32440 |
| 64 / 255 | 16268 |
| 255 / 255 | 64714 |
| 0 / 255 | 0 |
| Effacement des corrections de densité | 64714 |

Le vérificateur compare les ensembles d'identités, y compris après archivage et
réouverture. Les trois tests d'intégration passent ; le scénario existant couvre
maintenant aussi une densité nulle archivée puis annulée, sans réintroduire une
origine promue supprimée. Un test vérifie les poids aux quatre coins et au centre
d'un masque bilinéaire. Clippy module/exemple passe avec `-D warnings`.

## Limites

Cette étape qualifie les données et la projection CPU. Elle ne prouve pas encore
la peinture dans le viewport Studio ou son rendu. Les dégagements de routes et
bâtiments, la reprojection du relief retouché et les placements manuels restent
à raccorder. Les variations procédurales de densité/habitat de la recette n'ont
pas vocation à redistribuer la disposition importée : seules les corrections
de masque sont appliquées ici. L'objectif de carte et effets identiques reste ouvert.
