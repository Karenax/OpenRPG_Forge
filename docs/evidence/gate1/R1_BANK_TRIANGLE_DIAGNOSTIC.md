# R1 — Diagnostic des aplats triangulaires près du pont

2026-09-06, Rust. Diagnostic, pas correction de rendu ni validation de gate.

Le défaut observé après la traversée Web reste au même emplacement en High et Ultra,
captures inline inspectées. Ce seul changement global ne permet pas d'exclure tous les LOD,
mais il ne corrige pas le défaut. L'onglet n'était plus disponible lors de la tentative de
retour vers High ; aucun nouvel onglet ou changement de source n'a été imposé en remplacement.

Inspection : le terrain n'installe pas de ruban de route séparé dans ce parcours. En revanche,
`water.rs` installe `VisualValleyRiverBankSurface`, un ruban opaque distinct. Ses bords extérieurs
suivent la hauteur du terrain plus 1,5 cm, ses bords intérieurs utilisent les lanes d'eau ;
les triangles intermédiaires interpolent ces sommets, sans épouser toute la triangulation du sol.
La palette extérieure brun-vert et les bandes sont des candidats au défaut, pas une identification
GPU définitive de chaque pixel de la capture.

Un diagnostic CPU explicite, ignoré par défaut, a été ajouté :
`cargo test -p forge_engine diagnostic_bank_triangle_clearance --locked -- --ignored --nocapture`.
Il compile la WorldSketch courante et mesure le terrain triangulé au centre de chaque triangle
de berge, sans modifier les sources ni imposer un seuil de qualification.

Résultat initial : 1 380 triangles, **0 centre sous le terrain**, écart minimum +0,084199905 m,
maximum +1,4724926 m. Cela contredit l'hypothèse initiale d'une pénétration aux centres, et
met en évidence une surface surélevée ; ce n'est pas une preuve d'absence d'intersection
partout dans les triangles. Aucun décalage vertical arbitraire n'a été appliqué.

Suite nécessaire : isoler visuellement le ruban, mesurer plusieurs points intérieurs et
examiner son raccord au terrain sculpté. Une éventuelle correction doit préserver eau,
collision, sources authorées et cohérence natif/Web, sans simplement masquer la berge.

Logs externes : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-forest-crossing-20260906/`,
`bank-diagnostic.log` et rejeu `bank-diagnostic-final.log`, `bank-clippy-final.log`.
Le refus Clippy intermédiaire concernait `chunks_exact(3)`, remplacé par `as_chunks::<3>()`.
Le diagnostic n'ajoute pas une qualification à la suite normale : son verdict signifie que
la mesure s'est exécutée, pas que les berges sont conformes. Aucun push, Gate 1 ouverte.

Rejeu final terminé : même résultat numérique, 1 diagnostic PASS ; Clippy moteur/all-targets/
all-features strict PASS. Pas de rebuild applicatif ni de suite workspace relancée pour ce
diagnostic isolé sous `cfg(test)`. La géométrie de production est inchangée.

## Isolation visuelle du ruban — 2026-09-06

Un build Web de diagnostic séparé remplace uniquement le `base_color` du matériau de
berge par `Color::srgb(1.0, 0.0, 1.0)`. Les couleurs des sommets restent multiplicatives.
La source normale a été restaurée à `Color::WHITE` immédiatement après compilation,
puis cette restauration a été contrôlée. Aucun changement de terrain, d'eau, de collision,
de kit ou de végétation n'est inclus dans cette instrumentation.

Build Trunk terminé avec succès ; bundle externe :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-bank-isolation-20260906/web-dist/`.
SHA-256 WASM : `c5848c05949599e31ac3a04ce6791b7d0e70126ed3df1965d6358b2953bdd547`.
Ce bundle coloré est réservé au diagnostic, pas une version normale du Studio.

Chrome, High, WorldSketch verrouillée, kit gouverné et forêt candidate révision 2 :
traversée `completed`, 518/518 ticks au sol, 434 contacts, 42,528 m ; 20 instances,
36 colliders, 636 arbres et 7 632 primitives ; une initialisation. Capture inline
inspectée à l'arrivée (rendu affiché 2048 × 1101), non archivée comme fichier PNG.
Les aplats triangulaires du premier plan prennent la teinte rouge-magenta du matériau
instrumenté : **leur appartenance au ruban de berge est confirmée visuellement**.

