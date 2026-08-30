# Cadle baseline — ea0da625

Date: 2026-08-30

Qualification status: **PASS — Cadle baseline under the bounded Forge overlay from ADR-0003**.

The canonical snapshot stayed at commit `ea0da62549980816a6a27e08b75808b1f56d70e7`, tree
`f0d52e434454fe302052d22eafde203bc361cc1f`, with a clean tracked tree. Its stale npm lock is retained
as evidence. Qualification ran in independent detached clones where the only tracked delta was
`package-lock.json`, replaced by the reviewed Forge lock
`2e84d2a98f193fd53a9af0dc68c9cb7e1f0773c1bf2459613824524b6abd7c25`. Two independent generations
of that lock were byte-identical; two `npm ci` and builds succeeded. This is not described as an
intact upstream installation.

## Machine and runtime

| Item | Value |
|---|---|
| OS | Windows 11 Pro 10.0.26200 x64 |
| CPU | AMD Ryzen 5 7600, 6 cores / 12 threads |
| RAM | 33,463,984,128 bytes reported by Windows |
| GPU | AMD Radeon RX 7800 XT, driver 32.0.31041.1004 |
| Browser | Chrome for Testing 151.0.7922.34, Playwright Chromium build 1234 |
| Browser driver | Playwright 1.62.1 |
| GPU renderer | ANGLE AMD Radeon RX 7800 XT, D3D11; not SwiftShader |
| Node / npm | 22.22.3 / 10.9.8 |

## Fixed protocol

- URL: `http://127.0.0.1:5173/play/`
- viewport: 1920 × 1080, device scale 1;
- quality: High;
- seed: 1337;
- harness flags: D3D11, GPU enabled, VSync and browser frame-rate limit disabled;
- scene scripts: the versioned Cadle scripts named below;
- every run verified that the served tree matched the local checkout;
- raw PNG and JSON reports remain ignored under `upstream/cadle/tools/out/` and are not published.
- qualification raw data for this run stayed in an ignored disposable clone outside the product
  sources; only hashes, measurements and textual observations are recorded here.

## Commands and results

```text
./scripts/New-CadleQualification.ps1 -Destination <ignored-new-directory>
./scripts/Test-CadleQualification.ps1 -QualificationRoot <ignored-new-directory>
  npm ci, build, invariants, check, curvecheck and buildcheck PASS

node tools/gputest.mjs
  real GPU PASS: RX 7800 XT / D3D11

node tools/inspect.mjs --name gate-c-vale --w 1920 --h 1080 --q high --seed 1337 --script tools/scripts/fin/vale.json --url http://127.0.0.1:5173/
  3 shots, 0 harness/game errors

node tools/inspect.mjs --name gate-c-whisperwood --w 1920 --h 1080 --q high --seed 1337 --script tools/scripts/fin/forest.json --url http://127.0.0.1:5173/
  3 shots, 0 harness/game errors

node tools/inspect.mjs --name gate-c-water --w 1920 --h 1080 --q high --seed 1337 --script tools/scripts/critic3-water-1.json --url http://127.0.0.1:5173/
  14 shots plus 5-frame motion burst, 0 harness/game errors

node tools/inspect.mjs --name gate-c-overlay-benchmark --w 1920 --h 1080 --q high --seed 1337 --url http://127.0.0.1:5173/ --script <forge>/docs/evidence/cadle-dense-safe.steps.json
  2 shots, 30-second rAF benchmark, 0 harness/game errors
```

The bounded JSON waits 31 seconds, enables Cadle's automation-only god/passive controls, teleports to
the `yard-perf.json` village coordinates, waits 40 seconds, captures the live scene, holds `KeyW` for
two seconds, captures the end position, records a four-second engine window and samples 30 seconds of
`requestAnimationFrame` deltas. The exact JSON and 1%-low formula are versioned as
`docs/evidence/cadle-dense-safe.steps.json`.

## Measurements observed

