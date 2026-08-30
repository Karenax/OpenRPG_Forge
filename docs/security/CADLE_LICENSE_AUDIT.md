# Cadle licence and dependency audit

Date: 2026-08-30

Snapshot: package version `0.6.0` at commit
`ea0da62549980816a6a27e08b75808b1f56d70e7`, tree
`f0d52e434454fe302052d22eafde203bc361cc1f`.

This commit is **not** the Git tag `v0.6.0`; that tag points to another commit. All evidence below is
bound to the commit and tree, not to the moving branch or the version tag.

## Verdict

The code-level MIT grant is clear enough for later, attributed code reuse, but Gate C does not copy
any code. Media reuse is prohibited by Forge policy. The npm dependency evidence is incomplete
because the upstream lockfile is stale, so no distribution licence conclusion is possible yet.

## Cadle grant and media boundary

- `LICENSE`: MIT; `Copyright (c) 2026 Ian Cadle`.
- `NOTICE`: code in `src/`, `tools/`, `play/`, `index.html` and documentation is announced MIT.
- `NOTICE`: `public/assets/`, `docs/concepts/` and recorded audio takes are outside that grant.
- Inventory: `public/assets/` has 135 files / 31,590,144 bytes; `docs/concepts/` has 4 files /
  1,947,809 bytes; no recorded audio file is tracked.
- Additional media occurs under `docs/intro-ref/`, `progress/shots/` and `tools/`. Because the notice
  does not give those files a reliable independent provenance, Forge treats every Cadle image,
  capture, model, texture, audio and video file as local-only.
- Cadle marks and identity are not transferred.

Raw captures stay under ignored `upstream/cadle/tools/out/`. They are inspected locally and are never
committed or published.

## npm lock audit

`package.json` declares version `0.6.0`; the lock root still declares `0.1.0`. Five direct development
dependencies are missing from the lock: `@gltf-transform/core`, `@gltf-transform/extensions`,
`@gltf-transform/functions`, `meshoptimizer` and `sharp`. Therefore:

```text
npm ci --ignore-scripts --no-audit --no-fund
exit 1 — EUSAGE: package.json and package-lock.json are not in sync
```

The 46 package entries that do exist in the lock have licence metadata: MIT 28, Apache-2.0 3,
MPL-2.0 12, BSD-3-Clause 1, ISC 1 and Zlib 1. The 12 MPL-2.0 entries are `lightningcss` and platform
packages. This contradicts the upstream notice statement that none of the dependencies are copyleft.

`npm audit --package-lock-only --ignore-scripts --json` returned zero reported vulnerabilities for
those 46 entries only. It does not cover the five missing direct dependencies or their transitive
graph and must not be interpreted as a complete Cadle vulnerability audit.

The deterministic machine-readable inventory is
`docs/evidence/cadle-dependency-audit.json`. Its manifest hashes are:

| File | SHA-256 |
|---|---|
| `package.json` | `95952a2f2d4114c619a6c182b610c46d12db4c366aa59292c0a7ffc28650bcfc` |
| `package-lock.json` | `1d01d9703a7679fecb0ce2f4144eef382c0adc832d0298da19a9efb515af82a3` |
| `LICENSE` | `8f653f3f69f17d2dabacaba945334989d0d459c05f8adb790d29057365cc1de9` |
| `NOTICE` | `cdca80e358e0d7b97fa0badf71bc22e8ffd2f4e7a3bfdd9da5243b92486a5f24` |

## Technique references

`TECHNIQUES.md` names useful projects but does not pin their commits or exact source files. Most
listed licences are MIT, CC0 or Zlib-like, but the audit found corrections and unresolved provenance:

- `jeantimex/precomputed_atmospheric_scattering` is MIT, not BSD-3-Clause;
- `thebenezer/FluffyGrass` and `nwpointer/three-landscape` have MIT licence files;
- the Faraz/Nubis repositories have no licence file and remain ideas-only;
- `three-good-godrays` uses a Zlib-like custom text that must be preserved if code is reused;
- `Sky.js` and `Terrain.js` describe derived techniques without pinning a source commit or file.

No Cadle source derived from those references may be copied until a file-level comparison determines
whether the relationship is conceptual or substantive.

## Required controls

1. Obtain an upstream commit with a synchronized lock before claiming reproducible installation.
2. Re-run licence, SBOM and vulnerability audits from that exact lock.
3. Preserve the Cadle MIT notice and add SPDX headers only when exact code is actually reused.
4. Record every reused source path, destination, attribution and test in the reuse matrix.
5. Never copy or publish Cadle media, including media outside the two paths named by `NOTICE`.
