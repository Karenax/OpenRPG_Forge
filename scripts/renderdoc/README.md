# RenderDoc action accounting — preparation, not GPU qualification

This pure Python analyzer consumes normalized `forge-gate1-renderdoc-actions/v2`
JSON. It neither loads RenderDoc nor launches, injects, captures, or replays a
process. A PASS means the supplied action tree meets the accounting contract;
it does not authenticate the supplied data or qualify R1-037, Vulkan, or Gate 1.

Reused Forge sources: the Rust proof checkout
`E:/OpenRPG_Forge/.gate1-renderdoc-v2-ed802bc0`, inspected HEAD
`62a158c57f8cba3c189dbbd4ce3f78b28f897e06`, original paths
`scripts/renderdoc/gate1_renderdoc_model.py`, its unit tests and three fixtures.
No Cadle code/assets or RenderDoc binaries were copied. The old capture host,
runtime adapter, tool lock and conflicting ADR-0009 were not imported.

`accounting-contract.json` is an explicitly named excerpt for this analyzer, not
the old acquisition lock or evidence of its current GPU qualification. The test
retains all original assertions, with only the excerpt path/schema adapted.
The old host-only positive-metric requirement is not part of this model: an
instanced draw with zero instances must remain zero, never be rounded up to one.

Counting uses draw leaves, with expanded MultiAction children counted once and
the container omitted. Ordinary draws with children are rejected as ambiguous
instead of silently discarding the parent's work. The new regression failed on
the recovered implementation before this correction. Markers remain recursive.
Triangle/line/point list counts are separate submitted primitive totals, not
visible pixels or unique mesh geometry. Instances are summed per draw, including
repeated passes; they are not unique world objects. No FPS is derived.

Run from the repository:

```text
python -B -m unittest discover -s scripts/renderdoc -p test_gate1_renderdoc_model.py -v
python -B scripts/renderdoc/gate1_renderdoc_model.py scripts/renderdoc/fixtures/nominal_nested_actions.json
```

Actual capture provenance, binary/source hashes, readiness/frozen-state receipt,
replay extraction and backend qualification remain to be connected and checked.
Do not populate the runtime's unavailable global draw-call counter from this CLI.

## Replay API boundary

`gate1_renderdoc_replay.analyze_capture(rd, capture_path)` now extracts normalized
actions from an existing capture using the supplied RenderDoc API. It opens the
capture, requires supported non-degraded Vulkan replay, selects each draw's frame
event before reading its topology/stages, then calls the pure accounting model.
It releases the capture/controller on success and on the tested error paths.
It does not launch or inject any process, register a Vulkan layer, acquire a new
capture, or certify the provenance/readiness of the file supplied by its caller.

The extraction functions were adapted from the historical Forge
`scripts/renderdoc/gate1_renderdoc_qrenderdoc.py` at the source location above,
with an explicit API parameter and direct model import instead of temporary
`sys.path` changes. The host's profile mutation and process lifecycle were not copied.
Indirect zero counts remain conservatively unresolved; a plausible number must
not be promoted into verified work when replay metadata is ambiguous.

Run all 25 pure model/API-stub tests with:
`python -B -m unittest discover -s scripts/renderdoc -p "test_gate1_renderdoc_*.py" -q`.
Seven replay-boundary tests use synthetic API objects, not a live RenderDoc replay.
The next required integration is capture acquisition with candidate readiness
and frozen-state evidence; no real `.rdc` was acquired or replayed by these tests.

## Embedded SDK smoke

`../Test-RenderDocPython.ps1 -Output <fresh-E-directory>` copies the pinned portable
tool to external evidence storage and runs `smoke_embedded_python.py` with a unique
profile, analytics opt-out and update checks disabled. TEMP/TMP are scoped to the
run and restored. The exact disposable profile is archived after process exit.
A timeout is a failure, not proof of termination. The finally cleanup now stops
the owned tool process and checks termination before archiving its profile.

The embedded Python 3.8.20 distribution lacks `_overlapped`, so importing unittest
fails. The 25 tests run separately with normal Python. The embedded smoke checks
the nominal synthetic fixture, model/replay imports and callable SDK entry points;
it performs no injection, capture or GPU replay. A PASS report is required because
QRenderDoc handles SystemExit without propagating its numeric code to its process
exit status. See `docs/evidence/gate1/R1_037_RECOVERED_TOOLING.md` from repo root.

