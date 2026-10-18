// SPDX-License-Identifier: MIT
//! Source-shaped auxiliary inputs for Tidewater's temporal upscale.
//!
//! These derived GPU caches belong to Bevy's existing renderer. Registration
//! leaves them inactive until an adapter explicitly selects a main camera.
//! Auxiliary encoding is not a temporal resolve or a source-parity verdict.

use bevy::{
    camera::CameraUpdateSystems,
    pbr::Material,
    prelude::*,
    render::{
        Render, RenderApp, RenderSystems, extract_component::ExtractComponentPlugin,
        extract_resource::ExtractResourcePlugin,
    },
    shader::{ShaderDefVal, ShaderRef, Source},
};

pub mod base_readback;
pub mod consumer;
pub mod frame;
pub mod hdr;
pub mod observation;
pub mod phase;
pub mod render;
pub mod render_owned;
pub mod render_owner;
pub mod resolve;
pub mod resolve_readback;
pub mod shaders;
pub mod targets;
pub mod trace_readback;

#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum FrameSystems {
    Acknowledge,
    Prepare,
    Jitter,
}

#[derive(Resource)]
struct Installed;

#[derive(Component)]
struct OwnsJitter;

struct Derivation {
    family: shaders::Family,
    vertex: Handle<Shader>,
    fragment: Handle<Shader>,
    derived: phase::AuxShaders,
    previous_sources: Option<(String, String, Vec<ShaderDefVal>, Vec<ShaderDefVal>)>,
    error: Option<String>,
}

/// Installed WGSL derivations follow their real source assets, including terrain
/// corrections. A failed seam remains unready instead of replacing geometry.
#[derive(Resource, Default)]
pub struct Derivations(Vec<Derivation>);

impl Derivations {
    #[must_use]
    pub fn errors(&self) -> Vec<&str> {
        self.0
            .iter()
            .filter_map(|entry| entry.error.as_deref())
            .collect()
    }
}

/// Install the inactive auxiliary path. The first candidate requires rendering
/// without `PipelinedRenderingPlugin`; its CPU owner rejects stale render tags.
pub fn register(app: &mut App) -> Result<(), String> {
    if app.world().contains_resource::<Installed>() {
        return Ok(());
    }
    if app.get_sub_app(RenderApp).is_none() || !app.world().contains_resource::<Assets<Shader>>() {
        return Err("Temporal auxiliary inputs require initialized Bevy rendering".into());
    }
    let sky_source = shaders::sky();
    let sky = app
        .world_mut()
        .resource_mut::<Assets<Shader>>()
        .add(Shader::from_wgsl(
            sky_source,
            "forge://tidewater/temporal/sky.wgsl",
        ));
    app.insert_resource(Installed)
        .init_resource::<frame::Frames>()
        .init_resource::<frame::Acknowledgements>()
        .init_resource::<Derivations>()
        .add_plugins((
            ExtractComponentPlugin::<frame::ViewPacket>::default(),
            ExtractResourcePlugin::<frame::Acknowledgements>::default(),
        ))
        .configure_sets(
            PostUpdate,
            (
                FrameSystems::Acknowledge,
                FrameSystems::Prepare,
                FrameSystems::Jitter,
            )
                .chain()
                .after(CameraUpdateSystems)
                .after(forge_engine::finite_perspective::EffectivePerspectiveSystems)
                .after(bevy::transform::TransformSystems::Propagate)
                .before(crate::tidewater_refraction::Synchronize),
        )
        .add_systems(
            PostUpdate,
            (
                update_derived_shaders.before(FrameSystems::Prepare),
                frame::drain_aux_acknowledgements.in_set(FrameSystems::Acknowledge),
                drain_resolve_acknowledgements.in_set(FrameSystems::Acknowledge),
                frame::prepare_main_view.in_set(FrameSystems::Prepare),
                apply_jitter.in_set(FrameSystems::Jitter),
            ),
        );
    app.sub_app_mut(RenderApp)
        .init_resource::<targets::Allocator>()
        .add_systems(bevy::render::ExtractSchedule, frame::extract_encoding_owner)
        .add_systems(
            Render,
            targets::prepare_targets.in_set(RenderSystems::PrepareResources),
        );
    phase::install_render(app, sky)?;
    Ok(())
}

