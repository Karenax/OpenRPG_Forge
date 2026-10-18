//! Three offscreen ID views prove real scene and opaque authored depth masks.
use super::Probe;
use bevy::{
    diagnostic::FrameCount,
    prelude::*,
    render::gpu_readback::{Readback, ReadbackComplete},
};
use forge_tidewater::tidewater_plants::{
    picking::{
        decode,
        occluders::Prepared,
        render::{Pass, View},
    },
    scene::resident::Resident,
};
use std::{collections::BTreeMap, io::Write};

#[derive(Resource)]
pub(super) struct Pending {
    plain: Pass,
    masked: Pass,
    standard: Pass,
    readers: Option<(Entity, Entity, Entity)>,
    plain_data: Option<Vec<u8>>,
    masked_data: Option<Vec<u8>>,
    standard_data: Option<Vec<u8>>,
    occluders: (usize, usize),
    standard_occluders: usize,
    transparent_skipped: usize,
    pub done: bool,
}

/// Runs after Startup has spawned the terrain and village. The resident plant
/// selection remains owned by the fixture's normal scene projection.
pub(super) fn start_if_ready(world: &mut World) {
    if world.resource::<FrameCount>().0 < 2
        || world.contains_resource::<Pending>()
        || !matches!(
            world.resource::<Probe>().plant_projection,
            super::PlantProjection::ResidentNavigation
        )
        || !world.contains_resource::<super::vegetation_scene::ResidentProof>()
    {
        return;
    }
    let result = world.resource_scope(
        |world, resident: Mut<super::vegetation_scene::ResidentProof>| {
            start(world, &resident.resident)
        },
    );
    if let Err(error) = result {
        error!(%error, "plant ID occlusion setup failed");
        world.resource_mut::<Probe>().failed = true;
    }
}

fn start(world: &mut World, resident: &Resident) -> Result<(), String> {
    let probe = world.resource::<Probe>();
    let view = View {
        width: 1920,
        height: 1080,
        transform: super::camera_transform(probe, Vec3::ZERO),
        projection: super::camera_projection(probe),
    };
    let plain_snapshot = resident.picking_snapshot(world)?;
    let masked_snapshot = resident.picking_snapshot(world)?;
    let standard_snapshot = resident.picking_snapshot(world)?;
    let prepared = Prepared::from_world(world)?;
    let occluders = prepared.counts();
    let existing_standard = prepared.standard_count();
    if occluders.1 == 0 {
        return Err("Native island proof requires opaque village occluders".into());
    }
    let transparent_skipped = prepared.transparent_skipped;
    let diagnostic = diagnostic_standard(world, &view)?;
    let standard_prepared = Prepared::from_world(world);
    world.despawn(diagnostic);
    let standard_prepared = standard_prepared?;
    let standard_occluders = standard_prepared.standard_count();
    if standard_occluders != existing_standard + 1 {
        return Err("Diagnostic StandardMaterial occluder was not captured".into());
    }
    let plain = Pass::spawn_on_layer(world, plain_snapshot, view.clone(), 29)?;
    let mut masked = Pass::spawn(world, masked_snapshot, view.clone())?;
    if let Err(error) = masked.add_occluders(world, prepared) {
        plain.retire(world);
        masked.retire(world);
        return Err(error);
    }
    let mut standard = Pass::spawn_on_layer(world, standard_snapshot, view, 31)?;
    if let Err(error) = standard.add_occluders(world, standard_prepared) {
        plain.retire(world);
        masked.retire(world);
        standard.retire(world);
        return Err(error);
    }
    world.insert_resource(Pending {
        plain,
        masked,
        standard,
        readers: None,
        plain_data: None,
        masked_data: None,
        standard_data: None,
        occluders,
        standard_occluders,
        transparent_skipped,
        done: false,
    });
    Ok(())
}

/// Place a screen-space 40 x 100 pixel cuboid over visible left-edge plants.
/// The source entity is removed before render extraction; only its prepared
/// depth copy enters the third offscreen pass.
fn diagnostic_standard(world: &mut World, view: &View) -> Result<Entity, String> {
    let Projection::Perspective(projection) = &view.projection else {
        return Err("Diagnostic StandardMaterial proof requires perspective".into());
    };
    let depth = 3.0;
    let height = 2.0 * depth * (projection.fov * 0.5).tan();
    let width = height * (1920.0 / 1080.0);
    let x = (100.0 / 1920.0 - 0.5) * width;
    let y = (0.5 - 320.0 / 1080.0) * height;
    let transform = Transform::from_matrix(
        view.transform.to_matrix() * Mat4::from_translation(Vec3::new(x, y, -depth)),
    );
    let mesh = world.resource_mut::<Assets<Mesh>>().add(Cuboid::new(
        width * 40.0 / 1920.0,
        height * 100.0 / 1080.0,
        0.05,
    ));
    let material = world
        .resource_mut::<Assets<StandardMaterial>>()
        .add(StandardMaterial {
            base_color: Color::WHITE,
            ..default()
        });
    Ok(world
        .spawn((
            Mesh3d(mesh),
            MeshMaterial3d(material),
            transform,
            Visibility::Visible,
            InheritedVisibility::VISIBLE,
        ))
        .id())
}

