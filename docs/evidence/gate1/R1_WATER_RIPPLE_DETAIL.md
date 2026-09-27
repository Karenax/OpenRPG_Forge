# Gate 1 — Petites rides de surface filtrées

8 septembre 2026, Rust local ; candidat en cours, aucune publication.

## Diagnostic et portée

La variante de grandes crêtes déformées est rejetée dans
`R1_WATER_WAVE_VARIATION.md` et n'est pas réintroduite.
Inspection du Bevy 0.19.1 local : `pbr_functions.wgsl::calculate_F0_dielectric`
convertit bien reflectance par `0.16 * reflectance²` ; la valeur Forge 0,36
n'est donc pas une réflectance physique de 36 %. Pas de correction à ce niveau.
`ssr/ssr.wgsl` lit les normales du G-buffer, alimenté par `pbr_input.N` : les
normales de vagues Forge y sont déjà appliquées. Pas de défaut SSR démontré.

Le candidat ajoute deux petites rides uniquement aux normales par fragment,
dans les branches forward et deferred. Directions croisées par rapport au
courant, longueurs 0,085 / 0,137 fois la longueur authorée, amplitudes de hauteur
implicites 0,075 / 0,045 fois l'amplitude authorée (9 mm / 5,4 mm au profil courant).
Les grandes vagues, sommets, précédentes positions, paramètres de carte et
raccord eau/berge restent inchangés. Aucun asset, texture, triangle ou passe ajouté.

Les fréquences fines sont atténuées selon `fwidth` de leur phase : fondu entre
1 et pi radians/pixel, détail nul au-delà. Multiplicateurs temporels entiers 3/5
pour conserver le wrap TAU. Amplitude ou normal strength nulle neutralise ce
détail. C'est une approximation de surface, pas de l'hydrodynamique ni une
simulation physique de hauteur des petites rides.

Coût ajouté : deux cosinus et calcul de dérivées/filtre par fragment, pas de
coût géométrique. Performance et scintillement en mouvement restent à mesurer ;
ne pas annoncer de gain ou de coût identique. La garde de filtrage n'est pas
à elle seule une preuve d'absence d'aliasing.

## Vérification en cours

Racine `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-ripple-detail-20260908`.
`Verify.ps1` lancé (workspace, Clippy, check WASM, build natif). Référence fixe
réutilisable : `r1-water-wave-variation-20260908/before-fixed.png`, shader original,
caméra canonique `hero_reference_match`, temps figé ; aucun intérêt à la recapturer
avec les mêmes entrées. Capture après et inspection GPU encore requises.
Le binaire expérimental rejeté D72CB7… n'est pas la référence.

Aucune amélioration artistique ou condition de Gate 1 cochée. Studio non reconstruit.

## Résultats natifs

637 tests workspace PASS / un timing ignoré ; Clippy strict workspace PASS,
check Studio WASM PASS, build natif PASS (2 min 10 s). Warning futur
`proc-macro-error2 2.0.1` conservé. Ces contrôles ne qualifient pas tous les
variants GPU ; les captures suivantes prouvent l'exécution du chemin natif courant.

EXE SHA-256 `13888A6C044CAF73D78FC9088ED76737A6E50AD49486DC85C4EAAA14CCCE1967` ;
shader `ED777A24622822CB054FF469B5DEFAA9CB05BB4F8274322D3531D33E94318E9F`.
WorldSketch inchangée : `EA014301FB63835609FBF47494FD9FD87D135DB57AA82991B9C27194787AE95F`.

- Vue figée `after-fixed.png`, SHA-256
  `19D4B341A70414236024C4F89EC4C9E9DC2E2C285F06725A8EBDFD03DB6C7750`,
  PID 13448, capture vérifiée, sortie 0 sans ERROR et terminaison confirmée.
  Comparée à la référence figée existante : détail fin dans l'eau proche,
  grandes rides encore visibles au loin. Overlay 21,56 ms / p95 28,40 ms /
  1 % low 25,4 FPS, pas un benchmark.
