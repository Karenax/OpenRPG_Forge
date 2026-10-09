//! Execute the Studio collector's ownership/cache tests without a WebView.
mod fish_props_scene {
    pub fn update(_: &mut bevy::prelude::World) {}
}
mod fish_stand_scene {
    pub fn update(_: &mut bevy::prelude::World) {}
}
mod placements {
    pub fn report(_: String) {}
}
#[path = "../../../apps/forge_studio/src/authoring_assets/fish_shadow_scene.rs"]
mod fish_shadow_scene;

fn main() {}
