# R1-012/R1-049 — HLOD ramifié 3D rejeté

2026-09-06, Rust local, Gate 1 ouverte. Aucun gain visuel livré par cet essai.

## Essai et décision

Deux candidats remplaçaient les silhouettes lointaines sur plans croisés par des volumes de
branches dérivés du générateur proche/moyen : 58 puis 90 triangles par arbre contre 10 auparavant
(moyen 116, proche 249). Poses, vent, hauteur 5,65 m, sources et 64 lots HLOD étaient conservés,
sans entité supplémentaire. Un test vérifiait positions, normales, indices et poids de vent.

Les trois captures Full HD au même cadrage ont été inspectées. Le candidat 58 est trop
squelettique ; le candidat 90 reste trop clairsemé à distance. Ils suppriment les triangles plats,
mais perdent la masse apparente de feuillage. **Tous deux rejetés**, malgré les contrôles techniques.
Modifications expérimentales et test retirés par patch ciblé, fonctions/assertions préexistantes
rétablies sans reset Git. Code candidat conservé hors Git dans `rejected-vegetation.rs`.

La suite doit préserver la couverture apparente du feuillage dans les transitions de distance ;
réduire simplement les étages/branches n'y suffit pas. Ne pas reprendre ces variantes comme
amélioration validée. Les silhouettes précédentes restent prototypales et non acceptées.

## Preuves

Racine : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-hlod-volumetric-20260906`.

- `before.log` : capture refusée, ressources/pipelines non prêts avant frame 360, aucun PNG.
- `before-b.*` baseline ; `after.*` candidat 58 ; `after-dense.*` candidat 90 : trois sorties 0.
  Aucun ERROR dans le candidat dense. Le premier refus reste conservé.
- RX 7800 XT/Vulkan, 1920×1080, AutoVsync, caméra héroïque du kit, clair, WorldSketch révision 3.
  Capture frame 600, warmup 240, sortie 1200, 960 échantillons ; aucun build concomitant.
- Baseline → dense : moyenne 9,54865 → 9,64430 ms ; p95 10,2967 → 10,2975 ms ;
  1 % low 66,41 → 68,63 FPS ; un hitch >33,3 ms chacun. Smokes avec capture, pas qualification
  de performance ou gain de vitesse revendiqué.
- Champs GPU/fenêtre/kit, hashes terrain/végétation, placements et projection comparés égaux :
  7 433 placements, 636 arbres, 3 666 entités, 64 lots HLOD et 64 lots herbe.
  Triangles uniques : 305 808 → 356 688 ; HLOD : 15 424 → 66 304.
- Candidat 58 : 396 tests workspace PASS (total corrigé dans R1_054_NO_UPSTREAM.md), Clippy strict, compilation WASM et build natif PASS.
  Candidat 90 : 23 tests végétation PASS, Clippy workspace strict, compilation WASM et build natif
  PASS. Ces succès ne valent pas acceptation visuelle. Pas de lancement navigateur des candidats.
- Après rétablissement : 22 tests végétation PASS, formatage/diff-check PASS et build runtime
  PASS. Exécutable reconstruit SHA-256
  `97901364e1c0bacaeede44893d3623afcf2136ac5c27b753df40e5f73bfdfd72`.
  Pas de quatrième capture ni de nouveau benchmark : le rendu expérimental n'est plus installé.

SHA-256 octets :

| Artefact | SHA-256 |
|---|---|
| `before-b.png` | `2d59a851bca91e7a0b357db4dae46cabdc994e117d670f4e66382942002520ab` |
| `after.png` rejeté | `51e2beb615a0b5d343db59097a75d9a82adcd97782993d0c8406778531da0ca0` |
| `after-dense.png` rejeté | `22712e93563a09357b557577efd223cfa50b93b0f2fe504215eb32158ea66259` |
| `after-dense.json` | `79490287d8416fe34908636eccb115f9ef6ecc2224ccb84de1170ddd826becae` |
| candidat 90 exécutable | `686f0841a289d766c586e18ac09f02304caee41b63463ca9720258ce7a43fd50` |

WorldSketch, aperçu et GLB inchangés ; aucun asset ajouté au produit, aucun asset Cadle,
aucune publication. CI distante NOT RUN. Aucun passage de gate ni validation humaine déduit.
