//! Explicit renderer-resource barrier for an owning projection. Not proof of pixels or timing.
//! One exclusive primary owner and at most fourteen independent bounded scoped inventories.
//! A scoped request never replaces the primary projection's acknowledgement.
use crate::gpu_readiness::assets::Requirements;
pub use crate::gpu_readiness::effects::Requirements as RenderEffectRequirements;
use bevy::{
    asset::{UntypedAssetId, VisitAssetDependencies},
    pbr::Material,
    prelude::*,
};
use forge_core::{ContentHash, EntityId};
use std::{any::TypeId, collections::BTreeMap};

#[cfg(feature = "physics-rapier")]
pub(crate) mod audit;
mod gpu;
mod staging;
pub use gpu::{
    GraphicsContextReceipt, RenderInventoryEvidence, RenderInventoryPlugin, RenderInventoryToken,
    acquire, acquire_scoped, scoped_evidence,
};
pub(crate) use gpu::{PreparedGraphicsContext, prepared_graphics_context};
pub use staging::{ActivatedScopedInventory, StagedScopedInventory};

// Eleven display material families, independently owned sky and grass, and one
// spare scope for a single prepared replacement. Its receipt is independent.
// Each retains its own resource limits, generation and queue-completion token.
const MAX_SCOPED_INVENTORIES: usize = 14;

/// The source budget applied by every typed custom renderer inventory.
pub const MAX_SCOPED_MESHES: usize = crate::gpu_readiness::assets::MAX_CUSTOM_MESHES;

/// Include the actual base and extension of the engine's Bevy composites.
/// Bevy's `ExtendedMaterial` visitor does not visit either field; retaining its
/// native type keeps the renderer and binding path unchanged. Other material
/// types use their own declared visitor.
pub fn visit_material_dependencies<M: Material>(
    material: &M,
    visit: &mut impl FnMut(UntypedAssetId),
) {
    let value = material as &dyn std::any::Any;
    macro_rules! composite {
        ($ty:ty) => {
            if let Some(source) = value.downcast_ref::<$ty>() {
                source.base.visit_dependencies(visit);
                source.extension.visit_dependencies(visit);
                return;
            }
        };
    }
    composite!(crate::VisualValleyTerrainMaterial);
    composite!(crate::VisualValleyVegetationMaterial);
    composite!(crate::VisualValleyWaterMaterial);
    composite!(crate::VisualValleyRainMaterial);
    composite!(crate::source_surface::nature::NatureMaterial);
    #[cfg(feature = "physics-rapier")]
    {
        composite!(crate::source_graph::SourceGraphMaterial);
        composite!(crate::source_graph::SourceUnlitForwardMaterial);
    }
    material.visit_dependencies(visit);
}

#[derive(Resource, Default)]
struct ScopedRequests(BTreeMap<EntityId, Request>);

#[must_use]
pub fn current_owner(world: &World) -> Option<EntityId> {
    world.get_resource::<Request>().map(|request| request.owner)
}

#[derive(Resource, Clone)]
struct Request {
    owner: EntityId,
    identity: ContentHash,
    generation: u64,
    serial: u64,
    material_type: TypeId,
    required: Requirements,
    effects: Option<RenderEffectRequirements>,
    sources_exist: fn(&Requirements, &World) -> bool,
}

/// Survives release so a later request cannot revive an earlier GPU receipt.
#[derive(Resource, Default)]
struct RequestSerial(u64);

/// The projection owns immutable mesh/buffer sources. Dynamic uniforms may change;
/// authored identity, geometry, bindings or LOD-control changes require a new generation.
/// Materials must enumerate all their asset dependencies. Caller must warm all
/// specializations through the real renderer before publishing.
pub fn request<M: Material>(
    world: &mut World,
    owner: EntityId,
    identity: ContentHash,
    generation: u64,
    meshes: &[Handle<Mesh>],
    materials: &[Handle<M>],
) -> Result<(), String> {
    if world
        .get_resource::<ScopedRequests>()
        .is_some_and(|requests| requests.0.contains_key(&owner))
    {
        return Err("This owner already has a scoped render inventory".into());
    }
    let current = world.get_resource::<Request>().cloned();
    let request = prepare_request(
        world,
        current.as_ref(),
        owner,
        identity,
        generation,
        meshes,
        materials,
    )?;
    world.insert_resource(request);
    Ok(())
}

/// Additional independently revoked inventories, bounded to eleven owners. The
/// primary projection keeps its ownership and every material family stays typed.
pub fn request_scoped<M: Material>(
    world: &mut World,
    owner: EntityId,
    identity: ContentHash,
    generation: u64,
    meshes: &[Handle<Mesh>],
    materials: &[Handle<M>],
) -> Result<(), String> {
    if current_owner(world) == Some(owner) {
        return Err("The primary render inventory already uses this owner".into());
    }
    let requests = world.get_resource::<ScopedRequests>();
    let current = requests
        .and_then(|requests| requests.0.get(&owner))
        .cloned();
    if current.is_none()
        && requests.is_some_and(|requests| requests.0.len() >= MAX_SCOPED_INVENTORIES)
    {
        return Err("Scoped render inventory owner budget exceeded".into());
    }
    let request = prepare_request(
        world,
        current.as_ref(),
        owner,
        identity,
        generation,
        meshes,
        materials,
    )?;
    world.init_resource::<ScopedRequests>();
    world
        .resource_mut::<ScopedRequests>()
        .0
        .insert(owner, request);
    Ok(())
}

