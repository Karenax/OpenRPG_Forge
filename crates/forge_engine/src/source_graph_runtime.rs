//! Shared source-scene lifetime, frames, LOD and particle input.
//! Hosts supply scene clocks and UI commands; this does not reveal a scene or
//! claim GPU, pass-order, physical or visual qualification.
use crate::{
    source_graph_context::{HostFrame, time_parameters},
    source_graph_host::SourceSceneHost,
    source_graph_lod,
};
use bevy::ecs::change_detection::Tick;
use bevy::prelude::*;
use forge_assets::source_particle_emission::SourceEmissionEvent;
use std::{
    collections::{BTreeMap, BTreeSet},
    sync::Arc,
};

#[derive(Resource, Clone, Copy, Default)]
pub struct SourceClock {
    pub current_seconds: f32,
    pub previous_seconds: f32,
    pub delta_seconds: f32,
    pub sequence: u64,
}

#[derive(Clone, Copy)]
pub(crate) struct SourceClockWitness {
    clock: SourceClock,
    tick: Tick,
}

impl SourceClockWitness {
    pub(crate) fn capture(world: &World) -> Option<Self> {
        let clock = world.get_resource_ref::<SourceClock>()?;
        Some(Self {
            clock: *clock,
            tick: clock.last_changed(),
        })
    }

    pub(crate) fn is_fresh(self, world: &World) -> bool {
        world
            .get_resource_ref::<SourceClock>()
            .is_some_and(|current| {
                current.last_changed() == self.tick
                    && current.sequence == self.clock.sequence
                    && current.current_seconds.to_bits() == self.clock.current_seconds.to_bits()
                    && current.previous_seconds.to_bits() == self.clock.previous_seconds.to_bits()
                    && current.delta_seconds.to_bits() == self.clock.delta_seconds.to_bits()
            })
    }
}
#[derive(Clone, Copy)]
pub struct RuntimePolicy {
    pub source_world_from_target_world: Mat4,
    pub source_absolute_world_from_target_world: Mat4,
    pub global_mip_bias: f32,
    pub source_float_min: f32,
}
impl RuntimePolicy {
    pub fn frame(&self, clock: SourceClock) -> Result<HostFrame, String> {
        Ok(HostFrame {
            source_world_from_target_world: self.source_world_from_target_world,
            source_absolute_world_from_target_world: self.source_absolute_world_from_target_world,
            time_parameters: time_parameters(clock.current_seconds)?,
            previous_time_parameters: time_parameters(clock.previous_seconds)?,
            global_mip_bias: self.global_mip_bias,
            source_float_min: self.source_float_min,
        })
    }
}
struct Resident {
    host: SourceSceneHost,
    policy: RuntimePolicy,
    particle_input: crate::source_particle_host_input::HostedParticleInput,
    input_error: Option<String>,
}
#[derive(Resource, Default)]
pub struct SourceSceneResidents {
    scenes: BTreeMap<Entity, Resident>,
}

/// Transient owned particle observations; renderer completion remains separate.
#[derive(Clone, Debug, Default, serde::Serialize)]
pub struct SourceSceneParticleObservation {
    pub owners: usize,
    pub initialized_owners: usize,
    pub current_owners: usize,
    pub producer_steps: u64,
    pub projection_frames: u64,
    pub primitives: usize,
    #[serde(skip)]
    pub(crate) primitive_entities: BTreeSet<Entity>,
}

impl SourceSceneParticleObservation {
    #[must_use]
    pub fn initialized(&self) -> bool {
        self.owners > 0 && self.initialized_owners == self.owners
    }
    #[must_use]
    pub fn current(&self) -> bool {
        self.initialized() && self.current_owners == self.owners
    }
}
type ParticleIdentity = (Entity, Entity, Arc<()>, Arc<()>);
pub(crate) type ParticleIdentityIndex = BTreeMap<Entity, Option<ParticleIdentity>>;
impl SourceSceneResidents {
    pub fn observe_particles(
        &self,
        world: &World,
        root: Entity,
        carrier: forge_core::ContentHash,
    ) -> Result<Option<SourceSceneParticleObservation>, String> {
        let resident = self
            .scenes
            .get(&root)
            .filter(|resident| resident.host.carrier_sha256 == carrier)
            .ok_or("Particle source scene or carrier differs")?;
        if let Some(error) = &resident.input_error {
            return Err(error.clone());
        }
        if resident.host.particle_instances.is_empty() {
            return Ok(None);
        }
        let mut observed = SourceSceneParticleObservation::default();
        let mut materials = BTreeSet::new();
        for instance in &resident.host.particle_instances {
            let item = instance.observe(world, root)?;
            if !materials.insert(item.material) {
                return Err("Particle material has multiple owners".into());
            }
            observed.owners += 1;
            observed.initialized_owners += usize::from(item.initialized);
            observed.current_owners += usize::from(item.current_frame);
            observed.producer_steps = observed
                .producer_steps
                .checked_add(item.producer_steps)
                .ok_or("Particle step observation overflow")?;
            observed.projection_frames = observed
                .projection_frames
                .checked_add(item.projection_frames)
                .ok_or("Particle frame observation overflow")?;
        }
        let mut referenced = BTreeSet::new();
        for &entity in &resident.host.attached.primitives {
            if let Some(material) =
                world.get::<MeshMaterial3d<crate::source_graph::SourceGraphMaterial>>(entity)
                && materials.contains(&material.0.id())
            {
                if world
                    .get::<bevy::camera::visibility::NoFrustumCulling>(entity)
                    .is_none()
                {
                    return Err("Particle draw cannot use static carrier bounds".into());
                }
                referenced.insert(material.0.id());
                observed.primitive_entities.insert(entity);
            }
        }
        if referenced != materials {
            return Err("Particle material is not attached to its scene draw".into());
        }
        observed.primitives = observed.primitive_entities.len();
        Ok(Some(observed))
    }

