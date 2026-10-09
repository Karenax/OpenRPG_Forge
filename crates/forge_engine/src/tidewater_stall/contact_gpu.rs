//! Publish the matching contact/surface frame after RenderWorld camera preparation.
use super::contact_frame::{self, Uniform};
use crate::{
    tidewater_material::SceneLighting,
    tidewater_ocean_depth::{OpaqueDepthHistory, SceneDepth},
    tidewater_temporal::frame::{FrameTag, ViewPacket},
};
use bevy::{
    prelude::*,
    render::{
        Extract, ExtractSchedule, Render, RenderApp, RenderSystems,
        extract_resource::ExtractResource,
        render_asset::RenderAssets,
        render_resource::{BufferUsages, ShaderType},
        renderer::RenderQueue,
        storage::{GpuShaderBuffer, ShaderBuffer},
    },
};
use std::sync::{Arc, Mutex};

pub const BUFFER: Handle<ShaderBuffer> =
    bevy::asset::uuid_handle!("ce2a70bd-3e5f-49e1-b38a-17c8a3b50a08");

#[derive(Clone, Debug, ShaderType)]
pub struct GpuFrame {
    pub contact: Uniform,
    pub surface_parameters: Vec4,
}

/// CPU scene inputs extracted with the material resources; camera comes from Render.
#[derive(Resource, Clone, ExtractResource)]
pub struct Inputs {
    pub lighting: SceneLighting,
    pub time: f32,
    pub night: f32,
    pub frame_index: u32,
    pub sea_level: f32,
    pub contact_strength: f32,
}
impl Inputs {
    pub fn validate(&self) -> Result<(), String> {
        if !self.time.is_finite()
            || !self.night.is_finite()
            || !(0.0..=1.0).contains(&self.night)
            || !self.sea_level.is_finite()
            || !self.contact_strength.is_finite()
            || !(0.0..=1.0).contains(&self.contact_strength)
        {
            return Err("Invalid render-owned contact/surface inputs".into());
        }
        SceneLighting::new(
            self.lighting.sun_direction.truncate().to_array(),
            self.lighting.sun_color.truncate().to_array(),
            self.lighting
                .environment_sh
                .map(|v| v.truncate().to_array()),
        )?;
        Ok(())
    }
    fn surface(&self) -> Vec4 {
        Vec4::new(self.time, self.night, 1.0, 1.0)
    }
}

#[derive(Clone, Debug, Default)]
pub struct Report {
    pub tag: Option<FrameTag>,
    pub source_owned: bool,
    pub history_matched: bool,
    pub seed: bool,
    pub error: Option<String>,
}
#[derive(Resource, Clone, Default)]
pub struct Observation(pub Arc<Mutex<Report>>);
#[derive(Resource)]
struct Installed;

fn inactive(surface_parameters: Vec4) -> GpuFrame {
    GpuFrame {
        contact: Uniform {
            view: Mat4::IDENTITY,
            view_proj: Mat4::IDENTITY,
            previous_view_proj_no_jitter: Mat4::IDENTITY,
            camera_position: Vec3::ZERO,
            near: 0.1,
            sun_direction: Vec3::Y,
            sea_level: 0.0,
            sun_color: Vec3::ZERO,
            strength: 0.0,
            resolution: Vec2::ONE,
            frame_index: 0,
            padding: 0,
        },
        surface_parameters,
    }
}

pub(super) fn binding_seed(inputs: &Inputs) -> Result<GpuFrame, String> {
    inputs.validate()?;
    let mut frame = inactive(inputs.surface());
    frame.contact.sun_direction = inputs.lighting.sun_direction.truncate();
    frame.contact.sun_color = inputs.lighting.sun_color.truncate();
    frame.contact.sea_level = inputs.sea_level;
    frame.contact.frame_index = inputs.frame_index;
    Ok(frame)
}

