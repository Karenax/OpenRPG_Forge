# AV3 — Installation à la demande et placement 3D

## Périmètre

Cette tranche relie le Content Browser au placement déjà livré par G7-04. Elle ne copie jamais une
ressource sans manifeste, ne modifie pas le Vault et ne déplace pas la source de vérité dans la scène
du moteur 3D.

## Résultat livré

- commande `InstallAssetBlueprint` avec révision, dry-run, audit, patch inverse et historique du
  projet ;
- identité de blueprint déterministe dérivée de l’UUID du catalogue, provenance complète et palette
  ProjectContent favorite ;
- exigence d’un unique chunk modèle GLB requis, avec licence, identité et manifeste valides ;
- bouton « Installer + ajouter à la palette » dans le Content Browser ;
- réutilisation du glisser-déposer du Monde et du placement sur grille ;
- tags de style et override `visual.asset` ajoutés dans la même `PlaceEntityCommand` que l’entité ;
- chargeur `GodotAssetModelLibrary` partagé par Creator et runtime, avec confinement du chemin,
  contrôle SHA-256 et cache des scènes ;
- placeholder et diagnostic explicite si le modèle n’est plus chargeable.

## Preuves

- huit tests ciblés couvrent installation, dry-run, undo/redo, refus de licence, refus OBJ et
  héritage atomique des tags/overrides ;
- un test EngineAdapter refuse un GLB dont le hash diffère du manifeste ;
- les builds Toolset et Runtime passent sans avertissement ;
- le smoke `OPENRPG_FORGE_TOOLSET_G716_ASSETS_PASS` couvre l’installation à sec et le raccordement à
  la palette ;
- `OPENRPG_FORGE_TOOLSET_AV3_PLACEMENT_PASS` charge réellement un GLB autonome dans le viewport avec
  hash vérifié et sans erreur moteur ;
- le smoke Forgotten Forest conserve 22 manifestes et 18 instances de modèles de production.

## Limites explicites

- une source OBJ en staging doit terminer son round-trip Blender avant installation ;
- la désinstallation d’un blueprint depuis l’UI et la migration physique de packs restent à livrer ;
- la dispersion de masse et les recettes de biome constituent la prochaine tranche AV3.

## Critère de passage

Le lot est fermé lorsque compilation, tests ciblés, smokes Toolset/runtime, graphe d’architecture et
gate global passent sur le même état Git, puis que le commit est publié sur `main`.
