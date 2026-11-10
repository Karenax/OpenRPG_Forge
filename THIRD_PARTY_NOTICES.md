# Third-party notices

## Active source-derived renderer — 27 October 2026

The renderer includes code and WGSL substantially derived from Tidewater,
commit `1438b1abfcaee3267092b75573014f4d9b4a983c`, by DRG Software Solutions LLC.
Source headers retain their individual provenance when implementations move
from `forge_tidewater` into `forge_engine`. The complete upstream permission
text is retained in [third_party/tidewater/LICENSE](third_party/tidewater/LICENSE)
and reproduced below for distributions carrying these derived implementations.
Module media retain their own embedded provenance and licence inventory.

```text
MIT License

Copyright (c) 2026 DRG Software Solutions LLC

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Maintained dependency sources

The current manifests select these local dependency sources. Keep their
original licence texts with the generated dependency notices and preserve the
maintenance notes identifying the actual deltas and historical qualifications.

| Source | Licence texts | Maintenance/provenance |
| --- | --- | --- |
| Rerecast 0.4.0 laboratory fork | [MIT](third_party/rerecast-0.4.0/license-mit.txt), [Apache-2.0](third_party/rerecast-0.4.0/license-apache.txt) | [Navigation notice](third_party/navigation-NOTICE.md) |
| Rapier 0.35.0-glamx0.2 | [Apache-2.0](third_party/rapier3d-0.35.0-glamx0.2/LICENSE) | [Contact correction](third_party/rapier-normalization-NOTICE.md) |
| Bevy image 0.19.1 with ruzstd 0.9.1 | [MIT](third_party/bevy_image-0.19.1-ruzstd0.9.1/LICENSE-MIT), [Apache-2.0](third_party/bevy_image-0.19.1-ruzstd0.9.1/LICENSE-APACHE) | [Dependency correction](third_party/bevy-image-ruzstd-NOTICE.md) |
| GLib 0.18.5 with upstream VariantStrIter backport | [MIT](third_party/glib-0.18.5/LICENSE) | [Backport notice](third_party/glib-variant-iter-NOTICE.md) |
| Tachys 0.2.18 with upstream keyed-list change | [MIT](third_party/tachys-0.2.18/LICENSE) | [Backport notice](third_party/tachys-keyed-list-NOTICE.md) |
| Leptos macro 0.8.17 with direct case checks | [MIT](third_party/leptos_macro-0.8.17/LICENSE) | [Source notice](third_party/leptos-macro-case-NOTICE.md) |

This source notice is not a complete inventory of a distribution. Gate 7 still
requires notices collected from the actual Windows/Linux application graphs,
frontend and module payloads, with missing texts and rights resolved.

## Embedded notices in `const_soft_float` 0.1.4

The pinned Cargo package `const_soft_float` 0.1.4, revision `723b63e489c55359012b0c5c793b897c494c0bfa`,
contains math routines carrying the following file-level notices. Its own
MIT/Apache licence texts remain separate. Preserve both notices when packaging
the corresponding source or substantial portions, and review the actual linked
artifact and all other dependency notices before distribution.

### SunPro

```text
Copyright (C) 1993 by Sun Microsystems, Inc. All rights reserved.

Developed at SunPro, a Sun Microsystems, Inc. business.
Permission to use, copy, modify, and distribute this
software is freely granted, provided that this notice
is preserved.
```

Files: `src/soft_f32/cos.rs`, `src/soft_f32/helpers/k_cosf.rs`, `src/soft_f32/helpers/rem_pio2f.rs`, `src/soft_f32/sin.rs`, `src/soft_f64/cos.rs`, `src/soft_f64/helpers/rem_pio2.rs`, `src/soft_f64/sin.rs`.

The four `soft_f32` files identify their FreeBSD origins and credit Ian Lance
Taylor of Cygnus Support for conversion to float and Bruce D. Evans for
optimization or debugging and optimization. The `soft_f64` source headers
also identify their FreeBSD and, where given, Rust `libm` origins.

### SunSoft

```text
Copyright (C) 1993 by Sun Microsystems, Inc. All rights reserved.

Developed at SunSoft, a Sun Microsystems, Inc. business.
Permission to use, copy, modify, and distribute this
software is freely granted, provided that this notice
is preserved.
```

Files: `src/soft_f64/helpers/k_cos.rs`, `src/soft_f64/helpers/k_sin.rs`, `src/soft_f64/helpers/rem_pio2_large.rs`, `src/soft_f64/sqrt.rs`.

## Historical Gate C boundary

No Cadle source code, generated output or asset is incorporated into Forge at Gate C. The ignored
Cadle checkout is a development-only reference and is not part of a Forge source or binary
distribution. Consequently, this file records the notice that must accompany any later authorized
code reuse; it does not grant permission to distribute Cadle assets.

## Cadle code candidate notice

Cadle code is offered under the MIT License.

Copyright (c) 2026 Ian Cadle

Any later copy or substantial derivation must preserve the Cadle copyright and MIT permission text.
Before such a change is accepted, the exact source path, destination, derivation type and SPDX header
must be recorded in `docs/migration/CADLE_REUSE_MATRIX.md` and this notice must include the complete
MIT text shipped by the pinned snapshot.

Prepared source-header template (not applied during Gate C):

```text
SPDX-License-Identifier: MIT
Copyright (c) 2026 Ian Cadle
Derived from Cadle <exact pinned source path and commit>; see THIRD_PARTY_NOTICES.md.
```

Cadle's `public/assets/`, `docs/concepts/`, recorded audio, images, captures, models, textures and
other media are excluded from Forge reuse and must not be packaged or published.

## Dependency and technique boundary

The pinned npm lock currently includes MIT, Apache-2.0, MPL-2.0, BSD-3-Clause, ISC and Zlib packages.
It is incomplete relative to `package.json`, so this is not yet a redistribution-ready inventory.
In particular, future use of Sharp may introduce libvips components under LGPL-3.0-or-later. Cadle's
`TECHNIQUES.md` also cites moving upstream sources; any substantive reuse requires a commit-pinned,
file-level provenance review and the corresponding complete licence texts.

Evidence: `docs/security/CADLE_LICENSE_AUDIT.md` and
`docs/evidence/cadle-dependency-audit.json`.
