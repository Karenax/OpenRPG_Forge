# Cadle baseline — ea0da625

Date: 2026-08-30

Qualification status: **exploratory, not a passing Gate C baseline**.

The snapshot itself stayed at commit `ea0da62549980816a6a27e08b75808b1f56d70e7`, tree
`f0d52e434454fe302052d22eafde203bc361cc1f`, with a clean tracked tree. However, its
`package-lock.json` cannot satisfy `npm ci`. The captures below were possible only after an explicit,
untracked fallback installation with the lock disabled. They characterize the snapshot but do not
prove reproducible installation.

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

## Commands and results

```text
node tools/gputest.mjs
  real GPU PASS: RX 7800 XT / D3D11

node tools/inspect.mjs --name gate-c-vale --w 1920 --h 1080 --q high --seed 1337 --script tools/scripts/fin/vale.json --url http://127.0.0.1:5173/
  3 shots, 0 harness/game errors

node tools/inspect.mjs --name gate-c-whisperwood --w 1920 --h 1080 --q high --seed 1337 --script tools/scripts/fin/forest.json --url http://127.0.0.1:5173/
  3 shots, 0 harness/game errors

node tools/inspect.mjs --name gate-c-water --w 1920 --h 1080 --q high --seed 1337 --script tools/scripts/critic3-water-1.json --url http://127.0.0.1:5173/
  14 shots plus 5-frame motion burst, 0 harness/game errors

node tools/inspect.mjs --name gate-c-dense-safe --w 1920 --h 1080 --q high --seed 1337 --url http://127.0.0.1:5173/ --script ../../docs/evidence/cadle-dense-safe.steps.json
  2 shots (alive dense region and movement end), 0 harness/game errors
```

The bounded JSON waits 31 seconds, enables Cadle's automation-only god/passive controls, teleports to
the `yard-perf.json` village coordinates, waits 40 seconds, captures the live scene, holds `KeyW` for
two seconds, captures the end position and records a four-second performance window. The exact JSON
is versioned as `docs/evidence/cadle-dense-safe.steps.json`.

## Measurements observed

These numbers are diagnostic only because the dependency installation was not produced by `npm ci`.

| Window | FPS | mean ms | p95 ms | p99 ms | CPU mean ms | GPU mean / p95 ms | calls | triangles | JS heap MiB |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| water view | 200.4 | 4.99 | 7.9 | 24.1 | 4.79 | 4.96 / 7.23 | 168 | 1,795,581 | 496.9 |
| water, reflection disabled | 215.1 | 4.65 | 9.4 | 29.1 | 4.46 | 4.72 / 6.75 | 168 | 1,795,561 | 496.9 |
| no water in view | 184.8 | 5.41 | 6.9 | 7.9 | 5.05 | 4.14 / 5.96 | 230 | 2,731,102 | 496.9 |
| dense village, player alive | 166.4 | 6.01 | 7.9 | 9.5 | 5.79 | 4.63 / 6.86 | 227 | 2,873,156 | 414.8 |

The upstream harness does not emit a 1% low for these runs. No substitute value is invented. A
qualifying Gate C run must add or document a deterministic computation from raw post-warm-up frame
samples.

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

## Reproduction blocker and limits

`npm ci --ignore-scripts --no-audit --no-fund` exits `1` with `EUSAGE` because five direct development
dependencies are absent from the lock and the root versions differ. The exploratory fallback used
exact direct versions with `--no-save --package-lock=false --ignore-scripts`; manifest hashes and the
Git tree stayed unchanged. This fallback is deliberately not a Gate C installation recipe.

The full multi-region benchmark, 1% low, hitch survey and an independent second-machine visual
reproduction remain non-qualifying until a synchronized upstream lock is available. Captures contain
excluded Cadle media and must not leave the ignored checkout.
