//! Generic residency for caller-verified authored source fish.
use super::{clock, group, material::FishMaterial};
use crate::{
    ManualPlacementCollisionExpected, ManualPlacementCollisionNotRequired, ManualPlacementPart,
    ManualPlacementProjection, distance_lod::Policy,
};
use bevy::{prelude::*, render::storage::ShaderBuffer};
use forge_core::{AssetId, ContentHash, EntityId, Transform as ForgeTransform};
use forge_project::zone_authoring::placements::{MAX_MANUAL_PLACEMENTS, ManualPlacement};
use std::collections::{BTreeMap, BTreeSet, HashMap};

/// Caller-verified source identity, pinned content and high/low/shadow geometry.
pub struct Template {
    pub source_id: String,
    pub asset_id: AssetId,
    pub asset_hash: ContentHash,
    pub lod_meshes: Vec<Mesh>,
}

/// A caller-verified fish attachment whose geometry and material are already resident.
#[derive(Clone)]
pub struct Attachment {
    pub meshes: Vec<Handle<Mesh>>,
    pub material: Handle<FishMaterial>,
    pub pose: Transform,
}

/// One caller-verified fish in a mixed group of attached and world-space props.
#[derive(Clone)]
pub struct SceneInstance {
    pub attachment: Attachment,
    pub parent: Option<Entity>,
    pub placement: Option<EntityId>,
    pub name: Option<String>,
}

struct Instance {
    root: Entity,
    parts: [Entity; 2],
    materials: [Handle<FishMaterial>; 2],
    shadow_mesh: Handle<Mesh>,
    casts_shadow: bool,
    policy: Policy,
}

/// The depth adapter must use the source fin mask, never an opaque substitute.
pub struct ShadowInstance {
    pub root: Entity,
    pub mesh: Handle<Mesh>,
    pub world_from_local: Mat4,
    pub time: f32,
    pub frame_index: u32,
}

#[derive(Resource)]
pub struct ResidentFishProps {
    instances: Vec<Instance>,
    part_sources: HashMap<
        Entity,
        (
            Entity,
            bevy::asset::AssetId<Mesh>,
            bevy::asset::AssetId<FishMaterial>,
        ),
    >,
    authored: Vec<(Entity, EntityId, ForgeTransform)>,
    meshes: Vec<Handle<Mesh>>,
    materials: Vec<Handle<FishMaterial>>,
    clock: Option<Handle<ShaderBuffer>>,
    owns_clock: bool,
    group: group::Group,
    policy: Policy,
    time: f32,
    frame_index: u32,
}

pub(super) fn validate_companions(placement: &ManualPlacement) -> Result<(), String> {
    if placement.physics.is_some() || placement.particles.is_some() {
        return Err("Fish host cannot project the placement's saved physics or effects".into());
    }
    Ok(())
}

/// Stage only the accepted placement list. An empty list renders no defaults.
/// Clones receive independent LOD uniforms while sharing their source geometry.
///
/// # Errors
/// Refuses duplicate identities, altered pins, invalid transforms, missing
/// templates/materials or absent asset resources before changing the world.
pub fn stage_authored(
    templates: Vec<Template>,
    world: &mut World,
    materials: &BTreeMap<AssetId, Handle<FishMaterial>>,
    placements: &[ManualPlacement],
    policy: Policy,
) -> Result<ResidentFishProps, String> {
    let policies = placements
        .iter()
        .map(|placement| (placement.asset_id, policy))
        .collect();
    stage_authored_impl(templates, world, materials, placements, &policies, policy)
}

/// Stage authored fish using the explicitly supplied policy for each asset.
/// The group uses a conservative envelope; each fish keeps its own selection.
///
/// # Errors
/// Rejects an empty or incomplete policy map, extra asset identities, or any
/// invalid source, placement or resource before publishing a root.
pub fn stage_authored_with_policies(
    templates: Vec<Template>,
    world: &mut World,
    materials: &BTreeMap<AssetId, Handle<FishMaterial>>,
    placements: &[ManualPlacement],
    policies: &BTreeMap<AssetId, Policy>,
) -> Result<ResidentFishProps, String> {
    let fallback = policies
        .values()
        .next()
        .copied()
        .ok_or("Fish policies absent")?;
    stage_authored_impl(templates, world, materials, placements, policies, fallback)
}

