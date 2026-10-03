# Imported source fragment materials

`OPENRPG_source_fragment` is a private, required GLB extension for imported
materials whose proved source equations cannot be represented faithfully by
the bounded glTF metallic/roughness factors. The renderer-neutral contract is
in `forge_assets::source_fragment`; its Bevy adapter is
`forge_engine::source_fragment`. Source programs, purchased textures, import
receipts and model files remain in the private Vault/work directory.

The schema retains three affine rows for linear base RGB, packed RGBA
coefficients and offsets for metallic, occlusion and perceptual roughness,
the source normal-strength equation, a proved tangent-basis Y sign and an
explicit backface mode. Signed colour and roughness above one are retained.
Unknown fields, unsupported schema versions, invalid SHA pins and nonfinite
coefficients are rejected. The adapter admits only the documented opaque/mask
scope, neutral core factors, identity texture transforms and authored UV0 and
tangents. Rejected source materials are hidden with a diagnostic component;
they do not silently become a standard material.

The optional `normal_png` resource refers to a SHA-pinned bufferView in the GLB
BIN chunk. Its original RGB/RGBA PNG8/16 bytes are preserved. The Bevy adapter
decodes integer codes directly into `Rgba32Float`, retaining every distinct
16-bit code without an 8-bit intermediate. This avoids the native-only feature
needed for `Rgba16Unorm` on the installed wgpu 29.0.4 stack. The nonfilterable
texture uses four `textureLoad` calls for repeat/bilinear sampling. This first
route has one mip; imported Unity compression and mipmap parity are not claimed.

Bevy's glTF validator has a fixed list of known required extensions. The GLB
wrapper first validates the private contracts and autonomy, then recognizes
only this implemented extension in an in-memory validation copy. It adds an
internal required marker and preserves the BIN chunk. The original GLB stays
unchanged and continues to declare the required extension. Bevy then performs
full glTF validation: other unknown requirements, invalid topology, accessor
errors and resource errors remain failures. General glTF validation is never
disabled. Khronos Validator checks the standard GLB structure but reports this
private extension as unsupported; that report alone does not validate its data.

The lighting helper derives from Bevy 0.19.1 `pbr_functions.wgsl`. It keeps Bevy's
lighting path while replacing the roughness conversion at its single entry
point with `max(perceptual_roughness², 1/1024)`, as recorded by the imported
source equation. The copied MIT code retains attribution and the license notice.
This is not a second renderer and does not establish Unity lighting parity.

The finite-domain DFG helper is separate from that source roughness. Its
`F_AB` lookup/polynomial and the ambient helper receive a coordinate bounded to
`[0,1]`, as a clamped FGD lookup would. The authored perceptual roughness and
analytical-light roughness remain unchanged. Passing values above one into the
Bevy polynomial can collapse its terms to the epsilon floor and amplify
specular energy; a global roughness clamp is not an equivalent repair.

Proof examples accept external files; no purchased model is bundled:

```text
cargo run -p forge_assets --example source_fragment_contract_proof -- PROBES_JSON
cargo run -p forge_engine --example source_normal_precision_proof -- PNG16_REPORT [PILOT_GLB]
cargo run -p forge_engine --example source_fragment_render_probe -- GLB SHA256 CAPTURE_PNG REPORT_JSON
cargo run -p forge_engine --example source_fragment_capture_probe_v3 -- GLB SHA256 CAPTURE_PNG REPORT_JSON reverse
```

The offscreen render probe uses the existing Bevy renderer with depth, normal
and motion-vector prepasses enabled; it does not open a user window. It requires
loaded source materials, no standard fallback,
nonempty successfully compiled pipelines, twenty stable observations and an
actual screenshot GPU readback. It exits under a 120 second watchdog. Its
readback must contain at least 256 pixels that differ from the median corner
background. This rejects an empty capture but is not a proof of draw membership
in a particular material pipeline on its own. The probe also checks each source
entity's forward and prepass cache IDs for the capture camera, compiled GPU state
and the expected SourceFragment shader handles. The nonempty readback attributes
at least one draw to the source scene; it does not prove pixel coverage for every
primitive or validate the separate normal/motion prepass outputs. The capture
still requires pixel inspection.
The optional `reverse` view moves only the capture camera to the opposite
direction. It preserves model transforms, winding, normals and culling. This
allows a single-sided source surface to be inspected from its front without
changing the asset to satisfy a thumbnail gate. The report records the camera
direction and the number of stable observations at the capture request.
CPU equation/precision proofs, native
GPU execution, Studio WebGPU execution, library access, placement and runtime
qualification are separate verdicts. The prepass adapter shares the forward
normal equation and preserves standard alpha/depth/motion behavior; its GPU
qualification still requires the probe. Material lookup paths that explicitly
resolve `/std`, Unity importer/world transforms and
platform normal-map packing remain outside the first adapter's proved scope.
URP render states must come from the active target and effective override
fields; orphan saved HDRP properties are not a culling or alpha-test proof.

For shader diagnostics, the render probe accepts an optional
`OPENRPG_SOURCE_SHADER_PROOF_ROOT` directory containing the four source shader
files. It installs those files at the same embedded asset paths in the existing
renderer and records each actual file SHA in `diagnostic_shader_sources`.
An ordinary embedded-shader run reports an empty list. A diagnostic capture
does not qualify the production embedded shaders; a final embedded run must
verify the repaired source. Finite planar geometry is accepted when its bounds
have at least one positive extent; unordered or fully degenerate bounds fail.