Cela ne prouve pas encore la cause géométrique exacte des contours fragmentés : le ruban
interpole une autre triangulation que le terrain et ses sommets intérieurs restent liés
à la hauteur de l'eau. La correction du raccord doit traiter cette différence, pas
simplement relever le ruban ou le cacher. Elle reste à implémenter et à vérifier.
Les 120 premiers échantillons (28,63 ms moyenne, 32,20 ms p95) sont une observation
de démarrage, pas un benchmark contrôlé ni un gain de performance. Gate 1 reste ouverte.

## Correction du raccord canonique — 2026-09-06

Implémentation dans `crates/forge_engine/src/water/banks.rs`, appelée par la préparation
commune natif/Web. Le ruban est découpé contre les cellules de 2 m et leur diagonale
canonique ; chaque sommet est ensuite projeté sur le terrain + les 1,5 cm déjà utilisés.
Les sommets intérieurs ne sont plus hissés à la hauteur de l'eau. UV et couleurs sont
interpolés, l'emprise est conservée. Eau, terrain source, kit et collisions ne sont pas
modifiés. Budgets explicites : un million de cellules visitées et de sommets produits,
erreur typée en cas de dépassement ; statistiques mises à jour avec la géométrie réelle.

Le diagnostic ignoré devient un test de régression normal contrôlant sept points par
triangle de la WorldSketch réelle. Résultat : 40 369 triangles, 282 583 points, aucun
sous le terrain, écart min 0,014986038 m / max 0,015012741 m (tolérance 1 mm).
Un autre test contrôle l'aire conservée et l'interpolation UV/couleurs sur une emprise
synthétique traversant plusieurs cellules. Les tests existants contrôlent toujours le
winding positif et la projection indépendante de l'eau. Un premier échec a détecté
des inversions après conversion f64/f32 ; corrigées, sans retirer l'assertion.

Validation finale : 435 tests workspace PASS, 1 sonde de timing ignorée ; Clippy strict
workspace/all-targets/all-features PASS ; fmt et diff-check PASS ; builds natif et Trunk
Web terminés avec code 0. Avertissement de compatibilité future `proc-macro-error2` conservé.

Preuves externes : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-bank-conforming-20260906/`.

- `tests-final.log` SHA-256 `649fc1bff22a7a1c53fd6a8c9db519be9ea30ea13e898466cf2299fc217bb4df`.
- `surface.log` SHA-256 `c6f2ad41927e32702dcfa4c059ba755097335cac5ef75e51719c7526ca4c70a6`.
- Exécutable natif SHA-256 `7193707df9121119f2310fae6cb93621370e607905e341f972a17c7d3a2f78af`.
- WASM SHA-256 `db3deae622af8d0cb847fcf96b25d3c15b20c80e314874e472d8b67dae622e4c`.

Traversée native réelle avec forêt candidate révision 2 : succès, 518/518 ticks au sol,
435 contacts pont, 42,5282059 m, bindings terrain/joueur/pont préservés ; sortie 0,
aucune erreur runtime observée. Avertissement wgpu `@invariant`/Equal toujours présent.

Studio Web High, même kit : `completed`, 518/518 ticks, 434 contacts, 42,528 m ;
20 instances / 36 colliders, 636 arbres / 7 632 primitives, une initialisation.
Capture inline inspectée à l'arrivée, rendu affiché 2048 × 1101 : les fragments
triangulaires isolés sont remplacés par une bande continue épousant le sol. La bande
reste visiblement unie/facettée et sa frontière avec l'herbe est trop nette : **raccord
géométrique vérifié sur cette vue, qualité artistique non validée**. Pas de PNG archivé.
Le premier chargement a refusé le kit avec HTTP 404, copié trop tôt pendant la finalisation
Trunk ; redéploiement après sortie terminale du build puis rechargement réussi.

Coût explicite : 1 380 → 40 369 triangles et 121 107 sommets non partagés pour les berges.
Ce correctif n'est pas une optimisation. Les 120 échantillons de démarrage Web (27,61 ms
moyenne, 30,70 ms p95) ne constituent pas une comparaison de performance contrôlée.
Restent le mélange visuel terre/herbe, la qualification des LOD lointains et le budget de
rendu global ; ne pas déduire leur validation du test sur le terrain canonique. Gate 1
reste ouverte, CI distante NOT RUN sous dérogation, aucun push ni passage Gate 2.

### Partage exact des sommets : piste écartée après mesure

2026-09-06 : comptage temporaire sous `cfg(test)` de l'ensemble des tuples binaires
position/normale/UV/couleur/hauteur terrain, sur la même WorldSketch corrigée.
121 107 sommets, 120 848 tuples uniques : seuls 259 sommets (0,214 %) sont partageables
sans altérer les attributs. La mesure ne justifie pas une table de déduplication supplémentaire.
Aucune optimisation de production ajoutée ; instrumentation retirée après mesure.
Le test ciblé de raccord passe toujours, mêmes bornes 0,014986038–0,015012741 m.
Log : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-bank-storage-20260906/before.log`.
Pas de nouveau build applicatif ni de qualification visuelle pour cette mesure CPU.
Ne pas rouvrir ce micro-audit sans nouvelle régression : prochain travail produit sur
le matériau et la transition terre/herbe. Une simplification approximative modifierait
le rendu et exigerait une autre preuve ; elle n'est pas implicitement validée ici.

