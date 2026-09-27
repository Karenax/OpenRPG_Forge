# Gate 1 — Candidat Studio optimisé et cycle Tauri

8 septembre 2026, Rust, worktree modifié préservé ; aucune publication.

## Assemblage exécuté

Le frontend du précédent candidat Tauri était un bundle dev du 7 septembre.
Nouveau build depuis les sources courantes, sans modifier les profils authorés :
`trunk build --release --offline --locked --skip-version-check --config Trunk.toml
--dist <sortie-neuve-E>` depuis `apps/forge_studio`.
Cache `E:/OpenRPG_Forge/.gate1-build-cache/terrain-water-20260906`, deux jobs Cargo,
`CARGO_INCREMENTAL=0`, `RUSTFLAGS=-C debuginfo=0`, TEMP/TMP sur E:.

Premier essai : Cargo PASS en 16 min 32 s, assemblage FAIL car `wasm-opt` absent
en mode offline. Artefacts partiels conservés sous `r1-studio-current-20260908/web`.
Acquisition séparée de [Binaryen 131 officiel](https://github.com/WebAssembly/binaryen/releases/tag/version_131),
archive Windows x86_64 vérifiée contre le digest de la release :
`2F4EDAC1703A2F695254D6FF52EDE03481E67DB1F094915763D863158C17D9BC`.
Outil hors Git sous `.gate1-tools/binaryen/version_131`, licence Apache-2.0 conservée.
`wasm-opt --version` confirme 131 ; hash exécutable
`A3E857E565F18773E24CB3D81D00BAA70CEE9BE24EFE775E1960565FEA5E65B3`.
PATH modifié dans le seul processus de reprise, `BINARYEN_CORES=2`.

Reprise Trunk vers `r1-studio-current-20260908/web-complete` : PASS, Cargo réutilisé
en 0,81 s. WASM **41 126 864 octets**, hash
`51BCFFF130E9CF537FD6354FA0540F7D05EFB1DF0FC435E8E0B4CA7201A3DBD4`.
Ancien bundle : 151 097 795 octets. Réduction de taille, pas gain GPU démontré.
158 pins de sources relevés pendant le build et revérifiés avant assemblage du
shell ; manifeste externe `r1-studio-current-20260908/source-pins.json`.
Ce relevé n'est pas une révision propre ni l'inventaire exhaustif de qualification.

Kit/GLB/catalogue et vues copiés du candidat précédent, hashes vérifiés, pas de
nouveau contenu Cadle. `Build-StudioCandidate.ps1` PASS, shell compilé en 28,83 s.
Sortie `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-tauri-current-20260908/`,
14 fichiers frontend épinglés. SHA-256 du binaire :
`3888F31B322DA51D46AA5CD2851D8099C7CD58A98BC3B9D457D6B7E6EB301ADA`.

## Exécution réelle dans Tauri

Computer Use a lancé et activé ce binaire exact. Sélection du document végétation,
puis High → Low → High via les boutons Studio. Trois captures de fenêtre inspectées
et états d'accessibilité conservés dans `interaction-proof/` :
`high-before`, `low`, `high-after` (JPEG et TXT).

- une seule initialisation du monde ; sélection locale de document, pas une édition ;
- Low modifie visiblement végétation et ombres, High les rétablit ;
- kit 20/20, 36 colliders, 650 arbres / 7 800 primitives conservés ;
- budgets disponibles appliqués, mais eau/environnement partiels et forêt non qualifiée ;
- assombrissement temporaire au démarrage et pendant une transition, résorbé ensuite.

Export réel `forge-diagnostic-1788861668430.json`, copié en `high-after.json`, hash
`DF997E101F62F589A26074312A38C2C22D43EB89226DB2F9773E87BCF9A8DF59`.
`target=tauri_studio`, `requested_tier=high`, `available_budgets_projected=true`,
`full_preset_applied=false`, plan effectif bloqué par `vegetation`.
135 pipelines observés, zéro pending/failed au relevé ; pas d'acquittement global GPU.

La série de télémétrie de démarrage (120 échantillons) rapporte 21,34 ms moyens,
p95 23,6 ms. Elle est conservée dans l'export mais **ne mesure pas le cycle de
transition ni un benchmark GPU/release complet**, et ne satisfait pas le seuil
de 60 FPS. Pas de gain comparatif attribué à l'optimisation sur cette série courte.

Instance fermée par Alt+F4 ; contrôle système : zéro processus candidat restant.
Aucun navigateur de test ni compilateur restant à la fin de ce cycle.

## Suite nécessaire

Ne pas répéter ce cycle inchangé. Examiner la transition d'éclairage : le code de
remplacement de sonde retire l'ancienne map avant disponibilité de la nouvelle.
Correction réutilisable à vérifier, sans qualifier l'ancien éclairage comme nouveau
budget. La qualification artistique/forêt, la cible matérielle, les preuves finales
et les validations humaines restent ouvertes. Gate 1 non passée, Gate 2 non ouverte.

## Correction de continuité — tests et essai Tauri exécutés

Régression reproduite par `resize_retains_previous_lighting_without_acknowledging_the_new_budget` :
échec avant correction, composant d'éclairage absent (`None`) dès le resize au lieu
du handle précédent. Le gestionnaire garde maintenant la map filtrée lors du seul
remplacement d'une sonde possédée sur la même caméra. Il supprime toujours le
générateur de caméra ; arrêt ou changement de caméra retirent l'ancien éclairage.
`texture_bound=false` reste obligatoire jusqu'à publication du remplacement conforme.

Le test couvre attente sans texture, texture de taille incorrecte (ancien éclairage
conservé, nouveau budget non acquitté), publication conforme puis désactivation.
L'ancien test qui imposait une absence de map pendant le resize exige désormais
la conservation du handle et le non-acquittement ; ce changement de contrat est
intentionnel pour éviter la coupure visuelle, pas une validation de l'ancien budget.

Après correction : huit tests environnement PASS ; `cargo fmt --check`, Clippy
workspace/all-targets/all-features strict, **616 tests workspace PASS / un timing
préexistant ignoré**, check Studio WASM PASS. Commandes offline/locked, deux jobs.
Pas de dépendance moteur, paramètre authoré ou asset modifié.

Reconstruction Trunk release offline/locked PASS : compilation Rust 2 min 28 s,
puis optimisation Binaryen terminée. WASM 41 127 248 octets, SHA-256
`D9067AB4E6DDA29367EFF9E333FA05876EA94F7F9DB13F997079D3B09B420FF4`.
Kit/vues conservés et hash-vérifiés. Shell Tauri compilé en 28,12 s ; candidat
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-tauri-lighting-continuity-20260908/`,
EXE SHA-256 `7355C486E5F50791CDBA691991B4774C4CE14485AD16E8A137BAEF1872F71788`.

Ouverture réelle par Computer Use, puis High → Low → High : éclairage présent
sur les captures pendant le remplacement et après application, végétation Low
réduite puis High restaurée. Pas d'assombrissement observé dans ces captures de
transition ; ce relevé discret n'est pas une vidéo exhaustive et n'exclut pas
un transitoire entre deux captures. L'assombrissement du premier chargement reste
observé, puis se résorbe ; ce correctif ne traite pas le premier remplissage GPU.

Preuves JPEG/TXT sous `interaction-proof/` : `high-before` montre le chargement
initial sombre ; `high-ready`, `low-immediate`, `low-settled`, `low-applied`,
`high-return-immediate`, `high-return` suivent le cycle. Attention : malgré son
nom, `low-settled` montre encore une application en cours ; `low-applied` est
l'observation Low terminé. Image et arbre d'accessibilité ne sont pas atomiques.
Export réel `forge-diagnostic-1788862996760.json`, conservé comme `high-return.json`,
SHA-256 `FE7A543EBCB461FA02B1FCE522B092DD68B76A0E812284C9F6A475791147BB22`.
Une initialisation, `target=tauri_studio`, six hashes source alignés, kit 20/20,
36 colliders, 650 arbres ; High partiel disponible, forêt toujours bloquante.
La série de démarrage reste courte : 22,69 ms moyens / p95 27,6 ms, pas un
benchmark comparatif ni une qualification de performance.

Le clic canvas active la capture du pointeur et Échap la libère réellement.
Les taps V par l'outil n'ont pas produit de bascule visuelle démontrée : contrôle
clavier non qualifié, cause non établie. Les fichiers `camera-toggle` et
`camera-first-person` sont des tentatives, **pas une preuve de première personne**.
Ne pas modifier le contrôleur sur la seule hypothèse d'un problème de focus.

Instance fermée via Alt+F4, contrôle exact du chemin EXE : zéro processus restant.
Session Node de pilotage réinitialisée ; aucun navigateur de test lancé. Aucun
critère humain, matériel cible, révision propre ou passage de gate acquis.
