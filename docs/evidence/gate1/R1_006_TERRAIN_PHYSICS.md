# Gate 1 — Preuve R1-006 collisions terrain locales

Date de validation locale : **2026-08-31**

Branche : **`Rust`**

Commit code qualifié : **`decbb78f771130b5c69c82c858df29f606164f7b`**

Tree Git : **`98961aac1a04e8efa8eb7ea39d269dcff91c3d23`**

Verdict du lot : **PASS AUTOMATISÉ LOCAL — R1-006**

Gate 1 reste **OUVERTE**. Cette preuve ne qualifie ni la mise à jour partielle runtime `R1-007`,
ni le contrôleur personnage, ni la revue humaine comparative, ni le benchmark GPU milieu de gamme.

## Résultat livré

- `bevy_rapier3d 0.36.0` est épinglé exactement et activé sur les profils natif et WebGPU.
- La simulation Rapier utilise un pas fixe de **60 Hz**.
- Les **64 chunks** canoniques de Visual Valley produisent chacun un heightfield statique local de
  **33 × 33 échantillons**, centré sur le chunk et mis à l'échelle sur **64 × 64 m**.
- La conversion explicite transpose l'ordre Forge `Z puis X` vers l'ordre colonne-major attendu par
  Rapier.
- L'adaptateur refuse avant création tout payload non fini, mal dimensionné, non local, différent du
  mesh rendu ou utilisant un découpage de triangles incompatible.
- Les coutures entre chunks restent bit-identiques et des raycasts réels atteignent les deux
  heightfields de part et d'autre d'une couture canonique.
- Le runtime émet un événement machine-readable qui confirme la matérialisation effective des
  colliders.

Le lot ne contient aucun asset externe ou Cadle : les formes physiques sont dérivées exclusivement du
`TerrainDocument` Forge et de son cache déterministe.

## Validation automatisée

Environnement : Windows 11, `rustc 1.98.0 (88d9e12ae 2026-08-18)`, Cargo `1.98.0`.

| Contrôle | Résultat |
|---|---:|
| `cargo fmt --all -- --check` | PASS |
| `git diff --check` | PASS |
| `cargo test -p forge_project -p forge_world --lib --locked -j1` | **32/32 PASS** |
| `cargo test -p forge_engine --lib --locked -j1` en profil mémoire réduit | **19/19 PASS** |
| `cargo clippy -p forge_engine --all-targets --locked -j1 -- -D warnings` | PASS, zéro avertissement |
| `cargo check -p forge_engine --no-default-features --features web --target wasm32-unknown-unknown --locked -j1` | PASS en **4 min 27 s** |
| `cargo deny check` | PASS ; advisories, bans, licences et sources acceptés |
| `cargo audit --no-fetch` | PASS ; 20 avertissements autorisés de baseline, aucun blocage |

Le premier link complet des tests moteur a nécessité un profil à faible pression mémoire, le fichier
d'échange Windows ne laissant qu'environ 4 Go libres. La compilation a duré **21 min 13 s** ; les 19
tests ont ensuite réellement tourné en **0,16 s**. Une seconde exécution après les corrections Clippy a
terminé en **19,36 s**, dont **0,15 s** de tests.

Les tests R1-006 couvrent notamment :

- le mapping asymétrique ligne-major vers colonne-major ;
- le nombre, les dimensions, les centres et les formes des 64 payloads ;
- l'égalité mesh/collider, la finitude et le découpage exact de chaque cellule ;
- les coutures bit-identiques sur les axes X et Z ;
- le refus des payloads malformés ou non finis ;
- un raycast sur heightfield plat ;
- deux raycasts sur des chunks canoniques adjacents, à 1 mm de leur couture commune.

## Smoke runtime natif

Commande de capture, exécutée avec le même profil mémoire réduit :

