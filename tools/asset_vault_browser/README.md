# OpenRPG Asset Atlas

Application locale séparée pour parcourir le contenu réel de `OpenRPG_AssetVault`, examiner les
formats GLB/glTF et construire une sélection précise utilisable comme base de discussion.

## Lancer sous Windows

Double-cliquer sur `Launch-Asset-Atlas.cmd`, ou exécuter :

```powershell
& '.\tools\asset_vault_browser\Launch-Asset-Atlas.ps1'
```

Le Vault par défaut est `E:\OpenRPG_AssetVault`. Il peut être remplacé par
`OPENRPG_ASSET_VAULT` ou par le paramètre `-Vault`.

Le logiciel écoute uniquement sur `127.0.0.1:8765` et ouvre le navigateur par défaut. La fenêtre
PowerShell doit rester ouverte pendant l'utilisation.

## Ce qui est indexé

- une entrée par asset Poly Haven et ambientCG, matériaux et HDRI compris ;
- une entrée par modèle logique de chaque catalogue de pack CC0 (Kenney existant, KayKit,
  Quaternius), en regroupant ses représentations GLB, glTF, FBX et OBJ ;
- GLB puis glTF sont préférés pour l'aperçu 3D interactif, suivis par FBX puis OBJ ;
- FBX/OBJ, matériaux et HDRI restent sélectionnables avec leur miniature et leur chemin exact.

L'application ne modifie jamais `originals/` ni `normalized/`. Elle lit les catalogues portables du
dépôt et sert les fichiers du Vault sur la boucle locale avec validation des chemins.

## Sélection

La sélection utilise le schéma JSON `1` et est enregistrée par défaut dans :

```text
%LOCALAPPDATA%\OpenRPG Forge\Asset Vault Browser\selection.v1.json
```

Les boutons **Copier pour Codex** et **Exporter JSON** produisent les identifiants stables, la source,
le pack et le chemin relatif de chaque modèle. Les fichiers volumineux ne sont chargés dans la vue
3D qu'après un clic explicite.

## Commandes utiles

```powershell
# Vérifier seulement l'index
python .\tools\asset_vault_browser\server.py --scan-only

# Port ou Vault personnalisés
python .\tools\asset_vault_browser\server.py --vault 'E:\OpenRPG_AssetVault' --port 8877 --open

# Tests ciblés du logiciel
python -m unittest discover -s .\tools\asset_vault_browser\tests -p 'test_*.py'
```

## Dépendance embarquée

La vue GLB/glTF utilise `@google/model-viewer` 4.3.1, empaqueté localement sous licence Apache-2.0.
Son texte de licence est conservé dans `static/vendor/LICENSE.model-viewer.txt`. Aucun téléchargement
JavaScript n'est requis au lancement.