// Keep preflight and publication together so rejected batches never publish roots.
#[allow(clippy::too_many_lines)]
fn stage_authored_impl(
    templates: Vec<Template>,
    world: &mut World,
    materials: &BTreeMap<AssetId, Handle<FishMaterial>>,
    placements: &[ManualPlacement],
    policies: &BTreeMap<AssetId, Policy>,
    fallback: Policy,
) -> Result<ResidentFishProps, String> {
    if placements.len() > MAX_MANUAL_PLACEMENTS {
        return Err("Fish placement limit exceeded".into());
    }
    let assets: BTreeSet<_> = placements
        .iter()
        .map(|placement| placement.asset_id)
        .collect();
    if assets != policies.keys().copied().collect() {
        return Err("Fish asset policies differ from placements".into());
    }
    let policy = policies
        .values()
        .copied()
        .fold(fallback, Policy::broad_phase_cover);
    let mut indexed = BTreeMap::new();
    for template in templates {
        if indexed.insert(template.asset_id, template).is_some() {
            return Err("Repeated fish template identity".into());
        }
    }
    let templates = indexed;
    let mut ids = BTreeSet::new();
    let store = world
        .get_resource::<Assets<FishMaterial>>()
        .ok_or("Fish materials absent")?;
    if !world.contains_resource::<Assets<Mesh>>() {
        return Err("Fish meshes absent".into());
    }
    if !placements.is_empty() && !world.contains_resource::<Assets<ShaderBuffer>>() {
        return Err("Fish clock shader buffers unavailable".into());
    }
    let mut prepared = BTreeMap::new();
    for placement in placements {
        validate_companions(placement)?;
        if !ids.insert(placement.id) {
            return Err("Duplicate or altered common fish placement".into());
        }
        placement
            .transform
            .validate()
            .map_err(|error| error.to_string())?;
        let template = templates
            .get(&placement.asset_id)
            .ok_or("Fish template absent")?;
        if placement.asset_hash != template.asset_hash {
            return Err("Altered fish template pin".into());
        }
        if template.lod_meshes.len() != 3 {
            return Err("Fish requires high, low and masked shadow geometry".into());
        }
        let handle = materials
            .get(&placement.asset_id)
            .ok_or("Fish material absent")?;
        let material = store.get(handle).ok_or("Fish material not resident")?;
        prepared.insert(placement.asset_id, material.clone());
    }
    let clock = if placements.is_empty() {
        None
    } else {
        Some(clock::create(world)?)
    };
    let mut resident = ResidentFishProps {
        instances: Vec::new(),
        part_sources: HashMap::new(),
        authored: Vec::new(),
        meshes: Vec::new(),
        materials: Vec::new(),
        clock,
        owns_clock: true,
        group: group::Group::default(),
        policy,
        time: 0.0,
        frame_index: 0,
    };
    for (asset, template) in templates {
        let accepted: Vec<_> = placements.iter().filter(|p| p.asset_id == asset).collect();
        if accepted.is_empty() {
            continue;
        }
        let meshes: Vec<_> = template
            .lod_meshes
            .into_iter()
            .map(|mesh| world.resource_mut::<Assets<Mesh>>().add(mesh))
            .collect();
        resident.meshes.extend(meshes.iter().cloned());
        let instance_clock = resident
            .clock
            .as_ref()
            .expect("Accepted fish placements own a live clock")
            .clone();
        for placement in accepted {
            let root = world
                .spawn((
                    Name::new(template.source_id.clone()),
                    project(placement.transform),
                    GlobalTransform::default(),
                    Visibility::Inherited,
                    group::Member,
                    ManualPlacementProjection(placement.clone()),
                    ManualPlacementCollisionNotRequired(placement.id),
                    ManualPlacementCollisionExpected {
                        placement: placement.id,
                        primitives: Vec::new(),
                    },
                ))
                .id();
            let handles: [_; 2] = std::array::from_fn(|_| {
                let mut material = prepared[&asset].clone();
                material.clock = instance_clock.clone();
                world.resource_mut::<Assets<FishMaterial>>().add(material)
            });
            let parts = std::array::from_fn(|lod| {
                world
                    .spawn((
                        Mesh3d(meshes[lod].clone()),
                        MeshMaterial3d(handles[lod].clone()),
                        Transform::IDENTITY,
                        GlobalTransform::default(),
                        Visibility::Hidden,
                        ChildOf(root),
                        ManualPlacementPart(placement.id),
                    ))
                    .id()
            });
            for lod in 0..2 {
                resident
                    .part_sources
                    .insert(parts[lod], (root, meshes[lod].id(), handles[lod].id()));
            }
            resident.materials.extend(handles.iter().cloned());
            resident
                .authored
                .push((root, placement.id, placement.transform));
            resident.instances.push(Instance {
                root,
                parts,
                materials: handles,
                shadow_mesh: meshes[2].clone(),
                casts_shadow: false,
                policy: policies[&asset],
            });
        }
    }
    Ok(resident)
}

/// Attach verified fish to an existing parent using the same resident behavior.
/// Geometry and the live clock remain owned by the caller; cloned materials and
/// child entities belong to the returned resident.
///
/// # Errors
/// Refuses a missing parent, invalid pose, absent resources, neutral/missing
/// clock or incomplete geometry before publishing any child.
pub fn stage_attachments(
    world: &mut World,
    parent: Entity,
    placement: EntityId,
    attachments: &[Attachment],
    shared_clock: &Handle<ShaderBuffer>,
    policy: Policy,
) -> Result<ResidentFishProps, String> {
    if attachments.len() > MAX_MANUAL_PLACEMENTS || world.get_entity(parent).is_err() {
        return Err("Invalid fish attachment parent or count".into());
    }
    let instances: Vec<_> = attachments
        .iter()
        .cloned()
        .map(|attachment| SceneInstance {
            attachment,
            parent: Some(parent),
            placement: Some(placement),
            name: None,
        })
        .collect();
    stage_instances(world, &instances, shared_clock, policy)
}

/// Stage one visibility and clock group with optional parents and placement IDs.
/// Geometry and the live clock remain owned by the caller; cloned materials and
/// entities belong to the returned resident.
///
/// # Errors
/// Refuses a missing parent, invalid pose, absent resources, neutral/missing
/// clock or incomplete geometry before publishing any entity.
#[allow(
    clippy::too_many_lines,
    reason = "Preflight the complete group before publication"
)]
pub fn stage_instances(
    world: &mut World,
    instances: &[SceneInstance],
    shared_clock: &Handle<ShaderBuffer>,
    policy: Policy,
) -> Result<ResidentFishProps, String> {
    if instances.len() > MAX_MANUAL_PLACEMENTS
        || instances.iter().any(|instance| {
            instance
                .parent
                .is_some_and(|parent| world.get_entity(parent).is_err())
        })
    {
        return Err("Invalid fish attachment parent or count".into());
    }
    let meshes = world
        .get_resource::<Assets<Mesh>>()
        .ok_or("Fish meshes absent")?;
    let materials = world
        .get_resource::<Assets<FishMaterial>>()
        .ok_or("Fish materials absent")?;
    if shared_clock.id() == clock::NEUTRAL.id()
        || world
            .get_resource::<Assets<ShaderBuffer>>()
            .and_then(|buffers| buffers.get(shared_clock))
            .is_none()
    {
        return Err("Live fish clock absent".into());
    }
    let prepared = instances
        .iter()
        .map(|instance| {
            let attachment = &instance.attachment;
            if attachment.meshes.len() != 3
                || attachment
                    .meshes
                    .iter()
                    .any(|handle| meshes.get(handle).is_none())
                || !attachment.pose.translation.is_finite()
                || !attachment.pose.scale.is_finite()
                || !attachment.pose.rotation.is_finite()
                || (attachment.pose.rotation.length_squared() - 1.0).abs() > 0.001
                || !attachment.pose.to_matrix().is_finite()
                || attachment.pose.scale.abs().min_element() <= 0.0
            {
                return Err("Invalid fish attachment geometry or pose".to_owned());
            }
            let mut material = materials
                .get(&attachment.material)
                .cloned()
                .ok_or("Fish attachment material absent")?;
            material.clock = shared_clock.clone();
            Ok((instance, material))
        })
        .collect::<Result<Vec<_>, String>>()?;
    let mut resident = ResidentFishProps {
        instances: Vec::new(),
        part_sources: HashMap::new(),
        authored: Vec::new(),
        meshes: Vec::new(),
        materials: Vec::new(),
        clock: Some(shared_clock.clone()),
        owns_clock: false,
        group: group::Group::default(),
        policy,
        time: 0.0,
        frame_index: 0,
    };
    for (instance, material) in prepared {
        let attachment = &instance.attachment;
        let root = world
            .spawn((
                attachment.pose,
                GlobalTransform::default(),
                Visibility::Inherited,
                group::Member,
            ))
            .id();
        if let Some(parent) = instance.parent {
            world.entity_mut(root).insert(ChildOf(parent));
        }
        if let Some(name) = &instance.name {
            world.entity_mut(root).insert(Name::new(name.clone()));
        }
        let handles: [_; 2] = std::array::from_fn(|_| {
            world
                .resource_mut::<Assets<FishMaterial>>()
                .add(material.clone())
        });
        let parts = std::array::from_fn(|lod| {
            let part = world
                .spawn((
                    Mesh3d(attachment.meshes[lod].clone()),
                    MeshMaterial3d(handles[lod].clone()),
                    Transform::IDENTITY,
                    GlobalTransform::default(),
                    Visibility::Hidden,
                    ChildOf(root),
                ))
                .id();
            if let Some(placement) = instance.placement {
                world
                    .entity_mut(part)
                    .insert(ManualPlacementPart(placement));
            }
            part
        });
        for lod in 0..2 {
            resident.part_sources.insert(
                parts[lod],
                (root, attachment.meshes[lod].id(), handles[lod].id()),
            );
        }
        resident.materials.extend(handles.iter().cloned());
        resident.instances.push(Instance {
            root,
            parts,
            materials: handles,
            shadow_mesh: attachment.meshes[2].clone(),
            casts_shadow: false,
            policy,
        });
    }
    Ok(resident)
}

