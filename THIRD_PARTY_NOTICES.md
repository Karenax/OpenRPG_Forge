# Third-party notices

## Current Rust branch state

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
