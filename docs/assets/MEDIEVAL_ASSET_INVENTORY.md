# Inventaire de la collection d’assets Médiéval

> Référence dérivée et vérifiée localement le 25 août 2026. Les catalogues et manifestes du Vault restent la source de vérité.

## Portée et preuves

- collection : `medieval` / **Médiéval** ;
- manifeste : `asset_manifests/packs/medieval.json` ;
- Vault : `E:\OpenRPG_AssetVault` via `OPENRPG_ASSET_VAULT` ;
- licence commune : `CC0-1.0`, contrôlée par les manifestes ;
- **365 modèles logiques** : 362 modèles statiques et 3 conteneurs mannequin/animations ;
- formats préférés : 362 glTF et 3 GLB ;
- 365/365 fichiers primaires présents ;
- 1 843 références de dépendances glTF vérifiées, 0 absente ;
- 362/362 miniatures Blender rendues et vérifiées ;
- 16 planches de contact examinées visuellement ;
- aucun nœud source nommé comme collider ou LOD n’a été détecté : collisions, navigation et LOD restent des données à créer et déclarer explicitement.

Les planches visuelles dérivées sont conservées localement sous :

`artifacts/asset-inventory/medieval/contact-sheets/`

Ce dossier est ignoré par Git. Le CSV suivi dans `docs/assets/MEDIEVAL_ASSET_INVENTORY.csv` contient les identifiants stables, chemins du Vault, familles, dimensions, bornes, pivots heuristiques et métriques géométriques.

## Composition du pack

| Sous-pack | Modèles logiques | Rôle principal |
|---|---:|---|
| Fantasy Props MegaKit | 94 | accessoires, mobilier, atelier, marché et décoration |
| Medieval Village MegaKit | 176 | architecture modulaire, toits, murs, ouvertures et détails de village |
| Modular Character Outfits – Fantasy | 24 | tenues paysan/ranger, ensembles et pièces modulaires |
| Stylized Nature MegaKit | 68 | arbres, plantes, rochers, chemins de pierres et scatter |
| Universal Animation Library 2 | 3 | mannequin et 43 clips, avec variante root motion |

## Repères d’assemblage mesurés

- unité : **1 unité = 1 mètre** après import Blender/glTF ;
- sols principaux : **2 × 2 m**, épaisseur visuelle d’environ 0,02 m ;
- murs : **2 m** de large, environ **3,12 m** de haut et **0,41 m** d’épaisseur visuelle ;
- escaliers extérieurs : modules d’environ **2 × 2 × 1 m** ;
- clôtures : segments proches de **2 m** ;
- portes : environ **1,12 m** de large, 2,10 m en version plate et jusqu’à 2,36 m en version ronde ; leur origine se trouve près du côté charnière ;
- les toits suivent des tailles nominales `2x4`, `4x6`, `6x8`, etc., mais leurs bornes dépassent la grille à cause des pentes et débords ;
- les pièces `L`, `R`, `Half`, `Corner`, `Center`, `Mirror`, `Open` et `Closed` sont des variantes fonctionnelles, pas de simples doublons ;
- les origines ne sont pas globalement uniformes : utiliser les bornes exactes du CSV et comparer à une pièce voisine avant tout placement automatique ;
- `Window Roof Thin/Wide` et plusieurs pièces de toit ont volontairement une origine décalée par rapport à leur géométrie ;
- les `Prop Vine*` sont des cartes alpha destinées à être appliquées sur une surface, pas des arbustes autonomes ;
- les tenues de personnage ne fournissent pas toutes une tête ou un corps complet : elles doivent être combinées au personnage de base et à son rig.

## Familles de construction

