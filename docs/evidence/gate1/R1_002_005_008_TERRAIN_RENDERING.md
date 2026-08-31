# Gate 1 — Preuve terrain R1-002 à R1-005 et R1-008

Date : 2026-08-31
Branche : `Rust`
Commit de code qualifié : `4e8364b1f05fb2826cfb19025b13e180e8db46a0`
Arbre Git : `483660718914e0b59c3e776b0b94aea03f5a6f2a`
Verdict : **PASS LOCAL — R1-002, R1-003, R1-004, R1-005 et R1-008 uniquement**

Gate 1 reste **OUVERTE**. Cette preuve ne qualifie ni les collisions `R1-006`, ni l'application d'une
mise à jour partielle `R1-007`, ni les performances finales, ni la revue comparative humaine.

## Portée acquise

Le lot conserve `TerrainDocument` comme unique source persistante, puis construit un cache dérivé
déterministe dans `forge_world` et l'adapte en meshes Bevy dans `forge_engine` :

- `R1-002` — la grille source globale `257 × 257` est découpée en `8 × 8`, soit 64 chunks de
  `32 × 32` intervalles et `33 × 33` sommets ; chaque intervalle de la surface `512 × 512 m` est
  couvert exactement une fois et les identifiants de chunks sont stables ;
- `R1-003` — les 64 meshes totalisent 69 696 sommets locaux et 131 072 triangles. Les positions,
  UV, normales et poids de matière sont identiques bit à bit sur les coutures ; les normales sont
  unitaires, orientées vers `+Y`, et les triangles ont un winding antihoraire vus du dessus ;
- `R1-004` — un `ExtendedMaterial<StandardMaterial, _>` et un shader WGSL Forge original exposent
  trois couches PBR distinctes, herbe, terre et roche, avec couleur, rugosité, métallicité et
  réflectance propres ; aucune texture ni aucun asset externe n'est chargé ;
- `R1-005` — les poids herbe/terre/roche, finis, non négatifs et normalisés, sont générés depuis la
  hauteur et la pente, transportés par `COLOR.rgb`, renormalisés dans le shader et utilisés pour le
  mélange des propriétés PBR ;
- `R1-008` — le mode explicite `--terrain-debug` superpose les bornes des 64 chunks par gizmos et
  code la couleur par niveau de LOD. La tranche ne possède encore qu'un niveau, donc le ledger et
  l'overlay annoncent honnêtement `lod_levels = 1` et `lod_zero_chunks = 64`.

La sortie Bevy contient 64 meshes dérivés avec usages `MAIN_WORLD | RENDER_WORLD`. Les statistiques
communes distinguent un document source canonique, 64 instances de mesh dérivées, les statistiques de
pipeline GPU et les draw calls réellement indisponibles ; aucun de ces compteurs n'est substitué à
un nombre d'appels de dessin.

## Empreintes déterministes et artefacts locaux

| Objet | SHA-256 | Taille |
|---|---|---:|
| document canonique `TerrainDocument` | `9f16780a1de1e9dd4b65931624a8b45281713829434292bada320a578420b2ef` | — |
| cache dérivé `GeneratedTerrain` golden | `e01ecc7a878c2df6cf698feb2de557eb011af0c3fa307ef3b21ea29d0aa42af0` | — |
| capture finale `visual-valley-terrain-final.png` | `a06421b7f5a7e29f13e4bb9ceede6c4c60ca0456a76c734f9f01cc79e823deb0` | 1 350 018 octets |
| métriques finales `visual-valley-terrain-final.metrics.json` | `fff882b0dbb9b8b4ef2f370d88edaa5aee45a7e807bbc201fbcf5038b30db52e` | 4 938 octets |
| capture debug `visual-valley-terrain-debug-final.png` | `82a3ce504884550458aa247a4134a816b90ecfb432dd6db278a58360c96c59cb` | 1 484 972 octets |

Les trois fichiers de capture et métriques vivent sous `artifacts/forge-runtime/gate1/`, chemin
local ignoré par Git. Seules leurs empreintes et observations textuelles sont versionnées. La capture
finale montre le relief rendu et le mélange de couches ; la capture debug montre les 64 volumes cyan
LOD 0 superposés aux chunks. Cette inspection technique par l'agent n'est pas la revue humaine
comparative exigée à la fin de Gate 1.

