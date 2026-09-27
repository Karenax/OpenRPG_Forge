# Point 6 — qualification du kit courant

10 septembre 2026 — **audit structurel PASS ; qualification globale PARTIAL**.

Complément : six GLB réouverts dans Blender 5.2.1 LTS ; mesures des géométries
avec transforms composés et six copies `.blend` sauvegardées/réouvertes sans
changement de géométrie/matériaux. Ces copies sont prêtes pour la vraie retouche
humaine, non effectuée : voir `R1_CLOSURE_PROGRESS_20260910.md`. Aucun GLB actif modifié.

Kit exact : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-motion-final-20260910/kit`.
Build SHA-256 : `06a4ecb34e03aca33ba410a4378b36b38c272d78610364024dbaa2bf27566c1a`.
Audit : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-final-kit-audit-20260910/audit.json`.

## Réellement exécuté

`scripts/Audit-Gate1Kit.cjs` vérifie les hashes avant/après lecture, le confinement
des chemins, l'autonomie GLB, le validateur Khronos local, les matériaux et les
nœuds de collision. Aucun asset modifié ni importé dans ce lot.

| Asset | Triangles LOD0 / LOD1 / LOD2 | Nœuds UCX |
| --- | --- | --- |
| rock-a | 5120 / 1280 / 320 | 1 |
| rock-b | 5120 / 1280 / 320 | 1 |
| oak | 34158 / 2316 / 662 | 4 |
| pine | 46508 / 2310 / 1228 | 1 |
| wall | 7236 / 1408 / 12 | 1 |
| arch | 3564 / 924 / 96 | 3 |

Six GLB : **0 erreur, 38 avertissements** `MESH_PRIMITIVE_GENERATED_TANGENT_SPACE`.
Les 62 `UNUSED_OBJECT` sont informatifs. Trois LOD strictement décroissants par
asset. Il s'agit de triangles de nœuds nommés, pas de triangles soumis au GPU.
Matériaux et transforms enregistrés ; ceci ne certifie pas un pivot ergonomique,
la justesse géométrique des collisions ni la qualité visuelle de chaque LOD.

`Test-Gate1KitAudit.cjs` : contrôle du résultat courant, refus d'écrasement,
de hash erroné, de chemin sortant du kit et de chemin absolu : PASS.
Les fixtures négatives isolées ont été nettoyées ; les sources sont intactes.

Réutilisation explicite : rapports Blender 5.2.1 LTS `oak-reimport/inspection.json`
et `pine-reimport/inspection.json`, sous le même répertoire candidat. Hashes des
quatre PNG source/candidat revérifiés par `Verify-Gate1FinalEvidence.py`.
Ce ne sont ni de nouveaux réimports ni des retouches humaines réalisées ce tour.

## Provenance et reste obligatoire

Relecture technique du 10 septembre après le smoke Studio incrémental : hashes
des six GLB et six copies Blender toujours conformes à `inspection.json`.
Origines de tous les meshes LOD à (0,0,0). Les hauteurs composées LOD0/1/2
en mètres sont respectivement : rocher A 2,35/2,34/2,29 ; rocher B
1,77/1,76/1,72 ; chêne 17,75/17,78/18,13 ; pin 18,26/18,29/18,85 ;
mur 3,19/3,17/3,20 ; arche 3,60/3,49/3,51. Ce sont les coordonnées Blender
Z vertical. Les arbres LOD2 ont une enveloppe plus haute que LOD0 : à examiner
dans la revue de silhouette, pas une qualité LOD déduite des triangles décroissants.
Une origine partagée ne prouve pas un pivot ergonomique ; aucune retouche réalisée.

Le manifeste déclare `procedural_original_with_authored_edits`, licence
`LicenseRef-OpenRPG-Forge-Project-Generated`, distribution `internal_gate_proof_only`,
revue humaine de publication requise, aucun contenu Cadle/commercial utilisé.
Cette déclaration est conservée, pas transformée en autorisation de distribution.

Restent : revue des pivots/collisions/matériaux/LOD, véritable retouche humaine
suivie du réimport conservant son intention, chaîne finale de provenance et revue
de publication. Un script automatique ne remplace pas l'auteur humain requis.
La compétence d'import d'assets a conduit à conserver cette distinction et à
ne pas promouvoir le kit interne en bibliothèque distribuable.
