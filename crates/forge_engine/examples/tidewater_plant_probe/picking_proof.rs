//! Plant-only GPU ID proof. Terrain/building occlusion remains unqualified.
use super::Probe;
use bevy::{
    prelude::*,
    render::gpu_readback::{Readback, ReadbackComplete},
};
use forge_engine::tidewater_plants::{
    picking::{
        decode,
        render::{Pass, View},
    },
    scene::resident::Resident,
};
use std::{collections::BTreeMap, io::Write};

#[derive(Resource)]
pub(super) struct Pending {
    pass: Pass,
    pub done: bool,
}

pub(super) fn start(world: &mut World, resident: &Resident) -> Result<(), String> {
    let probe = world.resource::<Probe>();
    let view = View {
        width: 1920,
        height: 1080,
        transform: super::camera_transform(probe, Vec3::ZERO),
        projection: super::camera_projection(probe),
    };
    let snapshot = resident.picking_snapshot(world)?;
    let pass = Pass::spawn(world, snapshot, view)?;
    world.insert_resource(Pending { pass, done: false });
    Ok(())
}

pub(super) fn request(commands: &mut Commands) {
    commands.queue(|world: &mut World| {
        let Some(state) = world.get_resource::<Pending>() else {
            return;
        };
        let target = state.pass.target.clone();
        world.spawn(Readback::texture(target)).observe(receive);
    });
}

fn receive(
    event: On<ReadbackComplete>,
    mut state: ResMut<Pending>,
    mut probe: ResMut<Probe>,
    mut commands: Commands,
) {
    let result = verify(&event.data, &state, &probe);
    match result {
        Ok(()) => state.done = true,
        Err(error) => {
            error!(%error, "plant ID GPU proof failed");
            probe.failed = true;
        }
    }
    commands.entity(event.entity).despawn();
}

fn verify(data: &[u8], state: &Pending, probe: &Probe) -> Result<(), Box<dyn std::error::Error>> {
    if data.len() != 1920 * 1080 * 4 {
        return Err("Invalid ID image size".into());
    }
    let mut counts = BTreeMap::<u32, usize>::new();
    for pixel in data.as_chunks::<4>().0 {
        if let Some(id) = decode(*pixel) {
            state
                .pass
                .snapshot
                .resolve(id, state.pass.snapshot.head)?
                .ok_or("Missing canonical ID")?;
            *counts.entry(id).or_default() += 1;
        } else if pixel[..3] != [0, 0, 0] {
            return Err("Invalid picking alpha".into());
        }
    }
    if counts.len() < 2 {
        return Err("No distinct visible plants in GPU ID pass".into());
    }
    let mappings = counts
        .iter()
        .map(|(&id, &pixels)| {
            let canonical = state
                .pass
                .snapshot
                .resolve(id, state.pass.snapshot.head)
                .expect("checked")
                .expect("plant");
            serde_json::json!({"gpu_id":id,"canonical_id":canonical,"pixels":pixels})
        })
        .collect::<Vec<_>>();
    let receipt = serde_json::json!({
        "scope":"plant-only, no terrain/building occluders", "head":state.pass.snapshot.head,
        "width":1920,"height":1080,"format":"Rgba8Unorm","mappings":mappings,
    });
    std::fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(probe.screenshot.with_extension("plant-ids.bin"))?
        .write_all(data)?;
    std::fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(probe.screenshot.with_extension("plant-ids.json"))?
        .write_all(&serde_json::to_vec_pretty(&receipt)?)?;
    println!(
        "PLANT_ID_GPU_PASS unique={} canonical_resolution=pass occluders=NOT_IMPLEMENTED",
        counts.len()
    );
    Ok(())
}
