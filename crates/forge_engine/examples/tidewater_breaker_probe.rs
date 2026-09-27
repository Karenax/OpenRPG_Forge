//! Isolated crest-kernel qualification on Bevy's device, using paired FFT inputs.
#[path = "tidewater_breaker_probe/mod.rs"]
mod breakers;
#[path = "tidewater_ocean_spectrum_probe/mod.rs"]
mod ocean;
fn main() -> Result<(), Box<dyn std::error::Error>> {
    breakers::run()
}
