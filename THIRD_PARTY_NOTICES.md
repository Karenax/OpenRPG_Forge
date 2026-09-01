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

## Quaternius Stylized Nature MegaKit — governed Visual Valley subset

Forge includes a deliberately limited subset of nine models from the free Standard 1.0 edition of
Quaternius' **Stylized Nature MegaKit**. The models, binary geometry, referenced textures and original
pack notice are dedicated to the public domain under **CC0 1.0 Universal**. Attribution is not a
licence condition; this notice is retained for transparent provenance.

- Author: Quaternius
- Pack page: <https://quaternius.itch.io/stylized-nature-megakit>
- Audited archive route: <https://store.godotengine.org/asset/quaternius/stylized-nature-megakit/download/31/>
- Licence: <https://creativecommons.org/publicdomain/zero/1.0/>
- Exact subset, original paths, byte sizes and SHA-256 values:
  `assets/visual_valley/scenery/quaternius/stylized-nature-megakit-standard-1.0/subset.manifest.json`
- Original licence evidence:
  `assets/visual_valley/scenery/quaternius/stylized-nature-megakit-standard-1.0/License_Standard.txt`

No source archive, promotional media, engine project or unselected model from the pack is distributed
by Forge. No Cadle asset is part of this subset.
