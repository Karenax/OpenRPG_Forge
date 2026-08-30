# ADR-0059 — Direction artistique de production pour les cartes générées

Date : 2026-08-29
Statut : accepté
UUID : `d15a0000-0000-4000-8000-000000000059`
Schéma : v1
Provenance : `author:openrpg-forge`

## Contexte

Le worker WorldGeneration savait produire et valider une scène neutre, mais cette preuve technique
pouvait être promue comme livrable final. Le runtime affichait alors un sol coloré sans matériau réel,
des cônes ou des boîtes comptés comme végétation, et une route limitée au conteneur technique ensuite
supprimé. Les bâtiments pouvaient être correctement présents tout en lévitant au-dessus du terrain, et
un point joueur placé près d'une rupture de relief rendait la caméra difficile à contrôler. Une somme
de marqueurs valides ne constitue donc pas une carte de qualité production.

## Décision

- Une promotion nouvelle exige `WorldGenerationVisualTier.Production`, une zone extérieure de référence
  explicite et une provenance de revue commençant par `human-forward-plus:`.
- La zone de référence fournit un vocabulaire, pas sa géographie : matériaux de terrain, ressources de
  scatter, strates végétales, ambiance et matériaux de route sont remappés sur la surface générée.
- Au moins une texture réelle et trois strates distinctes — canopée, arbustes, couverture du sol — sont
  obligatoires. Les proxies `technical_proxy` ne comptent jamais comme ressources de production.
- Chaque route demandée par la recette devient une spline texturée, déterministe, recalée point par
  point sur le heightfield final et déplacée autour de l'établissement en maximisant sa distance aux
  fondations. Une route demandée sans modèle texturé dans la référence bloque la promotion.
- La contribution de la texture principale du shader de terrain possède un plancher visible, y compris
  sur les grands plateaux dont la couleur de sommet est dominée par la roche.
- La couverture du sol et les arbustes d'un établissement généré portent une zone de composition
  locale dérivée de ses fondations. Le scatter échantillonne directement cette emprise et calcule son
  budget sur sa surface réelle : la densité visuelle et le temps de génération ne dépendent plus des
  dimensions totales du terrain.
- Chaque point joueur réserve une emprise caméra complète de 64 m, limitée à 12 m de relief, et reste
  entre -0,25 m et 3,5 m du sol. Chaque fondation générée reste à 1,25 m maximum de la surface.
- Une patrouille à allure de marche doit disposer d'une animation de marche ; une créature qui ne
  fournit qu'une boucle de course doit se déplacer à au moins 2,4 m/s. Au runtime, toute patrouille
  terrestre extérieure rééchantillonne le terrain à chaque déplacement et le pivot visuel des
  créatures est aligné sur le point le plus bas de leurs bornes.
- Le manifeste persiste les comptes de couches texturées, ressources de scatter, strates, features,
  routes texturées et preuves spatiales dans une empreinte SHA-256. Le runtime supprime les visuels
  techniques uniquement lorsque le terrain et les biomes canoniques ainsi prouvés sont chargés.

## Preuves

- Promotion et défauts de qualité :
  `tests/Forge.Application.Tests/WorldGeneration/WorldGenerationWorkflowTests.cs`.
- Persistance et rejet des manifestes incomplets :
  `tests/Forge.ModuleFormat.Tests/Workspaces/WorldGenerationStoreTests.cs`.
- Contrat du shader : `tests/Forge.EngineAdapter.Tests/World/GodotTerrainMaterialAdapterTests.cs`.
- Qualification produit : lancement Windows Godot 4.7.1 en Forward+, inspection du chemin, des trois
  PNJ, des six maisons, de la végétation multi-strates, du sol texturé et des contrôles de caméra.
- Qualification locomotion et densité : tests du budget de scatter local, de l'ancrage visuel aux
  pieds, du suivi de terrain par segment et de la cohérence vitesse-animation.

## Conséquences

Une génération technique peut toujours être examinée et déboguée, mais elle ne peut plus devenir le
monde canonique par simple présence de nœuds. La génération de production dépend d'une référence
artistique explicitement choisie et d'une revue visuelle réelle. Le placement des routes reste
déterministe et générique ; une retouche humaine peut ensuite être protégée par la politique de
régénération sans introduire de dépendance à Solaren ou à un kit particulier dans le moteur.

## Remplacement

Cette décision complète l'ADR-0058 sur les preuves spatiales et l'ADR-0055 sur la tranche runtime.
Elle ne remplace ni le cycle MCP dry-run/apply, ni les règles de provenance des assets.