| Famille | Nombre | Rôles d’assemblage |
|---|---:|---|
| `Animation/Libraries` | 2 | `animation-source` |
| `Animation/Mannequins` | 1 | `animation-source` |
| `Architecture/Balconies` | 4 | `modular-structure` |
| `Architecture/Corners` | 8 | `modular-structure` |
| `Architecture/Door frames` | 4 | `modular-opening` |
| `Architecture/Doors` | 8 | `interactive-leaf` |
| `Architecture/Exterior stairs` | 15 | `modular-circulation` |
| `Architecture/Floor openings` | 5 | `modular-structure` |
| `Architecture/Floors` | 12 | `modular-structure` |
| `Architecture/Interior stairs` | 4 | `modular-circulation` |
| `Architecture/Overhangs` | 20 | `modular-structure` |
| `Architecture/Roofs` | 39 | `modular-structure` |
| `Architecture/Walls` | 20 | `modular-structure` |
| `Architecture/Window shutters` | 8 | `interactive-leaf` |
| `Architecture/Windows` | 6 | `modular-opening` |
| `Characters/Peasant/Female` | 5 | `complete-outfit, modular-character-part` |
| `Characters/Peasant/Male` | 5 | `complete-outfit, modular-character-part` |
| `Characters/Ranger/Female` | 7 | `complete-outfit, modular-character-part` |
| `Characters/Ranger/Male` | 7 | `complete-outfit, modular-character-part` |
| `Nature/Bushes` | 2 | `vegetation-medium` |
| `Nature/Clover` | 2 | `vegetation-ground` |
| `Nature/Ferns` | 1 | `vegetation-ground` |
| `Nature/Flowers` | 4 | `vegetation-ground` |
| `Nature/Grass` | 4 | `vegetation-scatter` |
| `Nature/Mushrooms` | 2 | `vegetation-ground` |
| `Nature/Pebbles` | 11 | `terrain-scatter` |
| `Nature/Petals` | 5 | `terrain-scatter` |
| `Nature/Plants` | 4 | `vegetation-ground` |
| `Nature/Rock paths` | 10 | `modular-path` |
| `Nature/Rocks` | 3 | `terrain-dressing` |
| `Nature/Trees` | 20 | `vegetation-major` |
| `Props/Alchemy` | 7 | `standalone-prop` |
| `Props/Banners` | 4 | `decoration` |
| `Props/Books & writing` | 15 | `standalone-prop` |
| `Props/Containers & storage` | 11 | `standalone-prop` |
| `Props/Food & tableware` | 10 | `standalone-prop` |
| `Props/Forge & workshop` | 10 | `standalone-prop` |
| `Props/Furniture` | 10 | `standalone-prop` |
| `Props/Lighting` | 8 | `standalone-prop` |
| `Props/Market & carts` | 2 | `standalone-prop` |
| `Props/Small dressing & valuables` | 9 | `decoration` |
| `Props/Utility` | 5 | `standalone-prop` |
| `Props/Weapons & display` | 3 | `standalone-prop` |
| `Village/Borders & trims` | 3 | `modular-detail` |
| `Village/Chimneys` | 2 | `modular-detail` |
| `Village/Fences` | 5 | `modular-boundary` |
| `Village/Masonry details` | 4 | `decoration` |
| `Village/Standalone props` | 2 | `standalone-prop` |
| `Village/Structural support` | 1 | `modular-detail` |
| `Village/Vines` | 6 | `surface-decoration` |

## Liste exhaustive par sous-pack et famille

### Fantasy Props MegaKit

#### `Props/Alchemy` — 7

`Cauldron`, `Potion 1`, `Potion 2`, `Potion 4`, `Shelf Small Bottles`, `Smallbottle`, `Smallbottles 1`.

#### `Props/Banners` — 4

`Banner 1`, `Banner 1 Cloth`, `Banner 2`, `Banner 2 Cloth`.

#### `Props/Books & writing` — 15

`Book 5`, `Book 7`, `Book Simplified Single`, `Book Stack 1`, `Book Stack 2`, `Bookcase 2`, `Bookgroup Medium 1`, `Bookgroup Medium 2`,
`Bookgroup Medium 3`, `Bookgroup Small 1`, `Bookgroup Small 2`, `Bookgroup Small 3`, `Bookstand`, `Scroll 1`, `Scroll 2`.

