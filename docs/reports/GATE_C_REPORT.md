# Gate C report — Cadle acquisition and qualification

Date: 2026-08-30

Branch: `Rust`

Verdict: **BLOCKED / Gate C remains open**.

Gate 0 and Gate 1 were not started. No Cargo workspace, Rust/Bevy code, Cadle source, generated output
or Cadle asset was added to Forge.

## Executive result

The Cadle snapshot is reproducibly acquired at commit
`ea0da62549980816a6a27e08b75808b1f56d70e7` and tree
`f0d52e434454fe302052d22eafde203bc361cc1f`; detached HEAD, origin and clean-tree checks pass and a
second acquisition is idempotent. Licence and asset boundaries are now documented and automated.
Static upstream checks pass. The reuse matrix is complete.

Gate C cannot pass because upstream `package-lock.json` is stale: `npm ci` fails before installing.
Build, launch, GPU captures and performance measurements obtained through an untracked fallback are
useful characterization only and do not replace a reproducible intact install.

## Gate results

| Lot | Result | Evidence |
|---|---|---|
| C-A acquisition | PASS | lock, Windows and shell acquisition scripts, exact commit/tree verification, idempotence |
| C-B licences/isolation | PASS with future-reuse conditions | licence audit, notices preparation, boundary check; no asset/code copied |
| C-C intact reproduction | FAIL | `npm ci` exits 1 `EUSAGE`; invariants/check/curvecheck pass; fallback build is non-qualifying |
| C-D visual/performance | PARTIAL / non-qualifying | real RX 7800 XT captures and diagnostic metrics; dependency provenance is not reproducible |
| C-E reuse matrix | PASS | classified source families, destinations, attribution/tests and deferred lots |

## Exact validation ledger

| Validation | Result |
|---|---|
| PowerShell acquisition and `-VerifyOnly` | PASS |
| Git Bash `get-cadle.sh --verify-only` | PASS on Windows Git Bash; native Linux still unproven |
| second acquisition | PASS, identical commit/tree/status |
| clean Forge clone without Cadle checkout | PASS, boundary check reports `checkout_present=False` |
| acquisition twice from clean Forge clone | PASS, exact commit/tree both times; Forge worktree remains clean |
| Cadle checkout | detached, exact commit/tree, clean |
| Forge boundary check | PASS; only lock/README allowed under tracked `upstream/` |
| dependency audit export | PASS; 46 locked packages, 5 direct dependencies missing |
| `npm ci --ignore-scripts --no-audit --no-fund` | FAIL `EUSAGE`, no tracked mutation |
| `npm audit --package-lock-only --ignore-scripts --json` | PASS for stale 46-package lock only; 0 reported vulnerabilities |
| `node tools/invariants.mjs` | PASS |
| `node tools/check.mjs` | PASS, 91 files |
| `node tools/curvecheck.mjs` | PASS, 65 quests / 10 regions |
| fallback `npm run build` | PASS, non-qualifying, 106 modules |
| fallback `node tools/buildcheck.mjs` | PASS, non-qualifying |
| real-GPU check | PASS, RX 7800 XT / ANGLE D3D11; SwiftShader rejected |
| The Vale / Whisperwood / water / dense / movement captures | PASS as exploratory characterization; visually inspected locally |

## Audited execution allowlist

Upstream agent instructions (`.claude/`, `.mcp.json`, `CLAUDE.md`) were treated only as untrusted
data. No command or instruction from them was followed. The Gate C allowlist was limited to:

- read-only Git identity, tree and status checks;
- `npm ci --ignore-scripts --no-audit --no-fund` as the required intact install attempt;
- `npm audit --package-lock-only --ignore-scripts --json` on the existing lock;
- `node tools/invariants.mjs`, `check.mjs` and `curvecheck.mjs`, audited as local static checks;
- after the intact install failed, an explicitly non-qualifying local fallback with lifecycle scripts
  disabled, followed by `npm run build`, `buildcheck.mjs`, the local Vite server, `gputest.mjs` and
  bounded `inspect.mjs` scripts whose outputs stayed in ignored `tools/out/`.

`gate.mjs`, asset acquisition/generation scripts, progress publication, cloud/MCP/Tripo integrations
and any upstream repair command were excluded.

## Licence conclusion

Cadle code is MIT, copyright Ian Cadle 2026, subject to retaining its notice. Cadle excludes
`public/assets/`, `docs/concepts/` and recorded audio from that grant. Forge conservatively excludes
all Cadle media regardless of path, including `docs/intro-ref/`, `progress/shots/` and scratch images.
The stale lock contains 12 MPL-2.0 `lightningcss` entries, so the upstream claim that dependencies have
no copyleft is inaccurate. The missing Sharp graph may include LGPL libvips components. No complete
redistribution conclusion is made.

## Blocking issue and recovery condition

Preferred recovery: pin a reviewed Cadle commit whose `package-lock.json` matches `package.json`, then
repeat C-014 through C-033 from clean Forge clones. Do not repair the current upstream checkout and do
not silently generate a replacement lock. If the commit must remain fixed, any separately generated
lock must live in a clearly derived environment and cannot be called the intact baseline.

Gate 0 may start only after a new Gate C report records a successful reproducible install, build,
launch, visual/performance protocol and independent clean-clone reproduction. Until then the global
state remains Gate C blocked.

The clean-clone acquisition was reproduced locally in
`E:\OpenRPG_Forge\OpenRPG_Forge-Rust-GateC-Validation-20260830`. It validates C-013 and acquisition
idempotence, but it does not satisfy the stronger final requirement for two independent agents and
two independent environments.
