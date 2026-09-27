# Studio — scène dégagée, métriques accessibles

2026-09-06, Rust local, Gate 1 ouverte. Aucune publication.

## Implémenté

Le Studio Gate 1 masque au démarrage le grand panneau de métriques posé sur le canvas.
Un bouton de l'inspecteur permet de l'afficher puis de le masquer. L'état `aria-pressed`
reflète la ressource moteur observée. Pas de nouvelle interface de sauvegarde, pas de
reconstruction de scène, de nouvelle caméra ou de modification des sources.

`DiagnosticsOverlaySettings` conserve un défaut visible dans le moteur ; le Studio
Gate 1 fournit son choix avant l'installation du plugin de preuves. Gate 0 et le runtime
natif gardent donc leur configuration historique. Seul `Node.display` des entités portant
le marqueur privé du panneau est changé. Le texte, les systèmes de collecte, les ledgers
et les exports ne sont pas désactivés. La commande ne masque pas un résultat de validation
et ne transforme pas une projection CPU en preuve GPU.

## Vérification ciblée

Test `hiding_overlay_preserves_entity_text_other_ui_and_frame_collection` PASS :
six mises à jour, alternance visible/masqué, identité et texte conservés, autre nœud UI
inchangé, six échantillons de frame collectés y compris à l'état masqué.
Check Studio WASM/web PASS. L'exécution Gate 0/native n'est pas rejouée par ce test ECS.

Preuves du lot :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-metrics-toggle-20260906`.
**415 tests workspace PASS**, une sonde de timing volontairement ignorée comme dans
le lot précédent. Clippy workspace tous targets/features, Clippy Studio WASM/web,
build Trunk, formatage et diff PASS. Avertissement de compatibilité future amont
`proc-macro-error2` 2.0.1 conservé ; CI distante NOT RUN sous ADR-0006.

## Inspection réelle du Studio

Chrome, URL WorldSketch + kit gouverné, loopback 14646. Huit fichiers du kit copiés
du lot précédent avec comparaison SHA-256 individuelle. Canvas 1408 × 741.
État initial du bouton : `aria-pressed=false`. Clic afficher : `true`, panneau
présent en capture. Clic masquer : `false`, panneau absent en capture, scène
dégagée. Deux captures inspectées dans l'outil, sans PNG archivé localement.

Le ledger qualité reste accessible avec High, cinq domaines CPU et deux partiels ;
kit 20/20 instances, 36 colliders ; un monde initialisé affiché. Les valeurs de
performance sont toujours accessibles lorsque le panneau est réaffiché, sans
qualification de performance nouvelle. Une lecture warning/error retourne une liste
vide (pas un audit exhaustif). Onglet et serveur de preuve fermés après contrôle.

SHA-256 :

- WASM : `cd8c75763e117c07fc33a7bbff2ff4a8aaa59be69c794659f279b5a1439aba76` ;
- `tests.log` : `ff82aaf49a44fefcb7628133966d61d9a2c1e6eed9d29219f1a83964f59000b7` ;
- `clippy.log` : `9e52ea7ca70397b2a94a3218ec74cceb78825de82ee7e7cb2ecbed83d364ce24` ;
- `clippy-web.log` : `da0ae5832c55af1e0204fa33a396cfa623559bb0ba5489364ec35052dc98a4c6` ;
- `build.log` : `c12f201ba602b4093af09a45c10a7c262991ef001b6e1c165efbf840b9372d0f`.

Verdict du lot : fonction d'affichage vérifiée dans Chrome. Ne qualifie ni Tauri,
ni sauvegarde, ni édition Gate 2, ni qualité artistique. Gate 1 reste ouverte.