/// Install the inactive pre/post-transparent auxiliary readback bus explicitly.
/// An adapter must still select a view and issue a bounded scoped capture request.
/// Registration alone neither copies textures nor qualifies numeric source parity.
pub fn register_base_readback(app: &mut App) -> Result<(), String> {
    register(app)?;
    base_readback::register(app)
}

#[allow(
    clippy::needless_pass_by_value,
    reason = "Bevy systems require owned system parameter wrappers."
)]
fn drain_resolve_acknowledgements(
    acknowledgements: Option<Res<resolve::ResolveAcknowledgements>>,
    observations: Option<Res<resolve::ResolveObservations>>,
    mut frames: ResMut<frame::Frames>,
) {
    let Some(acknowledgements) = acknowledgements else {
        return;
    };
    let Ok(acks) = acknowledgements.drain() else {
        frames.last_error = Some(frame::FrameError::InvalidEncoding);
        frames.rejected_resolve = frames.rejected_resolve.saturating_add(1);
        return;
    };
    for ack in acks {
        if ack.render_owned {
            // RenderWorld already committed this exact encoding under its
            // reservation. Arrival order and a newer observation cannot consume
            // or replace that camera history in MainWorld.
            if ack.complete() {
                frames.observe_render_encoded(ack.tag);
            } else {
                frames.rejected_resolve = frames.rejected_resolve.saturating_add(1);
                frames.last_error = Some(frame::FrameError::InvalidEncoding);
            }
            continue;
        }
        let same_encoding = observations
            .as_ref()
            .and_then(|observations| observations.snapshot(ack.tag.view))
            .is_some_and(|observation| {
                observation.complete()
                    && observation.tag == ack.tag
                    && observation.generation == ack.generation
                    && observation.auxiliary_generation == ack.auxiliary_generation
                    && observation.history_epoch == ack.history_epoch
            });
        let result = if ack.complete() && same_encoding {
            frames.accept_resolve(
                ack.tag,
                ack.history_epoch,
                ack.generation,
                ack.resolve_draws,
                ack.previous_depth_copied,
            )
        } else {
            Err(frame::FrameError::InvalidEncoding)
        };
        if let Err(error) = result {
            frames.rejected_resolve = frames.rejected_resolve.saturating_add(1);
            frames.last_error = Some(error);
        }
    }
}

/// Register an actual material type against its installed source shader assets.
/// This also follows subsequent source edits; derived assets are not documents.
pub fn register_material<M: Material>(
    app: &mut App,
    family: shaders::Family,
    role: phase::MaterialRole,
) -> Result<(), String> {
    register(app)?;
    let vertex = source_shader(app, M::vertex_shader())?;
    let fragment = source_shader(app, M::fragment_shader())?;
    let assets = app.world().resource::<Assets<Shader>>();
    let derived = phase::AuxShaders {
        vertex: assets.reserve_handle(),
        fragment: assets.reserve_handle(),
    };
    phase::register_material::<M>(app, derived.clone(), role)?;
    app.world_mut()
        .resource_mut::<Derivations>()
        .0
        .push(Derivation {
            family,
            vertex,
            fragment,
            derived,
            previous_sources: None,
            error: None,
        });
    Ok(())
}

fn source_shader(app: &App, source: ShaderRef) -> Result<Handle<Shader>, String> {
    match source {
        ShaderRef::Handle(handle) => Ok(handle),
        ShaderRef::Path(path) => app
            .world()
            .get_resource::<AssetServer>()
            .map(|server| server.load(path))
            .ok_or_else(|| "Temporal shader path requires the installed AssetServer".into()),
        ShaderRef::Default => {
            Err("Temporal source shader needs an explicit handle or asset path".into())
        }
    }
}

