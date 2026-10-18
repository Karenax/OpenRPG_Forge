//! Tidewater ocean numerical probe; implementation shared with scene rendering.
#[path = "tidewater_ocean_spectrum_probe/mod.rs"]
mod ocean;
fn main() -> Result<(), Box<dyn std::error::Error>> {
    ocean::run()
}
