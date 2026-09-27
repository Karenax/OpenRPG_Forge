# Gate 2 — candidat local v8

22 septembre 2026, branche `Rust`, HEAD
`f786a38aca2023f08d708687f99a47d14f34e6d4`. Construction locale seulement,
sans commit, push ni publication.

## Entrées épinglées

- 468 fichiers d'entrée Rust/Cargo, 5 407 764 octets ; manifeste déterministe
  `relative/path|bytes|sha256` SHA-256
  `AEF0DE612696A1B6052D57DC09B9AFE64F1CDACC024649829DC3B4A68768E6A8`
  avant et après les deux builds.
- Frontend release : `forge_studio_bg.wasm`, 45 536 100 octets, SHA-256
  `8DC3E06581536180E0241226D0154A169C27178F076823EC37E4269FAB515E5A`.
- Kit gouverné existant :
  `artifacts/forge-runtime/gate2-resume-20260919/r2-025-kit-rev5-astra-approved-20260922`.
- Caches Cargo, target, Binaryen et temporaires sur E:. Compilation offline,
  lockfile imposé, un job Cargo et deux workers Binaryen au maximum.

## Résultats de construction

- Trunk release/Binaryen 131 : PASS en 30 min 39 s.
- Tauri debug sans bundle : PASS en 14 min 35 s.
- Candidat :
  `artifacts/forge-runtime/gate2-resume-20260919/gate2-closure-candidate-20260922-v8/forge-studio-candidate.exe`.
- SHA-256 EXE :
  `126ED7C7A96095132D78E590FC1208E826D0E5C9E293B0A187D73C96652D23AE`.
- Le WASM copié dans le candidat conserve exactement le hash d'entrée.
- `build-proof.json` : `status=BUILD_ONLY`, `runtime_verified=false`,
  `gate_qualified=false`, branche `Rust`.
- Log natif SHA-256 :
  `CFD08B8546ECBB78907A9FE7D6C4BB5E0A29006FDF538DB8AABA5C40E00DD284`.
- Processus Trunk, Cargo, rustc, wasm-opt et Studio absents au contrôle final.

## Limite observée

Le build natif émet un avertissement `dead_code` pour `audit_label`, utilisé par
les vues Web mais compilé aussi dans la bibliothèque native. La construction
reste réussie et le frontend v8 contient les libellés d'audit attendus ; ce
warning devra être corrigé et repassé sous Clippy strict avant le candidat final.
Le build seul ne qualifie aucun critère fonctionnel, aucune revue humaine et
aucune Gate. CI distante : `NOT RUN — infrastructure billing`.
