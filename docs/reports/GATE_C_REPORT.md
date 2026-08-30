# Gate C report — Cadle acquisition and qualification

Date: 2026-08-30

Branch: `Rust`

Verdict: **PASS under the bounded ADR-0003 qualification overlay**.

Gate 0 may start. No Rust port, Cadle source, Cadle asset, generated Cadle build or capture was added
to Forge while closing this gate.

## Executive result

The canonical Cadle snapshot is reproducibly acquired at commit
`ea0da62549980816a6a27e08b75808b1f56d70e7` and tree
`f0d52e434454fe302052d22eafde203bc361cc1f`; origin, detached HEAD, clean tree and idempotence checks
pass. Licences and asset boundaries are documented and automated. The reuse matrix is complete.

Cadle's upstream npm lock remains defective. No official branch or v0.6 tag fixes it, and the last
synchronized historical lock predates the selected reference by 144 commits and 438 changed files.
ADR-0003 therefore preserves the source checkout intact and qualifies a separate disposable clone
where the only tracked delta is the reviewed Forge `package-lock.json` overlay. This result is named
**Cadle baseline under Forge overlay**, never an intact upstream installation.

## Gate results

| Lot | Result | Evidence |
|---|---|---|
| C-A acquisition | PASS | exact lock, Windows/shell acquisition, commit/tree/URL/detached verification, idempotence |
| C-B licences/isolation | PASS with local-only conditions | licence audits, notices, boundary check, no Cadle asset/code copied |
| C-C source-intact reproduction | PASS under ADR-0003 | two byte-identical lock generations; two `npm ci`/builds; scripted isolated clone |
| C-D visual/performance | PASS on documented RX 7800 XT machine | real GPU, fixed 1080p High scenes, 30-second frame sample and inspected captures |
| C-E reuse matrix | PASS | source families classified with destination, attribution, tests and deferred lots |

## Immutable identities

| Evidence | SHA-256 / identity |
|---|---|
| Cadle commit | `ea0da62549980816a6a27e08b75808b1f56d70e7` |
| Cadle tree | `f0d52e434454fe302052d22eafde203bc361cc1f` |
| `package.json` | `95952a2f2d4114c619a6c182b610c46d12db4c366aa59292c0a7ffc28650bcfc` |
| upstream lock (defective, preserved) | `1d01d9703a7679fecb0ce2f4144eef382c0adc832d0298da19a9efb515af82a3` |
| Forge qualification overlay (tracked LF) | `85f9b294da3d67fc698631b4b9903ad9a33214044573db82ed86e4b09d2f1c1f` |
| `LICENSE` | `8f653f3f69f17d2dabacaba945334989d0d459c05f8adb790d29057365cc1de9` |
| `NOTICE` | `cdca80e358e0d7b97fa0badf71bc22e8ffd2f4e7a3bfdd9da5243b92486a5f24` |

The raw CRLF overlay was independently generated twice with Node 22.22.3/npm 10.9.8 and was
byte-identical (`2e84d2a9…`), then normalized to the tracked UTF-8/LF hash above. It changes the root
version 0.1.0 → 0.6.0, locks ten rather than five direct dependencies and retains
the versions of all 46 existing upstream entries.

## Exact validation ledger

| Validation | Result |
|---|---|
| PowerShell acquisition and `-VerifyOnly` | PASS |
| Git Bash `get-cadle.sh --verify-only` | PASS on Windows Git Bash; native Linux remains unproven |
| second acquisition | PASS, identical commit/tree/status |
| clean Forge clone without Cadle | PASS, boundary reports `checkout_present=False` |
| clean Forge clone acquisition twice | PASS, exact identity and clean Forge tree |
| canonical Cadle checkout | PASS, detached/exact/clean |
| Forge boundary check | PASS; only README, source lock and qualification lock allowed under tracked `upstream/` |
| isolated qualification clone | PASS; only `package-lock.json` differs |
| two independent derived locks | PASS; byte-identical SHA-256 |
| two `npm ci --ignore-scripts --no-audit --no-fund` | PASS |
| two `npm run build` | PASS, 106 modules |
| invariants/check/curvecheck/buildcheck | PASS in both independent probes; scripted gate PASS |
| dependency inventories | PASS; upstream defect and complete overlay inventories kept separately |
| `npm audit` on complete overlay | FAIL as evidence: one high Sharp/libvips advisory, retained and bounded |
| real-GPU check | PASS, RX 7800 XT / ANGLE D3D11; SwiftShader rejected |
| Vale, forest, water and dense/movement captures | PASS; inspected locally, non-blank, no harness/game errors |
| canonical checkout after execution | PASS, unchanged |

## Performance result

Protocol: 1920×1080, High, seed 1337, VSync/browser cap disabled, RX 7800 XT, Chrome for Testing
151.0.7922.34, 31-second initial wait and 40-second dense-scene settling time.

Dense engine window: 169.5 FPS, mean 5.90 ms, p95 7.7 ms, p99 9.4 ms, CPU mean 5.69 ms, GPU mean
4.59 ms / p95 6.87 ms, 227 calls, 2,872,960 triangles and 414.8 MiB JS heap.

Independent 30-second rAF sample: 5,062 frames, mean 5.929 ms, p95 8.0 ms, p99 9.6 ms, max 14.3
ms, **1% low 94.6 FPS**, zero hitches above 16.7 ms and zero above 33.3 ms. Warm-up observations:
sky 275/276 ms, terrain preview 106 ms, full terrain 424 ms, vegetation 752 ms and EZTrees 1,623 ms.

These figures qualify the Gate C reference on this machine. They do not satisfy Gate 1's separate
RTX 3060/RX 6600-class hardware requirement.

## Visual inspection

- Vale: terrain, grass, landmark, HUD and weapon rendered in all three 1920×1080 views.
- Whisperwood: trunks, canopy, undergrowth and light shafts rendered; no blank frame.
- Water: reflection/refraction and golden-hour views rendered; the known noon-island occlusion remains.
- Dense village: live player, buildings, NPCs and well rendered after warm-up.
- Movement: the player moved from `[125.22, 6.80, -108]` to `[118.47, 6.51, -98.14]` and stopped
  against the well masonry; both frames were inspected.
- Existing Three.js deprecation, non-indexed geometry and shader warnings remain visible; error arrays
  were empty.

Raw reports and images remain ignored and local. Their hashes and detailed numbers are recorded in
`docs/baselines/cadle-ea0da625.md`.

## Licence and security conclusion

Cadle code is MIT, copyright Ian Cadle 2026, subject to its notice. Forge excludes every Cadle media
file from tracking and distribution, including paths not explicitly named by the notice. The complete
qualification graph includes MPL and LGPL components and one high Sharp/libvips advisory. It is safe
only under the local-reference policy: no lifecycle scripts, no redistribution, no product dependency.

## Residual limitations

- native Linux execution is not proven;
- both isolated reproductions used the same physical Windows machine, not two physical machines;
- the upstream lock remains invalid and would reopen Gate C if the tracked overlay or ADR exception
  were removed;
- Gate 1 still needs its own midrange GPU, native/WASM and Studio-authored comparative evidence.

These limitations are explicit and do not invalidate the bounded Gate C objective. Roadmap state is
advanced to Gate 0; any change to the snapshot, overlay hash, execution boundary or asset policy
reopens Gate C.
