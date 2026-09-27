# Gate 1 — couverture foliaire cohérente, 2026-09-06

Statut : **candidat isolé amélioré en capture native ; non promu, non qualifié artistiquement**.
Suite du défaut clairsemé observé dans `R1_FOREST_PREVIEW.md`.

## Changement et portée

Le générateur de candidat dispose de `--canopy-profile coherent` ; son défaut reste `legacy`.
Le worker de production, la recette source, le blend et les six GLB sources ne sont pas modifiés.
Le feuillage prend les centres de branches du LOD0 pour les trois niveaux. Le nombre de cartes
reste borné par celui du candidat précédent ; leur taille compense sa réduction pour conserver
une surface foliaire nominale comparable. La longueur proche est multipliée par 1,4.
Les cartes lointaines représentent donc des groupes de feuilles, pas des feuilles de taille réelle.

Ce changement ne prouve pas à lui seul une silhouette identique ni une couverture alpha constante
à l'écran. Le bois des LOD reste celui de la source ; le mouvement des transitions reste à examiner.
Aucune correction de mipmaps/alpha cutoff ni nouveau shader n'est introduit dans ce lot.

## Résultats mesurés

| Asset | Triangles LOD0 / LOD1 / LOD2 | Surface foliaire source LOD0 / LOD1 / LOD2 (m²) |
| --- | --- | --- |
| Chêne | 34 158 / 9 436 / 1 012 | 2 569,32 / 2 569,33 / 2 568,59 |
| Pin | 46 908 / 10 282 / 2 336 | 1 231,89 / 1 231,99 / 1 231,56 |

Ces surfaces sont celles des triangles dans le repère source, pas des pixels opaques ni une
mesure de performance. Les LOD éloignés ont moins de triangles que le candidat précédent.
La couverture accrue peut augmenter l'overdraw ; aucun gain GPU n'est déduit de ces comptes.

Génération répétée des six meshes et doubles exports GLB de la même scène : identiques.
Surface par LOD contrôlée dans ±10 % du niveau proche. Quatre vues Blender produites ; vues
chêne/pin à 0° inspectées. Réimport GLB : noms, UV, LOD, collisions et égalité des matériaux,
images/samplers à la base PASS. Quatre cas de rejet de sources périmées/arbres retouchés PASS.
Tous les hashes des sources originales sont conservés. Il ne s'agit pas de deux rebuilds indépendants.

Le réimport conserve maintenant les bounds de chaque LOD dans le rapport, en repère Blender Z-up.
Chêne LOD0 : base -0,06517145 m, sommet 17,68106842 m, hauteur 17,74624 m. Le catalogue natif
utilise cette hauteur vérifiée et la hauteur cible inchangée de 5,65 m.

La fixture finale utilise la **révision 2** du kit candidat (nouvelle option d'assemblage), pour
ne pas présenter de nouveaux GLB sous la même révision que le candidat précédent. Révision zéro
refusée avec sortie 2, sans création de dossier de sortie. Les 20 poses manuelles sont inchangées.
La première fixture intermédiaire de révision 1 est conservée, mais n'est pas celle du smoke final.

## Preuve native

Même binaire Rust que le lot précédent, aucun changement Rust/WGSL. Smoke de 420 frames avec
`--prototype-forest-preview`, capture frame 300, RX 7800 XT/Vulkan, 1920×1080 : sortie 0 sans
erreur runtime observée, 636 arbres / 7 632 primitives. Capture inspectée : couverture nettement
améliorée au loin. Les grandes cartes et silhouettes régulières restent visibles ; pas d'acceptation
artistique, de comparaison GPU qualifiée ni de preuve de transitions en mouvement.

Les 430 tests Rust du lot précédent ne sont pas rejoués pour ces seules modifications de scripts
et d'assets externes. Les contrôles applicables exécutés sont génération, réimport, garde des
sources, cas négatifs, assemblage et smoke natif réel. `git diff --check` PASS.

## Fichiers de preuve

- Candidat : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-foliage-coherent-20260906/`.
  `candidate-proof.json` SHA-256 `8312309d8e3e0599f9d75c9297dccab8eefe8ff78e473d72e6267ed7e285ed9c`.
- Réimport final : `r1-foliage-coherent-reimport-20260906-b/reimport-proof.json`, sous le même root.
  SHA-256 `cca8477b1037091fa957cd607a6a74f56acc525cfc2b6e73595a876eedd8236b`.
- Fixture finale : `r1-foliage-coherent-runtime-20260906-b/`, avec build, preview, catalogue,
  `runtime.log`, `metrics.json`, `negative-revision.log` et `hero.png`.
  Capture SHA-256 `85702751f0276c51f096050642266dac63d498bfc2d364c66991e07dbde95f74`.
- Chêne GLB : `f35667e876ba6a5cfc0f1cebca43ebd2ec6be6693d09062becc89294ba5e0462`.
- Pin GLB : `95749500cce1e66d10660ea4f6cffa8d12f3da93e70b2cff52d1e59df464fae1`.

Gate 1 reste ouverte. Web/Tauri, HLOD optimisé, ancrage animé, transitions, budget GPU cible,
parité et validations humaines restent à obtenir. Aucun asset Cadle, aucune promotion, aucun push.
