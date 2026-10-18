//! Headless atmosphere qualification using the shared native graph.
#[path = "tidewater_atmosphere_probe/mod.rs"]
mod atmosphere;
fn main() -> Result<(), Box<dyn std::error::Error>> {
    atmosphere::run()
}
