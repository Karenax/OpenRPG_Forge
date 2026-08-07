# Rapport d'import de la bibliothèque médiévale-fantastique

Date : 7 août 2026
Vault : `E:\OpenRPG_AssetVault`
Politique : archives originales immuables, CC0 uniquement, aucun nouveau téléchargement Kenney.

## Résultat

Asset Atlas indexe maintenant 6 140 entrées consultables, modèles, matériaux et HDRI compris. Le total par source est de 4 681 entrées Kenney préexistantes et conservées, 428 KayKit, 428 Quaternius, 553 Poly Haven et 50 ambientCG.

Cette mission ajoute 899 entrées logiques : 423 KayKit, 428 Quaternius, 40 matériaux PBR et 8 HDRI. Les 5 personnages Adventurers KayKit déjà présents sont seulement reliés au nouvel index multi-catalogues.

Le lot de cette mission représente 1 278 144 697 octets d'archives et fichiers officiels nouveaux, soit environ 1,19 Gio. Les 1 687 fichiers 3D acquis ont des SHA-256 tous distincts. Asset Atlas regroupe toutefois 836 représentations de format d'un même nom logique (glTF/FBX/OBJ) et préfère GLB, glTF, FBX, puis OBJ.

## Packs téléchargés et intégrés

| Source | Pack/version | Modèles logiques | Distribution officielle | Licence |
|---|---|---:|---|---|
| KayKit | Character Pack: Skeletons 1.0 | 17 | GitHub KayKit, commit `15b62b9…` | CC0 vérifiée dans `LICENSE.txt` |
| KayKit | Dungeon Remastered 1.0 | 406 | GitHub KayKit, commit `b0ca9bd…` | CC0 vérifiée dans `LICENSE.txt` |
| KayKit | Character Pack: Adventurers 1.0 | 5 | archive déjà présente, réutilisée | CC0 vérifiée |
| Quaternius | Medieval Village MegaKit Standard-1.0 | 176 | Godot Asset Store | CC0 1.0 |
| Quaternius | Fantasy Props MegaKit Standard-1.0 | 94 | Godot Asset Store | CC0 1.0 |
| Quaternius | Stylized Nature MegaKit Standard-1.0 | 68 | Godot Asset Store | CC0 1.0 |
| Quaternius | Universal Animation Library Standard-1.0 | 3 conteneurs | Godot Asset Store | CC0 1.0 |
| Quaternius | Universal Animation Library 2 Standard | 2 conteneurs | OpenGameArt, page auteur | CC0 |
| Quaternius | LowPoly Modular Dungeon Pack | 48 | OpenGameArt, page auteur | CC0 |
| Quaternius | LowPoly Animated Monsters | 4 | OpenGameArt, page auteur | CC0 |
| Quaternius | LowPoly Animated Knight | 9 | OpenGameArt, page auteur | CC0 |
| Quaternius | LowPoly Medieval Weapons | 24 | OpenGameArt, page auteur | CC0 |

Compléments : 24 matériaux PBR Poly Haven 2K, 8 HDRI Poly Haven 2K et 16 matériaux ambientCG 2K-JPG. Les cartes PBR, miniatures, archives et HDRI sont stockés localement avec leurs empreintes.

## Nombre d'assets ajoutés par catégorie

Les catégories se recouvrent pour permettre la recherche. Les comptes portent sur les entrées logiques nouvelles auxquelles chaque catégorie est attachée.

| Catégorie | Entrées ajoutées |
|---|---:|
| Village | 270 |
| Architecture | 630 |
| Dungeon | 454 |
| Nature | 75 |
| Props | 816 |
| Characters | 35 |
| Creatures | 21 |
| Weapons | 555 |
| Animations | 35 |
| Materials | 784 |
| HDRI | 8 |

## Traçabilité et déduplication

- chaque pack contient auteur, page source, version, date de récupération, licence, URL de licence, archive originale, taille et SHA-256 dans son catalogue JSON ;
- les téléchargements partagent le cache adressé par SHA-256 du Vault ;
- aucun des 1 687 fichiers 3D audités n'était identique par SHA-256 ;
- 836 variantes de représentation sont regroupées sous une seule entrée logique dans Asset Atlas ;
- Adventurers existait déjà et n'a pas été retéléchargé ; aucun pack Kenney n'a été retéléchargé.

## Téléchargements manuels et erreurs

Huit packs exclusivement accessibles par le flux Itch.io protégé restent listés dans `ASSETS_MANUAL_DOWNLOAD.md`. Deux détections OpenGameArt ont d'abord échoué à cause du caractère `@` encodé dans l'URL ; le parseur a été corrigé et la seconde exécution a importé les cinq packs avec zéro erreur finale. Tous les catalogues finaux déclarent `error_count: 0`.

## Validation

- Asset Atlas : 6 140 entrées, 5 752 modèles directement prévisualisables en GLB/glTF ; les FBX/OBJ, matériaux et HDRI utilisent leur miniature locale ;
- audit des cinq catalogues nouveaux : 59 enregistrements, 184 fichiers et 1 755 dépendances glTF contrôlés, zéro erreur ;
- tests Asset Atlas : 5/5 réussis ;
- tests ciblés catalogue et livraison : 4/4 réussis ;
- rapport de livraison : 665 entrées, 617 ressources 3D locales, 48 matériaux/HDRI indexés, 665 fiches de provenance et 16/16 contrôles réussis ;
- portail complet `scripts/Verify.ps1` : réussi jusqu'au marqueur terminal `OPENRPG_FORGE_VERIFICATION=PASS` ;
- licence distribuable : uniquement `CC0-1.0` dans les nouveaux catalogues ;
- exclusions contrôlées : aucun téléchargement Kenney nouveau, aucun Synty, Fab, CC-BY, CC-NC, CC-SA ou contenu propriétaire.