## Validation locale exécutée

| Commande | Résultat |
|---|---|
| `cargo fmt --all -- --check` | PASS |
| `cargo test -j 1 -p forge_world --locked` | PASS, 18 tests |
| `cargo clippy -j 1 -p forge_world --all-targets --locked -- -D warnings` | PASS |
| `cargo test -j 1 -p forge_engine --locked` | PASS, 12 tests |
| `cargo clippy -j 1 -p forge_engine --all-targets --locked -- -D warnings` | PASS |
| `cargo test -j 1 -p forge_runtime --all-features --locked` | PASS, 10 tests unitaires et 1 test d'intégration |
| `cargo clippy -j 1 -p forge_runtime --all-targets --all-features --locked -- -D warnings` | PASS |
| `cargo check -j 1 --target wasm32-unknown-unknown -p forge_engine --no-default-features --features web --locked` | PASS |
| `cargo test --workspace --locked -j 1` | PASS en 2 min 05 s, 71 tests, 0 échec |
| `cargo clippy --workspace --all-targets --all-features --locked -j 1 -- -D warnings` | PASS en 1 min 34 s |
| `cargo deny check` | PASS : advisories, bans, licences et sources |
| `cargo audit --no-fetch` | PASS sur la base locale : 1 226 advisories, 872 dépendances, aucune vulnérabilité bloquante |

Le replay workspace de 71 tests se répartit ainsi : 3 `forge_assets`, 3 intégrations `forge_cli`,
5 `forge_core`, 12 `forge_engine`, 8 `forge_project`, 10 unitaires `forge_runtime`, 1 intégration de
journalisation runtime, 8 `forge_studio_frontend`, 3 `forge_studio_shell` et 18 `forge_world`.

`cargo audit --no-fetch` n'a effectué aucun fetch réseau. Il a conservé visibles 20 avertissements
allowlistés existants de catégories `unmaintained`/`unsound`. `cargo deny check` n'a relevé que les
avertissements de duplication déjà présents. Le lot n'ajoute aucune dépendance externe ni transitive :
le seul changement de `Cargo.lock` ajoute le lien workspace interne `forge_engine → forge_world`.

## Smoke natif et correction du shader

La première exécution du smoke a terminé avec un code processus nul, mais elle a été rejetée comme
preuve : la validation WGSL signalait une incompatibilité de type sur `reflectance`, et les
statistiques de la passe opaque restaient à zéro. Un code de sortie nul n'a donc pas été assimilé à
un rendu réussi. L'affectation a été corrigée vers le `vec3<f32>` attendu par Bevy, puis les tests, le
smoke, les captures et les métriques ont été rejoués.

Commande finale qualifiée :

```text
cargo run -j 1 -p forge_runtime --locked -- --visual-valley --capture artifacts/forge-runtime/gate1/visual-valley-terrain-final.png --capture-frame 180 --metrics artifacts/forge-runtime/gate1/visual-valley-terrain-final.metrics.json --metrics-warmup-frames 120 --exit-after-frames 360 --no-vsync
```

Commande du debug chunks/LOD :

```text
cargo run -j 1 -p forge_runtime --locked -- --visual-valley --terrain-debug --capture artifacts/forge-runtime/gate1/visual-valley-terrain-debug-final.png --capture-frame 90 --exit-after-frames 100 --no-vsync
```

Environnement et résultat du smoke final :

| Champ | Valeur observée |
|---|---:|
| GPU / backend | AMD Radeon RX 7800 XT / Vulkan |
| pilote | AMD proprietary driver `26.8.1 (LLPC)` |
| fenêtre | 1920 × 1080, profil Cargo `dev`, VSync demandée désactivée |
| échantillons | 240 après 120 frames de warm-up |
| frame moyenne | 3,3574129167 ms |
| p95 / p99 | 4,458 ms / 4,8962 ms |
| 1 % low | 79,7249 FPS |
| hitches | 1 au-dessus de 16,7 ms ; 0 au-dessus de 33,3 ms |
| primitives opaques GPU | 124 928 |
| invocations vertex shader opaques | 131 760 |
| invocations fragment shader opaques | 1 972 949 |
| draw calls GPU | `null` — `unavailable_bevy_0_19_no_global_counter` |
| scène dérivée | 64 chunks, 69 696 sommets, 131 072 triangles, 3 couches, 1 LOD |

