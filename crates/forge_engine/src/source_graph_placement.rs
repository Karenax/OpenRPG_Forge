//! Source data mounts under the existing common placement root, without PBR fallback.
//! Data attachment is not GPU admission: these roots remain hidden pending rendering.
use crate::source_graph_runtime;
use crate::{source_graph_context, source_graph_mount, source_graph_route};
use bevy::prelude::*;
use forge_project::zone_authoring::physics::PhysicsSceneSettings;
use std::{collections::BTreeMap, sync::Arc};

/// Explicit scene contact policy shared by all source placements in this world.
#[derive(Resource, Clone)]
pub struct SceneContactSettings(pub PhysicsSceneSettings);

impl SceneContactSettings {
    pub fn resolved(&self) -> Result<crate::source_physics_contacts::ContactSceneSettings, String> {
        self.0.validate()?;
        Ok(crate::source_physics_contacts::ContactSceneSettings {
            layer_matrix: self.0.layer_matrix,
            contact_skin_m: self.0.contact_skin_m,
            evidence: self.0.evidence.clone(),
        })
    }
}

#[derive(Component, Clone, Debug)]
pub enum Status {
    Waiting,
    MountedPendingRender,
    Refused(String),
}

#[derive(Default)]
struct OwnedPhysics {
    simple: Option<crate::source_collider_projection::ColliderProjection>,
    scene: Option<crate::source_collider_scene::ColliderSceneProjection>,
    static_nodes: Option<Vec<StaticPhysicsNode>>,
}

#[derive(Clone, Copy)]
struct StaticPhysicsNode {
    entity: Entity,
    parent: Entity,
    source_node: usize,
    source_mesh: Option<usize>,
    transform: Transform,
}
impl OwnedPhysics {
    fn clear(&mut self, world: &mut World) {
        self.static_nodes = None;
        if let Some(scene) = self.scene.take() {
            scene.clear(world);
        }
        if let Some(simple) = self.simple.take() {
            simple.clear(world);
        }
    }
}

struct Record {
    carrier: Arc<[u8]>,
    physics: Option<source_graph_mount::SourcePhysicsPayload>,
    particles: Option<source_graph_mount::SourceParticlePayload>,
    owned_nodes: Vec<Entity>,
    pose: Option<Mat4>,
    contacts: Option<PhysicsSceneSettings>,
    projected_physics: OwnedPhysics,
    shadow_layer: usize,
}
impl Record {
    fn clear_nodes(&mut self, world: &mut World, root: Entity) {
        for entity in self.owned_nodes.drain(..) {
            if entity != root && world.get_entity(entity).is_ok() {
                world.despawn(entity);
            }
        }
    }
}
#[derive(Resource, Default)]
struct Records(BTreeMap<Entity, Record>);

#[derive(Component)]
struct ManualCollisionOwner {
    placement: forge_core::EntityId,
    primitives: Vec<Entity>,
}

#[derive(Component)]
struct ManualSourcePhysicsOwner {
    placement: forge_core::EntityId,
    source: forge_core::ContentHash,
    primitives: Vec<Entity>,
}

fn clear_owned_manual_collision(world: &mut World, root: Entity) {
    let owned = world.get::<ManualCollisionOwner>(root);
    let expected = world.get::<crate::ManualPlacementCollisionExpected>(root);
    if owned.zip(expected).is_some_and(|(owned, expected)| {
        owned.placement == expected.placement && owned.primitives == expected.primitives
    }) {
        let waiver = owned.is_some_and(|owned| {
            owned.primitives.is_empty()
                && world
                    .get::<crate::ManualPlacementCollisionNotRequired>(root)
                    .is_some_and(|marker| marker.0 == owned.placement)
        });
        world.entity_mut(root).remove::<(
            ManualCollisionOwner,
            crate::ManualPlacementCollisionExpected,
            crate::ManualPlacementCollisionStatus,
        )>();
        if waiver {
            world
                .entity_mut(root)
                .remove::<crate::ManualPlacementCollisionNotRequired>();
        }
    }
    if let Ok(mut entity) = world.get_entity_mut(root) {
        entity.remove::<ManualSourcePhysicsOwner>();
    }
}

pub fn submit(
    commands: &mut Commands,
    root: Entity,
    carrier: Arc<[u8]>,
    physics: Option<source_graph_mount::SourcePhysicsPayload>,
    particles: Option<source_graph_mount::SourceParticlePayload>,
    shadow_layer: usize,
) {
    commands.queue(move |world: &mut World| {
        if world.get_entity(root).is_err() {
            return;
        }
        if world.resource::<Records>().0.contains_key(&root) {
            world.entity_mut(root).insert(Status::Refused(
                "Un host possède déjà ce root source".into(),
            ));
            return;
        }
        world.resource_mut::<Records>().0.insert(
            root,
            Record {
                carrier,
                physics,
                particles,
                owned_nodes: Vec::new(),
                pose: None,
                contacts: None,
                projected_physics: OwnedPhysics::default(),
                shadow_layer,
            },
        );
        world.entity_mut(root).insert(Status::Waiting);
    });
}

/// Release owned physical handles before common root despawning.
pub fn remove(commands: &mut Commands, root: Entity) {
    commands.queue(move |world: &mut World| {
        let record = world.resource_mut::<Records>().0.remove(&root);
        if let Some(mut record) = record {
            record.projected_physics.clear(world);
            record.clear_nodes(world, root);
        }
        source_graph_runtime::release(world, root);
        if world.get_entity(root).is_ok() {
            world.despawn(root);
        }
    });
}

type CompanionPins = (
    forge_core::ContentHash,
    forge_core::ContentHash,
    forge_core::ContentHash,
);

fn companion_matches(
    saved: Option<(u32, CompanionPins)>,
    supplied: Option<CompanionPins>,
    carrier: forge_core::ContentHash,
) -> bool {
    match (saved, supplied) {
        (None, None) => true,
        (Some((1, pins)), Some(actual)) => pins.0 == carrier && pins == actual,
        _ => false,
    }
}

// Capture only the collider ancestor chains while the source mount transaction
// is still exclusive and the freshly attached hierarchy matches the pinned GLB.
fn capture_static_physics_nodes(
    world: &World,
    root: Entity,
    nodes: &BTreeMap<usize, Entity>,
    scene: &crate::source_collider_scene::ColliderSceneProjection,
) -> Result<Vec<StaticPhysicsNode>, String> {
    let mut indices = BTreeMap::new();
    for (&index, &entity) in nodes {
        if indices.insert(entity, index).is_some() {
            return Err("Source physics node ownership is ambiguous".into());
        }
    }
    let mut snapshot = BTreeMap::<Entity, StaticPhysicsNode>::new();
    for collider in scene.observe_owned(world)? {
        let mut entity = collider.parent;
        let mut depth = 0;
        let mut seen = std::collections::BTreeSet::new();
        while entity != root {
            if !seen.insert(entity) {
                return Err("Source physics node hierarchy contains a cycle".into());
            }
            if depth >= 128 {
                return Err("Source physics node ancestry exceeds its bound".into());
            }
            if let Some(saved) = snapshot.get(&entity) {
                entity = saved.parent;
                depth += 1;
                continue;
            }
            let index = *indices
                .get(&entity)
                .ok_or("Source physics ancestor is not owned by the mounted scene")?;
            let source = world
                .get::<crate::source_graph_scene::SourceGraphNode>(entity)
                .filter(|node| node.source_node == index)
                .ok_or("Source physics ancestor identity differs")?;
            let parent = world
                .get::<ChildOf>(entity)
                .ok_or("Source physics ancestor parent is absent")?
                .parent();
            let transform = *world
                .get::<Transform>(entity)
                .ok_or("Source physics ancestor transform is absent")?;
            if !transform.to_matrix().is_finite() {
                return Err("Source physics ancestor transform is invalid".into());
            }
            snapshot.insert(
                entity,
                StaticPhysicsNode {
                    entity,
                    parent,
                    source_node: source.source_node,
                    source_mesh: source.source_mesh,
                    transform,
                },
            );
            entity = parent;
            depth += 1;
        }
    }
    Ok(snapshot.into_values().collect())
}

