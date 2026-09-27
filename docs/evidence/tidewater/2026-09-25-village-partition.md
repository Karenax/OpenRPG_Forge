# Tidewater — partition exacte des lots statiques du village

L'export v3 relève les tailles de chaque lot avant `_assemble`, en plus des
plages émises par les 23 bâtiments. `partition-building-scene.py` reproduit
les décalages de fusion des matériaux et de concaténation des lots opaques.
Il vérifie que chaque plage d'indices calculée est identique à la plage source.
L'appartenance à un bâtiment est donc issue de la construction source, sans
rapprochement spatial ni ambiguïté entre triangles géométriquement identiques.

Les triangles non revendiqués reçoivent l'identifiant zéro (décor restant).
Les identifiants 1..23 correspondent à la liste d'identités de bâtiments du
descripteur. Les plages contiguës sont produites dans l'ordre d'origine.

## Preuves

Racine `E:/OpenRPG_Forge/.tidewater-integration/`.

- `building-parts-005` et `006`, schéma v3 : 778 fichiers identiques,
  observation sans changement de la scène finale ni du terrain.
  Rapport `building-parts-proof-003.json`.
- `village-partition-001` et `002` : sept lots statiques, 137656 triangles,
  dont 65363 attribués aux bâtiments et 72293 au décor restant.
- 121 plages ordonnées ; aucune superposition d'attribution. Leur
  concaténation reproduit exactement chaque flux d'indices source.
- `village-partition-proof-001.json` confirme la répétabilité des fichiers,
  les comptes et les hashes du descripteur.

Les 24 autres nœuds du village acquis ne font pas partie de ces sept lots et
doivent rester dans la scène. Ce décompte ne comprend pas les poissons omis
par l'acquisition initiale. La partition est un artefact de préparation : les
dessins natifs séparés, leur ordre de transparence et leur coût GPU restent
à valider. Les pivots, les transformations canoniques, le catalogue et Studio
ne sont pas encore raccordés. Aucun commit ni publication.
