# R1 — Kit aux couleurs corrigées dans le candidat Studio

8 septembre 2026, Rust uniquement, travail local conservé, aucune publication.

## Intégration reproductible

`Build-StudioCandidate.ps1` accepte maintenant `-GovernedKit <kit-existant>`.
Il vérifie statut technique du build, identités/révisions des trois documents,
hashes des GLB, chemins bornés et absence de reparse points. Un frontend contenant
déjà un kit est refusé avec ce paramètre : aucun remplacement implicite. Les
trois documents et les GLB déclarés rejoignent les pins du bundle, vérifiés
avant/après compilation. Le préflight complet du Runtime reste l'autorité sur
la validité des contrats et des assets ; ce script n'est pas un importeur général.

`Test-StudioCandidateKit.ps1` : deux cas valides et quatre refus PASS (hash,
traversée, révision, kit déjà présent), sans compilation ni création de candidat.
Les fixtures sont volontairement de faux GLB/WASM ; elles ne qualifient que les
contrôles d'entrée. `git diff --check` PASS.

Frontend fraîchement compilé par `trunk build --release --dist <racine>/frontend`,
cache/temp sur E:, deux jobs Cargo et deux cœurs Binaryen. Trunk terminé 0,
warning futur `proc-macro-error2 2.0.1` conservé. Assemblage Tauri debug sans bundle
de distribution, offline/locked, par le script avec le kit de
`r1-texture-colorspace-20260908/runtime-kit`, terminé avec `BUILD_ONLY`.
Treize fichiers embarqués et épinglés, dont les deux GLB corrigés du lot précédent.
Ce build inclut aussi la correction moteur de liaison IBL, absente des anciens
bundles ; ce n'est pas une comparaison visuelle isolant cette seule correction.

Racine des preuves :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-colorspace-20260908`.

## Exécution Tauri réellement observée

Candidat ouvert via son chemin exact puis inspecté avec Computer Use. Le premier
écran présente le chargement ; une première scène rendue comporte des zones noires
sur capsule/végétation (`studio-initial.png`). Elles disparaissent sans changement
de réglage sur les observations suivantes (`studio-ready.png`, `viewport.png`).
Cela établit un état transitoire de démarrage, **pas sa cause GPU exacte ni une
absence de défaut de warmup**. Ne pas qualifier la première frame à partir du
seul statut CPU « prêt », et ne pas présenter le noir comme persistant sur ce run.

Lecture UI puis export JSON réel : kit 20/20 instances, 36 colliders de kit,
forêt 1 662 arbres / 19 944 primitives ; un seul monde initialisé, une caméra
active. Les colliders de kit ne comptent pas les colliders de toute la forêt.
Six hashes de sources alignés (`source_manifest_matched`), `failure_reason=null`.
Qualité High disponible appliquée, eau/environnement partiels ; pas de profil
expérimental d'ombres natif promu. Viewport CSS/rendu 1408 × 966, pas Full HD.

Export `forge-diagnostic-1788882291490.json` et capture
`forge-viewport-1788882315144.png` récupérés dans Downloads puis copiés sur E:
avec vérification des hashes. PNG inspecté : arbres colorés et personnage bleu,
forêt sur la droite depuis le spawn en prairie. Aucun parcours jusqu'au sous-bois
ni égalité de caméra avec la capture native ne sont revendiqués. JSON et PNG
non atomiques. Un clic sur l'export hors écran a été refusé ; après défilement,
le vrai clic et le téléchargement ont réussi. Pas de clavier de déplacement testé.

120 samples du ledger existant : 32,715 FPS moyens, p95 38,8 ms, 1 % low 24,39 FPS.
Cette collecte de démarrage n'est ni la série finale de performance ni un benchmark
1080p sur matériel cible. `gpu_presentation=not_observed` reste la portée du ledger
automatique ; l'inspection du PNG est une preuve visuelle distincte.

Studio PID 29336 fermé ; conhost 20156 et WebViews 29588, 43440, 20236, 42140,
42228, 42796 également absents au contrôle CIM final. Trunk 37588 et wasm-opt
26012 terminés, session Computer Use réinitialisée. Aucun navigateur utilisateur tué.

## Verdict et limites

Kit corrigé **intégré au candidat Studio local, lancé et inspecté**, pas promu
artistiquement ni distribué. Anciennes preuves/bundles et sources préservés.
Gate 1 ouverte : warmup, performance cible, parcours humain, retouche Blender et
acceptation artistique restent à qualifier. Aucune Gate 2 ni publication Git.
Les tests Rust historiques ne sont pas présentés comme exécutés par ce lot de
packaging ; le nouveau frontend et la coque ont réellement compilé et tourné.

| Artefact | SHA-256 |
| --- | --- |
| forge-studio-candidate.exe | `1F515A2B725C458BE29B065EFEC5D47E0817A4EB89F09E8AC549790A62CC8DFB` |
| forge_studio_bg.wasm | `8C14D42EF7B6233D6B36DC7D709A7D56C77AE6E51AC95A47FE8D563BCFA1DC47` |
| build-proof.json | `F1E47657A030C34A63377470F6B319814D34D073CB219D51AB5949E71F573CED` |
| diagnostic.json | `3DBB5A774F6665CEA4D7BE5612D8CB9C1BAFEC16DBE47F69904A99E7ED13994F` |
| viewport.png | `C44D3576AFB3A667C3B25A845C3E53D59FC7980BD50A1DEF084601F5B303C66A` |
| studio-initial.png | `35023BEED9F3D8504A9E009A8436CC26AC4FF7A271E89709A99FE876143785C6` |
| studio-ready.png | `91B07A19D86F73676BFA39410393EE315733ABEC2C70BD3AAEE29BA9567869FF` |