fn verify_static_physics_nodes(world: &World, nodes: &[StaticPhysicsNode]) -> Result<(), String> {
    for saved in nodes {
        let current = world
            .get::<crate::source_graph_scene::SourceGraphNode>(saved.entity)
            .ok_or("Source physics ancestor identity is absent")?;
        if current.source_node != saved.source_node
            || current.source_mesh != saved.source_mesh
            || world.get::<ChildOf>(saved.entity).map(ChildOf::parent) != Some(saved.parent)
            || world.get::<Transform>(saved.entity) != Some(&saved.transform)
        {
            return Err("Source physics ancestor changed since the static mount".into());
        }
    }
    Ok(())
}

/// Match the persisted declarations before mounting. The common package
/// decoders still verify the actual bytes and their embedded manifests.
fn validate_saved_companions(
    placement: Option<&forge_project::zone_authoring::placements::ManualPlacement>,
    record: &Record,
) -> Result<(), String> {
    let Some(placement) = placement else {
        return if record.physics.is_none() && record.particles.is_none() {
            Ok(())
        } else {
            Err("Source companion placement identity is absent".into())
        };
    };
    let physics = placement.physics.as_ref().map(|saved| {
        (
            saved.schema_version,
            (
                saved.carrier_sha256,
                saved.manifest_sha256,
                saved.package_sha256,
            ),
        )
    });
    let supplied_physics = record.physics.as_ref().map(|payload| {
        (
            payload.carrier_sha256,
            payload.manifest_sha256,
            payload.package_sha256,
        )
    });
    if !companion_matches(physics, supplied_physics, placement.asset_hash) {
        return Err("Source physics companion pins differ from the saved placement".into());
    }
    let particles = placement.particles.as_ref().map(|saved| {
        (
            saved.schema_version,
            (
                saved.carrier_sha256,
                saved.manifest_sha256,
                saved.package_sha256,
            ),
        )
    });
    let supplied_particles = record.particles.as_ref().map(|payload| {
        (
            payload.carrier_sha256,
            payload.manifest_sha256,
            payload.package_sha256,
        )
    });
    if !companion_matches(particles, supplied_particles, placement.asset_hash) {
        return Err("Source particle companion pins differ from the saved placement".into());
    }
    Ok(())
}

fn mount(
    world: &mut World,
    root: Entity,
    record: &Record,
    pose: Mat4,
    contacts: Option<&PhysicsSceneSettings>,
) -> Result<(Vec<Entity>, OwnedPhysics), String> {
    validate_saved_companions(
        world
            .get::<crate::ManualPlacementProjection>(root)
            .map(|projection| &projection.0),
        record,
    )?;
    let glb =
        source_graph_route::read(&record.carrier)?.ok_or("Placement sans déclaration source")?;
    source_graph_context::ObjectBasis::from_document(glb.document())?
        .ok_or("Conversion source déclarée absente")?;
    // An object-local bake/normalization is not a scene-world coordinate policy.
    // Persisted roots require declared world mappings; no preview offset/default.
    let policy = source_graph_runtime::RuntimePolicy {
        source_world_from_target_world: source_graph_context::declared_world_mapping(
            glb.document(),
            "source_world_from_target_world",
        )?,
        source_absolute_world_from_target_world: source_graph_context::declared_world_mapping(
            glb.document(),
            "source_absolute_world_from_target_world",
        )?,
        global_mip_bias: 0.,
        source_float_min: f32::MIN_POSITIVE,
    };
    let clock = world
        .get_resource::<source_graph_runtime::SourceClock>()
        .copied()
        .ok_or("Horloge source explicite absente")?;
    let settings = if record.physics.is_some() {
        let settings = contacts.ok_or("Paramètres physiques acceptés absents")?;
        settings.validate()?;
        Some(SceneContactSettings(settings.clone()).resolved()?)
    } else {
        None
    };
    let frame = policy.frame(clock)?;
    let mut host = source_graph_mount::mount(
        world,
        source_graph_mount::SourceSceneMount {
            root,
            carrier: &record.carrier,
            selected_scene: None,
            target_world_from_root: pose,
            frame: &frame,
            ordinary: BTreeMap::new(),
            physics: record.physics.as_ref(),
            particles: record.particles.as_ref(),
            contacts: settings.as_ref(),
            shadow_layer: record.shadow_layer,
        },
    )?
    .ok_or("Déclaration source absente au montage")?;
    let owned = host
        .attached
        .nodes
        .values()
        .copied()
        .chain(host.attached.primitives.iter().copied())
        .collect::<Vec<_>>();
    // This placement adapter owns physical records separately so contact/pose
    // replacement and a vanished root can release them even after reparenting.
    let mut physics = OwnedPhysics {
        simple: host.collider_projection.take(),
        scene: host.collider_scene_projection.take(),
        static_nodes: None,
    };
    if record.physics.is_some() {
        let snapshot = physics
            .scene
            .as_ref()
            .ok_or_else(|| "Source physics scene projection is absent".to_owned())
            .and_then(|scene| {
                capture_static_physics_nodes(world, root, &host.attached.nodes, scene)
            });
        match snapshot {
            Ok(nodes) => physics.static_nodes = Some(nodes),
            Err(error) => {
                physics.clear(world);
                host.rollback_unpublished(world);
                return Err(error);
            }
        }
    }
    if let Err(failure) = source_graph_runtime::retain(world, host, policy) {
        physics.clear(world);
        failure.host.rollback_unpublished(world);
        return Err(failure.reason);
    }
    Ok((owned, physics))
}

fn current_pose(world: &World, root: Entity) -> Result<Mat4, String> {
    let mut node = Some(root);
    let mut seen = std::collections::BTreeSet::new();
    let mut pose = Mat4::IDENTITY;
    while let Some(entity) = node {
        if !seen.insert(entity) {
            return Err("Cycle dans les parents du placement source".into());
        }
        let local = world
            .get::<Transform>(entity)
            .ok_or("Transformation source absente")?
            .to_matrix();
        pose = local * pose;
        node = world.get::<ChildOf>(entity).map(ChildOf::parent);
    }
    if !pose.is_finite() || !pose.determinant().is_finite() || pose.determinant() == 0. {
        return Err("Pose source non finie ou non inversible".into());
    }
    Ok(pose)
}

fn refuse_record(world: &mut World, root: Entity, record: &mut Record, error: String) {
    clear_owned_manual_collision(world, root);
    record.projected_physics.clear(world);
    source_graph_runtime::release(world, root);
    record.clear_nodes(world, root);
    record.pose = None;
    world
        .entity_mut(root)
        .remove::<crate::source_graph_scene_attachment::SourceSceneAttached>()
        .insert((Visibility::Hidden, Status::Refused(error)));
}

