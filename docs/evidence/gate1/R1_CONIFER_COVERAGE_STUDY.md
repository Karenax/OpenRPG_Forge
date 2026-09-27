# Gate 1 — étude de couverture du conifère, 7 septembre 2026

Statut : expérience Blender vérifiée, **insuffisante pour corriger le pin sombre**.
Aucune promotion, modification de carte, changement du renderer ou qualification.

## Inspection et hypothèse

GLB du dérivé bois : trois matériaux PineNeedle en BLEND, double face, normales
à 0,82, textures présentes, pas de facteur de couleur noir ajouté dans le GLB.
Le cache de vent clone les matériaux PBR ; le shader gouverné conserve leur
couleur en météo sèche. Cela ne démontre pas l'absence de tout défaut de rendu,
mais ne justifie pas un éclaircissement global du moteur ou de la lumière.
Hypothèse testée : réduire les recouvrements de cartes sans toucher aux matériaux.

## Changement isolé et preuve

`Build-TreeFoliageCandidate.py` accepte `--conifer-area-scale` borné à [0,25 ; 1],
fini, défaut 1. Longueur et demi-largeur multipliées par sqrt(facteur) pour le
conifère ; points d'attache, orientations aléatoires, bois et textures conservés.
Ce paramètre n'est pas exposé dans le Studio ni promu dans la recette canonique.

Exécution Blender 4.5.10 LTS : mêmes arguments/base/manifeste que
`R1_WOOD_LOD_DERIVATIVE.md`, avec `--canopy-profile coherent --wood-profile reduced
--conifer-area-scale 0.5`, sortie nouvelle
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-conifer-coverage-20260907/candidate`.

Surface foliaire mesurée LOD0/1/2 : 615,95 / 615,99 / 615,78 m² dans le repère
source, ratios 0,50 au candidat précédent. Ni surface projetée opaque à l'écran,
ni densité botanique qualifiée. Triangles inchangés : 46 908 / 7 438 / 1 216.
GLB du chêne exactement identique au dérivé précédent (SHA-256 comparé).
Répétitions de géométrie et doubles exports identiques, cohérence d'aire LOD ±10 %,
budgets et sources épinglées PASS. Huit images produites ; comparaison des vues
proches pin à 0° avant/après inspectée : plus ajouré, mais régularité, sommet nu
et aspect sombre restent visibles. Pas de gain artistique final revendiqué.

`Test-TreeFoliageCandidate.py` : réimport des deux GLB PASS, matériaux/images/samplers
identiques à la base, UV/LOD/collisions contrôlés, quatre refus de sources PASS.
Option NaN refusée (sortie 1) avant création de dossier. Aucun test Rust relancé,
Rust/WGSL inchangés. Aucun assemblage/runtime/Web ou benchmark de cette variante.

- `candidate/candidate-proof.json` SHA-256
  `ff5e6da725f4357bcba1aadf8b5707337a0093bbb3a428e45d516cae78cb113d`.
- `reimport/reimport-proof.json` SHA-256
  `20abd79e38fdb254503aaf954702355db87f05c575568ef13e78f520cc9eaa11`.

## Décision

Ne pas promouvoir la seule réduction de surface comme correction du conifère.
La forme de la couronne et la disposition des branches/amas doivent être reprises
dans une règle génératrice, avec continuité des LOD, avant de refaire un test intégré.
Éviter une boucle de réglage de luminosité et de benchmarks inchangés. Le candidat
intégré reste la révision 3 (bois réduit, couverture originale). Gate 1 ouverte,
validations humaines toujours requises, aucune publication Git.