- Gros plan `after-bridge.png`, SHA-256
  `8B49D2939116684CF5461D5CE8C147BD9086CB2682AD9890F39FB878CA8460FE`,
  PID 44404, capture vérifiée, sortie 0 sans ERROR et terminaison confirmée.
  Vue comparée à `r1-water-shore-overlap-20260908/after-bridge.png` : éclat
  lumineux fragmenté et eau proche plus détaillée, raccord aux berges conservé.
  Motif croisé encore très régulier ; ni réalisme complet ni absence de scintillement
  démontrés. Cette paire rapprochée n'a pas une phase d'animation synchronisée.
  Overlay 28,79 ms / p95 33,56 ms / 1 % low 23,8 FPS, pas un benchmark.

Décision : conserver provisoirement le détail pour le candidat, sans validation
artistique ni de performance. Aucune géométrie/collision modifiée. Ancienne
fenêtre propriétaire conservée. `BuildStudio.ps1` lancé pour intégration du
même shader ; build et replay Web encore à terminer. Aucun succès Web déduit
du check WASM ni des captures natives. Aucun passage de gate ni publication.

## Intégration Studio vérifiée — 8 septembre, 19:19 UTC

Build terminé avec code 0 : WASM SHA-256
`864941EAAC075B4F7269D61ADACCC18BF74D3AB5F97CF7D880A16511B376B4A7`,
EXE `9C80007F820B4D7A28BC7E26540BBD40FE6091F91FEFD0093AF7EF192FCFE6EB`.
Même caméra candidate surélevée et kit que `R1_FIXED_VIEW_WARMUP.md` ; son
identifiant historique `human_bridge` ne vaut pas preuve de hauteur humaine.

Lancement PID 45016, rendu réellement inspecté dans la WebView. Warmup prepared
en 22 221,5 ms, 89 pipelines, huit observations stables, aucune attente/erreur
finale ; pic transitoire 19 shaders. Un monde/une caméra, kit 20/20, forêt 1 662
arbres. Rides fines et reflets fragmentés visibles, raccord des berges conservé.
Motif encore régulier, aucune acceptation artistique globale.

Exports officiels, sources Downloads conservées :

- `forge-diagnostic-1788895116908.json` → `studio-diagnostic.json`, SHA-256
  `CD5CC64365FCD25CDB5CE9DE906E3EA6DFC98842A985F3DA000C351AD5A890CD` ;
- `forge-viewport-1788895131587.png` → `studio-viewport.png`, SHA-256
  `2801A25FEB7985487DE0F0C59E8BC8914BE639ABCA6B0B116C357FDCB30418AC` ;
- `forge-diagnostic-1788895187408.json` → `studio-step-diagnostic.json`, SHA-256
  `80F3AD62705BE0E8D3C68172226E8923D36B51B4780EED0442491143A10C6A16` ;
- `studio-step-ui.jpg`, SHA-256
  `3312F89C61FF47C0B055BDF48990AF91D1CB88541AF5CEB29E8FA1234319BBCC`.

Le bouton d'avancement est exercé une fois : horloge 0 → 0,250 s, 1/32 pas,
caméra inchangée ; 36 colliders enregistrés ensuite et kit prêt. Ce pas confirme
la commande de vue diagnostique, pas la fluidité, l'absence de scintillement,
une traversée jouable ni un benchmark. Les 120 frames initiales affichent
40,71 ms moyen / p95 48,50 ms ; autre Studio ouvert, pas de verdict performance.
PNG 1408×966 inspecté, JSON/PNG non atomiques.

Fenêtre d'essai fermée normalement. PIDs 45016 (Studio), 26264 (conhost),
37108 (Trunk) et 39308 (optimiseur) absents après contrôle. Fenêtre propriétaire
39624 préservée, session Computer Use libérée. Aucune publication, Gate 1 ouverte.
