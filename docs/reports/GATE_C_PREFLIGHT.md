# Gate C preflight — Rust branch

Date: 2026-08-30

Forge branch: `Rust`

Forge starting commit: `3f0e9e2d7575c53f9f57ee20d51cdfb580cbf526`

Historical reference verified live: `origin/main@a7f4a3f5dd0eeff1dc060ea1237b1d7d650b7ca0`

## Scope

Only Gate C is authorized: acquire, audit, reproduce and measure the locked Cadle snapshot, then write
the reuse matrix and gate report. Gate 0/1 implementation, Cargo workspace creation, Bevy code and
asset copying are out of scope.

## Clean checkout

The original checkout contained preserved untracked artifacts from `main`. They were not removed or
modified. Gate C work therefore uses the dedicated clean Git worktree:

```text
E:\OpenRPG_Forge\OpenRPG_Forge-Rust
```

Initial `git status --short --branch`:

```text
## Rust...origin/Rust
```

## Toolchains observed

```text
OS       Microsoft Windows 11 Professionnel 10.0.26200 x64
CPU      AMD Ryzen 5 7600, 6 cores / 12 logical processors
GPU      AMD Radeon RX 7800 XT, driver 32.0.31041.1004
iGPU     AMD Radeon Graphics, driver 32.0.21045.5002
RAM      33,463,984,128 bytes reported by Windows
Git      2.55.0.windows.2
PowerShell 7.6.4
Node     22.22.3
npm      10.9.8
Chrome   C:\Program Files\Google\Chrome\Application\chrome.exe
Edge     C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe
```

Windows WMI reported an unreliable `AdapterRAM` value for the discrete GPU, so this preflight does not
claim a VRAM quantity. Browser/runtime diagnostics must supply any later memory measurement.

## Inputs re-read

- `AGENTS.md`
- `PROJECT_CONTEXT.md`
- `docs/ADR/ADR-0001-rust-webgpu-greenfield.md`
- `docs/ADR/ADR-0002-cadle-upstream-reference.md`
- `docs/planning/MIGRATION_ROADMAP.md`
- `docs/migration/LEGACY_REUSE_MATRIX.md`

The active gate is Gate C. Cadle is a local, ignored, pinned and untrusted upstream reference; it is
not a product dependency. Its source may be used only within the licence and attribution boundary,
and its excluded assets/captures must never enter Forge Git history or distributions.
