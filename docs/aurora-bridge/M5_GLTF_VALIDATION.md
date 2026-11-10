# Validation glTF M5

**Date :** 8 novembre 2026.
**Validateur :** Khronos glTF-Validator `2.0.0-dev.3.10` (cache npm local, aucune réseau).
**Rapport machine :** `Preuves-et-analyses/actives/aurora-bridge/m5/m5-gltf-validation.json`.

## Résultat

| Élément | Valeur |
| --- | ---: |
| Fichiers GLB validés | 10 |
| Erreurs | 0 |
| Warnings | 69 |
| Infos / hints | 0 |

Les 10 GLB se trouvent sous `apps/forge_studio/dist/forge-imports/` (personnage, architecture,
rochers, arbres). Ils couvrent le pipeline GLB du Studio, pas les assets du bundle M2.

## Warnings classés

| Code | Occurrences | Interprétation |
| --- | ---: | --- |
| `MESH_PRIMITIVE_GENERATED_TANGENT_SPACE` | 65 | tangentes générées au runtime ; non portable selon le validateur |
| `UNUSED_OBJECT` | 45 | nœuds/matériaux non référencés ; nettoyage possible |
| `NODE_SKINNED_MESH_NON_ROOT` | 2 | mesh skiné non racine ; les transforms parents ne s'appliquent pas |

Aucun warning n'est converti en succès silencieux. Les scènes de navigation M2 sont couvertes par
les tests WOK/`forge_navigation` et les transitions répétées M5, pas par ce validateur.

## Limites

- aucun GLB du bundle `ab_proving` n'était disponible : le bundle M1 est sans assets ;
- la scène de collision/navigation n'a pas été rejouée dans un viewer Khronos ;
- les warnings de tangentes et de nœuds skinés restent à corriger dans les assets source si une
  distribution les inclut ;
- aucune capture ou preuve visuelle n'est revendiquée.

## Reproduction

Le validateur est invoqué avec `validateBytes` sur chaque `.glb`. Le script de campagne est
`E:\OpenRPG_Forge\.build\tmp\validate_gltf.js` (outil local, non versionné) ; le rapport JSON est
conservé dans la campagne. Une automatisation durable devra ajouter le validateur au pipeline
d'assets et échouer sur toute erreur.
