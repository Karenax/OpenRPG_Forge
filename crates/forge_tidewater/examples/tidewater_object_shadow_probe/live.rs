//! Filesystem bootstrap only; updates reuse the shared engine cascade/stream APIs.
use bevy::prelude::*;
use forge_tidewater::{
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
    selection: Option<Selection>,
}

#[derive(serde::Serialize)]
pub struct Selection {
    scope: &'static str,
    frame_index: u32,
    camera: [f32; 4],
    groups: Vec<forge_tidewater::tidewater_plants::scene::shadow::SelectionObservation>,
}

impl Sources {
    pub fn set_sun(&mut self, sun: bevy::math::DVec3) -> Result<(), String> {
        self.tracker.set_sun(sun)
    }
    pub fn selection_observation(&self) -> Option<&Selection> {
        self.selection.as_ref()
    }
    pub fn frame(
        &mut self,
        view: CascadeView,
        index: u32,
        flush: bool,
    ) -> Result<Frame, Box<dyn Error>> {
        let terrain = self.terrain.select(view)?;
        let camera = view.world_from_view.w_axis.as_vec4();
        let (plants, groups) = self.plants.select_observed(camera)?;
        if flush {
            self.tracker.invalidate();
        }
        let mut frame = self.tracker.next(view, index)?;
        frame.terrain = Some(terrain);
        frame.plants = plants;
        self.selection = Some(Selection {
            scope: "CPU caster streams supplied to live shadow frame; not GPU depth coverage",
            frame_index: index,
            camera: camera.to_array(),
            groups,
        });
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
        selection: None,
    });
    Ok(super::install_input(app, input))
}
