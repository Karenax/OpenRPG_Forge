# Native texture capture admission

The prepared common storage schema 2 retains the entire original capture JSON
and a protocol plus source GUID. It complements historical storage schema 1;
schema 1 keeps its original shape and rejects the new identity envelope.
The prepared implementation is isolated until its workspace installation and
integration tests have completed. Integrity admission does not qualify rendering.

Supported new protocols are `unity_isolated_readable_native_mips.v1` and
`unity_native_gpu_mip_readback.v1`. A row must retain exact format, dimensions,
mip count and sizes, sampler settings and identity. Readable extraction requires
equal before/restored importer settings and successful probe flags. GPU readback
requires success for every mip in the selected row. A batch may contain refused
rows; its diagnostic status and size do not determine admission of another row.
Unknown protocols and versions refuse. Full capture JSON, including other rows'
refusals, stays unchanged in the resource.

The parent texture contract binds the captured GUID to exactly one root `guid:`
line in the embedded original metadata. Recomputing outer hashes cannot attach
the native capture to metadata with a different, missing or duplicate GUID.
Original PNG, metadata and native mip storage remain independently hashed.
Sampler defaults serialized in original metadata remain verbatim; an effective
native mip bias must come from measurement, never a blanket conversion of `-1`.

## Normal profiles

Authored texture slots and decoder profile slots are separate indices. Eight
common decoder profile values occupy two vec4 lanes in the existing uniform.
For the three measured source normal maps, native/prototype mode 1 means RG/AG;
the common enum `RgOrAg` has shader value 2. These numbers must not be copied
directly. The staged program uses an explicit bridge. Unsupported modes refuse
rather than selecting a decoder automatically. The common uniform layout is
unchanged; validating the scalar mapping does not validate the actual engine's
uniform encoding or binding.

Proofs, purchased sources and private staged programs are under
`F:/OpenRPG_ImportWork`. Current receipts separate source observation, candidate
contract admission, native GPU mip loads, decoder scalar controls, filtered
sampling, full material rendering, Web and Studio usage. A successful numeric
control adds zero fully delivered assets.
