# forge_runtime

Native Bevy executable that loads a versioned Forge scene document. With no arguments it generates
the deterministic procedural Gate 0 proof. Automation options are `--capture <png>`,
`--capture-frame <n>`, `--metrics <json>` and `--exit-after-frames <n>`.

The primary window is resizable, requests VSync by default and keeps Bevy's normal close-button
lifecycle. `--no-vsync` requests `AutoNoVsync`. The proof harness can exercise the real window path
without UI automation:

```text
--resize-at-frame 60 --resize-width 1280 --resize-height 720
--close-at-frame 120
```

Resize evidence distinguishes the request from the later `WindowResized` event emitted by the OS
backend, and a bounded proof fails unless that backend event confirms the requested dimensions. The
close harness sends a synthetic but normal `WindowCloseRequested`; it proves Bevy's lifecycle path,
not a physical title-bar action. It is deliberately standalone so it cannot pre-empt capture,
metrics, bounded-exit or resize evidence. A real Alt+F4/title-bar close remains part of qualification.

Native logs are JSON Lines on stderr through `tracing`. Forge events carry
`schema=forge.runtime-event`, a numeric `schema_version` and a stable `event_kind`; fields such as
dimensions, frame numbers, booleans and exit codes remain typed JSON values. `RUST_LOG` controls the
filter. `--log-probe` emits a bounded machine-parseable event and exits successfully without opening
a GPU window.