/// Allocate a bindable inactive frame once; never reset a live render-owned buffer.
pub fn install_buffer(world: &mut World) -> Result<(), String> {
    let mut buffers = world
        .get_resource_mut::<Assets<ShaderBuffer>>()
        .ok_or("Render contact requires shader buffer assets")?;
    if buffers.get(&BUFFER).is_none() {
        buffers
            .insert(
                BUFFER.id(),
                ShaderBuffer::from(inactive(Vec4::new(0.0, 0.0, 1.0, 1.0))),
            )
            .map_err(|error| error.to_string())?;
    }
    Ok(())
}

/// Register one main-view publisher in the existing Bevy device and render schedule.
pub fn register(app: &mut App, inputs: Inputs) -> Result<(), String> {
    inputs.validate()?;
    register_deferred(app)?;
    app.insert_resource(inputs);
    Ok(())
}

/// Install the publisher before asynchronously imported scene inputs are resident.
/// No contact frame is active until the adapter publishes validated `Inputs`.
pub fn register_deferred(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Installed>() {
        return Ok(());
    }
    if app.get_sub_app(RenderApp).is_none()
        || !app.world().contains_resource::<OpaqueDepthHistory>()
        || !app.world().contains_resource::<SceneDepth>()
    {
        return Err("Render contact requires the shared opaque depth path".into());
    }
    install_buffer(app.world_mut())?;
    let observation = Observation::default();
    app.insert_resource(Installed)
        .insert_resource(observation.clone());
    super::contact_camera::register(app);
    app.sub_app_mut(RenderApp)
        .insert_resource(observation)
        .add_systems(ExtractSchedule, extract_inputs)
        .add_systems(
            Render,
            upload
                .after(RenderSystems::PrepareAssets)
                .after(super::contact_camera::Prepare)
                .before(RenderSystems::Queue),
        );
    Ok(())
}

/// The generic ExtractResourcePlugin retains a removed main resource. Explicitly
/// remove it here so switching/removing an imported scene cannot reuse old inputs.
fn extract_inputs(mut commands: Commands, inputs: Extract<Option<Res<Inputs>>>) {
    if let Some(inputs) = &*inputs {
        commands.insert_resource((**inputs).clone());
    } else {
        commands.remove_resource::<Inputs>();
    }
}