impl ResidentFishProps {
    /// Confirm that this resident still owns the exact root, mesh and material.
    #[must_use]
    pub fn owns_part(&self, world: &World, entity: Entity) -> bool {
        self.part_sources
            .get(&entity)
            .is_some_and(|(root, mesh, material)| {
                world.get::<group::Member>(*root).is_some()
                    && world
                        .get::<ChildOf>(entity)
                        .is_some_and(|parent| parent.parent() == *root)
                    && world
                        .get::<Mesh3d>(entity)
                        .is_some_and(|current| current.id() == *mesh)
                    && world
                        .get::<MeshMaterial3d<FishMaterial>>(entity)
                        .is_some_and(|current| current.id() == *material)
            })
    }

    #[must_use]
    pub fn is_dormant(&self) -> bool {
        self.group.is_dormant()
    }

    #[must_use]
    pub fn authored_roots(&self) -> &[(Entity, EntityId, ForgeTransform)] {
        &self.authored
    }

    #[must_use]
    pub fn authored_parts(&self, id: EntityId) -> Option<[Entity; 2]> {
        let root = self.authored.iter().find(|(_, placed, _)| *placed == id)?.0;
        self.instances
            .iter()
            .find(|instance| instance.root == root)
            .map(|instance| instance.parts)
    }

    /// Update poses for the same accepted identities without rebuilding meshes.
    ///
    /// # Errors
    /// Refuses missing, duplicated or changed identities and invalid transforms
    /// before changing any projected root.
    pub fn move_authored(
        &mut self,
        world: &mut World,
        placements: &[ManualPlacement],
    ) -> Result<(), String> {
        if placements.len() != self.authored.len() {
            return Err("Fish placement inventory changed".into());
        }
        let mut by_id = BTreeMap::new();
        for placement in placements {
            validate_companions(placement)?;
            placement
                .transform
                .validate()
                .map_err(|error| error.to_string())?;
            if by_id.insert(placement.id, placement).is_some() {
                return Err("Repeated fish placement identity".into());
            }
        }
        for &(root, id, _) in &self.authored {
            let next = by_id.get(&id).ok_or("Fish placement absent")?;
            let current = world
                .get::<ManualPlacementProjection>(root)
                .ok_or("Fish placement projection absent")?;
            if current.0.id != id
                || current.0.asset_id != next.asset_id
                || current.0.asset_hash != next.asset_hash
                || world.get::<Transform>(root).is_none()
            {
                return Err("Fish placement identity or revision changed".into());
            }
        }
        for (root, id, transform) in &mut self.authored {
            let next = by_id.get(id).copied().expect("Fish placements preflighted");
            world.entity_mut(*root).insert((
                ManualPlacementProjection(next.clone()),
                crate::domain_transform_to_bevy(next.transform),
            ));
            *transform = next.transform;
        }
        Ok(())
    }

    /// Synchronize scene receivers only for the LOD parts selected to draw.
    /// Sleeping groups retain their last bindings until they wake.
    ///
    /// # Errors
    /// Refuses invalid lighting or stale visible parts before changing materials.
    pub fn update_scene_receivers(
        &self,
        world: &mut World,
        lighting: &crate::source_surface::SourceSurfaceMaterial,
    ) -> Result<(), String> {
        crate::source_lighting::SceneLighting::new(
            lighting.scene_lighting.sun_direction.truncate().to_array(),
            lighting.scene_lighting.sun_color.truncate().to_array(),
            lighting
                .scene_lighting
                .environment_sh
                .map(|value| value.truncate().to_array()),
        )?;
        if self.group.is_dormant() || self.instances.is_empty() {
            return Ok(());
        }
        let materials = world
            .get_resource::<Assets<FishMaterial>>()
            .ok_or("Fish materials absent")?;
        let mut replacements = Vec::new();
        for instance in &self.instances {
            for (part, handle) in instance.parts.iter().zip(&instance.materials) {
                let visibility = world
                    .get::<Visibility>(*part)
                    .ok_or("Fish visibility absent")?;
                if *visibility != Visibility::Inherited {
                    continue;
                }
                let material = materials.get(handle).ok_or("Fish material absent")?;
                let mut next = material.clone();
                next.update_scene_receivers(lighting)?;
                if next != *material {
                    replacements.push((handle.clone(), next));
                }
            }
        }
        let mut materials = world.resource_mut::<Assets<FishMaterial>>();
        for (handle, next) in replacements {
            *materials
                .get_mut(&handle)
                .ok_or("Fish material disappeared after preflight")? = next;
        }
        Ok(())
    }