pub(super) fn request(commands: &mut Commands) {
    commands.queue(|world: &mut World| {
        let Some(state) = world.get_resource::<Pending>() else {
            return;
        };
        if state.readers.is_some() {
            return;
        }
        let plain = state.plain.target.clone();
        let masked = state.masked.target.clone();
        let standard = state.standard.target.clone();
        let plain = world.spawn(Readback::texture(plain)).observe(receive).id();
        let masked = world.spawn(Readback::texture(masked)).observe(receive).id();
        let standard = world
            .spawn(Readback::texture(standard))
            .observe(receive)
            .id();
        world.resource_mut::<Pending>().readers = Some((plain, masked, standard));
    });
}

fn receive(
    event: On<ReadbackComplete>,
    mut state: ResMut<Pending>,
    mut probe: ResMut<Probe>,
    mut commands: Commands,
) {
    let Some((plain, masked, standard)) = state.readers else {
        return;
    };
    if event.entity == plain {
        state.plain_data = Some(event.data.clone());
    } else if event.entity == masked {
        state.masked_data = Some(event.data.clone());
    } else if event.entity == standard {
        state.standard_data = Some(event.data.clone());
    } else {
        return;
    }
    commands.entity(event.entity).despawn();
    if state.plain_data.is_none()
        || state.masked_data.is_none()
        || state.standard_data.is_none()
        || state.done
    {
        return;
    }
    match verify(&state, &probe) {
        Ok(()) => state.done = true,
        Err(error) => {
            error!(%error, "plant ID GPU occlusion proof failed");
            probe.failed = true;
        }
    }
}

#[allow(
    clippy::too_many_lines,
    reason = "one native receipt compares all three GPU ID buffers"
)]
fn verify(state: &Pending, probe: &Probe) -> Result<(), Box<dyn std::error::Error>> {
    let plain = state
        .plain_data
        .as_deref()
        .ok_or("Missing baseline readback")?;
    let masked = state
        .masked_data
        .as_deref()
        .ok_or("Missing masked readback")?;
    let standard = state
        .standard_data
        .as_deref()
        .ok_or("Missing standard readback")?;
    if plain.len() != 1920 * 1080 * 4
        || masked.len() != plain.len()
        || standard.len() != plain.len()
    {
        return Err("Invalid ID image size".into());
    }
    let mut counts = BTreeMap::<u32, usize>::new();
    let mut occluded_pixels = 0_usize;
    let mut standard_occluded_pixels = 0_usize;
    for ((before, after), with_standard) in plain
        .as_chunks::<4>()
        .0
        .iter()
        .zip(masked.as_chunks::<4>().0)
        .zip(standard.as_chunks::<4>().0)
    {
        let baseline_id = decode(*before);
        let masked_id = decode(*after);
        let standard_id = decode(*with_standard);
        if let Some(id) = standard_id {
            if masked_id != Some(id) {
                return Err("StandardMaterial pass changed a visible plant ID".into());
            }
            state
                .standard
                .snapshot
                .resolve(id, state.standard.snapshot.head)?
                .ok_or("Missing standard-pass canonical ID")?;
        } else if masked_id.is_some() {
            standard_occluded_pixels += 1;
        }
        if let Some(id) = baseline_id {
            state
                .plain
                .snapshot
                .resolve(id, state.plain.snapshot.head)?
                .ok_or("Missing baseline canonical ID")?;
        }
        if let Some(id) = masked_id {
            if baseline_id != Some(id) {
                return Err("Occluder pass changed a visible plant ID".into());
            }
            state
                .masked
                .snapshot
                .resolve(id, state.masked.snapshot.head)?
                .ok_or("Missing masked canonical ID")?;
            *counts.entry(id).or_default() += 1;
        } else if baseline_id.is_some() {
            occluded_pixels += 1;
        }
    }
    if counts.len() < 2 || occluded_pixels == 0 {
        return Err("No distinct plants or no measurable terrain/village occlusion".into());
    }
    if standard_occluded_pixels == 0 {
        return Err("Opaque StandardMaterial did not mask any visible plant pixels".into());
    }
    let mappings = counts
        .iter()
        .map(|(&id, &pixels)| {
            let canonical = state
                .masked
                .snapshot
                .resolve(id, state.masked.snapshot.head)
                .expect("checked")
                .expect("plant");
            serde_json::json!({"gpu_id":id,"canonical_id":canonical,"pixels":pixels})
        })
        .collect::<Vec<_>>();
    let receipt = serde_json::json!({
        "scope":"native same-camera plant IDs with terrain, village and diagnostic StandardMaterial depth",
        "head":state.masked.snapshot.head,
        "width":1920,"height":1080,"format":"Rgba8Unorm",
        "terrain_draws":state.occluders.0,"village_draws":state.occluders.1,
        "standard_draws":state.standard_occluders,
        "diagnostic_standard":true,
        "transparent_village_skipped":state.transparent_skipped,
        "occluded_pixels":occluded_pixels,"standard_occluded_pixels":standard_occluded_pixels,
        "mappings":mappings,
    });
    let receipt_bytes = serde_json::to_vec_pretty(&receipt)?;
    for (extension, data) in [
        ("plant-ids-plain.bin", plain),
        ("plant-ids.bin", masked),
        ("plant-ids-standard.bin", standard),
        ("plant-ids.json", receipt_bytes.as_slice()),
    ] {
        std::fs::OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(probe.screenshot.with_extension(extension))?
            .write_all(data)?;
    }
    println!(
        "PLANT_ID_GPU_PASS unique={} canonical_resolution=pass terrain={} village={} standard={} occluded_pixels={} standard_occluded_pixels={}",
        counts.len(),
        state.occluders.0,
        state.occluders.1,
        state.standard_occluders,
        occluded_pixels,
        standard_occluded_pixels
    );
    Ok(())
}
