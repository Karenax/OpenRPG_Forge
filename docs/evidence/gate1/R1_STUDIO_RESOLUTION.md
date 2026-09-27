# Gate 1 — Mesure de résolution Studio honnête

2026-09-05, branche `Rust`, local non publié. Correction du défaut observé dans
`R1_STUDIO_WEBGPU_LAUNCH_20260905.md`.

## Implémenté

Le bandeau n'affiche plus une constante 1920 × 1080. Il montre le dernier relevé des
dimensions CSS et du buffer de rendu. Les valeurs manquantes, partielles ou nulles sont
affichées « non mesuré ». Aucun suivi continu de redimensionnement n'est revendiqué.

Le ledger v1 conserve `canvas_width_px`/`canvas_height_px` (historiquement CSS) et ajoute
`render_width_px`/`render_height_px`, facultatifs, obtenus par les attributs du canvas.
Les nouveaux lecteurs acceptent les anciens rapports ; un ancien validateur strict non mis
à jour peut refuser les nouveaux champs. Le JSON Schema du dépôt est mis à jour avec eux.

## Vérifié

- Clippy workspace/all-targets/all-features avec `-D warnings` : PASS.
- `cargo test --workspace --locked -q` : **301 PASS** (Studio 23, autres comptes inchangés).
- Nouveaux tests : CSS 1280 × 720 / rendu 2560 × 1440, valeurs absentes/nulles,
  lecture et sérialisation historique inchangée, round-trip des dimensions de rendu.
- `cargo fmt --all -- --check` et `git diff --check` : PASS.
- `Test-Json` valide le ledger Chrome archivé contre le JSON Schema ; le même ledger sans
  les deux champs ajoutés est également accepté. Les tentatives Python ont échoué faute de
  module `jsonschema` ; aucune dépendance installée, validation faite avec PowerShell.
- Bundle exécutable reconstruit par Trunk, sortie 0 ; étape Cargo 6,25 s grâce au cache.
  Warning futur Rust `proc-macro-error2` inchangé. Pas de nouveau benchmark release.

Même target/variables et commande que la preuve précédente, sortie distincte :
`E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-studio-resolution-20260905\dist`.
SHA-256 de `forge_studio_bg.wasm` :
`0bb5f1cd10f43fc5f42a1a33aeb98fffa5132106738c9604d9db7e772a022c85`.

## Observation navigateur

Chrome, URL locale `/?forge_scene=visual-valley&forge_weather=clear`.
Le bandeau affiche exactement :

```text
CSS 2048 × 1157 · rendu 2048 × 1157 px · dernier relevé
```

Lecture DOM indépendante : `clientWidth=2048`, `clientHeight=1157`, attributs
`width="2048"`, `height="1157"`. Ces valeurs correspondent au ledger réel copié dans
`R1_STUDIO_RESOLUTION_LEDGER.json` (13:20:48.359 UTC). Un recadrage navigateur du bandeau a été
inspecté dans la conversation ; aucune capture durable exportée ni hash visuel revendiqué.
Le skill de contrôle navigateur sert ici à confronter UI, DOM et ledger plutôt qu'à déduire
un résultat du seul build.

Ce test ne prouve pas encore un écran à DPR différent ni un redimensionnement en continu.
Le cas CSS/rendu différents est couvert automatiquement au niveau de présentation du contrat.
Le monde reste initialisé une fois, avec six hashes alignés et une caméra active. Le run de
développement mesure 29,159 ms moyens et p95 36 ms : aucun gain de performance attribué
à ce correctif de texte/télémétrie. Gate 1 reste ouverte ; candidat artistique non promu,
revues humaines dues, CI distante NOT RUN. Aucun commit ni push.