fn update_derived_shaders(
    mut assets: ResMut<Assets<Shader>>,
    mut derivations: ResMut<Derivations>,
) {
    if !assets.is_changed() && !derivations.is_changed() {
        return;
    }
    for entry in &mut derivations.bypass_change_detection().0 {
        let (Some(vertex), Some(fragment)) =
            (assets.get(&entry.vertex), assets.get(&entry.fragment))
        else {
            assets.remove(entry.derived.vertex.id());
            assets.remove(entry.derived.fragment.id());
            entry.error = Some("Source shader asset is not loaded".into());
            entry.previous_sources = None;
            continue;
        };
        let (Source::Wgsl(vertex_source), Source::Wgsl(fragment_source)) =
            (&vertex.source, &fragment.source)
        else {
            assets.remove(entry.derived.vertex.id());
            assets.remove(entry.derived.fragment.id());
            entry.error = Some("Temporal source shaders must use WGSL".into());
            entry.previous_sources = None;
            continue;
        };
        if entry
            .previous_sources
            .as_ref()
            .is_some_and(|(v, f, vd, fd)| {
                v.as_str() == vertex_source.as_ref()
                    && f.as_str() == fragment_source.as_ref()
                    && vd == &vertex.shader_defs
                    && fd == &fragment.shader_defs
            })
        {
            continue;
        }
        let vertex_defs = vertex.shader_defs.clone();
        let fragment_defs = fragment.shader_defs.clone();
        let sources = (
            vertex_source.to_string(),
            fragment_source.to_string(),
            vertex_defs.clone(),
            fragment_defs.clone(),
        );
        match shaders::derive(entry.family, &sources.0, &sources.1) {
            Ok(derived) => {
                let mut vertex = Shader::from_wgsl(
                    derived.vertex,
                    format!(
                        "tidewater-temporal-{:?}-vertex.wgsl",
                        entry.derived.vertex.id()
                    ),
                );
                vertex.shader_defs = vertex_defs;
                let mut fragment = Shader::from_wgsl(
                    derived.fragment,
                    format!(
                        "tidewater-temporal-{:?}-fragment.wgsl",
                        entry.derived.fragment.id()
                    ),
                );
                fragment.shader_defs = fragment_defs;
                if let Err(error) = assets
                    .insert(entry.derived.vertex.id(), vertex)
                    .and_then(|()| assets.insert(entry.derived.fragment.id(), fragment))
                {
                    entry.error = Some(error.to_string());
                } else {
                    entry.error = None;
                    entry.previous_sources = Some(sources);
                }
            }
            Err(error) => {
                // Removing an old derivative invalidates its cached pipeline.
                // A failed authoring seam must not keep displaying stale coverage.
                assets.remove(entry.derived.vertex.id());
                assets.remove(entry.derived.fragment.id());
                entry.error = Some(error.to_string());
                entry.previous_sources = Some(sources);
            }
        }
    }
}

/// Explicit adapter opt-in. Refraction capture cameras cannot be selected.
pub fn select_camera(world: &mut World, camera: Option<Entity>) -> Result<(), String> {
    validate_camera_selection(world, camera)?;
    if world
        .get_resource::<render_owned::Control>()
        .is_some_and(|control| control.revision() > 0)
    {
        return Err("Legacy auxiliary diagnostics cannot resume after RenderWorld ownership; use a fresh diagnostic app".into());
    }
    let Some(mut frames) = world.get_resource_mut::<frame::Frames>() else {
        return Err("Temporal auxiliary inputs are not registered".into());
    };
    frames.activate(camera);
    Ok(())
}

pub(super) fn validate_camera_selection(
    world: &World,
    camera: Option<Entity>,
) -> Result<(), String> {
    if camera.is_some_and(|entity| {
        world
            .get::<crate::tidewater_refraction::CaptureCamera>(entity)
            .is_some()
    }) {
        return Err("A refraction capture cannot own temporal history".into());
    }
    if camera.is_some_and(|entity| {
        world
            .get::<bevy::anti_alias::taa::TemporalAntiAliasing>(entity)
            .is_some()
    }) {
        return Err("Disable Bevy TAA before selecting source temporal auxiliary inputs".into());
    }
    if camera.is_some_and(|entity| {
        world
            .get::<bevy::render::camera::TemporalJitter>(entity)
            .is_some()
            && world.get::<OwnsJitter>(entity).is_none()
    }) {
        return Err("The selected camera jitter already belongs to another consumer".into());
    }
    Ok(())
}