    /// Initial encoded output must be bound before the ordinary GPU inventory
    /// can admit it. Per-frame camera and clock validation remains in the draw path.
    #[must_use]
    pub fn particles_ready_for_inventory(&self, world: &World) -> bool {
        self.scenes.iter().all(|(root, resident)| {
            self.observe_particles(world, *root, resident.host.carrier_sha256)
                .is_ok_and(|observed| observed.is_none_or(|item| item.current()))
        })
    }

    pub(crate) fn particle_index(&self) -> ParticleIdentityIndex {
        let mut index = BTreeMap::new();
        for (root, resident) in &self.scenes {
            for instance in &resident.host.particle_instances {
                if let Some((owner, projection, owner_identity, projection_identity)) =
                    instance.scene_identity()
                {
                    index
                        .entry(projection)
                        .and_modify(|entry| *entry = None)
                        .or_insert(Some((*root, owner, owner_identity, projection_identity)));
                }
            }
        }
        index
    }

    pub(crate) fn owns_particle(
        &self,
        witness: &crate::source_particle_host::ParticleSceneWitness,
        index: &ParticleIdentityIndex,
    ) -> bool {
        self.scenes.get(&witness.root).is_some_and(|resident| {
            resident.input_error.is_none()
                && witness.matches_nodes(&resident.host.attached.nodes)
                && index
                    .get(&witness.projection)
                    .and_then(Option::as_ref)
                    .is_some_and(|(root, owner, owner_identity, projection_identity)| {
                        *root == witness.root
                            && *owner == witness.owner
                            && Arc::ptr_eq(owner_identity, &witness.owner_identity)
                            && Arc::ptr_eq(projection_identity, &witness.projection_identity)
                    })
        })
    }
    #[must_use]
    pub fn contains(&self, root: Entity) -> bool {
        self.scenes.contains_key(&root)
    }

    /// The exact primitives mounted for this root; shadow proxies and unrelated
    /// descendants are not part of the canonical source geometry.
    #[must_use]
    pub fn primitives_for_carrier(
        &self,
        root: Entity,
        carrier: forge_core::ContentHash,
    ) -> Option<&[Entity]> {
        self.scenes
            .get(&root)
            .filter(|resident| resident.host.carrier_sha256 == carrier)
            .map(|resident| resident.host.attached.primitives.as_slice())
    }

    /// Static graph owners whose visible mesh set can be probed without an
    /// independent LOD camera, particle frame, or source contact projection.
    #[must_use]
    pub fn static_primitives_for_carrier(
        &self,
        root: Entity,
        carrier: forge_core::ContentHash,
    ) -> Option<&[Entity]> {
        self.scenes
            .get(&root)
            .filter(|resident| {
                let host = &resident.host;
                host.carrier_sha256 == carrier
                    && host.particle_instances.is_empty()
                    && host
                        .original_state
                        .as_ref()
                        .is_none_or(|state| state.lod_groups.is_empty())
            })
            .map(|resident| resident.host.attached.primitives.as_slice())
    }

    /// Exact resident primitives and their verified derived shadow proxies.
    pub fn owned_draws(&self, world: &World) -> Result<BTreeSet<Entity>, String> {
        let mut draws = BTreeSet::new();
        for resident in self.scenes.values() {
            for &primitive in &resident.host.attached.primitives {
                if !draws.insert(primitive) {
                    return Err("Source draw has multiple scene owners".into());
                }
                if let Some(route) =
                    world.get::<crate::source_graph::SourceGraphShadowRoute>(primitive)
                    && (!crate::source_graph::source_graph_shadow_proxy_owned_by(
                        world,
                        primitive,
                        route.proxy,
                    ) || !draws.insert(route.proxy))
                {
                    return Err("Source shadow proxy ownership differs".into());
                }
            }
        }
        Ok(draws)
    }

    pub fn push_particle_event(
        &mut self,
        root: Entity,
        event: SourceEmissionEvent,
    ) -> Result<(), String> {
        let resident = self
            .scenes
            .get_mut(&root)
            .ok_or("Effet chargé introuvable")?;
        if resident.host.particle_instances.is_empty() {
            return Err("Cet objet ne contient pas d’effet chargé".into());
        }
        if let Some(error) = &resident.input_error {
            return Err(error.clone());
        }
        resident.particle_input.push_event(event)
    }
}

#[derive(Component, Debug, Clone, PartialEq, Eq)]
pub enum SourceFrameStatus {
    Updated,
    Refused(String),
}

/// Host-selected source LOD bias; Unity source measurements use 1.0.
#[derive(Resource)]
pub struct SourceLodBias(pub f32);

/// Host input belongs between clock capture and the common simulation advance.
/// Scene-specific requests can be ordered before placement refresh without
/// duplicating the common scene lifecycle in each host.
#[derive(SystemSet, Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum SourceSceneSystems {
    CaptureClock,
    HostInput,
    Advance,
    Frames,
    Placements,
}

