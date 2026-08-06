# G13-D — Butin, vol à la tire et cadavres

## Périmètre

- `ORF-CRE-023` — objets droppables par entrée, slot et créature, avec quantité et probabilité ;
- `ORF-CRE-024` — vol à la tire gouverné par compétences, perception, consentement serveur, crime,
  délai et audit ;
- `ORF-CRE-032` — cadavre visuel ou conteneur, durée de vie, propriété, résurrection et nettoyage.

## Contrat livré

`CreatureLootProfile` est le document canonique v1 attaché au blueprint de créature. Chaque entrée
possède un UUID stable, une référence vers la table de butin Combat, une plage de quantité, une
probabilité, des usages mort/vol, une politique de propriété et un mode d’instanciation partagé,
par joueur ou par groupe. Le profil sépare explicitement le vol et le cycle de vie du cadavre.

`SetCreatureLootHandler` impose révision, type de blueprint, lecture seule, références d’objets et
de compétences, UUID uniques, dry-run, patch, inverse, audit et historique. Le projet agrégé passe
en `0.6.25`, le workspace en `1.19.0`, et `0.6.24`/`1.18.0` migrent avec un profil absent. Le
round-trip JSON et l’export/chargement `.orpmod` conservent le document.

L’écran **Butin, vol & cadavres** fournit un catalogue, trois onglets et un résumé jouable. Il
prévisualise puis applique la même commande que les autres clients. `CreatureLootScriptService`
offre un handle UUID/schéma en lecture seule et résout mort ou tentative de vol sans accès aux
fichiers. Dans la Crypte, le Gardien produit désormais une dépouille visible et interactable : la
touche d’interaction transfère son butin résolu à l’inventaire. Les anciens modules sans profil
conservent leur table Combat historique.

## Preuves

- domaine : `CreatureLootProfileTests` — validation, mort, propriété, instanciation, vol, crime,
  cooldown et fallback ;
- commande et handle script : 4 tests ciblés ;
- sérialisation et `.orpmod` : migration, round-trip exact et export/chargement réel ;
- runtime ciblé : création du cadavre, interaction et obtention de `warden-ember` ;
- smoke Toolset : `OPENRPG_FORGE_TOOLSET_G13D_CREATURE_LOOT_PASS` ;
- portail intégré : 865 tests .NET, 24 tests Python généraux, 30 tests du pipeline d’assets,
  smokes Toolset/runtime/packages, graphe 4 696 nœuds / 11 587 relations et marqueur final
  `OPENRPG_FORGE_VERIFICATION=PASS`.

## Limites explicites

- Le prototype matérialise un conteneur de cadavre local. La réplication de plusieurs instances de
  butin est décrite par le contrat mais relève du serveur autoritaire lors d’une partie réseau.
- Le profil de ragdoll sélectionne une politique canonique ; le corps de démonstration emploie une
  représentation légère afin de rester fonctionnel en headless et sans asset imposé.