/// Rebuild only owned derived nodes when author pose or accepted contacts change.
/// Common root, persisted identity, selection and editor components are retained.
pub fn refresh(world: &mut World) {
    let contacts = world
        .get_resource::<SceneContactSettings>()
        .map(|settings| settings.0.clone());
    world.resource_scope(|world, mut records: Mut<Records>| {
        records.0.retain(|&root, record| {
            if world.get_entity(root).is_err() {
                record.projected_physics.clear(world);
                source_graph_runtime::release(world, root);
                record.clear_nodes(world, root);
                return false;
            }
            let pose = match current_pose(world, root) {
                Ok(pose) => pose,
                Err(error) => {
                    refuse_record(world, root, record, error);
                    return true;
                }
            };
            if let Err(error) = validate_saved_companions(
                world
                    .get::<crate::ManualPlacementProjection>(root)
                    .map(|projection| &projection.0),
                record,
            ) {
                let unchanged_refusal = record.pose.is_none()
                    && matches!(world.get::<Status>(root), Some(Status::Refused(previous)) if previous == &error)
                    && world.get::<Visibility>(root) == Some(&Visibility::Hidden);
                if !unchanged_refusal {
                    refuse_record(world, root, record, error);
                }
                return true;
            }
            let should_rebuild = record.pose != Some(pose)
                || record.physics.is_some() && record.contacts != contacts;
            if !should_rebuild {
                return true;
            }
            clear_owned_manual_collision(world, root);
            record.projected_physics.clear(world);
            source_graph_runtime::release(world, root);
            record.clear_nodes(world, root);
            world
                .entity_mut(root)
                .remove::<crate::source_graph_scene_attachment::SourceSceneAttached>();
            world
                .entity_mut(root)
                .insert((Visibility::Hidden, GlobalTransform::from(pose)));
            record.pose = Some(pose);
            record.contacts.clone_from(&contacts);
            let status = match mount(world, root, record, pose, contacts.as_ref()) {
                Ok((nodes, physics)) => {
                    record.owned_nodes = nodes;
                    record.projected_physics = physics;
                    Status::MountedPendingRender
                }
                Err(error) => Status::Refused(error),
            };
            world.entity_mut(root).insert(status);
            true
        });
    });
}

