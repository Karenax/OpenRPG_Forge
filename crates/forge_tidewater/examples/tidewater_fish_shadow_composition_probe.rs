//! Execute the Studio collector's ownership/cache tests without a `WebView`.
#[allow(
    dead_code,
    reason = "Test-only collector host stub is unused in the example binary"
)]
mod fish_props_scene {
    pub fn update(_: &mut bevy::prelude::World) {}
}
#[allow(
    dead_code,
    reason = "Test-only collector host stub is unused in the example binary"
)]
mod fish_stand_scene {
    pub fn update(_: &mut bevy::prelude::World) {}
}
#[allow(
    dead_code,
    reason = "Test-only collector host stub is unused in the example binary"
)]
mod placements {
    pub fn report(_: String) {}
}
#[path = "../../../apps/forge_studio/src/authoring_assets/fish_shadow_scene.rs"]
#[allow(
    dead_code,
    reason = "The collector module is included to execute its ownership tests without a WebView"
)]
mod fish_shadow_scene;

fn main() {}