```powershell
$env:CARGO_BUILD_JOBS='1'
cargo run -p forge_runtime --locked -j1 `
  --config 'profile.dev.opt-level=0' `
  --config 'profile.dev.debug=0' `
  --config 'profile.dev.codegen-units=512' `
  --config 'profile.dev.package."*".opt-level=0' `
  --config 'profile.dev.package."*".debug=0' `
  --config 'profile.dev.package."*".codegen-units=512' `
  -- --visual-valley `
  --capture artifacts/forge-runtime/gate1/visual-valley-physics-final.png `
  --capture-frame 120 `
  --metrics artifacts/forge-runtime/gate1/visual-valley-physics-final.metrics.json `
  --metrics-warmup-frames 60 --exit-after-frames 240 --no-vsync
```

| Artefact local | SHA-256 | Taille |
|---|---|---:|
| `visual-valley-physics-final.png` | `b2e44bfd3323029c9377fc00d74f64e139f2ac7543f2169bfb43aed5e367ea74` | 1 350 081 octets |
| `visual-valley-physics-final.metrics.json` | `445b59ac1cfc1685f08dd6bf2ef759e3974d3acf7580802feeb0964e4966b1ae` | 4 973 octets |

La capture 1920 × 1080 a été inspectée : la vallée reste visible et cohérente avec la preuve terrain
précédente. Le ledger confirme le hash source
`9f16780a1de1e9dd4b65931624a8b45281713829434292bada320a578420b2ef`, 64 chunks,
69 696 vertices et 131 072 triangles sur une AMD Radeon RX 7800 XT / Vulkan.

Un second lancement court avec `RUST_LOG=forge::physics=info` a produit :

```json
{"schema":"forge.runtime-event","schema_version":1,"event_kind":"terrain_heightfields_materialized","colliders":64,"simulation_hz":60.0}
```

Les timings de cette capture ne constituent pas un benchmark Gate 1 : un hitch d'initialisation de
687 ms pollue le 1 % low. `R1-041` et `R1-042` restent donc ouverts.

## Dépendances et licences

Le graphe actif ajouté par ce lot repose notamment sur :

| Crate | Version | Licence déclarée |
|---|---:|---|
| `bevy_rapier3d` | `0.36.0` | Apache-2.0 |
| `rapier3d` | `0.35.0-glamx0.2` | Apache-2.0 |
| `parry3d` | `0.30.2-glamx0.2-b` | Apache-2.0 |
| `nalgebra` | `0.34.2` | Apache-2.0 |
| `glamx` | `0.2.0` | MIT OR Apache-2.0 |

Toutes les sources résolues viennent du registre crates.io. `cargo deny check` a validé licences et
sources ; aucun renderer de debug Rapier, parallélisme ou SIMD8 optionnel n'est activé.

## CI distante — exception ADR-0006

Le push du commit code a déclenché le run GitHub Actions
[`33349027247`](https://github.com/Karenax/OpenRPG_Forge/actions/runs/33349027247).
Les quatre jobs ont échoué avant toute étape :

| Job | ID | Étapes exécutées | Classification |
|---|---:|---:|---|
| `native (windows-2025)` | `99358608173` | 0 | `NOT RUN — infrastructure billing` |
| `native (ubuntu-24.04)` | `99358608224` | 0 | `NOT RUN — infrastructure billing` |
| `wasm` | `99358608254` | 0 | `NOT RUN — infrastructure billing` |
| `supply-chain` | `99358608432` | 0 | `NOT RUN — infrastructure billing` |

Conformément à l'autorisation explicite du propriétaire et à ADR-0006, cette absence de CI distante
ne bloque pas R1-006, mais elle n'est jamais présentée comme verte. La dérogation ne couvre pas la
revue humaine comparative de Gate 1 ni le benchmark RTX 3060/RX 6600.

## Limites et suite obligatoire

- `R1-007` reste ouvert jusqu'à ce qu'un patch de terrain mette réellement à jour uniquement les
  meshes et colliders concernés, tout en préservant les entités et handles non touchés.
- Aucun joueur n'exerce encore ces collisions ; le contrôleur Rapier et la caméra commencent à
  `R1-027`.
- La capture RX 7800 XT est une preuve de fonctionnement, pas la qualification milieu de gamme.
- Aucune revue humaine Gate 1 n'a été dérogée.
