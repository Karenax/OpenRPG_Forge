#[path = "tidewater_ocean_spectrum_probe/mod.rs"]
mod ocean;
#[path = "tidewater_shore_sim_probe/mod.rs"]
mod shore_sim;
#[path = "tidewater_spray_probe/mod.rs"]
mod spray;
#[path = "tidewater_plant_probe/terrain_edits.rs"]
#[allow(dead_code)]
mod terrain_edits;
fn main() -> Result<(), Box<dyn std::error::Error>> {
    spray::run()
}