Les primitives et invocations sont des statistiques de pipeline, pas des draw calls. ADR-0004
expire avant `R1-037` et ne qualifie pas cet item.

Ces timings constituent un smoke de développement sur RX 7800 XT, pas le benchmark High de Gate 1.
Ils ne qualifient ni `R1-041` sur RTX 3060/RX 6600, ni `R1-042`, ni les conditions de performance de
la gate.

## Non-régression Gate 0

Le mode runtime par défaut a été relancé sans `--visual-valley` :

```text
cargo run -j 1 -p forge_runtime --locked -- --capture artifacts/forge-runtime/gate1/gate0-regression.png --capture-frame 120 --metrics artifacts/forge-runtime/gate1/gate0-regression.metrics.json --metrics-warmup-frames 60 --exit-after-frames 240 --no-vsync
```

| Objet | SHA-256 | Taille |
|---|---|---:|
| capture Gate 0 | `e10394409db40635492a892f5524c7c08d21ea224ea5375c99bb4d1d4e19223d` | 1 043 088 octets |
| métriques Gate 0 | `348c2e623d3ec1a7345b4a8bf19be925aa3bb47f824aa13b2275fdbfe66eecdc` | 4 663 octets |

Le ledger conserve 4 entités source, 2 meshes source, 14 primitives opaques et
`gpu_draw_calls = null`. La section optionnelle `visual_valley` est absente, ce qui prouve que le
nouveau chemin n'a pas remplacé silencieusement la scène Gate 0.

## Exception CI distante ADR-0006

Le commit qualifié est publié sur `origin/Rust` avec une divergence locale/distante `0/0`. Le run
GitHub Actions [`33343208613`](https://github.com/Karenax/OpenRPG_Forge/actions/runs/33343208613)
cible exactement `4e8364b1f05fb2826cfb19025b13e180e8db46a0` :

| Job | ID | Conclusion d'interface | Étapes | Qualification Forge |
|---|---:|---|---:|---|
| `supply-chain` | `99342459850` | `failure` | 0 | `NOT RUN — infrastructure billing` |
| `native (windows-2025)` | `99342459931` | `failure` | 0 | `NOT RUN — infrastructure billing` |
| `wasm` | `99342459980` | `failure` | 0 | `NOT RUN — infrastructure billing` |
| `native (ubuntu-24.04)` | `99342459991` | `failure` | 0 | `NOT RUN — infrastructure billing` |

Les quatre annotations sont identiques :

```text
The job was not started because recent account payments have failed or your spending limit needs to be increased. Please check the 'Billing & plans' section in your settings
```

Le run origine de l'exception reste `33335683606`. Le run courant n'a exécuté ni checkout, ni
toolchain, ni build, ni test. Les validations locales sont `PASS`; les CI distantes Windows, Linux,
WASM et supply-chain restent **NOT RUN — infrastructure billing** sous ADR-0006, jamais « vertes ».

## Limites conservées

- `R1-006` reste ouvert : le cache dérivé expose des données de collider, mais aucun collider Rapier
  actif ni aucune collision runtime ne sont prouvés.
- `R1-007` reste ouvert : le calcul de région sale est testé, mais la régénération et l'application
  partielles des chunks ne sont pas implémentées ni observées.
- `R1-037` reste ouvert : aucun compteur exhaustif de draw calls n'est disponible.
- Les captures sont locales, ignorées et non publiées ; aucune capture Cadle n'est versionnée.
- Aucune comparaison A/B Cadle et aucune revue humaine Gate 1 n'ont été réalisées. `R1-049` à
  `R1-052` restent ouverts.
- Aucun code, asset, nom, lore, texte ou runtime Cadle n'a été copié dans Forge.
- Gate 1 reste ouverte : végétation, caméra/Rapier, atmosphère, eau, profils qualité, parité Studio,
  benchmark milieu de gamme et verdict final restent dus.