    /// The caller supplies the actual camera's sphere/frustum intersection.
    /// Original uniform scales retain the source radius L*0.8; nonuniform author
    /// scales use their largest axis as a conservative bound and LOD length.
    ///
    /// # Errors
    /// Refuses stale roots, transforms, parts/materials or invalid camera/time
    /// before changing visibility or uniforms for any instance.
    pub fn update_view(
        &mut self,
        world: &mut World,
        camera: Vec3,
        mut intersects: impl FnMut(Vec3, f32) -> bool,
        time: f32,
        frame_index: u32,
    ) -> Result<(), String> {
        if !camera.is_finite() || !time.is_finite() || time < 0.0 {
            return Err("Invalid fish camera or time".into());
        }
        if !world.contains_resource::<Assets<FishMaterial>>() {
            return Err("Fish materials absent".into());
        }
        if self.instances.is_empty() {
            self.time = time;
            self.frame_index = frame_index;
            return Ok(());
        }
        let contributes = self.group.may_contribute(
            world,
            self.instances.iter().map(|instance| instance.root),
            self.policy,
            camera,
            &mut intersects,
        )?;
        if !contributes {
            self.enter_dormant(world)?;
            return Ok(());
        }
        let plans = self.plan_view(world, camera, &mut intersects)?;
        let clock = self.clock.as_ref().ok_or("Live fish clock absent")?;
        clock::update(world, clock, time, frame_index)?;
        world.resource_scope(
            |world, mut materials: Mut<Assets<FishMaterial>>| -> Result<(), String> {
                for (instance, plan) in self.instances.iter_mut().zip(plans) {
                    instance.casts_shadow = plan.shadow;
                    for lod in 0..2 {
                        let draw = plan.draws.iter().find(|draw| draw.lod == lod);
                        let visibility = if draw.is_some() {
                            Visibility::Inherited
                        } else {
                            Visibility::Hidden
                        };
                        if world.get::<Visibility>(instance.parts[lod]) != Some(&visibility) {
                            *world
                                .entity_mut(instance.parts[lod])
                                .get_mut::<Visibility>()
                                .ok_or("Fish visibility disappeared after preflight")? = visibility;
                        }
                        let material = materials
                            .get(&instance.materials[lod])
                            .ok_or("Fish material disappeared after preflight")?;
                        let mut settings = material.settings.clone();
                        if let Some(draw) = draw {
                            settings.lod =
                                Vec4::new(draw.fade, f32::from(u8::from(draw.outgoing)), 0.0, 0.0);
                        }
                        // Mutable access emits an asset event and rebuilds the bind group.
                        // Frozen views still evaluate camera LOD, but upload only changes.
                        if settings != material.settings {
                            materials
                                .get_mut(&instance.materials[lod])
                                .ok_or("Fish material disappeared after preflight")?
                                .settings = settings;
                        }
                    }
                }
                Ok(())
            },
        )?;
        self.group.set_dormant(false);
        self.time = time;
        self.frame_index = frame_index;
        Ok(())
    }

    fn plan_view(
        &self,
        world: &World,
        camera: Vec3,
        intersects: &mut impl FnMut(Vec3, f32) -> bool,
    ) -> Result<Vec<crate::distance_lod::Selection>, String> {
        let materials = world
            .get_resource::<Assets<FishMaterial>>()
            .ok_or("Fish materials absent")?;
        self.instances
            .iter()
            .map(|instance| {
                let global = world
                    .get::<GlobalTransform>(instance.root)
                    .ok_or("Fish root transform stale")?;
                let (scale, _, center) = global.to_scale_rotation_translation();
                let length = scale.abs().max_element();
                if !center.is_finite()
                    || !length.is_finite()
                    || length <= 0.0
                    || instance
                        .parts
                        .iter()
                        .any(|part| world.get::<Visibility>(*part).is_none())
                    || instance
                        .materials
                        .iter()
                        .any(|handle| materials.get(handle).is_none())
                {
                    return Err("Stale fish placement or render resource".to_owned());
                }
                let distance = (center.as_dvec3() - camera.as_dvec3()).length();
                instance.policy.select(
                    distance,
                    f64::from(length),
                    intersects(center, length * 0.8),
                )
            })
            .collect()
    }

    fn enter_dormant(&mut self, world: &mut World) -> Result<(), String> {
        if self.group.is_dormant() {
            return Ok(());
        }
        let materials = world.resource::<Assets<FishMaterial>>();
        let clock = self.clock.as_ref().ok_or("Live fish clock absent")?;
        if world
            .get_resource::<Assets<ShaderBuffer>>()
            .and_then(|buffers| buffers.get(clock))
            .is_none()
            || self.instances.iter().any(|instance| {
                world.get::<GlobalTransform>(instance.root).is_none()
                    || instance
                        .parts
                        .iter()
                        .any(|part| world.get::<Visibility>(*part).is_none())
                    || instance
                        .materials
                        .iter()
                        .any(|handle| materials.get(handle).is_none())
            })
        {
            return Err("Stale fish placement or render resource".into());
        }
        for instance in &mut self.instances {
            instance.casts_shadow = false;
            for &part in &instance.parts {
                if world.get::<Visibility>(part) != Some(&Visibility::Hidden) {
                    *world
                        .entity_mut(part)
                        .get_mut::<Visibility>()
                        .ok_or("Fish visibility disappeared after preflight")? = Visibility::Hidden;
                }
            }
        }
        self.group.set_dormant(true);
        Ok(())
    }

    /// Stable full caster inventory; flags retain source distance culling.
    ///
    /// # Errors
    /// Refuses roots without a finite propagated transform.
    pub fn shadow_inventory(&self, world: &World) -> Result<Vec<(ShadowInstance, bool)>, String> {
        self.instances
            .iter()
            .map(|instance| {
                let model = world
                    .get::<GlobalTransform>(instance.root)
                    .ok_or("Fish shadow transform absent")?
                    .to_matrix();
                if !model.is_finite() {
                    return Err("Fish shadow transform invalid".into());
                }
                Ok((
                    ShadowInstance {
                        root: instance.root,
                        mesh: instance.shadow_mesh.clone(),
                        world_from_local: model,
                        time: self.time,
                        frame_index: self.frame_index,
                    },
                    instance.casts_shadow,
                ))
            })
            .collect()
    }

    /// Selected low-detail proxies for a dedicated masked shadow pass.
    ///
    /// # Errors
    /// Refuses a lost source root rather than publishing a stale caster matrix.
    pub fn shadow_instances(&self, world: &World) -> Result<Vec<ShadowInstance>, String> {
        self.instances
            .iter()
            .filter(|instance| instance.casts_shadow)
            .map(|instance| {
                let matrix = world
                    .get::<GlobalTransform>(instance.root)
                    .ok_or("Fish shadow root absent")?
                    .to_matrix();
                if !matrix.is_finite() {
                    return Err("Fish shadow matrix invalid".into());
                }
                Ok(ShadowInstance {
                    root: instance.root,
                    mesh: instance.shadow_mesh.clone(),
                    world_from_local: matrix,
                    time: self.time,
                    frame_index: self.frame_index,
                })
            })
            .collect()
    }

