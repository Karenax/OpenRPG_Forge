//! Headless depth qualification using the shared native shadow graph.
#[path = "tidewater_object_shadow_probe/mod.rs"]
mod object_shadow;
fn main() -> Result<(), Box<dyn std::error::Error>> {
    object_shadow::run()
}