/// Initialize placement ownership; the host schedules refresh after its own
/// placement projection, transform propagation and source frame update.
pub fn initialize(app: &mut App) {
    app.init_resource::<Records>();
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum ManualAdmission {
    Pending,
    Attached,
}

fn source_primitives(
    world: &World,
    root: Entity,
    source: forge_core::ContentHash,
) -> Result<&[Entity], String> {
    let primitives = world
        .get_resource::<source_graph_runtime::SourceSceneResidents>()
        .and_then(|residents| residents.primitives_for_carrier(root, source))
        .filter(|items| {
            !items.is_empty()
                && items
                    .iter()
                    .all(|entity| world.get::<Mesh3d>(*entity).is_some())
        })
        .ok_or("Source placement pin or admitted primitives differ")?;
    if primitives.iter().any(|entity| {
        world
            .get::<bevy_rapier3d::prelude::Collider>(*entity)
            .is_some()
            || world
                .get::<crate::ManualPlacementCollision>(*entity)
                .is_some()
    }) {
        return Err("Source visual primitive must not own a generated collider".into());
    }
    Ok(primitives)
}

/// Observe only the physical source batch attached to the exact saved placement.
/// `None` means a mount, frame, or physical replacement is still in progress.
/// Active shapes are counted only after the current static Rapier backend and
/// contact settings match. Disabled and unassigned declarations stay separate.
pub fn observe_manual_physics(
    world: &World,
    root: Entity,
) -> Result<Option<crate::source_collider_scene::ColliderBackendObservation>, String> {
    use crate::source_graph_runtime::SourceFrameStatus;

    match world.get::<Status>(root) {
        Some(Status::Refused(reason)) => return Err(format!("Source mount refused: {reason}")),
        Some(Status::MountedPendingRender) => {}
        Some(Status::Waiting) | None => return Ok(None),
    }
    match world.get::<SourceFrameStatus>(root) {
        Some(SourceFrameStatus::Refused(reason)) => {
            return Err(format!("Source frame refused: {reason}"));
        }
        Some(SourceFrameStatus::Updated) => {}
        None => return Ok(None),
    }
    let record = world
        .get_resource::<Records>()
        .and_then(|records| records.0.get(&root))
        .ok_or("Source placement owner is absent")?;
    let placement = world
        .get::<crate::ManualPlacementProjection>(root)
        .ok_or("Source placement identity is absent")?;
    validate_saved_companions(Some(&placement.0), record)?;
    if record.physics.is_none() {
        return Err("Source physics companion is absent".into());
    }
    if crate::static_hlod::moving_object_retained(world, placement.0.id) {
        return Err("Source physics on moving nodes is not qualified".into());
    }
    let contacts = world
        .get_resource::<SceneContactSettings>()
        .ok_or("Source physics scene settings are absent")?;
    contacts.0.validate()?;
    if record.pose != Some(current_pose(world, root)?)
        || record.contacts.as_ref() != Some(&contacts.0)
    {
        return Ok(None);
    }
    source_primitives(world, root, placement.0.asset_hash)?;
    if record.projected_physics.simple.is_some() {
        return Err("Source physics owns an unexpected simple projection".into());
    }
    let projection = record
        .projected_physics
        .scene
        .as_ref()
        .ok_or("Source physics scene projection is absent")?;
    let nodes = record
        .projected_physics
        .static_nodes
        .as_ref()
        .ok_or("Static source physics node snapshot is absent")?;
    verify_static_physics_nodes(world, nodes)?;
    let observed = projection.observe_backend(world)?;
    if observed.active_solids == 0 && observed.active_sensors == 0 {
        return Err("Source physics has no active assigned shape in this qualification".into());
    }
    Ok(Some(observed))
}

fn source_owner_matches(world: &World, root: Entity) -> bool {
    if world.get::<ManualCollisionOwner>(root).is_some()
        || world
            .get::<crate::ManualPlacementCollisionExpected>(root)
            .is_some()
    {
        return false;
    }
    let Some(owner) = world.get::<ManualSourcePhysicsOwner>(root) else {
        return false;
    };
    let Some(placement) = world.get::<crate::ManualPlacementProjection>(root) else {
        return false;
    };
    if owner.placement != placement.0.id || owner.source != placement.0.asset_hash {
        return false;
    }
    let Ok(primitives) = source_primitives(world, root, owner.source) else {
        return false;
    };
    owner.primitives == primitives
        && primitives.iter().all(|entity| {
            world
                .get::<crate::ManualPlacementPart>(*entity)
                .is_some_and(|part| part.0 == owner.placement)
                && world.get::<crate::static_hlod::StaticHlodSource>(*entity)
                    == Some(&crate::static_hlod::StaticHlodSource {
                        object: owner.placement,
                        source: owner.source,
                    })
        })
}

pub(crate) fn manual_physics_ready(world: &World, root: Entity) -> bool {
    source_owner_matches(world, root)
        && matches!(observe_manual_physics(world, root), Ok(Some(status)) if status.is_ready())
}

fn admit_source_physics(world: &mut World, root: Entity) -> Result<ManualAdmission, String> {
    let Some(observation) = observe_manual_physics(world, root)? else {
        return Ok(ManualAdmission::Pending);
    };
    if !observation.is_ready() {
        return Ok(ManualAdmission::Pending);
    }
    if world.get::<ManualSourcePhysicsOwner>(root).is_some() {
        return if source_owner_matches(world, root) {
            Ok(ManualAdmission::Attached)
        } else {
            Err("Source physical placement ownership changed".into())
        };
    }
    if world.get::<ManualCollisionOwner>(root).is_some()
        || world
            .get::<crate::ManualPlacementCollisionExpected>(root)
            .is_some()
    {
        return Err("Source physical placement has a foreign collision contract".into());
    }
    let placement = &world
        .get::<crate::ManualPlacementProjection>(root)
        .ok_or("Source placement identity is absent")?
        .0;
    let id = placement.id;
    let source = placement.asset_hash;
    let primitives = source_primitives(world, root, source)?.to_vec();
    if primitives.iter().any(|entity| {
        world.get::<crate::ManualPlacementPart>(*entity).is_some()
            || world
                .get::<crate::static_hlod::StaticHlodSource>(*entity)
                .is_some()
    }) {
        return Err("Source visual primitive already belongs to another adapter".into());
    }
    for entity in &primitives {
        world.entity_mut(*entity).insert((
            crate::ManualPlacementPart(id),
            crate::static_hlod::StaticHlodSource { object: id, source },
        ));
    }
    world.entity_mut(root).insert(ManualSourcePhysicsOwner {
        placement: id,
        source,
        primitives,
    });
    Ok(ManualAdmission::Attached)
}

/// Attach the existing manual-placement collision contract to exactly the
/// primitives owned by a source scene. Visibility remains the host's decision.
pub fn admit_manual(world: &mut World, root: Entity) -> Result<ManualAdmission, String> {
    use crate::source_graph_runtime::{SourceFrameStatus, SourceSceneResidents};

    match world.get::<Status>(root) {
        Some(Status::Refused(reason)) => return Err(format!("Source mount refused: {reason}")),
        Some(Status::MountedPendingRender) => {}
        Some(Status::Waiting) | None => return Ok(ManualAdmission::Pending),
    }
    match world.get::<SourceFrameStatus>(root) {
        Some(SourceFrameStatus::Refused(reason)) => {
            return Err(format!("Source frame refused: {reason}"));
        }
        Some(SourceFrameStatus::Updated) => {}
        None => return Ok(ManualAdmission::Pending),
    }
    let record = world
        .get_resource::<Records>()
        .and_then(|records| records.0.get(&root))
        .ok_or("Source placement owner is absent")?;
    let placement = world
        .get::<crate::ManualPlacementProjection>(root)
        .ok_or("Source placement identity is absent")?;
    validate_saved_companions(Some(&placement.0), record)?;
    let particles = if record.particles.is_some() {
        let observed = world
            .get_resource::<SourceSceneResidents>()
            .ok_or("Particle source scene absent")?
            .observe_particles(world, root, placement.0.asset_hash)?
            .ok_or("Saved particle companion was not mounted")?;
        if !observed.current() {
            return Ok(ManualAdmission::Pending);
        }
        Some(observed)
    } else {
        None
    };
    if record.physics.is_some() {
        return admit_source_physics(world, root);
    }
    let (id, source) = world
        .get::<crate::ManualPlacementProjection>(root)
        .map(|placement| (placement.0.id, placement.0.asset_hash))
        .ok_or("Source placement identity is absent")?;
    admit_manual_primitives(world, root, id, source, particles.as_ref())
}

fn admit_manual_primitives(
    world: &mut World,
    root: Entity,
    id: forge_core::EntityId,
    source: forge_core::ContentHash,
    particles: Option<&source_graph_runtime::SourceSceneParticleObservation>,
) -> Result<ManualAdmission, String> {
    let primitives = world
        .get_resource::<source_graph_runtime::SourceSceneResidents>()
        .and_then(|residents| residents.primitives_for_carrier(root, source))
        .filter(|items| {
            !items.is_empty()
                && items
                    .iter()
                    .all(|entity| world.get::<Mesh3d>(*entity).is_some())
        })
        .ok_or("Source placement pin or admitted primitives differ")?;
    let owner = world.get::<ManualCollisionOwner>(root);
    let collision_primitives: Vec<_> = primitives
        .iter()
        .copied()
        .filter(|entity| {
            particles
                .as_ref()
                .is_none_or(|observed| !observed.primitive_entities.contains(entity))
        })
        .collect();
    let waived = particles.is_some() && collision_primitives.is_empty();
    if let Some(expected) = world.get::<crate::ManualPlacementCollisionExpected>(root) {
        if expected.placement != id
            || expected.primitives != collision_primitives
            || owner.is_none_or(|owner| {
                owner.placement != id || owner.primitives != collision_primitives
            })
            || world
                .get::<crate::ManualPlacementCollisionNotRequired>(root)
                .map(|marker| marker.0)
                != waived.then_some(id)
        {
            return Err("Source placement collision ownership changed".into());
        }
        return Ok(ManualAdmission::Attached);
    }
    if owner.is_some() {
        return Err("Source placement collision expectation is absent".into());
    }
    let primitives = primitives.to_vec();
    for entity in &primitives {
        world.entity_mut(*entity).insert((
            crate::ManualPlacementPart(id),
            crate::static_hlod::StaticHlodSource { object: id, source },
        ));
    }
    for entity in &collision_primitives {
        world.entity_mut(*entity).insert((
            crate::ManualPlacementCollision(id),
            crate::ManualPlacementCollisionGeometry::TriangleMesh,
        ));
    }
    if waived {
        world
            .entity_mut(root)
            .insert(crate::ManualPlacementCollisionNotRequired(id));
    }
    world.entity_mut(root).insert((
        crate::ManualPlacementCollisionExpected {
            placement: id,
            primitives: collision_primitives.clone(),
        },
        ManualCollisionOwner {
            placement: id,
            primitives: collision_primitives,
        },
    ));
    Ok(ManualAdmission::Attached)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::source_collider_projection::{ColliderProjection, SourceColliderIdentity};
    use forge_core::{AssetId, EntityId, bytes_sha256};
    use forge_project::zone_authoring::placements::{
        ManualPlacement, ParticlePackageReference, PhysicsPackageReference,
    };

    fn companion_fixture() -> (ManualPlacement, Record) {
        let carrier: Arc<[u8]> = Arc::from(&b"source carrier"[..]);
        let carrier_sha256 = bytes_sha256(&carrier);
        let manifest = br#"{"rows":[]}"#.to_vec();
        let manifest_sha256 = bytes_sha256(&manifest);
        let physics_wire = forge_assets::source_physics_package::SourcePhysicsPackage {
            carrier_sha256,
            manifest_sha256,
            manifest: manifest.clone(),
            candidates: BTreeMap::new(),
        }
        .encode()
        .unwrap();
        let particle_wire = forge_assets::source_particle_package::SourceParticlePackage {
            carrier_sha256,
            manifest_sha256,
            manifest,
            resources: BTreeMap::new(),
        }
        .encode()
        .unwrap();
        let physics_hash = bytes_sha256(&physics_wire);
        let particle_hash = bytes_sha256(&particle_wire);
        let placement = ManualPlacement {
            id: EntityId::deterministic("source-companion-placement"),
            name: "source".into(),
            asset_id: AssetId::deterministic("source-companion-carrier"),
            asset_hash: carrier_sha256,
            physics: Some(Box::new(PhysicsPackageReference {
                schema_version: 1,
                carrier_sha256,
                manifest_sha256,
                package_sha256: physics_hash,
            })),
            particles: Some(Box::new(ParticlePackageReference {
                schema_version: 1,
                carrier_sha256,
                manifest_sha256,
                package_sha256: particle_hash,
            })),
            local_lights: None,
            transform: forge_core::Transform::IDENTITY,
            locked: false,
        };
        let record = Record {
            carrier,
            physics: Some(source_graph_mount::SourcePhysicsPayload {
                wire: physics_wire.into(),
                carrier_sha256,
                manifest_sha256,
                package_sha256: physics_hash,
            }),
            particles: Some(source_graph_mount::SourceParticlePayload {
                wire: particle_wire.into(),
                carrier_sha256,
                manifest_sha256,
                package_sha256: particle_hash,
            }),
            owned_nodes: Vec::new(),
            pose: None,
            contacts: None,
            projected_physics: OwnedPhysics::default(),
            shadow_layer: 31,
        };
        (placement, record)
    }

    #[test]
    fn saved_companions_require_exact_versions_and_all_pins() {
        let (placement, record) = companion_fixture();
        assert!(validate_saved_companions(Some(&placement), &record).is_ok());
        let different = bytes_sha256(b"different");
        for field in 0..4 {
            let mut changed = placement.clone();
            let physics = changed.physics.as_mut().unwrap();
            match field {
                0 => physics.schema_version = 2,
                1 => physics.carrier_sha256 = different,
                2 => physics.manifest_sha256 = different,
                _ => physics.package_sha256 = different,
            }
            assert!(
                validate_saved_companions(Some(&changed), &record)
                    .unwrap_err()
                    .contains("physics")
            );
            let mut changed = placement.clone();
            let particles = changed.particles.as_mut().unwrap();
            match field {
                0 => particles.schema_version = 2,
                1 => particles.carrier_sha256 = different,
                2 => particles.manifest_sha256 = different,
                _ => particles.package_sha256 = different,
            }
            assert!(
                validate_saved_companions(Some(&changed), &record)
                    .unwrap_err()
                    .contains("particle")
            );
        }
    }

    #[test]
    fn saved_companions_reject_missing_and_extra_payloads() {
        let (placement, mut record) = companion_fixture();
        let physics = record.physics.take();
        assert!(validate_saved_companions(Some(&placement), &record).is_err());
        record.physics = physics;
        let particles = record.particles.take();
        assert!(validate_saved_companions(Some(&placement), &record).is_err());
        record.particles = particles;
        let mut without_physics = placement.clone();
        without_physics.physics = None;
        assert!(validate_saved_companions(Some(&without_physics), &record).is_err());
        let mut without_particles = placement;
        without_particles.particles = None;
        assert!(validate_saved_companions(Some(&without_particles), &record).is_err());
        assert!(validate_saved_companions(None, &record).is_err());
        record.physics = None;
        record.particles = None;
        without_particles.physics = None;
        assert!(validate_saved_companions(Some(&without_particles), &record).is_ok());
        assert!(validate_saved_companions(None, &record).is_ok());
    }

    #[test]
    fn companion_decoders_still_refuse_bytes_changed_after_pin_matching() {
        let (placement, mut record) = companion_fixture();
        let physics = record.physics.as_ref().unwrap();
        assert!(physics.decode_for_carrier(placement.asset_hash).is_ok());
        let particle = record.particles.as_ref().unwrap();
        let decode_particle = |payload: &source_graph_mount::SourceParticlePayload| {
            forge_assets::source_particle_package::SourceParticlePackage::decode_pinned(
                &payload.wire,
                payload.carrier_sha256,
                payload.manifest_sha256,
                payload.package_sha256,
            )
        };
        assert!(decode_particle(particle).is_ok());
        let physics = record.physics.as_mut().unwrap();
        let mut changed = physics.wire.to_vec();
        changed[0] ^= 1;
        physics.wire = changed.into();
        let particle = record.particles.as_mut().unwrap();
        let mut changed = particle.wire.to_vec();
        changed[0] ^= 1;
        particle.wire = changed.into();
        assert!(validate_saved_companions(Some(&placement), &record).is_ok());
        assert!(
            record
                .physics
                .as_ref()
                .unwrap()
                .decode_for_carrier(placement.asset_hash)
                .is_err()
        );
        assert!(decode_particle(record.particles.as_ref().unwrap()).is_err());
    }

    #[test]
    fn refresh_refuses_mismatched_companions_before_reading_or_mounting_carrier() {
        let (placement, mut record) = companion_fixture();
        record.physics = None;
        // This is deliberately not a GLB: the saved-pin refusal must come first.
        let mut world = World::new();
        world.init_resource::<Records>();
        let root = world
            .spawn((
                Transform::IDENTITY,
                Visibility::Inherited,
                crate::ManualPlacementProjection(placement),
                Name::new("persisted identity"),
            ))
            .id();
        let unrelated = world.spawn(Name::new("unrelated")).id();
        world.resource_mut::<Records>().0.insert(root, record);
        let count = world.entities().len();
        refresh(&mut world);
        assert!(matches!(
            world.get::<Status>(root),
            Some(Status::Refused(reason)) if reason.contains("physics companion pins")
        ));
        assert_eq!(world.get::<Visibility>(root), Some(&Visibility::Hidden));
        assert_eq!(
            world.get::<Name>(root).unwrap().as_str(),
            "persisted identity"
        );
        assert!(world.get_entity(unrelated).is_ok());
        assert_eq!(world.entities().len(), count);
        assert!(
            world
                .get::<crate::source_graph_scene_attachment::SourceSceneAttached>(root)
                .is_none()
        );
    }

    fn fixture(world: &mut World) -> (Entity, Entity, Entity, Entity) {
        world.init_resource::<Records>();
        let root = world
            .spawn((Transform::IDENTITY, Name::new("author identity")))
            .id();
        // Detached children still belong to the placement's explicit records.
        let node = world.spawn_empty().id();
        let physical = world
            .spawn(SourceColliderIdentity {
                placement_root: root,
                guid: "owned".into(),
                file_id: "1".into(),
                original: serde_json::json!({}),
                policy_evidence: "fixture".into(),
            })
            .id();
        let other = world.spawn_empty().id();
        let foreign = world
            .spawn(SourceColliderIdentity {
                placement_root: other,
                guid: "foreign".into(),
                file_id: "2".into(),
                original: serde_json::json!({}),
                policy_evidence: "fixture".into(),
            })
            .id();
        world.resource_mut::<Records>().0.insert(
            root,
            Record {
                carrier: Arc::from([]),
                physics: None,
                particles: None,
                owned_nodes: vec![node],
                pose: Some(Mat4::IDENTITY),
                contacts: None,
                shadow_layer: 31,
                projected_physics: OwnedPhysics {
                    simple: Some(ColliderProjection {
                        root,
                        entities: vec![physical, foreign],
                    }),
                    scene: None,
                    static_nodes: None,
                },
            },
        );
        (root, node, physical, foreign)
    }

    fn mounted_fixture(
        world: &mut World,
        with_companions: bool,
    ) -> (Entity, Entity, Entity, Entity, ManualPlacement) {
        let (root, node, physical, foreign) = fixture(world);
        let (mut placement, mut supplied) = companion_fixture();
        let original = placement.clone();
        if with_companions {
            let mut records = world.resource_mut::<Records>();
            let record = records.0.get_mut(&root).unwrap();
            record.physics = supplied.physics.take();
            record.particles = supplied.particles.take();
        } else {
            placement.physics = None;
            placement.particles = None;
        }
        let placement_id = placement.id;
        world.entity_mut(root).insert((
            crate::ManualPlacementProjection(placement),
            Visibility::Inherited,
            Status::MountedPendingRender,
            crate::source_graph_scene_attachment::SourceSceneAttached,
            ManualCollisionOwner {
                placement: placement_id,
                primitives: vec![node],
            },
            crate::ManualPlacementCollisionExpected {
                placement: placement_id,
                primitives: vec![node],
            },
            crate::ManualPlacementCollisionStatus::Registered { primitives: 1 },
        ));
        (root, node, physical, foreign, original)
    }

    #[test]
    fn changed_companions_revoke_only_owned_state_without_pose_change() {
        for (with_companions, particles) in
            [(false, false), (false, true), (true, false), (true, true)]
        {
            let mut world = World::new();
            let (root, node, physical, foreign, original) =
                mounted_fixture(&mut world, with_companions);
            refresh(&mut world);
            assert!(world.get_entity(node).is_ok());
            assert!(world.get_entity(physical).is_ok());
            assert_eq!(world.get::<Visibility>(root), Some(&Visibility::Inherited));

            {
                let mut projection = world
                    .get_mut::<crate::ManualPlacementProjection>(root)
                    .unwrap();
                if particles {
                    projection.0.particles =
                        (!with_companions).then(|| original.particles.clone().unwrap());
                } else {
                    projection.0.physics =
                        (!with_companions).then(|| original.physics.clone().unwrap());
                }
            }
            let kind = if particles { "particle" } else { "physics" };
            let reason_fragment = format!("{kind} companion pins");
            refresh(&mut world);
            assert!(matches!(
                world.get::<Status>(root),
                Some(Status::Refused(reason)) if reason.contains(reason_fragment.as_str())
            ));
            assert_eq!(world.get::<Visibility>(root), Some(&Visibility::Hidden));
            assert_eq!(world.get::<Name>(root).unwrap().as_str(), "author identity");
            assert!(world.get_entity(node).is_err());
            assert!(world.get_entity(physical).is_err());
            assert!(world.get_entity(foreign).is_ok());
            assert!(world.resource::<Records>().0[&root].pose.is_none());
            assert!(world.get::<ManualCollisionOwner>(root).is_none());
            assert!(
                world
                    .get::<crate::ManualPlacementCollisionExpected>(root)
                    .is_none()
            );
            assert!(
                world
                    .get::<crate::ManualPlacementCollisionStatus>(root)
                    .is_none()
            );
            assert!(
                world
                    .get::<crate::source_graph_scene_attachment::SourceSceneAttached>(root)
                    .is_none()
            );

            refresh(&mut world);
            assert!(matches!(
                world.get::<Status>(root),
                Some(Status::Refused(reason)) if reason.contains(reason_fragment.as_str())
            ));
            assert!(world.get_entity(foreign).is_ok());
            {
                let mut projection = world
                    .get_mut::<crate::ManualPlacementProjection>(root)
                    .unwrap();
                if particles {
                    projection.0.particles = with_companions.then_some(original.particles.unwrap());
                } else {
                    projection.0.physics = with_companions.then_some(original.physics.unwrap());
                }
            }
            refresh(&mut world);
            // The invalid fixture carrier now fails parsing: the repaired binding
            // reached a fresh mount attempt instead of keeping the old refusal.
            assert!(world.resource::<Records>().0[&root].pose.is_some());
            assert!(matches!(
                world.get::<Status>(root),
                Some(Status::Refused(reason)) if !reason.contains(reason_fragment.as_str())
            ));
            assert!(world.get_entity(foreign).is_ok());
        }
    }

    #[test]
    fn independent_callers_keep_their_own_roots_and_shadow_layers() {
        let mut app = App::new();
        initialize(&mut app);
        let world = app.world_mut();
        let first = world.spawn_empty().id();
        let second = world.spawn_empty().id();
        let mut queue = bevy::ecs::world::CommandQueue::default();
        {
            let mut commands = Commands::new(&mut queue, world);
            submit(
                &mut commands,
                first,
                Arc::from(&b"first"[..]),
                None,
                None,
                3,
            );
            submit(
                &mut commands,
                second,
                Arc::from(&b"second"[..]),
                None,
                None,
                17,
            );
        }
        queue.apply(world);
        assert_eq!(world.resource::<Records>().0[&first].shadow_layer, 3);
        assert_eq!(world.resource::<Records>().0[&second].shadow_layer, 17);
        {
            let mut commands = Commands::new(&mut queue, world);
            submit(
                &mut commands,
                first,
                Arc::from(&b"duplicate"[..]),
                None,
                None,
                8,
            );
        }
        queue.apply(world);
        assert!(matches!(
            world.get::<Status>(first),
            Some(Status::Refused(_))
        ));
        assert!(matches!(world.get::<Status>(second), Some(Status::Waiting)));
        assert_eq!(world.resource::<Records>().0[&first].shadow_layer, 3);
        {
            let mut commands = Commands::new(&mut queue, world);
            remove(&mut commands, first);
        }
        queue.apply(world);
        assert!(world.get_entity(first).is_err());
        assert!(world.get_entity(second).is_ok());
        assert!(world.resource::<Records>().0.contains_key(&second));
    }

    #[test]
    fn vanished_root_cleans_detached_owned_nodes_and_physics_only() {
        let mut world = World::new();
        let (root, node, physical, foreign) = fixture(&mut world);
        world.despawn(root);
        refresh(&mut world);
        assert!(world.get_entity(node).is_err());
        assert!(world.get_entity(physical).is_err());
        assert!(world.get_entity(foreign).is_ok());
        assert!(world.resource::<Records>().0.is_empty());
        refresh(&mut world);
        assert!(world.get_entity(foreign).is_ok());
    }

    #[test]
    fn invalid_pose_clears_derivatives_preserves_author_identity_and_refuses() {
        let mut world = World::new();
        let (root, node, physical, foreign) = fixture(&mut world);
        let placement = forge_core::EntityId::deterministic("owned-source-collision");
        world.entity_mut(root).insert((
            ManualCollisionOwner {
                placement,
                primitives: vec![node],
            },
            crate::ManualPlacementCollisionExpected {
                placement,
                primitives: vec![node],
            },
            crate::ManualPlacementCollisionStatus::Registered { primitives: 1 },
        ));
        world.get_mut::<Transform>(root).unwrap().scale = Vec3::ZERO;
        refresh(&mut world);
        assert!(world.get_entity(node).is_err());
        assert!(world.get_entity(physical).is_err());
        assert!(world.get_entity(foreign).is_ok());
        assert_eq!(world.get::<Name>(root).unwrap().as_str(), "author identity");
        assert!(matches!(
            world.get::<Status>(root),
            Some(Status::Refused(_))
        ));
        assert_eq!(*world.get::<Visibility>(root).unwrap(), Visibility::Hidden);
        assert!(world.resource::<Records>().0[&root].pose.is_none());
        assert!(world.get::<ManualCollisionOwner>(root).is_none());
        assert!(
            world
                .get::<crate::ManualPlacementCollisionExpected>(root)
                .is_none()
        );
        assert!(
            world
                .get::<crate::ManualPlacementCollisionStatus>(root)
                .is_none()
        );
    }

    #[test]
    fn rebuild_does_not_remove_a_collision_contract_owned_by_another_adapter() {
        let mut world = World::new();
        let (root, _, _, _) = fixture(&mut world);
        let placement = forge_core::EntityId::deterministic("foreign-collision");
        world
            .entity_mut(root)
            .insert(crate::ManualPlacementCollisionExpected {
                placement,
                primitives: vec![],
            });
        world.get_mut::<Transform>(root).unwrap().scale = Vec3::ZERO;
        refresh(&mut world);
        assert_eq!(
            world
                .get::<crate::ManualPlacementCollisionExpected>(root)
                .unwrap()
                .placement,
            placement
        );
    }

    #[test]
    fn manual_mesh_admission_cannot_bypass_source_ownership_for_companions() {
        use crate::source_graph_runtime::SourceFrameStatus;

        for physics in [true, false] {
            let mut world = World::new();
            let (root, _, _, _) = fixture(&mut world);
            world
                .entity_mut(root)
                .insert((Status::MountedPendingRender, SourceFrameStatus::Updated));
            let hash = forge_core::bytes_sha256(b"companion");
            {
                let mut records = world.resource_mut::<Records>();
                let record = records.0.get_mut(&root).unwrap();
                if physics {
                    record.physics = Some(source_graph_mount::SourcePhysicsPayload {
                        wire: Arc::from([]),
                        carrier_sha256: hash,
                        manifest_sha256: hash,
                        package_sha256: hash,
                    });
                } else {
                    record.particles = Some(source_graph_mount::SourceParticlePayload {
                        wire: Arc::from([]),
                        carrier_sha256: hash,
                        manifest_sha256: hash,
                        package_sha256: hash,
                    });
                }
            }
            let reason = admit_manual(&mut world, root).unwrap_err();
            assert_eq!(reason, "Source placement identity is absent");
            assert!(
                world
                    .get::<crate::ManualPlacementCollisionExpected>(root)
                    .is_none()
            );
        }
    }

    fn mounted_physics_fixture() -> (App, Entity, Entity, ManualPlacement) {
        use bevy_rapier3d::prelude::{NoUserData, RapierPhysicsPlugin};

        let mut app = App::new();
        app.add_plugins((
            MinimalPlugins,
            TransformPlugin,
            RapierPhysicsPlugin::<NoUserData>::default().in_fixed_schedule(),
        ));
        let world = app.world_mut();
        world.init_resource::<Records>();
        let (root, node, physical) = crate::source_collider_scene::tests::attach_fixture(world);
        let draw = world
            .spawn((
                ChildOf(node),
                Transform::IDENTITY,
                Mesh3d(Handle::default()),
            ))
            .id();
        let (mut placement, mut record) = companion_fixture();
        placement.particles = None;
        record.particles = None;
        record.pose = Some(Mat4::IDENTITY);
        let contacts = PhysicsSceneSettings {
            schema_version: 1,
            layer_matrix: [u32::MAX; 32],
            contact_skin_m: 0.01,
            evidence: "synthetic scene settings".into(),
        };
        record.contacts = Some(contacts.clone());
        let static_nodes =
            capture_static_physics_nodes(world, root, &BTreeMap::from([(0, node)]), &physical)
                .unwrap();
        record.owned_nodes = vec![node, draw];
        record.projected_physics = OwnedPhysics {
            simple: None,
            scene: Some(physical),
            static_nodes: Some(static_nodes),
        };
        world.insert_resource(SceneContactSettings(contacts));
        world.entity_mut(root).insert((
            crate::ManualPlacementProjection(placement.clone()),
            Status::MountedPendingRender,
            crate::source_graph_runtime::SourceFrameStatus::Updated,
            crate::source_graph_scene_attachment::SourceSceneAttached,
            Visibility::Hidden,
        ));
        let host = crate::source_graph_host::SourceSceneHost {
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
            carrier_sha256: placement.asset_hash,
            particle_instances: Vec::new(),
            collider_projection: None,
            collider_scene_projection: None,
        };
        source_graph_runtime::retain(
            world,
            host,
            source_graph_runtime::RuntimePolicy {
                source_world_from_target_world: Mat4::IDENTITY,
                source_absolute_world_from_target_world: Mat4::IDENTITY,
                global_mip_bias: 0.0,
                source_float_min: f32::MIN_POSITIVE,
            },
        )
        .unwrap();
        world.resource_mut::<Records>().0.insert(root, record);
        (app, root, draw, placement)
    }

    #[test]
    fn physical_manual_admission_waits_for_backend_and_never_collides_visual_meshes() {
        let (mut app, root, draw, placement) = mounted_physics_fixture();
        let initial = observe_manual_physics(app.world(), root).unwrap().unwrap();
        assert!(!initial.is_ready());
        assert_eq!(
            admit_manual(app.world_mut(), root).unwrap(),
            ManualAdmission::Pending
        );
        assert!(!crate::manual_placements_ready(
            app.world_mut(),
            std::slice::from_ref(&placement)
        ));
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        let ready = observe_manual_physics(app.world(), root).unwrap().unwrap();
        assert!(ready.is_ready());
        assert_eq!((ready.registered_solids, ready.registered_sensors), (2, 1));
        assert_eq!((ready.disabled, ready.unassigned), (1, 1));
        assert_eq!(
            admit_manual(app.world_mut(), root).unwrap(),
            ManualAdmission::Attached
        );
        assert!(crate::manual_placements_ready(
            app.world_mut(),
            &[placement]
        ));
        assert!(
            app.world()
                .get::<bevy_rapier3d::prelude::Collider>(draw)
                .is_none()
        );
        assert!(
            app.world()
                .get::<crate::ManualPlacementCollision>(draw)
                .is_none()
        );
        assert!(
            app.world()
                .get::<crate::ManualPlacementCollisionExpected>(root)
                .is_none()
        );
        assert!(
            app.world()
                .get::<crate::ManualPlacementPart>(draw)
                .is_some()
        );
    }

    #[test]
    fn physical_manual_admission_revokes_changed_pins_and_visual_ownership() {
        let (mut app, root, draw, placement) = mounted_physics_fixture();
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        assert_eq!(
            admit_manual(app.world_mut(), root).unwrap(),
            ManualAdmission::Attached
        );
        app.world_mut()
            .get_mut::<crate::ManualPlacementProjection>(root)
            .unwrap()
            .0
            .physics
            .as_mut()
            .unwrap()
            .package_sha256 = forge_core::bytes_sha256(b"changed");
        assert!(observe_manual_physics(app.world(), root).is_err());
        assert!(!crate::manual_placements_ready(
            app.world_mut(),
            std::slice::from_ref(&placement)
        ));
        app.world_mut()
            .get_mut::<crate::ManualPlacementProjection>(root)
            .unwrap()
            .0 = placement.clone();
        app.world_mut()
            .entity_mut(draw)
            .remove::<crate::static_hlod::StaticHlodSource>();
        assert!(!crate::manual_placements_ready(
            app.world_mut(),
            &[placement]
        ));
        assert!(admit_manual(app.world_mut(), root).is_err());
    }

    #[test]
    fn physical_admission_rejects_source_node_drift_before_and_after_backend_sync() {
        for fault in 0..3 {
            let (mut app, root, draw, _) = mounted_physics_fixture();
            app.update();
            app.world_mut().run_schedule(FixedUpdate);
            assert_eq!(
                admit_manual(app.world_mut(), root).unwrap(),
                ManualAdmission::Attached
            );
            let node = app.world().get::<ChildOf>(draw).unwrap().parent();
            match fault {
                0 => {
                    app.world_mut()
                        .get_mut::<Transform>(node)
                        .unwrap()
                        .translation
                        .x += 2.0;
                }
                1 => {
                    app.world_mut().get_mut::<Transform>(node).unwrap().rotation =
                        Quat::from_rotation_y(0.25);
                }
                _ => {
                    let parent = app
                        .world_mut()
                        .spawn((ChildOf(root), Transform::IDENTITY))
                        .id();
                    app.world_mut().entity_mut(node).insert(ChildOf(parent));
                }
            }
            assert!(
                observe_manual_physics(app.world(), root)
                    .unwrap_err()
                    .contains("ancestor"),
                "fault {fault} before sync"
            );
            app.update();
            app.world_mut().run_schedule(FixedUpdate);
            assert!(
                observe_manual_physics(app.world(), root)
                    .unwrap_err()
                    .contains("ancestor"),
                "fault {fault} after sync"
            );
        }
    }

    #[test]
    fn moving_source_physics_is_refused_without_stopping_the_shared_motion_host() {
        use crate::pendulum_motion::{
            LanternMotion, Wind,
            host::{Binding, Motion, Resident},
        };

        let (mut app, root, draw, placement) = mounted_physics_fixture();
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        assert_eq!(
            admit_manual(app.world_mut(), root).unwrap(),
            ManualAdmission::Attached
        );
        let node = app.world().get::<ChildOf>(draw).unwrap().parent();
        let mut motion = Resident::bind(
            app.world_mut(),
            &placement,
            root,
            vec![Binding {
                key: 1,
                entity: node,
                pivot: Vec3::ZERO,
                rest_rotation: Quat::IDENTITY,
                rest_scale: Vec3::ONE,
                motion: Motion::Suspended(LanternMotion {
                    t: 0.0,
                    ph: 0.7,
                    x: 0.0,
                    z: 0.0,
                    vx: 0.0,
                    vz: 0.0,
                }),
            }],
        )
        .unwrap();
        assert!(
            observe_manual_physics(app.world(), root)
                .unwrap_err()
                .contains("moving")
        );
        motion
            .advance(
                app.world_mut(),
                0.05,
                Wind {
                    speed: 6.0,
                    direction: [1.0, 0.0],
                },
            )
            .unwrap();
        assert_ne!(
            app.world().get::<Transform>(node).unwrap().rotation,
            Quat::IDENTITY
        );
        motion.release(app.world_mut());
        assert!(
            observe_manual_physics(app.world(), root)
                .unwrap_err()
                .contains("ancestor")
        );
    }

    #[test]
    fn physical_admission_waits_for_an_author_root_change_to_be_rebuilt() {
        let (mut app, root, _, _) = mounted_physics_fixture();
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        assert_eq!(
            admit_manual(app.world_mut(), root).unwrap(),
            ManualAdmission::Attached
        );
        app.world_mut()
            .get_mut::<Transform>(root)
            .unwrap()
            .translation
            .x = 1.0;
        assert!(observe_manual_physics(app.world(), root).unwrap().is_none());
    }

    #[test]
    fn removing_admitted_source_physics_retires_only_owned_backend_colliders() {
        use bevy_rapier3d::{
            plugin::context::RapierContextColliders,
            prelude::{Collider, RapierColliderHandle, RapierContextEntityLink},
        };

        let (mut app, root, draw, _) = mounted_physics_fixture();
        let foreign = app
            .world_mut()
            .spawn((Collider::ball(0.5), Transform::from_xyz(100.0, 0.0, 0.0)))
            .id();
        app.update();
        app.world_mut().run_schedule(FixedUpdate);
        assert_eq!(
            admit_manual(app.world_mut(), root).unwrap(),
            ManualAdmission::Attached
        );
        let scene = app.world().resource::<Records>().0[&root]
            .projected_physics
            .scene
            .as_ref()
            .unwrap();
        let owned = scene
            .observe_owned(app.world())
            .unwrap()
            .iter()
            .filter_map(|row| {
                app.world()
                    .get::<RapierColliderHandle>(row.entity)
                    .map(|handle| (row.entity, handle.0))
            })
            .collect::<Vec<_>>();
        assert_eq!(owned.len(), 4);
        let context = app
            .world()
            .get::<RapierContextEntityLink>(foreign)
            .unwrap()
            .0;
        let foreign_handle = app.world().get::<RapierColliderHandle>(foreign).unwrap().0;
        let mut queue = bevy::ecs::world::CommandQueue::default();
        {
            let mut commands = Commands::new(&mut queue, app.world_mut());
            remove(&mut commands, root);
        }
        queue.apply(app.world_mut());
        assert!(app.world().get_entity(root).is_err());
        assert!(app.world().get_entity(draw).is_err());
        assert!(app.world().get_entity(foreign).is_ok());
        assert!(!app.world().resource::<Records>().0.contains_key(&root));
        app.world_mut().run_schedule(FixedUpdate);
        let backend = app.world().get::<RapierContextColliders>(context).unwrap();
        for (entity, handle) in owned {
            assert!(!backend.entity2collider().contains_key(&entity));
            assert!(backend.colliders.get(handle).is_none());
        }
        assert!(backend.colliders.get(foreign_handle).is_some());
    }

    #[test]
    fn pose_composes_parent_rotation_and_nonuniform_scale_without_decomposition() {
        let mut world = World::new();
        let parent_pose =
            Transform::from_scale(Vec3::new(2., 3., 4.)).with_rotation(Quat::from_rotation_y(0.7));
        let child_pose = Transform::from_xyz(3., 2., 1.).with_rotation(Quat::from_rotation_x(0.3));
        let parent = world.spawn(parent_pose).id();
        let root = world.spawn((child_pose, ChildOf(parent))).id();
        assert_eq!(
            current_pose(&world, root).unwrap(),
            parent_pose.to_matrix() * child_pose.to_matrix()
        );
        world.get_mut::<Transform>(parent).unwrap().translation = Vec3::X;
        assert_ne!(
            current_pose(&world, root).unwrap(),
            parent_pose.to_matrix() * child_pose.to_matrix()
        );
    }

    #[test]
    fn object_local_mapping_cannot_replace_explicit_world_mapping() {
        let mut document = serde_json::json!({"extras":{"source_frame_binding":{
            "source_object_from_target_object": [1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]
        }}});
        assert!(
            source_graph_context::declared_world_mapping(
                &document,
                "source_world_from_target_world"
            )
            .is_err()
        );
        document["extras"]["source_frame_binding"]["source_world_from_target_world"] =
            serde_json::json!([1, 0, 0, 2, 0, 1, 0, 3, 0, 0, 1, 4, 0, 0, 0, 1]);
        assert_eq!(
            source_graph_context::declared_world_mapping(
                &document,
                "source_world_from_target_world"
            )
            .unwrap(),
            Mat4::from_translation(Vec3::new(2., 3., 4.))
        );
    }
}
