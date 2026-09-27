# R1 — Silhouettes lointaines des buissons et fougères

6 septembre 2026, Rust local non publié. Gate 1 ouverte.

## Défaut et règle réutilisable

La vue `forest_detail` de la série de douze montrait des losanges verts sur les pentes.
`build_hlod_chunk_mesh` utilisait effectivement deux quads en losange pour chaque
buisson/fougère. La fougère lointaine occupait 0,9 m de large et 0,82 m de haut avant
scale, contre des frondes proches étalées sur environ 2,1 m et culminant à 0,44 m.

La règle HLOD commune remplace ces losanges par deux contours convexes à six sommets,
à base étendue et sommet décentré. La fougère retrouve l'enveloppe basse 2,1 × 0,44 m ;
le buisson conserve ses dimensions. 12 sommets/8 triangles par plante au lieu de 8/4.
Pas de texture, entité, dépendance, changement de distance, placement, ID ou collider.
Arbres gouvernés, arbres procéduraux et meshes proches inchangés. Les UV de racines
conservent les mêmes points pour les transitions de distance. Aucune règle par position
ou par slug de carte. Ce sont toujours des plans croisés, pas un feuillage volumétrique.

## Exécution et inspection

34 tests végétation PASS / 1 sonde timing ignorée lors du premier build. Tests couvrant
comptages bornés, base non ponctuelle, enveloppe, déterminisme et UV exacts de racines.
Le test a ensuite été complété par le contrôle du winding et des normales des triangles.
Les tests de qualité/scatter/conservation et de colliders passent aussi.

Deux lancements natifs 1920×1080, même instantané de caméras et mêmes entrées que la série
de douze, `forest_detail`, High partiel, forêt gouvernée, clair/pluie, capture 900/sortie
1800. Sorties 0 et contrôles du collecteur PASS. Deux images ouvertes et inspectées.
Racine : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-low-growth-hlod-20260906`.
Manifeste SHA : `7b2419b24099d20ec3a7960cb9036e35b12b348bff9172c6d6715635d41ba756`.
Runtime SHA : `0b58892f8d50f949781ad8a96d4ae2e1720a8408eed57d3ec5db7e453eb64b72`.

En clair, les fougères deviennent des formes étalées basses ; les buissons perdent la
pointe inférieure de losange. Les silhouettes restent grossières et détachées sur les
pentes : correction intermédiaire, pas une végétation naturelle acceptée. La vue pluie
inclut aussi la correction antérieure de rugosité ; sa différence avec l'ancienne série
ne doit pas être attribuée exclusivement aux HLOD. Vent/HUD non synchronisés pixel à pixel.

Comptages CPU des meshes procéduraux préparés : HLOD 15 424 → 24 488 triangles,
soit +9 064 ; near/medium inchangés, 64 lots HLOD et 64 lots herbe, mêmes 3 666 entités
de projection procédurale. Ces statistiques **ne sont pas le total dessiné des arbres
gouvernés**, dont l'aperçu rend ces comptages incomplets. Aucun gain de performance
ou absence de coût GPU revendiqués. Pas de build concomitant aux captures.

Les recettes, sources et assets restent intacts. Les captures précédentes restent
historiques ; ces deux vues ne constituent pas une nouvelle série finale de douze.
Pas de rendu Web/Tauri qualifié, de revue humaine ou de passage de gate. Aucun push.

## Validation élargie terminée

`cargo test --workspace --all-features` : 462 PASS / 1 timing explicitement ignoré.
Journal `tests.log`, SHA `83f9faa5bb512cb9231a62203ab7e32291b59e67060a382b92afe0b1c4367b74`.
Le premier Clippy a refusé cinq expressions du nouveau test (comparaisons float et
chunks constants), journal `clippy.log` conservé. Comparaisons binaires exactes et
`as_chunks` introduits sans changer la géométrie ni retirer les assertions ; test ciblé
rejoué PASS (`test-recheck.log`). Clippy strict workspace/all-targets/all-features puis
Studio WASM/Web PASS (`clippy-recheck.log`, `wasm.log`). Avertissement de compatibilité
future connu de `proc-macro-error2 2.0.1`, pas d'erreur projet.
Format/diff-check PASS. La compilation Web ne prouve pas le rendu navigateur. CI NOT RUN.
