//! Update the shared building lamp buffer on Bevy's existing render queue.
//! A queued write is not GPU completion, mesh drawing or visual acceptance.
use crate::{
    source_local_lights::{PACKET_BYTES, Packet, collection::Collection},
    tidewater_refraction::CaptureCamera,
    tidewater_temporal::frame::{FrameTag, ViewPacket},
};
use bevy::{
    asset::{RenderAssetUsages, uuid_handle},
    prelude::*,
    render::{
        Render, RenderApp, RenderSystems,
        extract_resource::ExtractResourcePlugin,
        render_asset::RenderAssets,
        render_resource::BufferUsages,
        renderer::RenderQueue,
        storage::{GpuShaderBuffer, ShaderBuffer},
        sync_world::MainEntity,
        view::ExtractedView,
    },
};
use std::{
    collections::HashMap,
    sync::{Arc, Mutex},
};

pub const BUFFER: Handle<ShaderBuffer> = uuid_handle!("62aaf986-3ba8-4e7e-8bbe-e8f01f7ad0a3");

pub fn install_neutral(world: &mut World) -> Result<(), String> {
    let bytes = Packet::default().pack().map_err(|e| e.to_string())?;
    let mut buffers = world
        .get_resource_mut::<Assets<ShaderBuffer>>()
        .ok_or("Building local lights require shader-buffer assets")?;
    if let Some(existing) = buffers.get(&BUFFER) {
        if existing.data.as_deref() != Some(bytes.as_slice())
            || !existing
                .buffer_description
                .usage
                .contains(BufferUsages::STORAGE | BufferUsages::COPY_DST)
        {
            return Err("Building local-light buffer identity is already occupied".into());
        }
    } else {
        buffers
            .insert(
                BUFFER.id(),
                ShaderBuffer::new(&bytes, RenderAssetUsages::all()),
            )
            .map_err(|e| e.to_string())?;
    }
    Ok(())
}

#[derive(Clone, Debug, Default)]
pub struct Observation {
    pub queued_tag: Option<FrameTag>,
    pub queued_camera: Option<Entity>,
    pub queued_collection_revision: Option<u64>,
    pub queued_writes: u64,
    pub lights: i32,
    pub error: Option<String>,
}

#[derive(Resource, Clone, Default)]
pub struct Status(pub Arc<Mutex<Observation>>);

#[derive(Resource, Default)]
struct State {
    serials: HashMap<Entity, u64>,
    revisions: HashMap<Entity, u64>,
}

#[derive(Resource)]
struct Installed;

type CameraPackets<'w, 's> = Query<
    'w,
    's,
    (
        &'static MainEntity,
        &'static ExtractedView,
        Option<&'static ViewPacket>,
    ),
    (With<ExtractedView>, Without<CaptureCamera>),
>;

/// Register transport only. The adapter must collect the authoritative scene
/// after transforms and before extraction. Default collection is inactive.
pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Installed>() {
        return Ok(());
    }
    if app.get_sub_app(RenderApp).is_none() {
        return Err("Building local lights require the resident RenderApp".into());
    }
    install_neutral(app.world_mut())?;
    let status = Status::default();
    app.init_resource::<Collection>()
        .insert_resource(Installed)
        .insert_resource(status.clone())
        .add_plugins(ExtractResourcePlugin::<Collection>::default());
    app.sub_app_mut(RenderApp)
        .init_resource::<State>()
        .insert_resource(status)
        .add_systems(
            Render,
            upload
                .after(RenderSystems::PrepareAssets)
                .before(RenderSystems::Queue),
        );
    Ok(())
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy system resource wrappers"
)]
fn upload(
    collection: Res<Collection>,
    buffers: Res<RenderAssets<GpuShaderBuffer>>,
    queue: Res<RenderQueue>,
    mut state: ResMut<State>,
    status: Res<Status>,
    views: CameraPackets<'_, '_>,
    temporal_control: Option<Res<crate::tidewater_temporal::render_owned::Control>>,
) {
    let result = (|| -> Result<_, String> {
        let mut cameras = views.iter().filter_map(|(main, extracted, temporal)| {
            collection
                .for_camera(main.id(), extracted.world_from_view.to_matrix())
                .ok()
                .map(|(revision, packet)| (main.id(), revision, packet, temporal))
        });
        let (camera, revision, packet, temporal) = cameras
            .next()
            .ok_or("No matching prepared local-light camera")?;
        if cameras.next().is_some() {
            return Err("Ambiguous local-light camera frame".into());
        }
        validate_temporal_intent(
            camera,
            temporal.is_some(),
            temporal_control
                .as_ref()
                .and_then(|control| control.selected_view()),
        )?;
        if state
            .revisions
            .get(&camera)
            .is_some_and(|previous| revision <= *previous)
        {
            return Err("Stale local-light collection revision".into());
        }
        let tag = temporal_tag(&collection, &state.serials, camera, temporal)?;
        Ok((
            camera,
            revision,
            tag,
            packet.pack().map_err(|e| e.to_string())?.to_vec(),
            packet.count,
        ))
    })();
    // Telemetry poisoning must not skip clearing/updating a previously lit buffer.
    let mut observation = status
        .0
        .lock()
        .unwrap_or_else(std::sync::PoisonError::into_inner);
    observation.queued_tag = None;
    observation.queued_camera = None;
    observation.queued_collection_revision = None;
    observation.lights = 0;
    let Some(buffer) = buffers.get(&BUFFER) else {
        observation.error = Some("Building local-light GPU buffer is not prepared".into());
        return;
    };
    if buffer.buffer.size() != u64::try_from(PACKET_BYTES).unwrap()
        || !buffer
            .buffer
            .usage()
            .contains(BufferUsages::STORAGE | BufferUsages::COPY_DST)
    {
        observation.error = Some("Building local-light GPU buffer has the wrong size".into());
        return;
    }
    match result {
        Ok((camera, revision, tag, bytes, count)) => {
            queue.write_buffer(&buffer.buffer, 0, &bytes);
            state.revisions.insert(camera, revision);
            if let Some(tag) = tag {
                state.serials.insert(camera, tag.serial);
            }
            observation.queued_tag = tag;
            observation.queued_camera = Some(camera);
            observation.queued_collection_revision = Some(revision);
            observation.queued_writes = observation.queued_writes.saturating_add(1);
            observation.lights = count;
            observation.error = None;
        }
        Err(error) => {
            // Clear a previously lit buffer rather than leaving an old camera's
            // lamps active after collection or frame preparation fails.
            let neutral = Packet::default()
                .pack()
                .expect("Source neutral packet is valid");
            queue.write_buffer(&buffer.buffer, 0, &neutral);
            observation.error = Some(error);
        }
    }
}

