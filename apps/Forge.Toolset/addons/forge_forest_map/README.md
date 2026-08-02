# Forge Forest Map

Ce module transforme le prototype de biomes de
`GamesNotDeveloped/godot-forest-demo` en outil de carte Forge.

## Ouvrir l’outil

1. Ouvrir `apps/Forge.Toolset` dans Godot .NET 4.7.1.
2. Ouvrir `res://addons/forge_forest_map/scenes/forge_forest_map_demo.tscn`.
3. Sélectionner le nœud racine `ForgeForestMap`.
4. Ouvrir le panneau inférieur **Carte forêt**.
5. Choisir un calque, activer **Peindre dans la vue 3D**, puis peindre au clic gauche.

Le masque est enregistré dans `res://forest_maps/`. Le JSON voisin est la définition versionnée de
la carte ; les `MultiMesh` générés sont des vues reconstructibles.

## Calques

- rouge : densité de forêt ;
- vert : densité de fleurs ;
- bleu : densité de rochers ;
- alpha : zone interdite, exclue des trois scatters ;
- clairière : efface rouge, vert et bleu sans effacer la zone interdite.

## Remplacer les modèles

Le panneau accepte une scène ou un mesh déjà importé dans le projet. **Bibliothèque Forge…** lit
`asset_manifests/catalogs/cc0_3d_all.json`, vérifie `license_verified`, copie le dossier de l’asset
installé dans `res://forest_maps/imported/`, puis conserve sa fiche dans `provenance.json`.

Le Vault est lu depuis `OPENRPG_ASSET_VAULT`, ou depuis le chemin externe par défaut voisin du dépôt.

## Attribution

Voir `THIRD_PARTY_NOTICES.md`, le texte CC BY 4.0 dans `licenses/`, et les `license.txt` conservés
avec chaque modèle fourni.
