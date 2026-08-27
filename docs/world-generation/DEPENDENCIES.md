# Dépendances de génération de monde

Les deux backends spécialisés sont facultatifs et ne sont pas vendored dans Git. Le manifeste
reproductible est `third_party/world-generation.dependencies.json`.

## Versions épinglées

| Composant | Pin | Licence | Usage |
|---|---|---|---|
| [Terrain3D](https://github.com/TokisanGames/Terrain3D) | release `v1.0.2-stable`, archive SHA-256 `a071850250ec5e596aa54da61c01d75768774eb379ee997584d426a45f4884a2` | MIT | terrain et hydrologie |
| [Godot Flow](https://github.com/yabadabu/godot_flow) | commit `284231f060099a8943eb56567c21732b7cd678d3`, submodules inclus | Apache-2.0 | backend PCG pour biomes, routes, établissements et scatter |
| [GF-PCGODOT](https://github.com/Framedworld/GF-PCGODOT) | commit `91471c10d671ecf9e9214d801e8f5b68d3a7df07`, submodules inclus | Apache-2.0 | candidat PCG isolé pour comparaison |

Terrain3D documente cette release pour Godot 4.4 à 4.6+ ; les deux fournisseurs PCG ciblent Godot
4.7. OpenRPG Forge a exécuté Terrain3D avec chacun des deux fournisseurs réels sous Godot
`4.7.1-stable` sur Windows, avec reçu vérifié, zéro fallback, sauvegarde/réouverture, collision,
navigation et candidat de promotion neutre. La preuve Linux et les exports cibles restent requis
avant une compatibilité de production multiplateforme.

## Installation

Depuis la racine du dépôt :

```powershell
./scripts/Install-WorldGenerationDependencies.ps1 -GodotProjectPath E:\MonModuleGodot -WhatIf
./scripts/Install-WorldGenerationDependencies.ps1 -GodotProjectPath E:\MonModuleGodot
./scripts/Install-WorldGenerationDependencies.ps1 -GodotProjectPath E:\FixtureGodot -GraphProvider gf-pcgodot
```

La première commande est une simulation pure : aucun téléchargement ni changement de fichier. Une
installation réelle télécharge l'archive Terrain3D, contrôle son SHA-256, clone le fournisseur PCG
sélectionné, checkout le commit détaché, initialise ses submodules, puis installe :

- `addons/terrain_3d` ;
- `addons/flow_nodes_editor` ;
- les fichiers PCG épinglés sous `bin`, sans supprimer les fichiers voisins ;
- `addons/openrpg_world_generation`.

Godot Flow et GF-PCGODOT possèdent les mêmes chemins d'addon et ne peuvent donc jamais être installés
ensemble. `godot-flow` reste la valeur par défaut ; `gf-pcgodot` est réservé aux fixtures de benchmark.
`-BuildPcgFromSource` reconstruit la GDExtension du fournisseur sélectionné pour la plateforme courante.
Le reçu `.openrpg/world-generation/provider-installation.json` enregistre clés, pins, révisions de
tree/submodules et SHA-256 des marqueurs réellement chargés.

Un dossier addon existant arrête l'installation. `-Force` remplace uniquement les addons ciblés et
les fichiers PCG en collision, avec staging et restauration locale en cas d'échec. Les destinations
sont résolues et vérifiées sous le projet Godot avant toute écriture.

Options :

```powershell
# Installer seulement le bridge et Godot Flow
./scripts/Install-WorldGenerationDependencies.ps1 -GodotProjectPath E:\MonModuleGodot -SkipTerrain3D

# Installer seulement le bridge et Terrain3D
./scripts/Install-WorldGenerationDependencies.ps1 -GodotProjectPath E:\MonModuleGodot -SkipPcgGodot
```

Activer ensuite dans **Project > Project Settings > Plugins** les addons installés. Créer les graphes
module sous `res://world/generation/graphs/` ou modifier le registre du bridge. Sans addon ou sans
graphe, l'exécution revient au backend Godot natif et journalise le fallback.

## Mise à jour d'un pin

Une mise à jour doit être explicite : vérifier release/commit et licence en amont, recalculer le
SHA-256 de l'archive, tester `-WhatIf`, faire une installation propre dans un projet jetable, exécuter
le smoke Godot avec et sans addons, puis mettre à jour manifeste, probe, documentation et ADR si le
contrat change. Ne jamais remplacer un pin par une branche mouvante ou une URL non HTTPS.
