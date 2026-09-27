# forge_navigation_benchmark

Outil isolé de sélection du fournisseur de navigation. Il consomme les documents Forge et mesure
des crates candidates sans les introduire dans `forge_engine` ou `forge_runtime`.

Le benchmark doit rester plus strict qu'une simple compilation :

- trois générations déterministes de la vallée sculptée et du pont ;
- requête de chemin sur le franchissement route/rivière réel ;
- intérieur avec porte ouverte, porte fermée puis réouverte ;
- profils d'agent de tailles différentes ;
- coûts de zones, lien hors maillage et évitement local ;
- compilation native et `wasm32-unknown-unknown` ;
- rapport explicite des fonctions documentées mais absentes chez le fournisseur.

L'outil n'est pas un moteur de navigation Forge. Il appelle les algorithmes des fournisseurs et
produit uniquement une preuve dérivée.

## Pile candidate épinglée

- `rerecast 0.4.0` / `bevy_rerecast 0.5.0`, feature déterministe `libm` ;
- `landmass 0.9.2` / `bevy_landmass 0.12.0` ;
- `landmass_rerecast 0.3.0` ;
- `getrandom 0.3.4` avec `wasm_js` uniquement pour `wasm32`.

Ces dépendances restent isolées dans ce laboratoire Cargo autonome, explicitement exclu du workspace
produit. Elles ne sont donc présentes ni dans le lockfile produit, ni dans `forge_engine`, ni dans
`forge_runtime`.

## Exécution

Depuis la racine du dépôt, avec un chemin de rapport qui n'existe pas encore :

```powershell
cargo run --manifest-path tools/forge_navigation_benchmark/Cargo.toml --locked -- `
  examples/visual_valley/world-sketch.v1.json `
  E:\OpenRPG_Forge\.gate1-evidence-runtime\navigation-benchmark-new\navigation-provider-benchmark.v1.json
```

L'écriture utilise `create_new` et refuse donc d'écraser une preuve précédente. Le statut de
sélection se trouve dans `selection.status` ; une exécution réussie du programme ne signifie pas que
le fournisseur a passé le contrat.

## Vérifications

```powershell
cargo fmt --manifest-path tools/forge_navigation_benchmark/Cargo.toml -- --check
cargo clippy --manifest-path tools/forge_navigation_benchmark/Cargo.toml --all-targets --locked -- -D warnings
cargo test --manifest-path tools/forge_navigation_benchmark/Cargo.toml --locked
cargo check --manifest-path tools/forge_navigation_benchmark/Cargo.toml --target wasm32-unknown-unknown --locked
cargo deny --manifest-path tools/forge_navigation_benchmark/Cargo.toml check
```

Au 1er septembre 2026, la pile est `blocked_pending_contract_and_governance_gaps`. Le crop extérieur,
les portes, profils d'agents, coûts, liens et l'évitement passent. La vallée brute est rejetée par
Landmass sur le winding du polygone 2 819 ; les tuiles, la reconstruction partielle, le carving
d'obstacle dynamique et un backend Rapier maintenu manquent ; `cargo-deny` refuse aussi
`bincode 2.0.1` via `RUSTSEC-2025-0141`.

Le rapport contient une normalisation diagnostique séparée. Elle ne doit jamais être activée
silencieusement : `winding_normalization_accepted` reste faux jusqu'à une décision architecturale et
une correction qualifiée.
