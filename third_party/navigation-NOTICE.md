# Navigation provider notices and local maintenance

ADR-0009 owner acceptance on 2026-10-02 authorizes Rerecast 0.4.0 and
Landmass 0.9.2. The direct product graph does not include the Bevy wrappers.

`rerecast-0.4.0/` is the exact tested NAV019 laboratory fork, not a new upstream
release. Sources and normalized Cargo manifest are copied without modification
from the LAB archive qualified in `navigation-kcc-019/decision-packet.json`.
Its fixes cover WASM jitter overflow, detail mesh global indices/bounds, contour
chord intersections, hole triangulation and diagonal endpoint validation.
The Forge owner accepts local fork maintenance; no upstream submission is claimed.
Rerecast is authored by Jan Hohenheim, MIT OR Apache-2.0. Included license texts
are the upstream notices observed at commit
`94c069ebe43701eb9687ab236cdfcfbb5282cee5`; the release manifest declares the same
license. They are preserved separately from the project's own license.

`forge_navigation::provider::BuiltTile::raw` is substantially derived from the
raw conversion in landmass_rerecast 0.3.0, by Andriy Dev, MIT OR Apache-2.0.
Its ordinary coordinate conversion reversals are unconditional and preserve
the qualified raw conversion. There is no invalid-output normalization path.
Landmass 0.9.2 is an unmodified registry dependency (MIT OR Apache-2.0).

The owner acceptance selects the provider and permits integration. It does not
claim product route/browser qualification or close Gate 3.