/// Install the shared source-scene clock, updates and placement lifecycle.
/// The host still supplies its render plugins, camera and any user commands.
pub struct SourceSceneRuntimePlugin {
    pub lod_bias: f32,
    /// A host with its own simulation clock can supply `SourceClock` in the
    /// `CaptureClock` set instead of using Bevy time.
    pub capture_bevy_clock: bool,
}

impl Default for SourceSceneRuntimePlugin {
    fn default() -> Self {
        Self {
            lod_bias: 1.0,
            capture_bevy_clock: true,
        }
    }
}

impl Plugin for SourceSceneRuntimePlugin {
    fn build(&self, app: &mut App) {
        app.insert_resource(SourceLodBias(self.lod_bias))
            .init_resource::<SourceSceneResidents>();
        crate::source_graph_placement::initialize(app);
        app.configure_sets(
            PreUpdate,
            (
                SourceSceneSystems::CaptureClock,
                SourceSceneSystems::HostInput,
                SourceSceneSystems::Advance,
            )
                .chain(),
        )
        .add_systems(
            PreUpdate,
            advance_particle_inputs.in_set(SourceSceneSystems::Advance),
        )
        .add_systems(
            PostUpdate,
            update
                .in_set(SourceSceneSystems::Frames)
                .after(bevy::transform::TransformSystems::Propagate)
                .after(bevy::camera::CameraUpdateSystems)
                .before(crate::source_particle_compute::SourceParticleProjectionUpdate),
        )
        .add_systems(
            PostUpdate,
            crate::source_graph_placement::refresh
                .in_set(SourceSceneSystems::Placements)
                .after(SourceSceneSystems::Frames),
        );
        if self.capture_bevy_clock {
            app.init_resource::<SourceClock>().add_systems(
                PreUpdate,
                capture_clock.in_set(SourceSceneSystems::CaptureClock),
            );
        }
    }
}

fn viewport_lod_camera(world: &mut World) -> Result<source_graph_lod::LodCamera, String> {
    let views: Vec<_> = world
        .query_filtered::<(Entity, &Camera), crate::source_sky::preview::SceneCameraFilter>()
        .iter(world)
        .filter(|(_, camera)| camera.is_active)
        .map(|(entity, _)| entity)
        .collect();
    if views.len() != 1 {
        return Err("Exactly one active source viewport camera required".into());
    }
    let bias = world
        .get_resource::<SourceLodBias>()
        .ok_or("Source viewport LOD quality absent")?
        .0;
    source_graph_lod::camera(world, views[0], bias)
}

/// A successful mount owns its strong handles until this root is removed.
/// Do not remove borrowed assets or cache entries from Assets explicitly.
/// A refusal returns the complete host to its caller, which retains explicit
/// responsibility for disposing of its physical and particle projections.
pub struct RetainFailure {
    pub reason: String,
    pub host: Box<SourceSceneHost>,
}

impl std::fmt::Debug for RetainFailure {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("RetainFailure")
            .field("reason", &self.reason)
            .field("root", &self.host.root)
            .finish_non_exhaustive()
    }
}

pub fn retain(
    world: &mut World,
    host: SourceSceneHost,
    policy: RuntimePolicy,
) -> Result<(), RetainFailure> {
    world.init_resource::<SourceSceneResidents>();
    if world
        .resource::<SourceSceneResidents>()
        .scenes
        .contains_key(&host.root)
    {
        return Err(RetainFailure {
            reason: "A source host already owns this render root".into(),
            host: Box::new(host),
        });
    }
    if world
        .get::<crate::source_graph_scene_attachment::SourceSceneAttached>(host.root)
        .is_none()
    {
        return Err(RetainFailure {
            reason: "Source root is not attached".into(),
            host: Box::new(host),
        });
    }
    for instance in &host.particle_instances {
        if let Err(reason) = instance.validate_scene_binding(world) {
            return Err(RetainFailure {
                reason,
                host: Box::new(host),
            });
        }
    }
    for instance in &host.particle_instances {
        if let Err(reason) = instance.bind_scene(world) {
            return Err(RetainFailure {
                reason,
                host: Box::new(host),
            });
        }
    }
    world.resource_mut::<SourceSceneResidents>().scenes.insert(
        host.root,
        Resident {
            host,
            policy,
            particle_input: crate::source_particle_host_input::HostedParticleInput::default(),
            input_error: None,
        },
    );
    Ok(())
}

