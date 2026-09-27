# R1-040 — Réduire le blocage de préparation végétale sans réduire la scène

2026-09-06, Rust local non publié, Gate 1 ouverte.

## Cause examinée et modification

Le cycle Web précédent a connu une expiration de lecture à trois secondes pendant
Ultra. Une sonde native de la préparation végétale confirme un travail CPU de plusieurs
secondes ; elle ne prouve pas que tout le délai navigateur vient de cette seule étape.

Chaque racine d'herbe appelait `surface_at`, qui évalue quatre sommets de terrain,
leurs normales et les poids des matériaux. Seule la hauteur était consommée.
`TerrainGenerationPlan::surface_height_at` évalue désormais les trois hauteurs du
triangle choisi et réutilise exactement l'interpolation f64 vers f32 existante.
Validation des coordonnées, choix du triangle sur la diagonale et bords restent identiques.
Il ne faut pas remplacer ce calcul par `height_at`, qui évalue la recette continue,
pas sa surface triangulée. Le seul consommateur raccordé est la racine des brins.

Aucun cache, dépendance, réduction de densité, déplacement canonique, changement de
shader, collider, matériau ou asset. `surface_at` conserve son résultat complet pour
les autres usages. Le test d'égalité couvre 480 positions sur recette initiale et
WorldSketch sculpté (deux triangles, diagonale, bords et limites de chunks), plus
les erreurs NaN, infini et hors domaine. Égalité de hauteur **bit à bit**, hash inchangé.

## Mesure native avant/après

Même sonde, même machine, même WorldSketch rev3, trois passages séquentiels
Low/Ultra/High, sans compilation concurrente. Profil Cargo de développement optimisé,
`RUSTFLAGS=-C debuginfo=0`, incrémental désactivé, cache de build existant.
Temps mesuré : appel complet de préparation végétale CPU, avant insertion ECS ou GPU.
La génération canonique est mesurée séparément ; elle n'a pas été supprimée.

| Profil | Avant, ms (trois passages) | Après, ms (trois passages) | Triangles uniques inchangés |
| --- | --- | --- | ---: |
| Low | 397,899 / 474,043 / 492,771 | 168,908 / 167,507 / 218,985 | 242 132 |
| Ultra | 3 020,329 / 3 469,698 / 3 473,633 | 716,717 / 752,233 / 920,767 | 2 163 416 |
| High | 2 844,206 / 2 907,404 / 2 897,956 | 555,807 / 682,883 / 663,035 | 1 860 176 |

12 508 placements canoniques dans tous les relevés. La sonde
`quality_preparation_cpu_probe` est explicitement ignorée dans la suite courante
pour ne pas chronométrer chaque exécution de tests ; elle a été exécutée avant et
après avec `cargo test -p forge_engine quality_preparation_cpu_probe --locked -- --ignored --nocapture`.
Aucun seuil de timing fragile ajouté à la CI. Trois observations ne qualifient ni
p95 de frames, ni 1 % low, ni mémoire/VRAM, ni performance finale de Gate 1.

Transcription des sorties de tests (pas un export du renderer) :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-grass-height-only-20260906/cpu-preparation-comparison.json`,
SHA-256 `22c01533082597500397a5782463344757ac0fccbf3260b5cfb1390502988c91`.

## Validation et limites

Test d'égalité ciblé PASS ; sonde explicite avant/après PASS. **414 tests workspace
PASS**, une sonde de timing volontairement ignorée dans la suite courante et exécutée
séparément. Clippy workspace tous targets/features avec warnings projet en erreurs,
check Studio WASM/web, formatage et diff PASS. Avertissement de compatibilité future
`proc-macro-error2` 2.0.1 conservé. SHA-256 des journaux locaux :

- `tests.log` : `3ce23582e6a5270b0406ea3e0adfa5c3ec5bf7aa7dead3f7a75a3c36b403b6f6` ;
- `clippy.log` : `40aa179a990fcb6204d7026e426b1a4dd0b417dc44e30c5690d16c61c2dbe2d5` ;
- `wasm.log` : `b454fd2a4aa0d78d9de857e6ade96554601ccc72ecd83c4c4a4086d7eb017e5b`.

La transition complète conserve génération, reconstruction de meshes et transfert
GPU ; aucune garantie d'absence de pause n'est donnée. L'effet sur la latence du
Studio Web reste à mesurer sur un nouveau build ; pas de nouvelle capture ici,
les hauteurs/rendus visés ne changent pas. Aucun passage de gate, CI distante NOT RUN.

## Complément : exécution du nouveau build dans Chrome

Le build Web est ensuite reconstruit avec Trunk, code 0, et servi sur loopback
14644. Première tentative refusée avant compilation car `NO_COLOR=1` n'est pas
accepté par Trunk ; variable de ce processus corrigée à `true`, pas de changement
du système ou du produit. Les huit fichiers du kit sont copiés depuis le lot
précédent et chacun vérifié par SHA-256.

- WASM : `b63a4adf1d0834a6583dcb4bd453f9cad781ff5b87b9a440a08ddbcb069adba7` ;
- JS : `d17582f23cc92ab3c5129fb318059ad3aae17cb53b804ae9a33de771cb584a43` ;
- source servie : `r1-grass-height-only-20260906/web-dist` dans la racine locale de preuves.

Même URL de scène WorldSketch/kit, nouveau processus de page, sans parcours automatique.
Page amenée au premier plan avant le cycle ; visible et focalisée au début et à la
fin de chaque mesure. Une sonde temporaire observe les clics réels et les mutations
du ledger : fin lorsque le niveau demandé concorde et que les budgets disponibles
sont projetés. Elle attend aussi deux callbacks `requestAnimationFrame` après ce
relevé. Ces événements ne prouvent ni GPU terminé ni pixels présentés à cet instant.

| Changement | Clic → observation des budgets | Clic → deux callbacks RAF après observation |
| --- | ---: | ---: |
| High → Ultra | 2 127,6 ms | 2 157,1 ms |
| Ultra → High | 1 232,7 ms | 1 249,8 ms |

Une mesure par sens seulement. Pas de pourcentage d'amélioration Web revendiqué :
l'ancien timeout de trois secondes n'est pas une baseline mesurée avec ce protocole.
Les deux transitions finissent avec cinq domaines CPU projetés, deux partiels et
`full_preset_applied=false`. Kit 20/20 et 36 colliders, un monde initialisé affiché.
Capture du retour High inspectée : scène, herbe et kit présents ; rendu toujours
prototypal. Canvas 1408 × 741, pas du Full HD ni une capture normalisée. Capture
consultée dans l'outil sans PNG archivé. Lecture warning/error vide, non exhaustive.

La sonde est retirée (absence vérifiée), onglet et serveur fermés. Les délais restent
perceptibles : ce complément prouve l'exécution du changement, pas sa fluidité ni la
qualification du profil complet. Transcription locale : `web-cycle.json`, aux côtés
de `web-build.log`. Aucun changement de code supplémentaire ni publication.

SHA-256 : `web-cycle.json`
`51918be661bf793512466b95d87a0c4bd862378c326c6ac9b6727a09cff26ccd` ;
`web-build.log` `62c15b947ae394c76fe7a589191179450cb2203f589a15817d12a96b3c04dfdb`.