| Window | FPS | mean ms | p95 ms | p99 ms | CPU mean ms | GPU mean / p95 ms | calls | triangles | JS heap MiB |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| water view | 203.3 | 4.92 | 6.2 | 7.3 | 4.72 | 4.98 / 7.25 | 252 | 2,869,452 | 468.3 |
| water, reflection disabled | 214.1 | 4.67 | 13.6 | 27.6 | 4.52 | 4.66 / 6.75 | 196 | 2,286,177 | 468.3 |
| no water in view | 180.2 | 5.55 | 7.3 | 8.7 | 5.24 | 4.13 / 6.05 | 256 | 3,372,016 | 468.3 |
| dense village, player alive | 169.5 | 5.90 | 7.7 | 9.4 | 5.69 | 4.59 / 6.87 | 227 | 2,872,960 | 414.8 |

The independent 30-second post-warm-up rAF sample recorded 5,062 frames: mean 5.929 ms, p95 8.0 ms,
p99 9.6 ms, maximum 14.3 ms, **1% low 94.6 FPS**, zero frames above 16.7 ms and zero above 33.3 ms.
The 1% low is `1000 / mean(frame time of the slowest ceil(n × 1%) frames)`.

Warm-up observations: sky bakes 275/276 ms, terrain preview 106 ms, full terrain bake 424 ms,
vegetation 752 ms and EZTrees 1,623 ms. No post-warm-up hitch above 16.7 ms appeared in the fixed
dense window. These measurements qualify the RX 7800 XT reference machine only; they are not a Gate 1
midrange-GPU qualification.

## Human visual inspection

- The Vale: three 1920 × 1080 views rendered correctly; terrain, grass, landmark, HUD and weapon were
  visible, with no blank frame.
- Whisperwood: canopy, trunks, undergrowth and shafts of light were visible in all three views.
- Water: golden-hour and night reflection/refraction views rendered and a five-frame motion burst was
  non-blank.
- Preserved upstream defect: `shot-noon-island.png` places the camera inside or behind a large
  untextured grey-purple geometry that obscures much of the frame. It is recorded, not corrected.
- Preserved harness defect: all three `yard-perf.json` captures reached a valid rendered region but
  showed the player dead with the `Defeated` overlay because that script does not enable its available
  god/passive automation controls. Those frames are not accepted as dense-scene visual proof.
- A bounded rerun with god/passive enabled produced a live village frame. The two-second forward
  movement ended against the well masonry with the player alive; the before/after frames were both
  non-blank and were inspected as a short movement/collision characterization.
- Console warnings included deprecated `THREE.Clock`, repeated `toNonIndexed()` warnings and shader
  precision/gradient warnings. Harness/game error arrays were empty for the recorded runs.

## Evidence hashes and limits

| Local ignored evidence | SHA-256 |
|---|---|
| dense benchmark report | `74ae2fa940b961792c7b32c1b89d2309648cefbb4d9b66948e8664fffb8af946` |
| dense alive capture | `ce22be7798ccd71c455e21ae46186acf5b8ed41e8abd6f72d6eb9cd92d141503` |
| movement end capture | `0f8ad8c239282b59718f5df16af73570c6b15b7f46ec0cc3f63e038574165bc2` |
| water report | `5fcbcec5603457923e5c04bdff546cdfa2dacd5db4f19c63b4194277e3c17f4e` |
| Vale report | `772d359c57b0409bce530bc2dfae221e124a9926700e513b81be8e51e839ee99` |
| forest report | `94cbabd2a4845dc0180eb661983031ad0f5cea045bf8675f0c75711ce748da9c` |

The upstream lock itself still fails `npm ci`; ADR-0003 preserves that fact and authorizes the pinned
metadata overlay only for local qualification. `npm audit` reports one high-severity Sharp/libvips
advisory. No Cadle dependency, build or capture is distributed. A native Linux run and a genuinely
independent second physical machine remain unproven; two isolated Windows clones reproduced the lock,
install and build on the documented machine. Captures contain excluded Cadle media and must never
leave ignored local evidence storage.
