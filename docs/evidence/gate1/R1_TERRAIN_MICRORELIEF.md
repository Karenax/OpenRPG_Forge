# R1-049 — Microrelief du sol

7 septembre 2026, Rust locale, worktree existant préservé. Gate 1 ouverte.

## Objectif et périmètre

Le grain d'albédo/rugosité existant ne modifiait pas la normale d'éclairage : le sol
restait lisse. Le candidat ajoute une normale de surface issue des deux échelles de
détail déjà échantillonnées (3 et 32 cellules/m), pondérée par herbe/terre/roche.
Ce sont des règles de matériau réutilisables, pas une retouche de Visual Valley.
Positions, normales géométriques de biais d'ombre, collisions, masques et documents
authorés restent inchangés. Aucun asset externe, lecture texture ou mesh ajouté.

Le gradient de surface est calculé dans le plan tangent à partir des dérivées écran
de la position mondiale et de la hauteur de détail. Sa pente est bornée à 0,65 ;
un déterminant nul conserve la normale géométrique. Cela ne déplace pas la silhouette.
Le shader écrit la normale d'éclairage PBR ; le prépass normal standard n'est pas
remplacé dans ce lot. Ne pas revendiquer le détail dans les normales SSR/SSAO.

## Premier essai — remplacé, non accepté

Preuves externes : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-ground-relief-20260907/`.
`Run.ps1 -Stage before` puis `-Stage after` reconstruisent le runtime et capturent
clear/rain à la frame 300, sortie 420, chauffe 180, High sans VSync, caméra
`human_bridge`, kit gouverné et forêt par lots. Quatre runs terminés sans erreur
runtime, pipelines prêts, 20 instances chargées et 36 colliders enregistrés.
Les quatre sources épinglées par `pins.json` restent identiques avant/après.

Captures natives avant clair, après clair et après pluie ouvertes et inspectées.
Le relief proche apparaît, mais le filtrage linéaire du cache révèle des raccords
en quadrillage, particulièrement sous pluie. Ce candidat ne doit pas être présenté
comme qualité finale. L'interpolation doit être corrigée avant de retenir le relief.

| Essai court | FPS moyens | p95 ms | 1 % low FPS |
| --- | ---: | ---: | ---: |
| Avant clair | 67,02 | 16,4027 | 39,94 |
| Après clair, premier essai | 63,92 | 17,4568 | 42,35 |
| Avant pluie | 65,43 | 17,5690 | 39,24 |
| Après pluie, premier essai | 66,07 | 16,6241 | 39,76 |

RX 7800 XT, 1920×1080. Captures incluses dans les mesures courtes : aucun gain,
coût isolé ou verdict de performance cible ne peut en être déduit. Les avertissements
préexistants de profils partiels et `@invariant`/Equal restent visibles dans les logs.

Les dix tests ciblés terrain et Clippy strict moteur passent sur le premier essai.
Le test de raccord shader est textuel ; les lancements apportent la compilation GPU.
Ce lot ne rejoue pas les 579 tests workspace antérieurs et ne les revendique pas
comme nouvelle qualification.

## Correction d'interpolation — candidat courant

Les coordonnées entre centres de texels utilisent maintenant une interpolation cubique
`t²(3−2t)` avant la lecture linéaire du cache. La dérivée s'annule aux raccords ;
les mêmes trois lectures sont conservées. Cette correction modifie aussi le détail
d'albédo/rugosité puisqu'ils partagent les échantillons. Aucun nouveau cache ou asset.

`Run.ps1 -Stage smoothed` terminé, deux sorties 0. Captures clear/rain ouvertes et
inspectées : raccords anguleux atténués, grain éclairé visible au premier plan. Le
motif reste procédural, les zones nues et le manque de prairie ne sont pas résolus.
Pas d'acceptation artistique, de preuve en mouvement ou de qualification des trois
matériaux séparément déduite de cette vue majoritairement herbeuse.

- Clear : 66,30 FPS moyens, p95 16,8305 ms, 1 % low 40,49 FPS.
- Rain : 66,02 FPS moyens, p95 16,7527 ms, 1 % low 36,27 FPS.
- 20/20 instances, 36 colliders, zéro pipeline en échec, sources épinglées inchangées.
- Shader : `c5f71f7c9b38f924e631cfddbd13d63b41895847f6418882b7aa11058db08039`.
- Runtime : `d838b7aeeb6db8a0c1b2080d36f927a3406ff6361d70e13124c4e3a236134bc6`.
- `smoothed/clear.png` : `6fa3ef0dd1abae9d14d8d35f04d77d083fc939c871dd1737281f4d728c68c2b4`.
- `smoothed/rain.png` : `2530BB278C53C09D59704C4D9C190FF2B856552F7AC5159829EBBDAAD6AB34AE`.

Mesures courtes avec capture, non benchmark : les seuils Gate 1 ne sont pas démontrés.
CI distante NOT RUN, aucune publication Git et aucun contenu Cadle.

Validation de la correction : dix tests terrain PASS, Clippy moteur all-targets /
all-features strict PASS, fmt PASS, build Studio WASM/Trunk PASS. Avertissement futur
préexistant `proc-macro-error2 2.0.1` conservé. Bundle corrigé :
`47fe5a7dc4252053cee927363c47ec8221f7b3f1bfdce1ef1a73ca15fd0705df`.

## Studio Web exécuté

Chrome, port local 1448, bundle corrigé avec les dossiers externes `forge-kit` et
`forge-views` du candidat précédent. Vue fixe `human_bridge`, horloge avancée une
fois à 0,250 s pour enregistrer la physique (pas de simulation à temps zéro).
Capture `smoothed/forge-viewport-1788786997006.png`, 1408×741, ouverte et inspectée :
sol avec grain éclairé, scène entière rendue. Diagnostic
`smoothed/forge-diagnostic-1788787009375.json` : kit 20/20, 36 colliders prêts,
aucun mesh non comptable ; `gate_passed=false`, `human_acceptance=false`.
Console error vide au relevé. Les deux téléchargements ont été archivés et leurs
copies vérifiées par SHA-256. PNG et JSON restent non atomiques.

Un seul smoke Web clair, pas de benchmark ni de parité pixel natif/Web à résolutions
différentes. La pluie a été inspectée en natif seulement pour cette correction.
Prochaine priorité visible : la couverture de prairie, déjà zonée mais encore trop
clairsemée ; ne pas recommencer ce contrôle de shader sans changement pertinent.
