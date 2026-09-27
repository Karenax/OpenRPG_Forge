# R1-035/036 — distribution consultable pendant l'exploration

## Clôture des capacités de mesure — 8 septembre 2026

L'affichage restant dû a été exercé dans le vrai shell Tauri du candidat
`r1-tauri-lighting-continuity-20260908`, EXE SHA-256
`7355C486E5F50791CDBA691991B4774C4CE14485AD16E8A137BAEF1872F71788`.
Bouton « Afficher les métriques » activé, capture inspectée : fenêtre glissante
240 images, moyenne 21,39 ms, p95 24,20 ms, 1 % low 37,2 FPS et histogramme
0/240/0 lisibles. Mention « non benchmark », passe opaque CPU et GPU indisponible
explicites ; aucune assimilation de cette passe au temps total de frame.
La valeur live évolue ; les chiffres ci-dessus désignent la capture, pas tout le run.

Preuve : `weather-proof/clear-overlay.jpg` sous ce candidat, SHA-256
`2959E1AD04D9D57DD3B95B9197D56C26C85DB3475F7D398F71DBB73789748A0A`.
L'export Tauri du premier cycle (`interaction-proof/high-return.json`, SHA-256
`FE7A543EBCB461FA02B1FCE522B092DD68B76A0E812284C9F6A475791147BB22`)
est également contrôlé : somme histogramme 0/119/1 = 120, durée/moyenne/FPS
concordants et ordre des percentiles. Sans échantillons bruts, les percentiles et
le 1 % low ne sont pas recalculés indépendamment depuis cet export.
Les tests du calcul partagé font partie des 616 tests workspace PASS de ce candidat.

**R1-035 et R1-036 terminés en tant que capacités de collecte/calcul/affichage.**
Ils ne doivent plus rester ouverts seulement parce que les performances mesurées
sont insuffisantes. R1-041 et les seuils de décision de Gate 1 restent ouverts :
aucun 60 FPS, p95 cible, 1 % low cible ou benchmark milieu de gamme validé ici.
R1-034 reste distinct pour la portée des timings GPU ; pas de compteur total inventé.

Complément Studio reçu le 2026-09-06 : export utilisateur
`C:/Users/karen/Downloads/forge-diagnostic-1788712975153.json`, SHA-256
`3d414be69b4aa5add6bf64d821b17106615f2c42adcd602c7958dc082a44840a`.
Relecture du fichier : histogramme 37/83/0 = 120 échantillons, durée 2071,8 ms,
moyenne 17,265 ms = 57,92065 FPS, p95 19,5 ms et 1 % low 47,05882 FPS.
Somme, moyenne, FPS et ordre des percentiles contrôlés en PowerShell ; les échantillons bruts
ne figurent pas dans l'export, donc p95 et 1 % low ne sont pas recalculables depuis ce seul JSON.
Deux tests Studio `frame_distribution` rejoués PASS (rangs/statistiques et compatibilité historique).
Les hashes des métriques et du PNG natif corrigé ci-dessous ont été revérifiés identiques.
Cela établit la collecte/export Web de ces valeurs, pas l'affichage du nouvel overlay dans
Studio/Tauri, ni un benchmark 60 FPS réussi ou une mesure GPU. Gate 1 reste ouverte.

2026-09-06, Rust local modifié, non publié. Gate 1 reste ouverte.

## Implémenté

L'overlay Gate 1 présente moyenne, p95, 1 % low et les trois classes de l'histogramme
sur les 240 dernières images valides au maximum. Actualisation toutes les 30 images valides.
Le calcul réutilise `forge_core::FrameDistribution` ; aucun second algorithme statistique.
La cadence vient de `Time<Real>` : ce n'est pas un temps CPU isolé ni un timestamp GPU.
Le collecteur de qualification et ses fenêtres de warmup restent inchangés. La fenêtre live
inclut le démarrage jusqu'à expiration de ses échantillons ; elle porte « non benchmark ».
Mémoire bornée, pas d'historique illimité, pas de changement des sources de scène.

## Vérifié

- Deux tests ciblés PASS : expiration des anciens hitches après 240 nouveaux échantillons,
  valeurs/histogramme attendus, refus des durées nulles, négatives ou non finies.
- Clippy engine tous targets/features strict PASS ; compilation engine WASM/web PASS.
- Runtime natif reconstruit, lancé sur WorldSketch révision 3 avec `--quality high`, sans kit
  externe ni volumétrie opt-in : sortie 0, arrêt success=true, aucune ERROR.
- Capture Full HD inspectée : deux lignes nouvelles visibles sans débordement, valeurs lisibles.
  La police existante affiche cependant certains séparateurs/accents en carrés, également dans
  les lignes historiques : défaut typographique conservé, pas une qualification UI complète.