fn apply_jitter(
    mut commands: Commands,
    packets: Query<(Entity, &frame::ViewPacket)>,
    retired: Query<Entity, (With<OwnsJitter>, Without<frame::ViewPacket>)>,
) {
    for entity in &retired {
        commands
            .entity(entity)
            .remove::<(OwnsJitter, bevy::render::camera::TemporalJitter)>();
    }
    for (entity, packet) in &packets {
        commands.entity(entity).insert((
            OwnsJitter,
            bevy::render::camera::TemporalJitter {
                offset: packet.frame.jitter_pixels,
            },
        ));
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn explicit_shader_path_waits_for_real_asset_server_without_default_fallback() {
        let app = App::new();
        assert!(source_shader(&app, ShaderRef::Default).is_err());
        assert!(source_shader(&app, ShaderRef::Path("breaker-lip.wgsl".into())).is_err());
        let handle: Handle<Shader> =
            bevy::asset::uuid_handle!("8d51f4a6-2945-4898-94c4-f93aa3902888");
        assert_eq!(
            source_shader(&app, ShaderRef::Handle(handle.clone()))
                .expect("explicit existing handle"),
            handle
        );
    }

    #[test]
    fn an_unloaded_source_can_return_unchanged_and_shader_defs_are_followed() {
        let mut app = App::new();
        app.init_resource::<Assets<Shader>>()
            .init_resource::<Derivations>()
            .add_systems(Update, update_derived_shaders);
        let source = include_str!("tidewater_stall.wgsl");
        let mut assets = app.world_mut().resource_mut::<Assets<Shader>>();
        let original = assets.add(Shader::from_wgsl(source, "stall-source.wgsl"));
        let derived = phase::AuxShaders {
            vertex: assets.reserve_handle(),
            fragment: assets.reserve_handle(),
        };
        app.world_mut()
            .resource_mut::<Derivations>()
            .0
            .push(Derivation {
                family: shaders::Family::FishStand,
                vertex: original.clone(),
                fragment: original.clone(),
                derived: derived.clone(),
                previous_sources: None,
                error: None,
            });
        app.update();
        assert!(
            app.world()
                .resource::<Assets<Shader>>()
                .contains(derived.vertex.id())
        );
        app.world_mut()
            .resource_mut::<Assets<Shader>>()
            .remove(original.id());
        app.update();
        assert!(
            !app.world()
                .resource::<Assets<Shader>>()
                .contains(derived.vertex.id())
        );
        app.world_mut()
            .resource_mut::<Assets<Shader>>()
            .insert(
                original.id(),
                Shader::from_wgsl(source, "stall-source.wgsl"),
            )
            .expect("reserved source");
        app.update();
        assert!(
            app.world()
                .resource::<Assets<Shader>>()
                .contains(derived.vertex.id())
        );
        app.world_mut()
            .resource_mut::<Assets<Shader>>()
            .get_mut(&original)
            .expect("source")
            .shader_defs
            .push(ShaderDefVal::Bool("AUTHORED_VARIANT".into(), true));
        app.update();
        let assets = app.world().resource::<Assets<Shader>>();
        assert_eq!(
            assets.get(&derived.vertex).expect("vertex").shader_defs,
            assets.get(&original).expect("source").shader_defs
        );
        assert_eq!(
            assets.get(&derived.fragment).expect("fragment").shader_defs,
            assets.get(&original).expect("source").shader_defs
        );
    }

    #[test]
    fn auxiliary_selection_preserves_another_jitter_owner_and_rejects_capture() {
        let mut world = World::new();
        world.init_resource::<frame::Frames>();
        let existing = world
            .spawn(bevy::render::camera::TemporalJitter {
                offset: Vec2::new(0.125, -0.25),
            })
            .id();
        assert!(select_camera(&mut world, Some(existing)).is_err());
        assert_eq!(
            world
                .get::<bevy::render::camera::TemporalJitter>(existing)
                .expect("retained jitter")
                .offset,
            Vec2::new(0.125, -0.25)
        );
        assert_eq!(world.resource::<frame::Frames>().selected_view(), None);
        let capture = world.spawn(crate::tidewater_refraction::CaptureCamera).id();
        assert!(select_camera(&mut world, Some(capture)).is_err());
        let main = world.spawn_empty().id();
        select_camera(&mut world, Some(main)).expect("explicit selection");
        assert_eq!(
            world.resource::<frame::Frames>().selected_view(),
            Some(main)
        );
    }
}
