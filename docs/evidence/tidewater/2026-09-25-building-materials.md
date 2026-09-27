# Tidewater — matériaux des bâtiments séparés

L'export des pièces conserve désormais les paramètres originaux en f64 avant
la fusion. Cela permet d'effectuer les additions et remappages dans la même
précision que JavaScript, puis de convertir une seule fois en f32 comme le
buffer final source. Les anciennes acquisitions sont conservées.

`prepare-building-materials.py` reproduit les remappages de `_assemble` :
verre vers bois avec motif 9, corde vers matériau dur, drapeaux et tissus vers
tissu, filets vers leur matériau dédié. Les autres attributs restent inchangés.
Les pièces référencent les sept identités de matériau du village acquis.

## Preuves

Racine `E:/OpenRPG_Forge/.tidewater-integration/`.

- `building-parts-003` et `004`, schéma v2 : 778 fichiers bit-identiques,
  y compris les paramètres f64. `building-parts-proof-002.json` confirme leur
  finitude et leur correspondance avec les attributs f32 déjà extraits.
- `building-materials-001` et `002` : 560 fichiers dédupliqués bit-identiques.
  Descripteur SHA-256
  `ab274a1d01f80f891d2b3223ce4d515f98d04dba3daf6e533fa165b2eece0e18`.
- Les 65363 triangles des 23 bâtiments sont retrouvés dans les géométries
  statiques de `source-017/village/render.json`, dans le bon matériau. Chaque
  comparaison porte sur les trois sommets ordonnés, leurs positions, normales,
  UV, teintes et paramètres shader. Le multiensemble source est consommé au
  fur et à mesure pour ne pas compter deux fois un même triangle disponible.
- Il reste 72293 triangles dans ces lots statiques, correspondant au décor
  non extrait par les quatre générateurs de bâtiments. Les autres nœuds
  dynamiques du village sont hors de ce compte.
- `building-materials-proof-001.json` conserve les comptes et hashes.

## Suite

La preuve établit l'égalité des attributs et matériaux des pièces avec la scène
acquise, pas encore celle d'un rendu natif séparé. Il faut partitionner les lots
sans perdre le décor restant, préserver l'ordre de dessin approprié, définir
les pivots et instances canoniques, puis raccorder catalogue et Studio.
Les paramètres f64 sont une donnée de provenance, pas le format GPU.
Aucun commit ni publication ; parité globale toujours incomplète.