- Capture demandée frame 180, sortie 360, métriques warmup 120. Capture live : 180 images,
  histogramme 171/3/6. Rapport final : 240 images, histogramme 239/0/1. Ces fenêtres distinctes
  expliquent les distributions différentes ; ne pas les comparer comme une régression.
- Smoke final 9,273 ms moyens, 9,876 ms p95, 49,90 FPS 1 % low ; RX 7800 XT.
  Ni GPU milieu de gamme ni benchmark final qualifiés par ce court lancement.

Commande native : `forge_runtime --world-sketch-visual-valley --quality high --capture
<preuve>/capture.png --capture-frame 180 --metrics <preuve>/metrics.json
--metrics-warmup-frames 120 --exit-after-frames 360`.

Preuve locale : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-live-frame-overlay-20260906`.

| Sortie | SHA-256 |
| --- | --- |
| runtime.exe | b1d009f3f4d5448b51eda617638d92a83d4b0623e26e385a15d5a946dae9eb62 |
| capture.png | 9cc4d35bfbd62f0f57b67687776b77bb9f867272a37e6b6f65f17f4f336f981b |
| metrics.json | de205a7e3c1ed1ed2ca6c8eb2cccd5e10c286f377baf204c81bcfb6e7fcd2cb3 |
| runtime.log | c0241f23f26f3a3a6a2e6f4583d0d4984c3fdcfbbbc3bab4ad695715928001f2 |

## Non déduit

Pas de nouvelle exécution Studio/Tauri pour cet affichage, de temps GPU ajouté, ni de passage
de R1-034/performance globale. Aucun preset complet annoncé. CI distante NOT RUN ; revue
humaine et qualification finale restent dues. Les images restent locales.

## Correctif de lisibilité — même date, collecte suivante

Le formatteur final de l'overlay utilise maintenant un fallback ASCII pour la police réduite
embarquée : accents français translittérés et séparateurs remplacés par `|`/`-`. Les caractères
non pris en charge deviennent `?`, limite explicite pour un nom d'adaptateur international.
Aucune source ni sortie JSON n'est translittérée ; aucun fichier de police externe ajouté.

Les 145 tests engine passent (dont conservation des chiffres, lignes et hashes du fallback),
Clippy strict et build runtime passent. Une seule capture native Full HD a été rejouée avec
les mêmes arguments dans `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-overlay-font-20260906`.
Inspection : plus de carrés dans les lignes affichées, nombres et histogramme lisibles.
Arrêt success=true, code 0, runtime_errors_observed=false, aucune ERROR. Pas de comparaison
de performance déduite de ce replay de lisibilité ; Studio non rejoué.

- PNG : `1ec1323aedb1daffe8990b6468638b44f6595515b50145dd28d663f8b4e93d0e`.
- Métriques : `feb88666944b39dbcc2c02fc1a56eba2f04df2775b47448f7802d29c3ad6b010`.
- Journal : `2c8c73036c7a8c1d659e02379030ba20f0c4c17286aec4865bb542161d71fe99`.

## R1-034 — temps CPU/GPU de passe, 2026-09-06

L'overlay expose aussi les derniers diagnostics Bevy `render/main_opaque_pass_3d/elapsed_cpu`
et `elapsed_gpu`, en millisecondes. Libellé explicite « passe opaque », « dernier releve » et
« pas le temps total de frame ». Aucun cumul de passes imbriquées ni estimation à partir des FPS.
Une donnée absente/non finie/négative reste indisponible ; les échantillons ne sont pas synchronisés
avec la capture et ne prouvent pas le coût global de la simulation ou du rendu.

Quatre tests ciblés PASS, Clippy engine strict PASS, check WASM PASS et build runtime PASS.
Nouveau lancement identique aux arguments ci-dessus, dans
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-render-timing-overlay-20260906` : sortie 0,
aucune ERROR, arrêt réussi. Capture Full HD inspectée : ligne CPU 0,009 ms / GPU 0,542 ms lisible,
sans débordement. Ces valeurs sont un instantané de passe, pas un benchmark qualifiant.
Sur WebGPU, l'absence de timestamps restera explicite ; pas de nouveau lancement Studio ici.

- PNG : `2d6d3466d8e563af38333c67dcf590b17ea94de0a0b5b1b4ccd6cd8fffcb6095`.
- Métriques : `a138d8897a4c03c0a079b992349997f4f85d8e12832304ed21f04194a85b0401`.
- Journal : `4a601925fc5eb3d616dac725c8122dff2ffda9d25745939aad0981b22d7339d2`.