#[allow(
    clippy::too_many_arguments,
    clippy::needless_pass_by_value,
    reason = "Bevy resources provide current camera, depth receipt and prepared buffer"
)]
fn upload(
    inputs: Option<Res<Inputs>>,
    history: Res<OpaqueDepthHistory>,
    target: Res<SceneDepth>,
    views: Query<
        (
            Option<&ViewPacket>,
            Option<&super::contact_camera::ContactView>,
        ),
        (
            Or<(With<ViewPacket>, With<super::contact_camera::ContactView>)>,
            Without<crate::tidewater_refraction::CaptureCamera>,
        ),
    >,
    independent: Res<super::contact_camera::History>,
    control: Option<Res<crate::tidewater_temporal::render_owned::Control>>,
    buffers: Res<RenderAssets<GpuShaderBuffer>>,
    queue: Res<RenderQueue>,
    observation: Res<Observation>,
) {
    let mut report = Report::default();
    let result = (|| -> Result<GpuFrame, String> {
        let inputs = inputs
            .as_ref()
            .ok_or("Render contact scene inputs not resident")?;
        inputs.validate()?;
        let selected = control.as_ref().and_then(|control| control.selected_view());
        let mut candidates = views
            .iter()
            .filter_map(|(source, independent)| {
                source
                    .map(|source| (contact_frame::CameraFrame::from(source), true))
                    .or_else(|| independent.map(|independent| (independent.0.clone(), false)))
            })
            .filter(|(view, _)| selected.is_none_or(|selected| selected == view.tag.view));
        let (view, source_owned) = candidates
            .next()
            .ok_or("Render contact has no prepared main camera")?;
        if candidates.next().is_some() {
            return Err("Render contact has ambiguous main cameras".into());
        }
        report.tag = Some(view.tag);
        report.source_owned = source_owned;
        let depth = if source_owned {
            &history.0
        } else {
            &independent.0
        }
        .lock()
        .map_err(|_| "Opaque depth receipt poisoned")?
        .clone();
        let contact = if view.needs_history_seed {
            report.seed = true;
            contact_frame::suspend_camera(
                &view,
                &inputs.lighting,
                inputs.sea_level,
                inputs.frame_index,
            )?
        } else {
            let depth = depth
                .as_ref()
                .ok_or("Previous opaque depth has not been encoded")?;
            if depth.image != target.0 {
                return Err("Render contact opaque image changed".into());
            }
            let contact = contact_frame::prepare_camera(
                &view,
                Some(depth),
                &inputs.lighting,
                inputs.sea_level,
                inputs.contact_strength,
                inputs.frame_index,
            )?;
            report.history_matched = true;
            contact
        };
        Ok(GpuFrame {
            contact,
            surface_parameters: inputs.surface(),
        })
    })();
    let frame = match result {
        Ok(frame) => frame,
        Err(error) => {
            report.error = Some(error);
            // Explicit invalidation prevents an old camera/depth pair persisting.
            // Preserve finite animation only; failed input validation is visible.
            inactive(
                inputs
                    .as_ref()
                    .filter(|inputs| inputs.validate().is_ok())
                    .map_or(Vec4::new(0.0, 0.0, 1.0, 1.0), |inputs| inputs.surface()),
            )
        }
    };
    if let Some(buffer) = buffers.get(&BUFFER) {
        if buffer.buffer.size() == 272
            && buffer
                .buffer
                .usage()
                .contains(BufferUsages::STORAGE | BufferUsages::COPY_DST)
        {
            if let Some(bytes) = ShaderBuffer::from(frame).data {
                queue.write_buffer(&buffer.buffer, 0, &bytes);
            } else {
                report.error = Some("Render contact serialization unavailable".into());
            }
        } else {
            report.error = Some("Render contact GPU buffer ABI differs".into());
        }
    } else {
        report.error = Some("Render contact GPU buffer is not prepared".into());
    }
    if let Ok(mut destination) = observation.0.lock() {
        *destination = report;
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn extraction_retires_removed_scene_inputs_and_accepts_the_next_scene() {
        let mut main = bevy::render::MainWorld::default();
        let mut next = Inputs {
            lighting: SceneLighting::new([0.0, 1.0, 0.0], [3.0; 3], [[0.2; 3]; 9]).unwrap(),
            time: 3.0,
            night: 0.7,
            frame_index: 11,
            sea_level: 0.0,
            contact_strength: 1.0,
        };
        main.insert_resource(next.clone());
        let mut render = World::new();
        render.insert_resource(main);
        let mut schedule = Schedule::new(ExtractSchedule);
        schedule.add_systems(extract_inputs);
        schedule.run(&mut render);
        assert_eq!(render.resource::<Inputs>().frame_index, 11);
        assert_eq!(render.resource::<Inputs>().time, 3.0);

        render
            .resource_mut::<bevy::render::MainWorld>()
            .remove_resource::<Inputs>();
        schedule.run(&mut render);
        assert!(!render.contains_resource::<Inputs>());

        next.time = 8.5;
        next.night = 0.25;
        next.frame_index = 19;
        render
            .resource_mut::<bevy::render::MainWorld>()
            .insert_resource(next);
        schedule.run(&mut render);
        let current = render.resource::<Inputs>();
        assert_eq!(current.frame_index, 19);
        assert_eq!(current.time, 8.5);
        assert_eq!(current.night, 0.25);
    }

    #[test]
    fn render_frame_extends_contact_abi_without_repacking_its_fields() {
        let frame = inactive(Vec4::new(3.0, 0.7, 1.0, 1.0));
        let uniform = ShaderBuffer::from(frame.contact.clone()).data.unwrap();
        let storage = ShaderBuffer::from(frame).data.unwrap();
        assert_eq!(uniform.len(), 256);
        assert_eq!(storage.len(), 272);
        assert_eq!(&storage[..256], &uniform);
        assert_eq!(&storage[256..260], &3.0_f32.to_le_bytes());
        assert_eq!(&storage[260..264], &0.7_f32.to_le_bytes());
        assert_eq!(&storage[236..240], &[0; 4]);
    }
}