#### `Props/Containers & storage` — 11

`Bag`, `Barrel`, `Barrel Apples`, `Barrel Holder`, `Chest Wood`, `Crate Metal`, `Crate Wooden`, `Farmcrate Apple`,
`Farmcrate Carrot`, `Farmcrate Empty`, `Pouch Large`.

#### `Props/Food & tableware` — 10

`Bottle 1`, `Carrot`, `Chalice`, `Mug`, `Pot 1`, `Pot 1 Lid`, `Table Fork`, `Table Knife`,
`Table Plate`, `Table Spoon`.

#### `Props/Forge & workshop` — 10

`Anvil`, `Anvil Log`, `Axe Bronze`, `Chain Coil`, `Dummy`, `Peg Rack`, `Pickaxe Bronze`, `Whetstone`,
`Workbench`, `Workbench Drawers`.

#### `Props/Furniture` — 10

`Bed Twin1`, `Bed Twin2`, `Bench`, `Cabinet`, `Chair 1`, `Nightstand Shelf`, `Shelf Arch`, `Shelf Simple`,
`Stool`, `Table Large`.

#### `Props/Lighting` — 8

`Candle 1`, `Candle 2`, `Candlestick`, `Candlestick Stand`, `Candlestick Triple`, `Chandelier`, `Lantern Wall`, `Torch Metal`.

#### `Props/Market & carts` — 2

`Stall Cart Empty`, `Stall Empty`.

#### `Props/Small dressing & valuables` — 9

`Cage Small`, `Coin`, `Coin Pile`, `Coin Pile 2`, `Key Gold`, `Key Metal`, `Vase 2`, `Vase 4`,
`Vase Rubble Medium`.

#### `Props/Utility` — 5

`Bucket Metal`, `Bucket Wooden 1`, `Rope 1`, `Rope 2`, `Rope 3`.

#### `Props/Weapons & display` — 3

`Shield Wooden`, `Sword Bronze`, `Weaponstand`.

### Medieval Village MegaKit

#### `Architecture/Balconies` — 4

`Balcony Cross Corner`, `Balcony Cross Straight`, `Balcony Simple Corner`, `Balcony Simple Straight`.

#### `Architecture/Corners` — 8

`Corner Exterior Brick`, `Corner Exterior Topdown`, `Corner Exterior Toponly`, `Corner Exterior Wood`, `Corner Exteriorwide Brick`, `Corner Exteriorwide Wood`, `Corner Interior Big`, `Corner Interior Small`.

#### `Architecture/Door frames` — 4

`Doorframe Flat Brick`, `Doorframe Flat Wooddark`, `Doorframe Round Brick`, `Doorframe Round Wooddark`.

#### `Architecture/Doors` — 8

`Door 1 Flat`, `Door 1 Round`, `Door 2 Flat`, `Door 2 Round`, `Door 4 Flat`, `Door 4 Round`, `Door 8 Flat`, `Door 8 Round`.

#### `Architecture/Exterior stairs` — 15

`Stairs Exterior Nofirststep`, `Stairs Exterior Platform`, `Stairs Exterior Platform45`, `Stairs Exterior Platform45Clean`, `Stairs Exterior Platformu`, `Stairs Exterior Sideplatform`, `Stairs Exterior Sides`, `Stairs Exterior Sides45`,
`Stairs Exterior Sidesu`, `Stairs Exterior Singleside`, `Stairs Exterior Singlesidethick`, `Stairs Exterior Straight`, `Stairs Exterior Straight Center`, `Stairs Exterior Straight L`, `Stairs Exterior Straight R`.

#### `Architecture/Floor openings` — 5

`Holecover 90Angle`, `Holecover 90Half`, `Holecover 90Stairs`, `Holecover Straight`, `Holecover Straighthalf`.

