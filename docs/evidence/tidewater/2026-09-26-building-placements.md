# Placements de bâtiments dans l'archive canonique

L'exemple `forge_assets/prepare_tidewater_placements` consomme le catalogue
importé et vérifie les octets de chaque descripteur contre son hash épinglé.
Il reprend les transformations source et prépare 23 `ManualPlacement`.
L'identité d'instance dérive du projet, de la zone et de la clé source ;
l'identité d'asset et son hash restent ceux du catalogue.

L'insertion passe par `PlacementEdit::InsertMany` et `ZoneEditSession::execute`.
Les exemples suivants utilisent `Transform`, `Remove` et `ZoneAction::Undo`.
Chaque état est écrit puis relu dans une nouvelle session et les heads comparés.
Les annulations rétablissent exactement les placements, pas les octets de
l'archive entière : l'historique des commandes reste présent.

## Exécution

Source : `authored-vegetation-states-002/initial.json` et `building-catalog-001`.
Sortie : `E:/OpenRPG_Forge/.tidewater-integration/building-placement-states-001`.

| État | SHA-256 de l'archive |
|---|---|
| initial, 23 placements | `50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720` |
| moved, premier ID trié déplacé de 12 m en X | `2a51726a4f6ff190c3f40c55c4c6a3ec62e821871e5a63f2090a44509e803bf0` |
| move-undo | `4dbde6459ad35c309b22000c5d68657e104faf39d278b86691803d74a6ffc10f` |
| removed, 22 placements | `7a9a2b931e3ed3bafa40685564dec3fc15cb0b971233944d7bf6463c450eb84c` |
| remove-undo, 23 placements | `2d5dbbadf4623afc54f48d2ecb9830e8e0acdafa70490013568abe06391efb5c` |

La base terrain/végétation demeure égale à l'entrée. Clippy strict de tous
les targets `forge_assets` passe après exécution de l'exemple.

## Limites

Ces placements sont canoniques dans l'archive ; ils ne sont pas encore consommés
par les morceaux de village du prototype. Aucune preuve de déplacement visuel,
de picking Studio, de duplication, de collision ou d'adoption workspace n'est
revendiquée ici. Les descripteurs sont vérifiés ; la fermeture portable des
dépendances de géométrie et de matériaux reste un travail distinct.