## Matériau terrain partagé et transition des berges — 2026-09-06

Implémenté : les berges utilisent `VisualValleyTerrainMaterial`, le shader terrain existant,
sa palette grass/earth/rock et son détail en coordonnées monde. L'enregistrement du plugin
matériau est commun et idempotent. Un second handle du même cache R8 128 × 128 est créé
pour les berges (16 Kio de texels, hors surcoûts GPU), aucun asset externe ni nouveau shader.
Les poids canoniques du terrain sont conservés au bord extérieur ; une interpolation
smoothstep évolue vers la terre côté eau. La géométrie, ses 40 369 triangles, les sources
authorées et les collisions ne sont pas modifiés par ce lot. La météo met à jour les deux
matériaux via le chemin existant, sans boucle spéciale pour les berges.

Tests : bords extérieur/intérieur, milieu normalisé, interpolation et raccord canonique ;
436 tests workspace PASS, 1 timing ignoré ; Clippy strict workspace/all-targets/all-features,
fmt et diff-check PASS. Sept tests headless ont d'abord échoué faute d'`Assets<Image>` dans
leurs fixtures ; initialisation ajoutée, assertions conservées. Un rejeu a aussi rencontré
le verrou Windows du runtime lancé en parallèle ; runtime terminé puis rejeu complet réussi.
Builds natif et Web terminés avec code 0 ; avertissement futur `proc-macro-error2` conservé.