#### `Architecture/Floors` — 12

`Floor Brick`, `Floor Redbrick`, `Floor Unevenbrick`, `Floor Wooddark`, `Floor Wooddark Half1`, `Floor Wooddark Half2`, `Floor Wooddark Half3`, `Floor Wooddark Overhangcorner`,
`Floor Wooddark Overhangcorner2`, `Floor Woodlight`, `Floor Woodlight Overhangcorner`, `Floor Woodlight Overhangcorner2`.

#### `Architecture/Interior stairs` — 4

`Stair Interior Rails`, `Stair Interior Simple`, `Stair Interior Solid`, `Stair Interior Solidextended`.

#### `Architecture/Overhangs` — 20

`Overhang Plaster Corner`, `Overhang Plaster Corner Front`, `Overhang Plaster Long`, `Overhang Plaster Short`, `Overhang Roof Plaster`, `Overhang Roof Unevenbricks`, `Overhang Roofincline Plaster`, `Overhang Roofincline Unevenbricks`,
`Overhang Side Plaster Long L`, `Overhang Side Plaster Long R`, `Overhang Side Plaster Short L`, `Overhang Side Plaster Short R`, `Overhang Side Unevenbrick Long L`, `Overhang Side Unevenbrick Long R`, `Overhang Side Unevenbrick Short L`, `Overhang Side Unevenbrick Short R`,
`Overhang Unevenbrick Corner`, `Overhang Unevenbrick Corner Front`, `Overhang Unevenbrick Long`, `Overhang Unevenbrick Short`.

#### `Architecture/Roofs` — 39

`Roof 2X4 Roundtile`, `Roof Dormer Roundtile`, `Roof Front Brick2`, `Roof Front Brick4`, `Roof Front Brick4 Half L`, `Roof Front Brick4 Half R`, `Roof Front Brick6`, `Roof Front Brick6 Half L`,
`Roof Front Brick6 Half R`, `Roof Front Brick8`, `Roof Front Brick8 Half L`, `Roof Front Brick8 Half R`, `Roof Frontsupports`, `Roof Log`, `Roof Modular Roundtiles`, `Roof Roundtile 2X1`,
`Roof Roundtile 2X1 Long`, `Roof Roundtiles 4X4`, `Roof Roundtiles 4X6`, `Roof Roundtiles 4X8`, `Roof Roundtiles 6X10`, `Roof Roundtiles 6X12`, `Roof Roundtiles 6X14`, `Roof Roundtiles 6X4`,
`Roof Roundtiles 6X6`, `Roof Roundtiles 6X8`, `Roof Roundtiles 8X10`, `Roof Roundtiles 8X12`, `Roof Roundtiles 8X14`, `Roof Roundtiles 8X8`, `Roof Support2`, `Roof Tower Roundtiles`,
`Roof Wooden 2X1`, `Roof Wooden 2X1 Center`, `Roof Wooden 2X1 Center Mirror`, `Roof Wooden 2X1 Corner`, `Roof Wooden 2X1 L`, `Roof Wooden 2X1 Middle`, `Roof Wooden 2X1 R`.

#### `Architecture/Walls` — 20

`Wall Arch`, `Wall Bottomcover`, `Wall Plaster Door Flat`, `Wall Plaster Door Round`, `Wall Plaster Door Roundinset`, `Wall Plaster Straight`, `Wall Plaster Straight Base`, `Wall Plaster Straight L`,
`Wall Plaster Straight R`, `Wall Plaster Window Thin Round`, `Wall Plaster Window Wide Flat`, `Wall Plaster Window Wide Flat2`, `Wall Plaster Window Wide Round`, `Wall Plaster Woodgrid`, `Wall Unevenbrick Door Flat`, `Wall Unevenbrick Door Round`,
`Wall Unevenbrick Straight`, `Wall Unevenbrick Window Thin Round`, `Wall Unevenbrick Window Wide Flat`, `Wall Unevenbrick Window Wide Round`.