`-CaptureProbe` explicitly opts into a development-only injected native capture
using the hash-pinned 2026-09-08 candidate. It waits for supported presenting Vulkan
and the PNG readback, then requests one frame. This is not an atomic state receipt
or gate qualification. Inputs, PID, trigger, capture message, SHA-256 and logs are
kept. The host cleans up the recorded runtime PID only after checking executable,
start time and the exact run-specific capture argument. It never kills by name.
If injection fails before target.json is available, inputs.json (written before
injection) supplies the exact executable and the run-specific capture argument
identifies its process. Multiple matches are refused, as are older start times.
This recovery never selects all processes by executable name.
Acquisition and replay are now separate: the first actual capture was received,
but its immediate concurrent replay crashed, so no GPU work totals were obtained.

The capture process uses `VK_IMPLICIT_LAYER_PATH` to select the copied tool's
manifest without registry changes. Other implicit overlays are absent in this
diagnostic run; it is not a representative performance run. Elevated execution
is refused because the loader ignores that override. The original registered
RenderDoc manifest pointed to a different copy than the injected DLL, which was
associated with missing presenting-API messages in the earlier attempts.
Three pure guard tests supplement the existing 25 tests; run all with
`python -B -m unittest discover -s scripts/renderdoc -p "test_*.py" -q`.

Use `-ReplayCapture <existing-E-file.rdc> -ReplaySha256 <sha256>` for isolated
replay without a runtime launch. Capture/replay together, missing hashes and
mismatches are refused before output creation. The host samples free physical and
virtual memory once per second and stops only its owned replay process below
2 GiB of either; no user process or Windows paging settings are changed.
The first isolated run hit this guard after 12.476 seconds. It produced no action
totals; do not rerun unchanged while memory headroom remains insufficient.
`host-result.json` now records host PASS/failure separately from `smoke.json`,
which native crashes or guarded termination can prevent from being written.

Cleanup change requested by the owner: tested on a disposable sleeping PowerShell
child (not a GPU workload): mismatched identity refused without stopping it,
correct identity terminated and verified, repeated cleanup safe. Ownership checks
accept the exact output path and reject another run's output. Full injected-runtime
cleanup remains unexercised; no new GPU test was launched for this change.

## Current bridge capture

Integrated manifests now retain their explicitly requested forward-water and cloud
inputs. Both are hash-checked; missing or contradictory request flags/pins fail.
The command remains allowlisted, never replayed from embedded manifest commands.
The full pure suite has 38 tests; real integrated native acquisition/replay on
10 September is documented in `R1_CLOSURE_PROGRESS_20260910.md`.

`Test-RenderDocPython.ps1 -CaptureProbe -BridgeCaptureManifest <existing-E-manifest.json>
-Output <fresh-E-directory>` rebuilds a bounded bridge command from the five pinned
inputs of a completed bridge-video manifest. It never executes `runtime_command`
from that document. The legacy pinned capture remains the default without this flag.
The trigger waits for frame-0162 PNG readback, so the captured GPU frame is **after**
that sample, not an exact paired transition or an atomic scene receipt. The runtime
forbids a separate still capture during this self-bounded probe; cleanup recognizes
the exact quoted bridge-frames argument and executable instead. Both capture and
replay now use the host memory guard. Replay stays in a separate process.

The 35 pure tests pass, including pinned-input mismatch, unsupported settings and
embedded-command rejection. Real current capture/replay evidence is documented in
`R1_FOLIAGE_CLUSTER_ATLAS.md`; a release capture can lack debug marker names, so draw
order alone does not establish pass identity or a specific shadow caster.

## Optional replay GPU durations

`Test-RenderDocPython.ps1 -ReplayCapture <existing-E-file.rdc>
-ReplaySha256 <sha256> -GpuDurations -Output <fresh-E-directory>` also requests
`EventGPUDuration` through the official RenderDoc counter API. Capture/injection
is not permitted by this option. The existing memory guard, exact process cleanup
and immutable capture checks remain in force. Ordinary accounting does not fetch
counters.

`analysis.json.gpu_durations` retains every returned leaf's event ID, name,
ancestor markers and duration in milliseconds. Float64 seconds are required;
unsupported counters, missing draw/dispatch results, duplicate/unknown events,
negative or non-finite values fail. Parent/container durations are retained
separately, never added again to child durations. Zero durations remain zero.

The sum is an **instrumented replay event sum**, not frame time, live FPS or an
acceptance threshold. An old capture remains an old candidate, even when replayed
today. Use its result to choose an investigation, not to promote the current kit.
No Bevy code is patched or rendering system replaced to acquire these counters.

API reference: [RenderDoc counter example](https://github.com/baldurk/renderdoc/blob/v1.x/docs/python_api/examples/renderdoc/fetch_counters.rst).