    pub fn clear(self, world: &mut World) {
        for instance in self.instances {
            world.despawn(instance.root);
        }
        for handle in self.meshes {
            world.resource_mut::<Assets<Mesh>>().remove(handle.id());
        }
        for handle in self.materials {
            world
                .resource_mut::<Assets<FishMaterial>>()
                .remove(handle.id());
        }
        if self.owns_clock
            && let Some(clock) = self.clock
            && let Some(mut buffers) = world.get_resource_mut::<Assets<ShaderBuffer>>()
        {
            buffers.remove(clock.id());
        }
    }
}

fn project(transform: ForgeTransform) -> Transform {
    Transform {
        translation: Vec3::new(
            transform.translation_m.x,
            transform.translation_m.y,
            transform.translation_m.z,
        ),
        rotation: Quat::from_xyzw(
            transform.rotation.x,
            transform.rotation.y,
            transform.rotation.z,
            transform.rotation.w,
        ),
        scale: Vec3::new(transform.scale.x, transform.scale.y, transform.scale.z),
    }
}
#[cfg(test)]
mod tests {
    use super::*;
    use crate::source_fish::material as fish_material;
    use crate::source_lighting::SceneLighting;
    use crate::source_shadows::ObjectShadowSettings;
    use crate::source_surface::LightingBindings;
    use bevy::asset::{AssetApp, AssetPlugin};

    fn test_material() -> FishMaterial {
        let mut images = Assets::<Image>::default();
        let lighting = LightingBindings {
            scene_lighting: SceneLighting::new([0.0, 1.0, 0.0], [3.0; 3], [[0.4; 3]; 9]).unwrap(),
            environment: Handle::default(),
            hill_shadow: Handle::default(),
            object_shadow: Handle::default(),
            object_settings: ObjectShadowSettings::default(),
            clouds: [Vec4::ZERO; 5],
            cloud_shadow: None,
            wind: Vec4::ZERO,
            flags: Vec4::ZERO,
            caustics: crate::source_surface::underwater::CausticMaterialInputs::disabled(
                &mut images,
            ),
            ground_bounce: None,
            bounce_settings: Vec4::ZERO,
        };
        fish_material::prepare(
            crate::source_fish::material::SurfaceParameters {
                pattern_and_seed: 0.25,
                source_length_m: 1.0,
                flags: [0.0; 4],
            },
            Handle::default(),
            &lighting,
            0.0,
        )
        .unwrap()
    }

    fn test_template(asset_id: AssetId, asset_hash: ContentHash) -> Template {
        Template {
            source_id: "independent-module:fish".into(),
            asset_id,
            asset_hash,
            lod_meshes: (0..3)
                .map(|_| {
                    Mesh::new(
                        bevy::render::render_resource::PrimitiveTopology::TriangleList,
                        bevy::asset::RenderAssetUsages::default(),
                    )
                })
                .collect(),
        }
    }

    #[test]
    fn different_fish_shader_cannot_replace_one_with_resident_materials() {
        let mut world = World::new();
        world.init_resource::<Assets<Shader>>();
        world.init_resource::<Assets<ShaderBuffer>>();
        world.init_resource::<Assets<FishMaterial>>();
        fish_material::stage_source(&mut world, "// first shader".into()).unwrap();
        let material = world
            .resource_mut::<Assets<FishMaterial>>()
            .add(test_material());
        assert!(fish_material::stage_source(&mut world, "// different shader".into()).is_err());
        assert_eq!(
            world
                .resource::<Assets<Shader>>()
                .get(&fish_material::SHADER)
                .unwrap()
                .source
                .as_str(),
            "// first shader"
        );
        fish_material::stage_source(&mut world, "// first shader".into()).unwrap();
        world
            .resource_mut::<Assets<FishMaterial>>()
            .remove(material.id());
        fish_material::stage_source(&mut world, "// different shader".into()).unwrap();
    }

