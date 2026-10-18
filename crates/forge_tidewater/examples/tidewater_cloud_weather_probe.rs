//! Headless qualification of the shared native cloud graph.
#[path = "tidewater_atmosphere_probe/mod.rs"]
mod atmosphere;
#[path = "tidewater_cloud_weather_probe/mod.rs"]
mod clouds;
fn main() -> Result<(), Box<dyn std::error::Error>> {
    clouds::run()
}