#### `Architecture/Window shutters` — 8

`Windowshutters Thin Flat Closed`, `Windowshutters Thin Flat Open`, `Windowshutters Thin Round Closed`, `Windowshutters Thin Round Open`, `Windowshutters Wide Flat Closed`, `Windowshutters Wide Flat Open`, `Windowshutters Wide Round Closed`, `Windowshutters Wide Round Open`.

#### `Architecture/Windows` — 6

`Window Roof Thin`, `Window Roof Wide`, `Window Thin Flat1`, `Window Thin Round1`, `Window Wide Flat1`, `Window Wide Round1`.

#### `Village/Borders & trims` — 3

`Prop Exteriorborder Corner`, `Prop Exteriorborder Straight1`, `Prop Exteriorborder Straight2`.

#### `Village/Chimneys` — 2

`Prop Chimney`, `Prop Chimney2`.

#### `Village/Fences` — 5

`Prop Metalfence Ornament`, `Prop Metalfence Simple`, `Prop Woodenfence Extension1`, `Prop Woodenfence Extension2`, `Prop Woodenfence Single`.

#### `Village/Masonry details` — 4

`Prop Brick1`, `Prop Brick2`, `Prop Brick3`, `Prop Brick4`.

#### `Village/Standalone props` — 2

`Prop Crate`, `Prop Wagon`.

#### `Village/Structural support` — 1

`Prop Support`.

#### `Village/Vines` — 6

`Prop Vine1`, `Prop Vine2`, `Prop Vine4`, `Prop Vine5`, `Prop Vine6`, `Prop Vine9`.

### Modular Character Outfits – Fantasy

#### `Characters/Peasant/Female` — 5

`Female Peasant`, `Female Peasant Arms`, `Female Peasant Body`, `Female Peasant Feet`, `Female Peasant Legs`.

#### `Characters/Peasant/Male` — 5

`Male Peasant`, `Male Peasant Arms`, `Male Peasant Body`, `Male Peasant Feet`, `Male Peasant Legs`.

#### `Characters/Ranger/Female` — 7

`Female Ranger`, `Female Ranger Acc Pauldrons`, `Female Ranger Arms`, `Female Ranger Body`, `Female Ranger Feet`, `Female Ranger Head Hood`, `Female Ranger Legs`.

#### `Characters/Ranger/Male` — 7

`Male Ranger`, `Male Ranger Acc Pauldron`, `Male Ranger Arms`, `Male Ranger Body`, `Male Ranger Feet Boots`, `Male Ranger Head Hood`, `Male Ranger Legs`.

### Stylized Nature MegaKit

#### `Nature/Bushes` — 2

`Bush Common`, `Bush Common Flowers`.

#### `Nature/Clover` — 2

`Clover 1`, `Clover 2`.

#### `Nature/Ferns` — 1

`Fern 1`.

#### `Nature/Flowers` — 4

`Flower 3 Group`, `Flower 3 Single`, `Flower 4 Group`, `Flower 4 Single`.

#### `Nature/Grass` — 4

`Grass Common Short`, `Grass Common Tall`, `Grass Wispy Short`, `Grass Wispy Tall`.

#### `Nature/Mushrooms` — 2

`Mushroom Common`, `Mushroom Laetiporus`.

#### `Nature/Pebbles` — 11

`Pebble Round 1`, `Pebble Round 2`, `Pebble Round 3`, `Pebble Round 4`, `Pebble Round 5`, `Pebble Square 1`, `Pebble Square 2`, `Pebble Square 3`,
`Pebble Square 4`, `Pebble Square 5`, `Pebble Square 6`.

#### `Nature/Petals` — 5

`Petal 1`, `Petal 2`, `Petal 3`, `Petal 4`, `Petal 5`.

#### `Nature/Plants` — 4

`Plant 1`, `Plant 1 Big`, `Plant 7`, `Plant 7 Big`.