/// Release this host's physical records before its hierarchy is removed.
/// The caller still owns despawning the root; source nodes and draws survive
/// this call. The returned count is released records, not backend contacts.
pub fn release(world: &mut World, root: Entity) -> usize {
    let Some(mut residents) = world.get_resource_mut::<SourceSceneResidents>() else {
        return 0;
    };
    let Some(mut resident) = residents.scenes.remove(&root) else {
        return 0;
    };
    let count = resident.host.remove_colliders(world);
    resident.host.release_particle_instances(world);
    if let Ok(mut entity) = world.get_entity_mut(root) {
        entity.remove::<SourceFrameStatus>();
    }
    count
}
/// Schedule after `TransformSystems::Propagate`, before render extraction.
/// The common viewport must explicitly supply its current/previous clock.
#[allow(
    clippy::too_many_lines,
    reason = "Each resident frame is prepared and sealed as one transaction"
)]
pub fn update(world: &mut World) {
    if !world.contains_resource::<SourceSceneResidents>() {
        let _ =
            world.remove_resource::<crate::source_particle_compute::SourceParticleCameraSnapshot>();
        return;
    }
    let clock = world.get_resource::<SourceClock>().copied();
    let clock_witness = SourceClockWitness::capture(world);
    let camera = viewport_lod_camera(world);
    let particle_camera = current_particle_camera(world).and_then(|(view, pose, clip, taa)| {
        let witness = crate::source_particle_host::ParticleViewWitness::capture(world, view, pose)?;
        Ok((view, pose, clip, taa, witness))
    });
    if let Ok((view, world_from_view, clip_from_view, bevy_taa, witness)) = &particle_camera {
        let creation_tick = world.read_change_tick();
        world.insert_resource(
            crate::source_particle_compute::SourceParticleCameraSnapshot {
                view: *view,
                world_from_view: *world_from_view,
                clip_from_view: *clip_from_view,
                bevy_taa: *bevy_taa,
                witness: Some(std::sync::Arc::new(witness.clone())),
                clock: clock_witness,
                creation_tick: Some(creation_tick),
            },
        );
    } else {
        let _ =
            world.remove_resource::<crate::source_particle_compute::SourceParticleCameraSnapshot>();
    }
    let mut sealed_particle_frame = false;
    world.resource_scope(|world, residents: Mut<SourceSceneResidents>| {
        let mut residents = residents;
        residents.scenes.retain(|root, resident| {
            // Despawned roots release handles naturally through Bevy's asset lifecycle.
            if world.get_entity(*root).is_err() {
                // Clear surviving owned children too, including a child that
                // lost its parent before the root disappeared.
                resident.host.remove_colliders(world);
                resident.host.release_particle_instances(world);
                return false;
            }
            resident.host.invalidate_particle_frames(world);
            let result = resident.input_error.as_ref().map_or_else(
                || {
                    if !resident.host.particle_instances.is_empty() {
                        let (_, pose, clip, _, _) = particle_camera.as_ref().map_err(Clone::clone)?;
                        resident.host.update_particle_frames_for_camera(
                            world,
                            resident.policy.source_absolute_world_from_target_world,
                            *pose,
                            *clip,
                        )?;
                    }
                    clock
                        .ok_or_else(|| "Explicit source viewport clock unavailable".to_owned())
                        .and_then(|clock| resident.policy.frame(clock))
                        .and_then(|frame| resident.host.update_runtime_frame(world, &frame))
                        .and_then(|_| {
                            if resident
                                .host
                                .original_state
                                .as_ref()
                                .is_some_and(|state| !state.lod_groups.is_empty())
                            {
                                let decisions = resident
                                    .host
                                    .update_lod(world, camera.as_ref().map_err(Clone::clone)?)?;
                                if bevy::log::tracing::enabled!(bevy::log::Level::TRACE) {
                                    for decision in decisions {
                                        bevy::log::trace!(source_root=?root,source_node=decision.node,
                                            relative_height=decision.relative_height,selected_lod=?decision.level,
                                            "source LOD decision");
                                    }
                                }
                            }
                            Ok(())
                        })
                },
                |error| Err(error.clone()),
            );
            let result = result.and_then(|()| {
                world.entity_mut(*root).insert(SourceFrameStatus::Updated);
                if !resident.host.particle_instances.is_empty() {
                    let nodes: BTreeMap<_, _> = resident.host.attached.nodes.iter()
                        .map(|(source, entity)| (*entity, *source)).collect();
                    if nodes.len() != resident.host.attached.nodes.len() {
                        return Err("Source scene has ambiguous node identities".into());
                    }
                    for instance in &resident.host.particle_instances {
                        instance.seal_scene_frame(world, *root, &nodes)?;
                        sealed_particle_frame = true;
                    }
                }
                Ok(())
            });
            if let Err(error) = result {
                resident.host.cancel_particle_frames(world);
                world
                    .entity_mut(*root)
                    .insert((Visibility::Hidden, SourceFrameStatus::Refused(error)));
            }
            true
        });
    });
    if sealed_particle_frame {
        // One boundary per invocation, regardless of scene/particle count.
        // Bevy flushes deferred commands before an exclusive system's own
        // final tick; separate those writes from the captured frame too.
        world.increment_change_tick();
    }
}

pub(crate) fn current_particle_camera(
    world: &mut World,
) -> Result<(Entity, Mat4, Mat4, bool), String> {
    let views: Vec<_> = world
        .query_filtered::<(Entity, &Camera), crate::source_sky::preview::SceneCameraFilter>()
        .iter(world)
        .filter(|(_, camera)| camera.is_active)
        .map(|(entity, _)| entity)
        .collect();
    if views.len() != 1 {
        return Err("Exactly one current particle viewport camera required".into());
    }
    let entity = views[0];
    let taa = particle_temporal_mode(world, entity)?;
    let camera = world.get::<Camera>(entity).unwrap();
    let pose = world
        .get::<GlobalTransform>(entity)
        .ok_or("Current particle camera transform absent")?
        .to_matrix();
    let clip = camera.clip_from_view();
    if camera.physical_viewport_size().is_none() {
        return Err("Current particle camera viewport not computed".into());
    }
    Ok((entity, pose, clip, taa))
}

