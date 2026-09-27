# R1 — éclairage de l'eau et contraste intégrés au Studio

9 septembre 2026, Rust, Gate 1 ouverte, aucune publication.

## Construction

Racine : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-studio-lit-contrast-20260909/`.
`BuildStudio.ps1` construit le frontend Trunk release et le candidat Tauri.
Clippy Studio WebGPU strict PASS ; builds PASS. 649 tests workspace / un ignoré
du lot `R1_WEATHER_SHADOW_CONTRAST.md` réutilisés : aucune modification source
depuis ces tests. Hashes eau/météo contrôlés avant construction.
Deux tâches Rust et deux cœurs Binaryen maximum ; fichiers temporaires sur E:.
L'avertissement de compatibilité future `proc-macro-error2 2.0.1` reste visible.

- EXE SHA256 : `4050A8CD0DF40FE042E6BBF50990EB5EB629447C544B2AD52D5EA91790D03119`.
- WASM SHA256 : `96B94A7B33DE6201131E47105E8D88C544F1B47AE8685BAE443446A63FC52827`.
- `candidate/build-proof.json` conserve son verdict BUILD_ONLY ; le lancement
  réel est attesté séparément ci-dessous.

## Exécution et pixels

Studio ouvert par son EXE exact, piloté par l'interface Windows. Kit gouverné,
forêt en batches, caméra candidate `water_close`, temps figé à zéro, eau
`depth-preview`. Canvas CSS/rendu 1408 × 966, monde initialisé une seule fois.
Même caméra que `R1_STUDIO_DIRECTIONAL_WATER.md`, documents inchangés.

Exports obtenus par les boutons du produit, originaux Downloads conservés :

- `forge-diagnostic-1788913779196.json`, SHA256
  `E1BC714B9F9FE7816C37BA4E53992DD850DF7E658B6F536E0150E9CD24B57A5A` ;
- `forge-viewport-1788913837526.png`, SHA256
  `242A2C30D14DADD5E546ADE69879CAE158E40713DEE3FAF25AA3E5D62FE2523B`.

JSON/PNG non atomiques ; temps de simulation figé entre exports. Un clic PNG
initial hors fenêtre a été refusé : défilement puis observation renouvelée
avant le clic effectif. Aucune capture substituée au résultat du produit.

Diagnostic : préparation en 16,805 s, 74 pipelines, pending 0, failed 0,
pic transitoire d'erreurs shader 18 conservé. Contraste moteur 1,
gamma 0,94339627 (= 1/1,06), exposition 0, saturation 1,04 : l'adaptation
du contraste clair est effectivement projetée. Soleil météo désactivé dans
ce profil clair ; cette séance ne qualifie pas le cas soleil réduit en WebGPU.

Capture inspectée : teinte bleutée sous le pont, fond visible et détails des
faces ombrées conservés. Le rendu demeure trop uniforme, avec des stries et
un fond brun dominant au premier plan. Pas d'acceptation artistique.
Kit 20/20, colliders affichés zéro/chargement sous horloge figée : aucune
qualification de collision ou de playtest déduite de cette séance.

Cadence exportée : 120 échantillons, moyenne 28,5875 ms, p95 34,1 ms,
1 % low 27,21 FPS. Ce relevé de vue fixe/WebView n'est pas le benchmark de
qualification et ne prouve pas la cible 60 FPS. Pas de mesure GPU temporelle
nouvelle ni de VRAM globale disponible. Mouvement non rejoué dans ce lot.

## Nettoyage et suite

Studio PID 20416 et enfant conhost 33824 absents après fermeture ; optimiseur
35528 terminé. Aucune session utilisateur fermée, aucun navigateur ouvert.
Les deux corrections sont intégrées et vérifiées dans ce candidat Studio.
Les critères globaux eau, mouvement, profils et revue humaine restent ouverts.
Ne pas répéter cette construction/capture sans changement pertinent ; poursuivre
le défaut visuel restant, pas un nouveau cycle de qualification de ces mêmes
corrections. R1-022/023 et Gate 1 restent ouverts.