    #[test]
    #[allow(
        clippy::too_many_lines,
        reason = "Exercise mixed parentage, atomic rejection and caller-owned resources"
    )]
    fn mixed_fish_group_follows_each_parent_and_preserves_unattached_world_pose() {
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, bevy::transform::TransformPlugin));
        app.world_mut().init_resource::<Assets<Mesh>>();
        app.world_mut().init_resource::<Assets<ShaderBuffer>>();
        app.world_mut().init_resource::<Assets<FishMaterial>>();
        let meshes: Vec<_> = (0..3)
            .map(|_| {
                app.world_mut()
                    .resource_mut::<Assets<Mesh>>()
                    .add(Mesh::new(
                        bevy::render::render_resource::PrimitiveTopology::TriangleList,
                        bevy::asset::RenderAssetUsages::default(),
                    ))
            })
            .collect();
        let material = app
            .world_mut()
            .resource_mut::<Assets<FishMaterial>>()
            .add(test_material());
        let shared_clock = clock::create(app.world_mut()).unwrap();
        let parent = app
            .world_mut()
            .spawn((
                Transform::from_xyz(100.0, 0.0, 0.0),
                GlobalTransform::default(),
            ))
            .id();
        let placement = EntityId::deterministic("independent-module:attached");
        let attachment = Attachment {
            meshes: meshes.clone(),
            material: material.clone(),
            pose: Transform::from_xyz(2.0, 0.0, 0.0),
        };
        let inputs = [
            SceneInstance {
                attachment: attachment.clone(),
                parent: Some(parent),
                placement: Some(placement),
                name: Some("attached fish".into()),
            },
            SceneInstance {
                attachment: Attachment {
                    pose: Transform::from_xyz(8.0, 0.0, 0.0),
                    ..attachment
                },
                parent: None,
                placement: None,
                name: Some("world fish".into()),
            },
        ];
        let policy = Policy::new(300.0, 270.0, 10.0, 12.0, 0.8).unwrap();
        let missing_parent = app.world_mut().spawn_empty().id();
        app.world_mut().despawn(missing_parent);
        let mut invalid = inputs.clone();
        invalid[1].parent = Some(missing_parent);
        assert!(stage_instances(app.world_mut(), &invalid, &shared_clock, policy).is_err());
        assert!(stage_instances(app.world_mut(), &inputs, &clock::NEUTRAL, policy).is_err());
        assert!(app.world().get::<Children>(parent).is_none());
        assert_eq!(app.world().resource::<Assets<FishMaterial>>().len(), 1);

        let mut resident =
            stage_instances(app.world_mut(), &inputs, &shared_clock, policy).unwrap();
        let attached = resident.instances[0].root;
        let free = resident.instances[1].root;
        assert_eq!(
            app.world().get::<Name>(attached).unwrap().as_str(),
            "attached fish"
        );
        assert!(app.world().get::<ChildOf>(free).is_none());
        for part in resident.instances[0].parts {
            assert_eq!(
                app.world().get::<ManualPlacementPart>(part).unwrap().0,
                placement
            );
        }
        for part in resident.instances[1].parts {
            assert!(app.world().get::<ManualPlacementPart>(part).is_none());
        }
        app.update();
        resident
            .update_view(app.world_mut(), Vec3::X * 50.0, |_, _| true, 1.0, 2)
            .unwrap();
        let inventory = resident.shadow_inventory(app.world()).unwrap();
        assert!((inventory[0].0.world_from_local.w_axis.x - 102.0).abs() < 1e-5);
        assert!((inventory[1].0.world_from_local.w_axis.x - 8.0).abs() < 1e-5);
        app.world_mut()
            .entity_mut(parent)
            .insert(Transform::from_xyz(200.0, 0.0, 0.0));
        app.update();
        resident
            .update_view(app.world_mut(), Vec3::X * 50.0, |_, _| true, 2.0, 3)
            .unwrap();
        let inventory = resident.shadow_inventory(app.world()).unwrap();
        assert!((inventory[0].0.world_from_local.w_axis.x - 202.0).abs() < 1e-5);
        assert!((inventory[1].0.world_from_local.w_axis.x - 8.0).abs() < 1e-5);
        resident.clear(app.world_mut());
        assert!(
            app.world()
                .resource::<Assets<ShaderBuffer>>()
                .get(&shared_clock)
                .is_some()
        );
        assert!(
            app.world()
                .resource::<Assets<FishMaterial>>()
                .get(&material)
                .is_some()
        );
        assert_eq!(app.world().resource::<Assets<FishMaterial>>().len(), 1);
        assert_eq!(app.world().resource::<Assets<Mesh>>().len(), 3);
    }

    #[test]
    #[allow(
        clippy::too_many_lines,
        reason = "Check parent propagation, shared resource ownership and atomic refusals together"
    )]
    fn attached_fish_follow_parent_and_keep_caller_owned_resources() {
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, bevy::transform::TransformPlugin));
        app.world_mut().init_resource::<Assets<Mesh>>();
        app.world_mut().init_resource::<Assets<ShaderBuffer>>();
        app.world_mut().init_resource::<Assets<FishMaterial>>();
        let mesh_handles: Vec<_> = (0..3)
            .map(|_| {
                app.world_mut()
                    .resource_mut::<Assets<Mesh>>()
                    .add(Mesh::new(
                        bevy::render::render_resource::PrimitiveTopology::TriangleList,
                        bevy::asset::RenderAssetUsages::default(),
                    ))
            })
            .collect();
        let base = app
            .world_mut()
            .resource_mut::<Assets<FishMaterial>>()
            .add(test_material());
        let shared_clock = clock::create(app.world_mut()).unwrap();
        let parent = app
            .world_mut()
            .spawn((
                Transform::from_xyz(100.0, 0.0, 0.0),
                GlobalTransform::default(),
            ))
            .id();
        let other_parent = app
            .world_mut()
            .spawn((
                Transform::from_xyz(10.0, 0.0, 0.0),
                GlobalTransform::default(),
            ))
            .id();
        let attachment = Attachment {
            meshes: mesh_handles.clone(),
            material: base.clone(),
            pose: Transform::from_xyz(2.0, 0.0, 0.0),
        };
        let id = EntityId::deterministic("independent-module:parent");
        let policy = Policy::new(40.0, 36.0, 10.0, 12.0, 0.8).unwrap();
        let mut invalid = attachment.clone();
        invalid.pose.translation.x = f32::NAN;
        assert!(
            stage_attachments(
                app.world_mut(),
                parent,
                id,
                &[invalid],
                &shared_clock,
                policy
            )
            .is_err()
        );
        assert!(
            stage_attachments(
                app.world_mut(),
                parent,
                id,
                std::slice::from_ref(&attachment),
                &clock::NEUTRAL,
                policy,
            )
            .is_err()
        );
        assert_eq!(app.world().resource::<Assets<FishMaterial>>().len(), 1);
        assert!(app.world().get::<Children>(parent).is_none());

        let mut first = stage_attachments(
            app.world_mut(),
            parent,
            id,
            std::slice::from_ref(&attachment),
            &shared_clock,
            policy,
        )
        .unwrap();
        let mut second = stage_attachments(
            app.world_mut(),
            other_parent,
            id,
            &[attachment],
            &shared_clock,
            policy,
        )
        .unwrap();
        app.update();
        first
            .update_view(app.world_mut(), Vec3::X * 102.0, |_, _| true, 1.0, 2)
            .unwrap();
        let shadow = first.shadow_instances(app.world()).unwrap();
        assert_eq!(shadow.len(), 1);
        assert!((shadow[0].world_from_local.w_axis.x - 102.0).abs() < 1e-5);
        app.world_mut()
            .entity_mut(parent)
            .insert(Transform::from_xyz(200.0, 0.0, 0.0));
        app.update();
        first
            .update_view(app.world_mut(), Vec3::X * 202.0, |_, _| true, 2.0, 3)
            .unwrap();
        let shadow = first.shadow_instances(app.world()).unwrap();
        assert!((shadow[0].world_from_local.w_axis.x - 202.0).abs() < 1e-5);

        first.clear(app.world_mut());
        assert!(
            app.world()
                .resource::<Assets<ShaderBuffer>>()
                .get(&shared_clock)
                .is_some()
        );
        assert_eq!(app.world().resource::<Assets<Mesh>>().len(), 3);
        second
            .update_view(app.world_mut(), Vec3::X * 12.0, |_, _| true, 3.0, 4)
            .unwrap();
        assert_eq!(second.shadow_instances(app.world()).unwrap().len(), 1);
        second.clear(app.world_mut());
        assert!(
            app.world()
                .resource::<Assets<ShaderBuffer>>()
                .get(&shared_clock)
                .is_some()
        );
        assert_eq!(app.world().resource::<Assets<FishMaterial>>().len(), 1);
        assert_eq!(app.world().resource::<Assets<Mesh>>().len(), 3);
    }

    #[test]
    #[allow(
        clippy::too_many_lines,
        reason = "Check independent identities, atomic refusals and resident lifecycle together"
    )]
    fn independent_module_stages_pinned_clones_and_refuses_altered_batches() {
        let asset_id = AssetId::deterministic("independent-module:fish");
        let hash = forge_core::bytes_sha256(b"independent fish content");
        let policy = Policy::new(40.0, 36.0, 10.0, 12.0, 0.8).unwrap();
        let placement = ManualPlacement {
            id: EntityId::deterministic("independent-module:first"),
            name: "fish".into(),
            asset_id,
            asset_hash: hash,
            transform: ForgeTransform::IDENTITY,
            locked: false,
            physics: None,
            particles: None,
            local_lights: None,
        };
        let mut world = World::new();
        world.init_resource::<Assets<Mesh>>();
        world.init_resource::<Assets<ShaderBuffer>>();
        world.init_resource::<Assets<FishMaterial>>();
        let material = world
            .resource_mut::<Assets<FishMaterial>>()
            .add(test_material());
        let materials = BTreeMap::from([(asset_id, material)]);
        let mut altered = placement.clone();
        altered.asset_hash = forge_core::bytes_sha256(b"different content");
        assert!(
            stage_authored(
                vec![test_template(asset_id, hash)],
                &mut world,
                &materials,
                &[altered],
                policy,
            )
            .is_err()
        );
        assert!(
            stage_authored(
                vec![test_template(asset_id, hash)],
                &mut world,
                &materials,
                &[placement.clone(), placement.clone()],
                policy,
            )
            .is_err()
        );
        assert!(
            stage_authored(
                vec![test_template(asset_id, hash), test_template(asset_id, hash)],
                &mut world,
                &materials,
                std::slice::from_ref(&placement),
                policy,
            )
            .is_err()
        );
        assert_eq!(world.resource::<Assets<Mesh>>().len(), 0);
        assert_eq!(world.resource::<Assets<ShaderBuffer>>().len(), 0);
        assert_eq!(world.resource::<Assets<FishMaterial>>().len(), 1);
        assert_eq!(
            world
                .query::<&ManualPlacementProjection>()
                .iter(&world)
                .count(),
            0
        );

        let mut clone = placement.clone();
        clone.id = EntityId::deterministic("independent-module:second");
        let mut resident = stage_authored(
            vec![test_template(asset_id, hash)],
            &mut world,
            &materials,
            &[placement.clone(), clone.clone()],
            policy,
        )
        .unwrap();
        let first = resident.authored_parts(placement.id).unwrap();
        let second = resident.authored_parts(clone.id).unwrap();
        let handles = [first[0], second[0]].map(|part| {
            world
                .get::<MeshMaterial3d<FishMaterial>>(part)
                .unwrap()
                .0
                .clone()
        });
        let first_clock = world
            .resource::<Assets<FishMaterial>>()
            .get(&handles[0])
            .unwrap()
            .clock
            .clone();
        let second_clock = world
            .resource::<Assets<FishMaterial>>()
            .get(&handles[1])
            .unwrap()
            .clock
            .clone();
        assert_eq!(first_clock, second_clock);
        assert_ne!(first_clock.id(), clock::NEUTRAL.id());
        resident
            .update_view(&mut world, Vec3::X * 5.0, |_, _| true, 1.0, 2)
            .unwrap();
        assert_eq!(resident.shadow_instances(&world).unwrap().len(), 2);
        let hidden_handle = world
            .get::<MeshMaterial3d<FishMaterial>>(first[1])
            .unwrap()
            .0
            .clone();
        let hidden_lighting = world
            .resource::<Assets<FishMaterial>>()
            .get(&hidden_handle)
            .unwrap()
            .scene_lighting
            .clone();
        let mut lighting = crate::source_surface::tests::lighting();
        resident
            .update_scene_receivers(&mut world, &lighting)
            .unwrap();
        assert_eq!(
            world
                .resource::<Assets<FishMaterial>>()
                .get(&handles[0])
                .unwrap()
                .scene_lighting,
            lighting.scene_lighting
        );
        assert_eq!(
            world
                .resource::<Assets<FishMaterial>>()
                .get(&hidden_handle)
                .unwrap()
                .scene_lighting,
            hidden_lighting
        );
        resident
            .update_view(&mut world, Vec3::X * 50.0, |_, _| true, 2.0, 3)
            .unwrap();
        assert!(resident.group.is_dormant());
        assert!(resident.shadow_instances(&world).unwrap().is_empty());
        lighting.scene_lighting.sun_color = Vec4::new(8.0, 7.0, 6.0, 0.0);
        resident
            .update_scene_receivers(&mut world, &lighting)
            .unwrap();
        assert_ne!(
            world
                .resource::<Assets<FishMaterial>>()
                .get(&handles[0])
                .unwrap()
                .scene_lighting,
            lighting.scene_lighting
        );
        let mut invalid = lighting.clone();
        invalid.scene_lighting.sun_direction = Vec4::ZERO;
        assert!(
            resident
                .update_scene_receivers(&mut world, &invalid)
                .is_err()
        );
        resident
            .update_view(&mut world, Vec3::X * 5.0, |_, _| true, 3.0, 4)
            .unwrap();
        resident
            .update_scene_receivers(&mut world, &lighting)
            .unwrap();
        assert_eq!(
            world
                .resource::<Assets<FishMaterial>>()
                .get(&handles[0])
                .unwrap()
                .scene_lighting,
            lighting.scene_lighting
        );
        resident.clear(&mut world);
        assert_eq!(world.resource::<Assets<Mesh>>().len(), 0);
        assert_eq!(world.resource::<Assets<ShaderBuffer>>().len(), 0);
        assert_eq!(world.resource::<Assets<FishMaterial>>().len(), 1);
        assert_eq!(
            world
                .query::<&ManualPlacementProjection>()
                .iter(&world)
                .count(),
            0
        );
    }

    #[test]
    #[allow(
        clippy::too_many_lines,
        reason = "Exercise asset events, frozen LOD and live time together"
    )]
    fn frozen_view_emits_no_material_uploads_but_camera_lod_and_time_still_update() {
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, AssetPlugin::default()))
            .init_asset::<FishMaterial>()
            .init_asset::<ShaderBuffer>();
        let mut material = test_material();
        let live_clock = clock::create(app.world_mut()).unwrap();
        material.clock = live_clock.clone();
        let handles = std::array::from_fn(|_| {
            app.world_mut()
                .resource_mut::<Assets<FishMaterial>>()
                .add(material.clone())
        });
        let root = app
            .world_mut()
            .spawn((group::Member, GlobalTransform::IDENTITY))
            .id();
        let parts = std::array::from_fn(|_| app.world_mut().spawn(Visibility::Hidden).id());
        let mut resident = ResidentFishProps {
            instances: vec![Instance {
                root,
                parts,
                materials: handles.clone(),
                shadow_mesh: Handle::default(),
                casts_shadow: false,
                policy: Policy::new(90.0, 81.0, 22.0, 28.0, 0.88).unwrap(),
            }],
            part_sources: HashMap::new(),
            authored: vec![],
            meshes: vec![],
            materials: handles.to_vec(),
            clock: Some(live_clock.clone()),
            owns_clock: true,
            group: group::Group::default(),
            policy: Policy::new(90.0, 81.0, 22.0, 28.0, 0.88).unwrap(),
            time: 0.0,
            frame_index: 0,
        };
        resident
            .update_view(
                app.world_mut(),
                Vec3::new(10.0, 0.0, 0.0),
                |_, _| true,
                0.0,
                0,
            )
            .unwrap();
        app.update();
        app.world_mut()
            .resource_mut::<Messages<AssetEvent<FishMaterial>>>()
            .clear();
        resident
            .update_view(
                app.world_mut(),
                Vec3::new(10.0, 0.0, 0.0),
                |_, _| true,
                0.0,
                0,
            )
            .unwrap();
        app.update();
        assert!(
            app.world()
                .resource::<Messages<AssetEvent<FishMaterial>>>()
                .is_empty()
        );
        assert_eq!(
            app.world().get::<Visibility>(parts[0]),
            Some(&Visibility::Inherited)
        );
        resident
            .update_view(
                app.world_mut(),
                Vec3::new(26.0, 0.0, 0.0),
                |_, _| true,
                0.0,
                0,
            )
            .unwrap();
        app.update();
        assert!(
            !app.world()
                .resource::<Messages<AssetEvent<FishMaterial>>>()
                .is_empty()
        );
        assert_eq!(
            app.world().get::<Visibility>(parts[0]),
            Some(&Visibility::Inherited)
        );
        assert_eq!(
            app.world().get::<Visibility>(parts[1]),
            Some(&Visibility::Inherited)
        );
        resident
            .update_view(
                app.world_mut(),
                Vec3::new(30.0, 0.0, 0.0),
                |_, _| true,
                0.0,
                0,
            )
            .unwrap();
        assert_eq!(
            app.world().get::<Visibility>(parts[0]),
            Some(&Visibility::Hidden)
        );
        assert_eq!(
            app.world().get::<Visibility>(parts[1]),
            Some(&Visibility::Inherited)
        );
        resident
            .update_view(
                app.world_mut(),
                Vec3::new(30.0, 0.0, 0.0),
                |_, _| true,
                2.0,
                7,
            )
            .unwrap();
        let value = app
            .world()
            .resource::<Assets<FishMaterial>>()
            .get(&handles[1])
            .unwrap();
        assert_eq!(value.clock, live_clock);
        let expected: Vec<_> = [2.0_f32.to_bits(), 7, 1, 0]
            .into_iter()
            .flat_map(u32::to_le_bytes)
            .collect();
        assert_eq!(
            app.world()
                .resource::<Assets<ShaderBuffer>>()
                .get(&live_clock)
                .unwrap()
                .data,
            Some(expected)
        );

        let second_root = app
            .world_mut()
            .spawn((group::Member, GlobalTransform::IDENTITY))
            .id();
        let second_parts = std::array::from_fn(|_| app.world_mut().spawn(Visibility::Hidden).id());
        let second_handles = std::array::from_fn(|index| {
            let value = app
                .world()
                .resource::<Assets<FishMaterial>>()
                .get(&handles[index])
                .unwrap()
                .clone();
            app.world_mut()
                .resource_mut::<Assets<FishMaterial>>()
                .add(value)
        });
        resident.materials.extend(second_handles.iter().cloned());
        resident.instances.push(Instance {
            root: second_root,
            parts: second_parts,
            materials: second_handles,
            shadow_mesh: Handle::default(),
            casts_shadow: false,
            policy: resident.policy,
        });
        resident.group.invalidate();
        let before_dormant = app
            .world()
            .resource::<Assets<ShaderBuffer>>()
            .get(&live_clock)
            .unwrap()
            .data
            .clone();
        let intersections = std::cell::Cell::new(0);
        resident
            .update_view(
                app.world_mut(),
                Vec3::new(1_000.0, 0.0, 0.0),
                |_, _| {
                    intersections.set(intersections.get() + 1);
                    true
                },
                3.0,
                8,
            )
            .unwrap();
        assert_eq!(intersections.get(), 1, "A dormant group is tested once");
        for part in parts.into_iter().chain(second_parts) {
            assert_eq!(
                app.world().get::<Visibility>(part),
                Some(&Visibility::Hidden)
            );
        }
        assert!(resident.shadow_instances(app.world()).unwrap().is_empty());
        assert_eq!(
            app.world()
                .resource::<Assets<ShaderBuffer>>()
                .get(&live_clock)
                .unwrap()
                .data,
            before_dormant
        );
        resident
            .update_view(
                app.world_mut(),
                Vec3::new(10.0, 0.0, 0.0),
                |_, _| true,
                4.0,
                9,
            )
            .unwrap();
        assert_eq!(resident.shadow_instances(app.world()).unwrap().len(), 2);
        for part in [parts[0], second_parts[0]] {
            assert_eq!(
                app.world().get::<Visibility>(part),
                Some(&Visibility::Inherited)
            );
        }
        resident
            .update_view(
                app.world_mut(),
                Vec3::new(1_000.0, 0.0, 0.0),
                |_, _| true,
                5.0,
                10,
            )
            .unwrap();
        app.world_mut().clear_trackers();
        app.world_mut()
            .entity_mut(second_root)
            .insert(GlobalTransform::from_translation(Vec3::X * 1_000.0));
        resident
            .update_view(
                app.world_mut(),
                Vec3::new(1_000.0, 0.0, 0.0),
                |_, _| true,
                6.0,
                11,
            )
            .unwrap();
        assert_eq!(
            app.world().get::<Visibility>(second_parts[0]),
            Some(&Visibility::Inherited)
        );
        assert_eq!(resident.shadow_instances(app.world()).unwrap().len(), 1);
    }
}
