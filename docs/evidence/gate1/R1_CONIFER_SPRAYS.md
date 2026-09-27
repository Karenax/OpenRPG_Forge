# Gate 1 — rameaux foliaires du conifère, 7 septembre 2026

Statut : **candidat Blender généré/réimporté et chargé en natif, non promu/non qualifié**.
Suite de `R1_CONIFER_COVERAGE_STUDY.md` : la réduction de surface seule ne suffit pas.

## Réalisation

Profil opt-in `branch_sprays` dans `Build-TreeFoliageCandidate.py`. Quatre sites
interpolés entre milieu et extrémité de chaque branche source, huit autour du
leader. Amas aplatis, orientation radiale plutôt que nuages sphériques. Le bois,
les collisions, les textures et les sources originales ne sont pas remplacés.
Le chêne conserve le profil coherent ; son GLB est identique au candidat bois réduit.
Sites identiques pour chaque LOD, cartes moins nombreuses et plus grandes au loin.
Ce n'est ni une retopologie humaine ni une commande Studio.

## Vérifications exécutées

Blender 4.5.10 LTS, base/manifeste et commande de `R1_WOOD_LOD_DERIVATIVE.md`, avec
`--canopy-profile branch_sprays --wood-profile reduced`, facteur d'aire par défaut 1.
Sortie `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-conifer-sprays-20260907/candidate/`.
Répétitions géométriques et doubles exports identiques, budgets LOD et sources
épinglées PASS. Pin : 46 508 / 7 522 / 1 228 triangles, contre 46 908 / 7 438 /
1 216 auparavant. Ne pas revendiquer une baisse globale : LOD1/2 légèrement accrus.
Surface foliaire 1 231,89 / 1 232,01 / 1 231,57 m² source, cohérente à ±10 %.

Huit images générées ; vues pin LOD0 à 0° et LOD2 inspectées. Rameaux plus lisibles
et sommet mieux couvert au proche ; grandes cartes encore visibles dans le LOD2
agrandi. Silhouette toujours étroite et régulière. Ni réalisme accepté, ni preuve
de transition à distance de jeu, ni correction du pin noir dans Forge déduite.

Réimport : deux GLB, UV/LOD/collisions et égalité des matériaux/images/samplers à
la base PASS. Quatre refus de sources PASS. Test synthétique des sites : interpolation,
leader, déterminisme, entrée intacte et rejet d'une paire incomplète PASS.
Les tests Rust ne sont pas rejoués, aucun Rust/WGSL modifié. `git diff --check` PASS.

- `candidate/candidate-proof.json` SHA-256
  `0a3ab8b7e2cef29f05239cf899ba05d32ebc35d8b9d70b52d01d5e0457ac17f0`.
- `reimport/reimport-proof.json` SHA-256
  `baf37213c8e9c51af6eb7e3373ab195f33d1949f8c286a370aa550f7ee658e39`.

## Chargement natif de la révision candidate 4

`Assemble.py` dans le root de preuve assemble une fixture séparée `runtime/` par
l'outil du dépôt. Révision 4 explicite ; vingt poses inchangées, cinq GLB autres
que le pin identiques à la révision 3. Hauteurs et pivots du catalogue dérivés des
bounds réimportés : pin base Y -0,0518859476 m, hauteur 18,2592280954 m, cible 5,65 m
inchangée. Ces dimensions sont confrontées aux meshes par l'adapter de chargement.
Catalogue et kit précédents restent intacts, aucune promotion dans le Studio.

`Run.ps1` utilise le binaire immuable du lot variantes, SHA-256
`69aae8b071684e9e311ccfb5f81bb1847ce8de2a3ed2cf9ef23d1000f9305c0b` : pas de nouveau
build Rust revendiqué. High/clear, caméra human_bridge, RX 7800 XT/Vulkan,
1920×1080 avant/après, sans VSync, 420 frames. Code 0 sans ERROR/panic observé,
kit 20/20, 36 colliders prêts, 650 arbres/7 800 primitives LOD. Sources épinglées
et six GLB contrôlés avant/après. Capture frame 300, callback 303, 116/116 pipelines
prêts et 211 observations stables. PNG inspecté : rameaux plus lisibles dans la
vallée, mais silhouette régulière et teinte sombre persistantes. Pas de traversée
interactive ni de transition LOD qualifiées par cette seule vue.

Smoke avec capture : 239 échantillons, 56,43 FPS moyens, p95 19,3494 ms,
1 % low 32,29 FPS. Ce n'est pas une comparaison de performance, ni un passage
des seuils. Aucun benchmark supplémentaire lancé pour cet examen de silhouette.

- `assembly-proof.json` : `5167e0f0439eda0f3b78e096d580e34f5ba42ff5f937985f4fdeb562e5dcf312`.
- `native/clear.png` : `2c52dfb247b2d098b942358a9a37e0dc597b5fbb49ddd12fd92134e13aab69d2`.
- `native/clear.json`, `clear.log` et `pins.json` conservés dans le root de preuve.

La révision 4 reste une candidate : rendu sombre, transitions, Web et revue humaine
restent ouverts. Pas de benchmark répété sur l'ancien candidat ; la révision 3
reste intacte. Gate 1 ouverte,
Gate 2 non commencée, revues humaines requises et aucune publication Git.