fn validate_temporal_intent(
    camera: Entity,
    has_packet: bool,
    selected: Option<Entity>,
) -> Result<(), String> {
    if let Some(selected) = selected {
        if selected != camera {
            return Err("Local-light camera differs from selected source temporal camera".into());
        }
        if !has_packet {
            return Err("Selected source temporal camera has no prepared local-light frame".into());
        }
    }
    Ok(())
}

fn temporal_tag(
    collection: &Collection,
    serials: &HashMap<Entity, u64>,
    camera: Entity,
    temporal: Option<&ViewPacket>,
) -> Result<Option<FrameTag>, String> {
    let Some(view) = temporal else {
        return Ok(None);
    };
    // Invalid temporal packets never fall back to the non-temporal path.
    if view.tag.view != camera {
        return Err("Local-light temporal camera identity mismatch".into());
    }
    let bound = collection
        .for_view(view)
        .map_err(|error| error.to_string())?;
    let tag = bound.tag();
    if serials
        .get(&camera)
        .is_some_and(|serial| tag.serial <= *serial)
    {
        return Err("Stale local-light camera serial".into());
    }
    Ok(Some(tag))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn local_lights_gpu_source_selection_requires_its_camera_and_packet() {
        let mut world = World::new();
        let camera = world.spawn_empty().id();
        let other = world.spawn_empty().id();
        // No source resolve selected: Off/TAA may publish without a temporal tag.
        assert!(validate_temporal_intent(camera, false, None).is_ok());
        assert!(validate_temporal_intent(camera, true, None).is_ok());
        // Selecting source resolve adds requirements; it never relaxes them.
        assert!(validate_temporal_intent(camera, true, Some(camera)).is_ok());
        assert!(validate_temporal_intent(camera, false, Some(camera)).is_err());
        assert!(validate_temporal_intent(camera, true, Some(other)).is_err());
        assert!(validate_temporal_intent(camera, false, Some(other)).is_err());
    }

    #[test]
    fn local_lights_gpu_neutral_buffer_is_exact_and_refuses_identity_reuse() {
        let mut world = World::new();
        world.init_resource::<Assets<ShaderBuffer>>();
        install_neutral(&mut world).unwrap();
        install_neutral(&mut world).unwrap();
        let expected = Packet::default().pack().unwrap();
        assert_eq!(
            world
                .resource::<Assets<ShaderBuffer>>()
                .get(&BUFFER)
                .unwrap()
                .data
                .as_deref(),
            Some(expected.as_slice())
        );
        world
            .resource_mut::<Assets<ShaderBuffer>>()
            .get_mut(&BUFFER)
            .unwrap()
            .data
            .as_mut()
            .unwrap()[0] = 123;
        assert!(install_neutral(&mut world).is_err());
        assert_eq!(
            world
                .resource::<Assets<ShaderBuffer>>()
                .get(&BUFFER)
                .unwrap()
                .data
                .as_ref()
                .unwrap()[0],
            123
        );
        let mut app = App::new();
        assert!(register(&mut app).is_err());
        assert!(!app.world().contains_resource::<Installed>());
    }
}