Preuves : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-bank-material-20260906/`.

- `tests-qualified.log` SHA-256 `3218d1af78dad9130d9f3f857ad056a214d644db4901d5c3cfa1ad441d64b40a`.
- `player.png` SHA-256 `5c1ca21fc6e84d48f552c1aac40fd8983d7bbcb35a29abfcfef527c85e271ad0`.
- WASM SHA-256 `5a3ec09aac9ce78d6809fc250dc5a7bdcc4d5f928b96530a28a07083839b1421`.

Natif : 420 frames, capture 300 inspectée, 1920 × 1080, RX 7800 XT/Vulkan, kit/forêt
candidate révision 2 ; sortie 0 sans erreur runtime. Wetness claire : deux matériaux
terrain, uniforms cohérents, valeur 0. La vue native est le spawn joueur, pas l'arrivée
du parcours : elle ne suffit pas à prouver le raccord de la berge. Observation 360 frames :
16,42 ms moyenne, 18,63 ms p95, 35,82 FPS 1 % low ; exécution avec compilations concurrentes,
donc pas une qualification de performance ni une comparaison contrôlée.

Studio Web High : traversée `completed`, 518/518 ticks au sol, 434 contacts, 42,528 m ;
20 instances / 36 colliders, 636 arbres / 7 632 primitives, une initialisation. Capture
inline inspectée à l'arrivée, rendu affiché 2048 × 1101 : plus de bande opaque uniforme,
détail du sol continu et transition terre/herbe moins tranchée. De faibles facettes restent
visibles : pas de qualification artistique générale ni de preuve des LOD lointains.
Premières 120 frames : moyenne 28,43 ms, p95 31,70 ms, simple observation de démarrage.
Gate 1 ouverte, CI distante NOT RUN sous dérogation, aucun push.

## Raccord des normales d'éclairage — 2026-09-06

Implémenté : `TerrainGenerationPlan::surface_with_shading_normal_at` renvoie l'échantillon
géométrique inchangé et, séparément, l'interpolation normalisée des normales de sommets
du maillage canonique. Ces normales étaient déjà évaluées pendant l'échantillonnage des
matériaux ; aucune requête de hauteur supplémentaire n'est introduite. Les berges reprennent
cette normale au lieu de recalculer une normale par triangle découpé. Les normales de collision,
les pentes, hauteurs, couleurs, UV, indices, documents et colliders ne sont pas remplacés.

Test monde : comparaison au maillage réellement émis sur les 64 chunks de la WorldSketch,
sur les deux orientations de triangles ; identité des échantillons géométriques et rejet
des coordonnées invalides. Test moteur : normales de tous les sommets de berge identiques
bit à bit au nouvel échantillonnage, et sept points de hauteur vérifiés par triangle.
437 tests workspace PASS / 1 timing ignoré ; Clippy strict workspace/all-targets/all-features,
fmt et diff-check PASS. Builds natif et Web terminés avec code 0.

Preuves externes : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-bank-normals-20260906/`.

- `tests.log` SHA-256 `c758f9e6c7086746e5bf0d0e36e7bddc55d483b0eac1f6e7eb224c3005147670`.
- `player.png` SHA-256 `a96f0e338ad4e554554ddfb5db82376a6ca7bf0f324620f7a089489bcabc3cdc`.
- WASM SHA-256 `c1e6835985e47cd9e1f2c487f9ad81a8c89540d29be5120408729a745e9c5928`.

Natif pluie : 420 frames, capture 300 inspectée, 1920 × 1080 RX 7800 XT/Vulkan,
kit/forêt candidate révision 2 ; sortie 0, aucune erreur runtime, wetness 0,78 cohérente
sur les deux matériaux. Cette vue au spawn n'est pas une preuve du raccord à l'arrivée.
Observation 360 frames : moyenne 15,76 ms, p95 16,80 ms, 1 % low 39,12 FPS ; compilation
Web concurrente, pas de benchmark contrôlé ni qualification du GPU cible. Avertissement
de compatibilité future `proc-macro-error2` conservé lors du build Web.

Studio High : traversée `completed`, 518/518 ticks au sol, 434 contacts, 42,528 m,
20 instances / 36 colliders, 636 arbres / 7 632 primitives, une initialisation. Les
120 échantillons de démarrage donnent 29,09 ms moyenne / 33,80 ms p95 : pas un benchmark.

Contrôle Web à l'arrivée puis passage en pluie, humidité stabilisée à 0,78 : capture inline
inspectée, rendu affiché 2048 × 1101. Les facettes brillantes observées sur le build précédent
au premier plan ne sont plus visibles ; éclairage continu avec le terrain, gouttes et reflets
toujours actifs. Le défaut ciblé est corrigé sur cette vue, sans qualification artistique
générale, de tous les angles ou des LOD lointains. Capture Web non archivée comme fichier PNG.
La précédente réserve sous pluie concernait le build matériau antérieur et est levée pour
ce défaut précis, pas pour toute Gate 1. Aucun push, CI distante NOT RUN sous ADR-0006.

Contrôle pluie après cette capture : transition à `rain`, humidité 0,78, gouttes visibles,
même monde initialisé une fois. Capture inline inspectée : les reflets rendent les facettes
des berges nettement visibles au premier plan, malgré la continuité de couleur en clair.
**Le matériau n'est donc pas qualifié sous la pluie.** Prochaine correction : raccorder les
normales d'éclairage à celles du terrain rendu ; ne pas confondre conformité des hauteurs et
continuité des normales. Aucune désactivation de la pluie ou de la wetness pour masquer ce défaut.
