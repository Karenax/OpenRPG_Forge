//! Qualified source rendering metadata, without renderer dependencies.
use serde::Deserialize;
use std::collections::BTreeMap;
#[derive(Deserialize)]
pub struct Description {
    pub schema: String,
    pub version: u32,
    pub groups: Vec<GroupDescription>,
}
#[derive(Deserialize)]
pub struct GroupDescription {
    pub name: String,
    pub count: usize,
    pub query_radius: f64,
    pub cell_size: f64,
    pub far_exclude_near: bool,
    pub near_range: f64,
    pub refresh_distance: f64,
    pub sort_far: bool,
    pub streams: BTreeMap<String, String>,
    pub levels: Vec<Level>,
}
#[derive(Deserialize)]
pub struct Level {
    pub near: bool,
    pub capacity: usize,
    pub range: [f32; 3],
    pub kind: u8,
    pub attributes: BTreeMap<String, String>,
    pub indices: String,
}

impl super::PlantPackage {
    pub fn scene_descriptor(&self) -> Result<Description, String> {
        let d: Description = serde_json::from_slice(
            self.file("vegetation-scene.json")
                .ok_or("Missing plant scene descriptor")?,
        )
        .map_err(|e| e.to_string())?;
        if d.schema != "forge.tidewater.vegetation-scene-probe" || d.version != 1 {
            return Err("invalid vegetation scene".into());
        }
        super::profile::validate_reference_population(
            d.groups.len(),
            d.groups.iter().map(|g| g.count).sum(),
        )?;
        for g in &d.groups {
            if !g.query_radius.is_finite()
                || !(0.0..=500.0).contains(&g.query_radius)
                || g.cell_size.to_bits() != 32.0_f64.to_bits()
                || !g.near_range.is_finite()
                || !(0.0..=500.0).contains(&g.near_range)
                || !g.refresh_distance.is_finite()
                || !(0.0..=50.0).contains(&g.refresh_distance)
            {
                return Err("Source plant group bounds differ".into());
            }
            if !(1..=2).contains(&g.levels.len())
                || g.levels.iter().any(|l| {
                    l.kind > 2
                        || l.capacity == 0
                        || l.capacity > 65_383
                        || l.range.iter().any(|v| !v.is_finite())
                        || l.range[0] < 0.0
                        || l.range[0] >= l.range[1]
                        || l.range[1] >= l.range[2]
                })
            {
                return Err("Source plant LOD contract differs".into());
            }
        }
        Ok(d)
    }
}
