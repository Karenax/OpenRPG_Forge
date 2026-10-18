//! Native Tidewater shore simulation, shared with scene rendering.
#[path = "tidewater_shore_sim_probe/mod.rs"]
mod shore;
#[path = "tidewater_plant_probe/terrain_edits.rs"]
#[allow(dead_code)]
mod terrain_edits;
fn main() -> Result<(), Box<dyn std::error::Error>> {
    shore::run()
}