fn particle_temporal_mode(world: &World, entity: Entity) -> Result<bool, String> {
    let taa = world
        .get::<bevy::anti_alias::taa::TemporalAntiAliasing>(entity)
        .is_some();
    let jitter = world
        .get::<bevy::render::camera::TemporalJitter>(entity)
        .is_some();
    if world.get::<Camera3d>(entity).is_none()
        || jitter != taa
        || (taa
            && (world
                .get::<bevy::render::view::Msaa>(entity)
                .is_none_or(|msaa| msaa.samples() != 1)
                || world
                    .get::<crate::source_temporal::resolve::ResolveEnabled>(entity)
                    .is_some()))
    {
        return Err("Particle viewport requires an admitted 3D temporal camera".into());
    }
    Ok(taa)
}

/// Opt in to the host's Bevy clock. Paused time remains paused; hosts that own
/// a different simulation clock can instead provide the same explicit resource.
pub fn capture_clock(time: Res<Time>, mut clock: ResMut<SourceClock>) {
    let time = time.into_inner();
    clock.current_seconds = time.elapsed_secs();
    clock.previous_seconds = time.elapsed_secs() - time.delta_secs();
    clock.delta_seconds = time.delta_secs();
    clock.sequence = clock.sequence.wrapping_add(1);
}

