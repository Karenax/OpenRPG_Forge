//! Isolated real plant depth: fixed light view, varying main-camera distance.
#[path = "tidewater_object_shadow_probe/mod.rs"]
mod object_shadow;

fn main() -> Result<(), Box<dyn std::error::Error>> {
    object_shadow::isolated_lod::run()
}