fn prepare_request<M: Material>(
    world: &mut World,
    current: Option<&Request>,
    owner: EntityId,
    identity: ContentHash,
    generation: u64,
    meshes: &[Handle<Mesh>],
    materials: &[Handle<M>],
) -> Result<Request, String> {
    if generation == 0 || owner.as_uuid().is_nil() {
        return Err("Render inventory requires a real owner and nonzero generation".into());
    }
    if let Some(current) = current {
        if current.owner != owner {
            return Err("Render inventory is already owned by another projection".into());
        }
        if generation < current.generation {
            return Err("Render inventory generation cannot regress".into());
        }
    }
    let required = Requirements::custom(world, meshes, materials)?;
    if let Some(current) = current
        && current.generation == generation
        && (current.identity != identity
            || current.required != required
            || current.material_type != TypeId::of::<M>())
    {
        return Err("Changed render sources need a new generation".into());
    }
    let serial = if let Some(current) = current
        && current.generation == generation
    {
        current.serial
    } else {
        let next = world
            .get_resource::<RequestSerial>()
            .map_or(0, |serial| serial.0)
            .checked_add(1)
            .ok_or("Render inventory request lifetime overflow")?;
        world.insert_resource(RequestSerial(next));
        next
    };
    Ok(Request {
        owner,
        identity,
        generation,
        serial,
        material_type: TypeId::of::<M>(),
        required,
        effects: current
            .filter(|request| request.generation == generation)
            .and_then(|request| request.effects.clone()),
        sources_exist: Requirements::sources_exist::<M>,
    })
}

/// A typed inventory plus the public GPU prerequisites of its display settings.
/// Changing these prerequisites requires a new generation, just like geometry.
pub fn request_scoped_with_effects<M: Material>(
    world: &mut World,
    owner: EntityId,
    identity: ContentHash,
    generation: u64,
    meshes: &[Handle<Mesh>],
    materials: &[Handle<M>],
    effects: RenderEffectRequirements,
) -> Result<(), String> {
    if world
        .get_resource::<ScopedRequests>()
        .and_then(|requests| requests.0.get(&owner))
        .is_some_and(|current| {
            current.generation == generation && current.effects.as_ref() != Some(&effects)
        })
    {
        return Err("Changed render effects need a new generation".into());
    }
    request_scoped(world, owner, identity, generation, meshes, materials)?;
    if let Some(request) = world.resource_mut::<ScopedRequests>().0.get_mut(&owner) {
        request.effects = Some(effects);
    }
    Ok(())
}

pub fn release_scoped(world: &mut World, owner: EntityId) {
    if let Some(mut requests) = world.get_resource_mut::<ScopedRequests>() {
        requests.0.remove(&owner);
    }
}

/// Retire an adapter's recorded generation without deleting a newer or foreign
/// request reusing that owner. Owners must remain exclusive between live hosts.
pub(crate) fn release_scoped_matching(
    world: &mut World,
    owner: EntityId,
    identity: ContentHash,
    generation: u64,
) {
    if world
        .get_resource::<ScopedRequests>()
        .and_then(|requests| requests.0.get(&owner))
        .is_some_and(|request| request.identity == identity && request.generation == generation)
    {
        release_scoped(world, owner);
    }
}

#[must_use]
pub fn scoped_sources_current(world: &World, owner: EntityId) -> bool {
    world
        .get_resource::<ScopedRequests>()
        .and_then(|requests| requests.0.get(&owner))
        .is_some_and(|request| (request.sources_exist)(&request.required, world))
}

/// CPU source containment only; the consumer must still acquire the current
/// renderer token and require a completed frame after each scene publication.
#[must_use]
pub fn scoped_contains_sources<M: Material>(
    world: &World,
    owner: EntityId,
    meshes: &[Handle<Mesh>],
    materials: &[Handle<M>],
) -> bool {
    world
        .get_resource::<ScopedRequests>()
        .and_then(|requests| requests.0.get(&owner))
        .is_some_and(|request| {
            request.material_type == TypeId::of::<M>()
                && (request.sources_exist)(&request.required, world)
                && Requirements::custom(world, meshes, materials)
                    .is_ok_and(|current| request.required.contains(&current))
        })
}

/// Retires the request only; the owner separately retires its entities and strong handles.
pub fn release(world: &mut World, owner: EntityId) -> Result<(), String> {
    if world
        .get_resource::<Request>()
        .is_some_and(|request| request.owner != owner)
    {
        return Err("Another projection owns this render inventory".into());
    }
    world.remove_resource::<Request>();
    Ok(())
}

#[cfg(test)]
mod tests;