/// Advance admitted particle owners once for the explicitly supplied clock.
/// Scene-specific command routing stays with the caller.
pub fn advance_particle_inputs(world: &mut World) {
    let clock = world.get_resource::<SourceClock>().copied();
    if !world.contains_resource::<SourceSceneResidents>() {
        return;
    }
    world.resource_scope(|world, residents: Mut<SourceSceneResidents>| {
        let mut residents = residents;
        for (root, resident) in &mut residents.scenes {
            if resident.host.particle_instances.is_empty() || resident.input_error.is_some() {
                continue;
            }
            let result = clock
                .ok_or("Horloge explicite de scène indisponible".to_owned())
                .and_then(|clock| {
                    resident.particle_input.advance(
                        world,
                        &resident.host.particle_instances,
                        clock.sequence,
                        clock.delta_seconds,
                    )
                });
            if let Err(error) = result {
                resident.input_error = Some(error.clone());
                if let Ok(mut entity) = world.get_entity_mut(*root) {
                    entity.insert((Visibility::Hidden, SourceFrameStatus::Refused(error)));
                }
            }
        }
    });
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::source_collider_projection::{ColliderProjection, SourceColliderIdentity};

    #[test]
    fn particle_camera_admits_only_bevy_taa_owned_jitter() {
        use bevy::{
            anti_alias::taa::TemporalAntiAliasing,
            render::{camera::TemporalJitter, view::Msaa},
        };

        let mut world = World::new();
        let camera = world.spawn((Camera3d::default(), Msaa::Off)).id();
        assert!(!particle_temporal_mode(&world, camera).unwrap());
        world.entity_mut(camera).insert(TemporalJitter::default());
        assert!(particle_temporal_mode(&world, camera).is_err());
        world
            .entity_mut(camera)
            .insert(TemporalAntiAliasing::default());
        assert!(particle_temporal_mode(&world, camera).unwrap());
        world
            .entity_mut(camera)
            .insert(crate::source_temporal::resolve::ResolveEnabled);
        assert!(particle_temporal_mode(&world, camera).is_err());
        world
            .entity_mut(camera)
            .remove::<crate::source_temporal::resolve::ResolveEnabled>();
        world.entity_mut(camera).remove::<TemporalJitter>();
        assert!(particle_temporal_mode(&world, camera).is_err());
    }

    #[derive(Resource, Default)]
    struct ScheduleTrace(Vec<(&'static str, u64)>);

    #[expect(
        clippy::needless_pass_by_value,
        reason = "Bevy systems receive resources by value"
    )]
    fn host_input(clock: Res<SourceClock>, mut trace: ResMut<ScheduleTrace>) {
        trace.0.push(("input", clock.sequence));
    }

    #[expect(
        clippy::needless_pass_by_value,
        reason = "Bevy systems receive resources by value"
    )]
    fn advanced(clock: Res<SourceClock>, mut trace: ResMut<ScheduleTrace>) {
        trace.0.push(("advance", clock.sequence));
    }

    #[expect(
        clippy::needless_pass_by_value,
        reason = "Bevy systems receive resources by value"
    )]
    fn updated(clock: Res<SourceClock>, mut trace: ResMut<ScheduleTrace>) {
        trace.0.push(("frames", clock.sequence));
    }

    #[expect(
        clippy::needless_pass_by_value,
        reason = "Bevy systems receive resources by value"
    )]
    fn refreshed(clock: Res<SourceClock>, mut trace: ResMut<ScheduleTrace>) {
        trace.0.push(("placements", clock.sequence));
    }

    fn external_clock(mut clock: ResMut<SourceClock>) {
        clock.sequence += 7;
    }

    fn scheduled_app(capture_bevy_clock: bool) -> App {
        let mut app = App::new();
        if capture_bevy_clock {
            app.init_resource::<Time>();
        } else {
            app.init_resource::<SourceClock>().add_systems(
                PreUpdate,
                external_clock.in_set(SourceSceneSystems::CaptureClock),
            );
        }
        app.init_resource::<ScheduleTrace>()
            .add_plugins(SourceSceneRuntimePlugin {
                lod_bias: 1.0,
                capture_bevy_clock,
            })
            .add_systems(PreUpdate, host_input.in_set(SourceSceneSystems::HostInput))
            .add_systems(
                PreUpdate,
                advanced
                    .in_set(SourceSceneSystems::Advance)
                    .after(advance_particle_inputs),
            )
            .add_systems(
                PostUpdate,
                updated.in_set(SourceSceneSystems::Frames).after(update),
            )
            .add_systems(
                PostUpdate,
                refreshed
                    .in_set(SourceSceneSystems::Placements)
                    .after(crate::source_graph_placement::refresh),
            );
        app
    }

    #[test]
    fn common_scene_schedule_captures_time_before_host_input_and_refresh() {
        let mut app = scheduled_app(true);
        app.update();
        app.update();
        assert_eq!(
            app.world().resource::<ScheduleTrace>().0,
            [
                ("input", 1),
                ("advance", 1),
                ("frames", 1),
                ("placements", 1),
                ("input", 2),
                ("advance", 2),
                ("frames", 2),
                ("placements", 2),
            ]
        );
    }

    #[test]
    fn common_scene_schedule_accepts_a_host_owned_clock() {
        let mut app = scheduled_app(false);
        app.update();
        assert_eq!(
            app.world().resource::<ScheduleTrace>().0,
            [
                ("input", 7),
                ("advance", 7),
                ("frames", 7),
                ("placements", 7),
            ]
        );
    }

    #[test]
    fn external_clock_is_never_fabricated_when_the_host_omits_it() {
        let mut app = App::new();
        app.add_plugins(SourceSceneRuntimePlugin {
            lod_bias: 1.0,
            capture_bevy_clock: false,
        });
        app.update();
        assert!(!app.world().contains_resource::<SourceClock>());
    }

    fn fixture(world: &mut World) -> (Entity, Entity, Entity, Entity, Entity) {
        let root = world
            .spawn((
                crate::source_graph_scene_attachment::SourceSceneAttached,
                SourceFrameStatus::Updated,
                Name::new("owned root"),
            ))
            .id();
        let node = world.spawn((ChildOf(root), Name::new("source node"))).id();
        let draw = world.spawn((ChildOf(node), Name::new("source draw"))).id();
        let other = world.spawn_empty().id();
        let physical = world
            .spawn((
                ChildOf(node),
                SourceColliderIdentity {
                    placement_root: root,
                    guid: "owned fixture".into(),
                    file_id: "1".into(),
                    original: serde_json::json!({"owned_fixture":true}),
                    policy_evidence: "cleanup fixture".into(),
                },
            ))
            .id();
        let foreign = world
            .spawn((
                ChildOf(other),
                SourceColliderIdentity {
                    placement_root: other,
                    guid: "foreign fixture".into(),
                    file_id: "2".into(),
                    original: serde_json::json!({"owned_fixture":true}),
                    policy_evidence: "cleanup fixture".into(),
                },
            ))
            .id();
        let host = SourceSceneHost {
            root,
            attached: crate::source_graph_scene_attachment::AttachedScene {
                nodes: BTreeMap::from([(0, node)]),
                primitives: vec![draw],
            },
            registered: crate::source_graph_resources::RegisteredScene {
                owned_image_ids: std::collections::BTreeSet::new(),
                sampling: crate::source_graph::SourceGraphSceneSampling::default(),
                bindings: crate::source_graph_scene_attachment::SceneBindings::default(),
                original_passes: BTreeMap::new(),
            },
            original_state: None,
            object_basis: None,
            source_document: serde_json::json!({}),
            carrier_sha256: forge_core::bytes_sha256(b"fixture"),
            particle_instances: Vec::new(),
            // Include a foreign record as a negative ownership control. The
            // real projection clear must preserve its differently owned child.
            collider_projection: Some(ColliderProjection {
                root,
                entities: vec![physical, foreign],
            }),
            collider_scene_projection: None,
        };
        retain(
            world,
            host,
            RuntimePolicy {
                source_world_from_target_world: Mat4::IDENTITY,
                source_absolute_world_from_target_world: Mat4::IDENTITY,
                global_mip_bias: 0.,
                source_float_min: f32::MIN_POSITIVE,
            },
        )
        .unwrap();
        (root, node, draw, physical, foreign)
    }

    #[test]
    fn release_preserves_source_hierarchy_and_foreign_children_and_is_idempotent() {
        let mut world = World::new();
        let (root, node, draw, physical, foreign) = fixture(&mut world);
        let pin = forge_core::bytes_sha256(b"fixture");
        assert_eq!(
            world
                .resource::<SourceSceneResidents>()
                .owned_draws(&world)
                .unwrap(),
            BTreeSet::from([draw])
        );
        assert_eq!(
            world
                .resource::<SourceSceneResidents>()
                .primitives_for_carrier(root, pin),
            Some([draw].as_slice())
        );
        assert_eq!(
            world
                .resource::<SourceSceneResidents>()
                .primitives_for_carrier(root, forge_core::bytes_sha256(b"other")),
            None
        );
        assert_eq!(
            world
                .resource::<SourceSceneResidents>()
                .primitives_for_carrier(foreign, pin),
            None
        );
        assert_eq!(release(&mut world, root), 2);
        assert!(world.get_entity(physical).is_err());
        for entity in [root, node, draw, foreign] {
            assert!(world.get_entity(entity).is_ok());
        }
        assert!(world.get::<SourceFrameStatus>(root).is_none());
        assert!(world.resource::<SourceSceneResidents>().scenes.is_empty());
        assert_eq!(release(&mut world, root), 0);
        assert_eq!(world.get::<Name>(root).unwrap().as_str(), "owned root");
    }

    #[test]
    fn manual_admission_requires_the_mounted_carrier_pin_and_keeps_visibility() {
        use crate::source_graph_placement::{self, ManualAdmission, Status};

        let mut app = App::new();
        source_graph_placement::initialize(&mut app);
        let (root, _, draw, _, _) = fixture(app.world_mut());
        app.world_mut()
            .entity_mut(draw)
            .insert(Mesh3d(Handle::default()));
        {
            let mut commands = app.world_mut().commands();
            source_graph_placement::submit(
                &mut commands,
                root,
                std::sync::Arc::from(&b"fixture"[..]),
                None,
                None,
                31,
            );
        }
        app.world_mut().flush();
        let placement = forge_project::zone_authoring::placements::ManualPlacement {
            id: forge_core::EntityId::deterministic("source-graph/manual"),
            name: "source".into(),
            asset_id: forge_core::AssetId::deterministic("source-graph/model"),
            asset_hash: forge_core::bytes_sha256(b"different"),
            physics: None,
            particles: None,
            local_lights: None,
            transform: forge_core::Transform::IDENTITY,
            locked: false,
        };
        app.world_mut().entity_mut(root).insert((
            Status::MountedPendingRender,
            Visibility::Hidden,
            crate::ManualPlacementProjection(placement),
        ));
        assert!(
            source_graph_placement::admit_manual(app.world_mut(), root)
                .unwrap_err()
                .contains("pin")
        );
        assert!(
            app.world()
                .get::<crate::ManualPlacementCollisionExpected>(root)
                .is_none()
        );
        app.world_mut()
            .get_mut::<crate::ManualPlacementProjection>(root)
            .unwrap()
            .0
            .asset_hash = forge_core::bytes_sha256(b"fixture");
        assert_eq!(
            source_graph_placement::admit_manual(app.world_mut(), root).unwrap(),
            ManualAdmission::Attached
        );
        assert_eq!(
            source_graph_placement::admit_manual(app.world_mut(), root).unwrap(),
            ManualAdmission::Attached
        );
        assert_eq!(
            app.world()
                .get::<crate::ManualPlacementCollisionExpected>(root)
                .unwrap()
                .primitives,
            [draw]
        );
        assert_eq!(
            app.world().get::<Visibility>(root),
            Some(&Visibility::Hidden)
        );
    }

    #[test]
    fn refused_unattached_host_returns_its_physics_to_the_caller() {
        let mut world = World::new();
        let (root, node, draw, physical, foreign) = fixture(&mut world);
        let resident = world
            .resource_mut::<SourceSceneResidents>()
            .scenes
            .remove(&root)
            .unwrap();
        world
            .entity_mut(root)
            .remove::<crate::source_graph_scene_attachment::SourceSceneAttached>();
        let mut failure = retain(&mut world, resident.host, resident.policy).unwrap_err();
        assert!(world.get_entity(physical).is_ok());
        failure.host.remove_colliders(&mut world);
        failure.host.release_particle_instances(&mut world);
        assert!(world.get_entity(physical).is_err());
        for entity in [root, node, draw, foreign] {
            assert!(world.get_entity(entity).is_ok());
        }
        assert!(!world.resource::<SourceSceneResidents>().contains(root));
    }

    #[test]
    fn duplicate_host_refusal_preserves_the_first_owner_and_foreign_physics() {
        let mut world = World::new();
        let (first, _, _, first_physical, first_foreign) = fixture(&mut world);
        let (second, node, draw, second_physical, second_foreign) = fixture(&mut world);
        let mut incoming = world
            .resource_mut::<SourceSceneResidents>()
            .scenes
            .remove(&second)
            .unwrap();
        incoming.host.root = first;
        let mut failure = retain(&mut world, incoming.host, incoming.policy).unwrap_err();
        assert!(world.resource::<SourceSceneResidents>().contains(first));
        assert!(world.get_entity(second_physical).is_ok());
        failure.host.remove_colliders(&mut world);
        failure.host.release_particle_instances(&mut world);
        assert!(world.get_entity(second_physical).is_err());
        for entity in [
            first,
            first_physical,
            first_foreign,
            second,
            node,
            draw,
            second_foreign,
        ] {
            assert!(world.get_entity(entity).is_ok());
        }
    }

    #[test]
    fn vanished_root_releases_an_owned_child_that_lost_its_parent() {
        let mut world = World::new();
        let (root, _, _, physical, foreign) = fixture(&mut world);
        world.entity_mut(physical).remove::<ChildOf>();
        world.despawn(root);
        assert!(world.get_entity(physical).is_ok());
        update(&mut world);
        assert!(world.get_entity(physical).is_err());
        assert!(world.get_entity(foreign).is_ok());
        assert!(world.resource::<SourceSceneResidents>().scenes.is_empty());
        update(&mut world);
    }

    #[test]
    fn missing_clock_hides_the_shared_scene_without_releasing_other_owners() {
        let mut world = World::new();
        let (root, node, draw, physical, foreign) = fixture(&mut world);
        update(&mut world);
        assert_eq!(world.get::<Visibility>(root), Some(&Visibility::Hidden));
        assert!(matches!(
            world.get::<SourceFrameStatus>(root),
            Some(SourceFrameStatus::Refused(reason)) if reason.contains("clock")
        ));
        for entity in [node, draw, physical, foreign] {
            assert!(world.get_entity(entity).is_ok());
        }
        assert!(world.resource::<SourceSceneResidents>().contains(root));
    }

    #[test]
    fn shared_scene_updates_live_material_frames_without_revealing_the_root() {
        use crate::source_graph::{SourceGraphMaterial, SourceGraphUniforms};
        use crate::source_graph_context::ObjectBasis;
        use crate::source_particle_host::tests as particle_tests;

        let (mut world, _, material) = particle_tests::setup(false);
        let (root, node, draw, _, foreign) = fixture(&mut world);
        world.entity_mut(root).insert(Visibility::Hidden);
        world.entity_mut(node).insert(GlobalTransform::IDENTITY);
        world.entity_mut(draw).insert((
            GlobalTransform::IDENTITY,
            MeshMaterial3d::<SourceGraphMaterial>(material.clone()),
        ));
        {
            let mut scenes = world.resource_mut::<SourceSceneResidents>();
            let resident = scenes.scenes.get_mut(&root).unwrap();
            resident.host.object_basis = Some(ObjectBasis {
                source_from_target: Mat4::IDENTITY,
                target_from_source: Mat4::IDENTITY,
            });
            resident
                .host
                .registered
                .bindings
                .source_materials
                .insert((0, 0), material.clone());
        }
        world.insert_resource(SourceClock {
            current_seconds: 3.0,
            previous_seconds: 2.5,
            delta_seconds: 0.5,
            sequence: 1,
        });
        update(&mut world);
        assert_eq!(
            world.get::<SourceFrameStatus>(root),
            Some(&SourceFrameStatus::Updated)
        );
        assert_eq!(world.get::<Visibility>(root), Some(&Visibility::Hidden));
        let source: &SourceGraphUniforms = &world
            .resource::<Assets<SourceGraphMaterial>>()
            .get(&material)
            .unwrap()
            .extension
            .source;
        assert_eq!(source.source_time_parameters, time_parameters(3.0).unwrap());
        assert_eq!(
            source.source_previous_time_parameters,
            time_parameters(2.5).unwrap()
        );

        world
            .resource_mut::<Assets<SourceGraphMaterial>>()
            .remove(material.id());
        update(&mut world);
        assert!(matches!(
            world.get::<SourceFrameStatus>(root),
            Some(SourceFrameStatus::Refused(_))
        ));
        assert_eq!(world.get::<Visibility>(root), Some(&Visibility::Hidden));
        assert!(world.get_entity(foreign).is_ok());
    }

    #[test]
    fn particle_commands_require_the_exact_resident_and_a_particle_owner() {
        let mut world = World::new();
        let (root, _, _, _, foreign) = fixture(&mut world);
        let mut scenes = world.resource_mut::<SourceSceneResidents>();
        assert!(scenes.contains(root));
        assert!(!scenes.contains(foreign));
        assert!(
            scenes
                .push_particle_event(foreign, SourceEmissionEvent::Play)
                .is_err()
        );
        assert!(
            scenes
                .push_particle_event(root, SourceEmissionEvent::Play)
                .is_err()
        );
    }

    #[test]
    fn shared_resident_forwards_ordered_particle_events_once_per_scene_tick() {
        use crate::source_particle_compute::SourceParticleSceneOwner;
        use crate::source_particle_host::{HostedSourceParticle, tests as particle_tests};

        let (mut world, particle_node, material) = particle_tests::setup(true);
        let particle = HostedSourceParticle::install(
            &mut world,
            particle_tests::prepared(),
            particle_node,
            material,
            7,
            1,
        )
        .unwrap();
        let (root, _, _, _, _) = fixture(&mut world);
        {
            let mut scenes = world.resource_mut::<SourceSceneResidents>();
            scenes
                .scenes
                .get_mut(&root)
                .unwrap()
                .host
                .particle_instances
                .push(particle);
            scenes
                .push_particle_event(root, SourceEmissionEvent::Stop)
                .unwrap();
            scenes
                .push_particle_event(root, SourceEmissionEvent::Play)
                .unwrap();
        }
        world.insert_resource(SourceClock {
            current_seconds: 1.0,
            previous_seconds: 0.875,
            delta_seconds: 0.125,
            sequence: 10,
        });

        advance_particle_inputs(&mut world);
        advance_particle_inputs(&mut world);
        let owner = world
            .get::<SourceParticleSceneOwner>(particle_node)
            .unwrap();
        assert_eq!(owner.queued_frames(), 1);
        assert_eq!(
            owner.queued_frame_events(0),
            &[SourceEmissionEvent::Stop, SourceEmissionEvent::Play]
        );

        world
            .resource_mut::<SourceSceneResidents>()
            .push_particle_event(root, SourceEmissionEvent::Stop)
            .unwrap();
        world.resource_mut::<SourceClock>().sequence = 11;
        advance_particle_inputs(&mut world);
        let owner = world
            .get::<SourceParticleSceneOwner>(particle_node)
            .unwrap();
        assert_eq!(owner.queued_frames(), 2);
        assert_eq!(owner.queued_frame_events(1), &[SourceEmissionEvent::Stop]);
        release(&mut world, root);
        assert!(
            world
                .get::<SourceParticleSceneOwner>(particle_node)
                .is_none()
        );
    }
}
