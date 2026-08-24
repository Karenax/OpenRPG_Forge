# Rapport d'import de la bibliothèque médiévale-fantastique

Date : 7 août 2026 — compléments manuels validés les 21 et 24 août 2026
Vault : `E:\OpenRPG_AssetVault`
Politique : archives originales immuables, CC0 uniquement, aucun nouveau téléchargement Kenney.

## Résultat

### Pack Médiéval du 24 août 2026

Les cinq archives fournies dans `E:\3d unity` sont regroupées par
`asset_manifests/packs/medieval.json`. Le pack expose 365 modèles logiques locaux, issus de 1 136
fichiers glTF/GLB/FBX/OBJ et de 728 387 757 octets d’archives originales. Les cinq licences internes
déclarent CC0 1.0 Universal ; l’audit ne trouve ni chemin dangereux, ni chiffrement, ni lien
symbolique, ni exécutable.

Fantasy Props, Medieval Village, Modular Character Outfits et Stylized Nature correspondaient déjà
octet pour octet aux originaux du Vault. Seule la version Standard avec root motion de Universal
Animation Library 2 a été ajoutée : 6 fichiers 3D, 3 modèles logiques et un original SHA-256
`4008ea208a604773a2b2177d965f0f5d3195498b5bf838c3f5785d68e95f2a68`.

Asset Atlas indexe désormais 6 470 ressources logiques, dont 6 049 directement prévisualisables,
et expose une facette **Médiéval** de 365 modèles tous présents localement. Le catalogue gouverné
compte 674 entrées ; Quaternius représente 471 ressources logiques.

### Complément manuel du 21 août 2026

Les huit archives `AV7-M1` à `AV7-M8` ont été récupérées manuellement depuis les pages officielles
Itch.io, puis importées par `tools/asset_pipeline/import_manual_cc0_packs.py`. Elles ajoutent 327
modèles logiques à Asset Atlas, issus de 1 190 fichiers glTF/GLB/FBX/OBJ et de 463 485 351 octets
d'archives originales. Les 1 190 fichiers 3D possèdent des SHA-256 distincts.

Asset Atlas indexe désormais 6 467 entrées, dont 6 049 directement prévisualisables. Le total par
source est de 4 681 entrées Kenney préexistantes, 715 KayKit, 468 Quaternius, 553 Poly Haven et
50 ambientCG.

| Source | Pack/version | Modèles logiques | Licence contrôlée |
|---|---|---:|---|
| Quaternius | Universal Base Characters Standard | 10 | `License_Standard.txt` et page officielle CC0 |
| Quaternius | Modular Character Outfits - Fantasy Standard 2.1 | 24 | `License_Standard.txt` et page officielle CC0 |
| Quaternius | LowPoly Animated Easy Enemies January-2019 | 6 | page officielle CC0 et archive épinglée par SHA-256 |
| KayKit | Forest Nature Pack 1.0 FREE | 105 | `License.txt` CC0 |
| KayKit | Character Animations 1.1 FREE | 16 | `License.txt` CC0 |
| KayKit | Fantasy Weapons Bits 1.0 FREE | 31 | `License.txt` CC0 |
| KayKit | RPG Tools Bits 1.0 FREE | 59 | `License.txt` CC0 |
| KayKit | Resource Bits 1.0 FREE | 76 | `License.txt` CC0 |

Les catégories du complément se recouvrent : `Characters` 56, `Creatures` 6, `Animations` 56,
`Weapons` 90, `Props` 295, `Nature` 181 et `Materials` 181.

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

Les huit packs Itch.io sont maintenant importés et consignés dans `ASSETS_MANUAL_DOWNLOAD.md`.
L'archive Universal Base Characters contenait trois URI glTF erronées (`*_png.png`) ; l'importeur
les corrige uniquement dans la copie normalisée vers les textures présentes, sans modifier
l'archive originale. Deux détections OpenGameArt avaient auparavant échoué à cause du caractère
`@` encodé dans l'URL ; le parseur avait été corrigé. Tous les catalogues finaux déclarent
`error_count: 0`.

## Validation

- complément manuel initial : 8/8 archives, 1 190 fichiers 3D, 327 modèles logiques et zéro doublon SHA-256 ;
- catalogue manuel actuel : 9 archives, 1 196 fichiers 3D, 330 modèles logiques et zéro erreur ;
- pack Médiéval : 5 archives, 1 136 fichiers 3D, 365 modèles logiques locaux et licences CC0 internes vérifiées ;
- Asset Atlas après le pack : 6 470 ressources logiques, 6 049 prévisualisables et 365 modèles dans la facette Médiéval ;
- intégrité du complément : 8 archives et 700 dépendances glTF contrôlées, zéro erreur ;
- Asset Atlas après complément : 6 467 entrées, dont 6 049 directement prévisualisables ;

- Asset Atlas : 6 140 entrées, 5 752 modèles directement prévisualisables en GLB/glTF ; les FBX/OBJ, matériaux et HDRI utilisent leur miniature locale ;
- audit des cinq catalogues nouveaux : 59 enregistrements, 184 fichiers et 1 755 dépendances glTF contrôlés, zéro erreur ;
- tests Asset Atlas : 5/5 réussis ;
- tests ciblés catalogue et livraison : 4/4 réussis ;
- rapport de livraison : 665 entrées, 617 ressources 3D locales, 48 matériaux/HDRI indexés, 665 fiches de provenance et 16/16 contrôles réussis ;
- portail complet `scripts/Verify.ps1` : réussi jusqu'au marqueur terminal `OPENRPG_FORGE_VERIFICATION=PASS` ;
- licence distribuable : uniquement `CC0-1.0` dans les nouveaux catalogues ;
- exclusions contrôlées : aucun téléchargement Kenney nouveau, aucun Synty, Fab, CC-BY, CC-NC, CC-SA ou contenu propriétaire.
