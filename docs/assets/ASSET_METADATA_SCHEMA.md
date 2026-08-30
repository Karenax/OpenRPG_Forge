# Profil d'intelligence d'un asset — schéma v1

`AssetIntelligenceProfile` complète la provenance et la licence existantes. Il décrit ce que la Forge
et un agent peuvent raisonnablement faire d'un asset, et surtout sur quelles preuves ils s'appuient.

## Identité et stockage

- `schemaVersion` : version du contrat, actuellement `1` ;
- `externalId` : identifiant stable du catalogue ;
- `modelContentSha256` : empreinte du fichier modèle individuel ;
- `dependencyClosureSha256` : empreinte déterministe du modèle et de ses dépendances locales ;
- `generatedBy`, `confidence`, `evidence` : générateur, confiance globale et preuves par champ.

Le Vault externe conserve l'index dérivé dans
`metadata/asset-intelligence.v1.json`. Les overlays gouvernés et transportables vivent dans
`asset_manifests/intelligence/`. Le profil est aussi embarqué dans l'état d'un pack installé et dans
le manifeste copié vers un workspace.

## Sémantique et placement

| Groupe | Champs principaux | Usage |
| --- | --- | --- |
| Description | `descriptions.fr`, `descriptions.en` | recherche et explication à l'humain ou à l'agent |
| Sémantique | `semanticType`, `family`, `assemblyRole`, `useCases` | sélection d'un asset pour une intention de jeu ou de décor |
| Géométrie | `bounds`, `dimensionsMeters`, `sourceOriginMeters`, axes, `pivotClass` | échelle, cadrage et diagnostic du repère source |
| Placement | `surface`, `snapMode`, `suggestedPivot`, `anchors` | placement au sol, sur un mur, dans une ouverture ou dans un assemblage |
| Technique | maillages, sommets, triangles, matériaux, textures, armatures, os, animations | budget, compatibilité et préparation du pipeline |
| Gouvernance | `readiness`, `reviewStatus`, `confidence`, `evidence` | distinguer une mesure, une déduction et une revue humaine |

Une dimension peut être nulle sur un axe pour une géométrie plane. Elle ne peut jamais être négative
ou non finie. Le repère Forge est en mètres, `Y` vertical et `-Z` avant ; les mesures Blender `Z-up`
sont converties lors de la génération de l'overlay.

## États de confiance

- `catalogued` : profil minimal, sans mesure géométrique ;
- `semantic-enriched` : sens et usages déduits de champs gouvernés ;
- `geometry-declared` : dimensions déclarées par la source mais non remesurées ;
- `placement-suggested` : bornes mesurées et pivot ou ancres proposés, toujours révisables.

Dans `reviewStatus`, `generated-review-required` est l'état normal d'une génération automatique ;
`human-reviewed` est réservé à une action humaine explicite et traçable. La confiance exprime la
qualité de la preuve, pas une autorisation de publier l'asset.

## Construction automatique

La Forge construit le profil à trois frontières :

1. chargement du catalogue, avec un fallback pour chaque entrée et les overlays plus fiables ;
2. intégration d'un pack au Vault ou dans un workspace, avec recalcul des hashes du modèle réel ;
3. finalisation Blender, avec promotion des bornes, du pivot observé et des métriques techniques.

Une erreur d'inspection non critique n'empêche pas d'importer un modèle dont la licence et l'intégrité
sont valides : elle produit un profil partiel et une preuve explicite. Une violation du schéma, un
chemin dangereux ou un hash incohérent bloque en revanche l'écriture atomique.

## Utilisation IA

Le MCP expose le profil avec les résultats du catalogue. Les filtres `semanticType`, `assemblyRole`,
`placementSurface`, `readiness` et `humanReviewed` réduisent le choix avant tout raisonnement plus
coûteux. Un agent doit préférer les mesures aux déclarations, les déclarations aux heuristiques, et
demander une revue humaine lorsqu'un point de placement porte `requiresReview=true`.
