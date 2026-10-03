# Tidewater — extraction exacte du quai source

Le quai, son arche, ses accessoires intégrés, l'enseigne et les onze lanternes
suspendues sont observés sans modifier le clone propre
`dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`.
L'instrumentation de `buildPier` conserve les géométries fixes, l'enseigne et
chaque lanterne séparément, ainsi que les repères de rotation, 122 boîtes et
93 cylindres de collision, les lumières et les accessoires assemblés.

Deux extractions indépendantes, `pier-raw-001` et `pier-raw-002`, ont produit
197 fichiers byte à byte identiques. L'empreinte de la scène source complète,
terrain et collisions inclus, est identique avant/après instrumentation :
`e2f4799bb6f425afb4b9b800fb547528a037da15ad95f8c9d2182889c1e805f1`.
Le [rapport de vérification](../../../../.tidewater-integration/pier-parts-proof-001.json)
compare aussi les plages d'indices et les attributs des maillages au
`source-017/village/render.json` : 32 454 triangles fixes, 232 pour l'enseigne
et 3 508 pour les lanternes dans leur pose au repos. Le terrain source et le
clone Git restent inchangés.

Les matériaux ont été repliés selon les règles de `Village._assemble`, puis
les 28 pièces ont été converties en coordonnées locales autour de
`(55, 0, −64)` m. L'aller-retour vers les positions source a une erreur
maximale de `3,8147 × 10⁻⁶` m. Le paquet portable
`E:/OpenRPG_Forge/.tidewater-integration/pier-package-001/` contient
`pier.json`, 121 blobs de géométrie adressés par SHA-256 et la licence MIT,
pour 4 053 874 octets de contenu. Son `package.json` a le SHA-256
`f3d89e2a8b92d7f99044b40500d28525a223955b28c1deffe0e9f1deeb8c5e73` ;
il référence la même bibliothèque de matériaux vérifiée que les bâtiments.

Scripts reproductibles :
`export-pier-parts.mjs`, `verify-pier-parts.py`, `prepare-pier-frames.py`
et `package-pier-assets.py` sous `scripts/tidewater/`. Le lecteur `forge_assets`
valide désormais le paquet et rejette un fichier altéré (test ciblé réussi).
`forge_engine` prépare les 28 parties et sépare les 207 collisions solides des
8 capteurs d'échelle ; son test ciblé de placement déplacé réussit. Le Playtest
Studio sait charger et projeter ce paquet ; sa compilation WASM `--lib` et sa
construction de candidat WebGPU réussissent. La
[preuve Studio isolée](2026-09-30-studio-pier-editable-playtest.md) montre le
quai rendu, converti en placement éditable, sauvegardé, rouvert et parcouru
jusqu'à sa tête côté mer avec le joueur Rapier, sans saut. Un appui Rapier
invisible et dérivé franchit les marches source. L'animation de l'enseigne et des lanternes, et la
parité visuelle avec la référence restent ouvertes.