#### `Nature/Rock paths` — 10

`Rockpath Round Small 1`, `Rockpath Round Small 2`, `Rockpath Round Small 3`, `Rockpath Round Thin`, `Rockpath Round Wide`, `Rockpath Square Small 1`, `Rockpath Square Small 2`, `Rockpath Square Small 3`,
`Rockpath Square Thin`, `Rockpath Square Wide`.

#### `Nature/Rocks` — 3

`Rock Medium 1`, `Rock Medium 2`, `Rock Medium 3`.

#### `Nature/Trees` — 20

`Commontree 1`, `Commontree 2`, `Commontree 3`, `Commontree 4`, `Commontree 5`, `Deadtree 1`, `Deadtree 2`, `Deadtree 3`,
`Deadtree 4`, `Deadtree 5`, `Pine 1`, `Pine 2`, `Pine 3`, `Pine 4`, `Pine 5`, `Twistedtree 1`,
`Twistedtree 2`, `Twistedtree 3`, `Twistedtree 4`, `Twistedtree 5`.

### Universal Animation Library 2

#### `Animation/Libraries` — 2

`Ual2 Standard`, `Ual2 Standard Rm`.

#### `Animation/Mannequins` — 1

`Mannequin F`.

## Bibliothèque d’animations UAL2

Les conteneurs `Ual2 Standard` et `Ual2 Standard Rm` partagent **43 clips** et un rig de **65 articulations**. `Rm` est la variante root motion. `Mannequin F` est un mannequin skinné sans animation intégrée.

`A_TPose`, `Chest_Open`, `ClimbUp_1m`, `Consume`, `Farm_Harvest`, `Farm_PlantSeed`,
`Farm_Watering`, `Hit_Knockback`, `Idle_FoldArms_Loop`, `Idle_Lantern_Loop`, `Idle_No_Loop`, `Idle_Rail_Call`,
`Idle_Rail_Loop`, `Idle_Shield_Break`, `Idle_Shield_Loop`, `Idle_TalkingPhone_Loop`, `LayToIdle`, `Melee_Hook`,
`Melee_Hook_Rec`, `NinjaJump_Idle_Loop`, `NinjaJump_Land`, `NinjaJump_Start`, `OverhandThrow`, `Shield_Dash`,
`Shield_OneShot`, `Slide_Exit`, `Slide_Loop`, `Slide_Start`, `Sword_Block`, `Sword_Dash`,
`Sword_Heavy_Combo`, `Sword_Regular_A`, `Sword_Regular_A_Rec`, `Sword_Regular_B`, `Sword_Regular_B_Rec`, `Sword_Regular_C`,
`Sword_Regular_Combo`, `TreeChopping_Loop`, `Walk_Carry_Loop`, `Yes`, `Zombie_Idle_Loop`, `Zombie_Scratch`,
`Zombie_Walk_Fwd_Loop`.

## Limites à garder en mémoire

1. Le pack est un **kit d’assemblage** : les maisons complètes doivent être composées avec murs, coins, planchers, overhangs, façades de toit, couvertures, ouvertures et détails.
2. Les façades et sols minces ne remplacent pas automatiquement les collisions de gameplay.
3. Les nombreuses variantes de toit ne partagent pas toutes le même pivot ni les mêmes débords ; vérifier la silhouette en jeu.
4. Les végétaux alpha exigent le bon mode `MASK` ou `BLEND`, un matériau double face et un contrôle du vent/culling.
5. Les tenues modulaires sont des enveloppes de vêtements en T-pose ; ne pas les considérer comme des personnages finis.
6. Les miniatures sont des vues isométriques neutres. `Corner Exterior Topdown` et les cartes de vigne paraissent faibles dans cette vue mais restent des pièces spécialisées valides.
7. Toute création de modèle compatible doit suivre `docs/assets/MEDIEVAL_BLENDER_STYLE_GUIDE.md`.
