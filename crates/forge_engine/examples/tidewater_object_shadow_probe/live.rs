//! Filesystem bootstrap only; updates reuse the shared engine cascade/stream APIs.
use bevy::prelude::*;
use forge_engine::{
    sun_cascades::CascadeView,
    tidewater_object_shadow::{
        live::{Frame, Tracker},
        terrain::authored::PreparedSource,
    },
};
use std::{collections::BTreeMap, error::Error, path::Path};

#[derive(Resource)]
pub struct Sources {
    tracker: Tracker,
    terrain: PreparedSource,
    plants: super::plants::Source,
}
impl Sources {
    pub fn frame(
        &mut self,
        view: CascadeView,
        index: u32,
        flush: bool,
    ) -> Result<Frame, Box<dyn Error>> {
        let terrain = self.terrain.select(view)?;
        let plants = self.plants.select(view.world_from_view.w_axis.as_vec4())?;
        if flush {
            self.tracker.invalidate();
        }
        let mut frame = self.tracker.next(view, index)?;
        frame.terrain = Some(terrain);
        frame.plants = plants;
        Ok(frame)
    }
}

#[allow(dead_code)] // Shared development module also built by static probes.
pub fn install(
    app: &mut App,
    root: &Path,
    files: &BTreeMap<String, Vec<u8>>,
) -> Result<super::Target, Box<dyn Error>> {
    let mut input = super::load(root, true, true, Some(files))?;
    let fixture = serde_json::from_slice(&std::fs::read(root.join("fixture.json"))?)?;
    let shadow: serde_json::Value =
        serde_json::from_slice(&super::checked_file(root, &fixture, "shadow.json")?)?;
    let sun =
        bevy::math::DVec3::from_array(serde_json::from_value(shadow["sun_direction"].clone())?);
    let (mut plants, source) =
        super::plants::load_live(root, &fixture, input.settings.camera, Some(files))?;
    for (batch, stream) in plants
        .batches
        .iter_mut()
        .zip(source.select(input.settings.camera)?)
    {
        batch.records = stream.records;
        batch.models = stream.models;
        batch.count = stream.count;
    }
    input.plants = Some(plants);
    app.insert_resource(Sources {
        tracker: Tracker::new(sun)?,
        terrain: super::terrain::source(root, &fixture, Some(files))?,
        plants: source,
    });
    Ok(super::install_input(app, input))
}
