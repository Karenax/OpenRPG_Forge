//! Optional shared renderer barrier for an editor consuming the admitted water host.
//! Existing game graphics admission can continue to own its own material scopes.
use super::{Status, source_ocean, source_shore_sim, source_water_optics};
use crate::{render_inventory, source_breaker_resident as breakers};
use bevy::prelude::*;
use forge_core::{ContentHash, EntityId};
use std::collections::{BTreeMap, BTreeSet};
use std::sync::atomic::Ordering;

#[derive(Resource, Default)]
struct Fences {
    owner: Option<EntityId>,
    leases: BTreeMap<&'static str, Lease>,
}

struct Lease {
    key: ContentHash,
    generation: u64,
    published: u32,
}

/// CPU publications and encoded compute flags alone never release this barrier.
/// Each actual draw family needs its own prepared resources and completed queue.
pub fn ready(world: &mut World, owner: EntityId) -> Result<bool, String> {
    let Some(status) = world.get_resource::<Status>() else {
        return Ok(false);
    };
    if let Some(error) = &status.error {
        return Err(error.clone());
    }
    let document = status.document.ok_or("Water document absent")?;
    if !status.staged {
        return Ok(false);
    }
    let ocean = world
        .get_resource::<source_ocean::Targets>()
        .ok_or("Water FFT absent")?;
    let shore = world
        .get_resource::<source_shore_sim::Targets>()
        .ok_or("Water shore absent")?;
    if let Some(error) = shore
        .error
        .lock()
        .map_err(|_| "Shore error lock failed")?
        .clone()
    {
        return Err(error);
    }
    let effects = world
        .get_resource::<breakers::Targets>()
        .ok_or("Water effects absent")?;
    if let Some(error) = effects.error() {
        return Err(error);
    }
    if !ocean.ready.load(Ordering::Acquire)
        || !shore.ready.load(Ordering::Acquire)
        || shore.document_hash != document
        || !effects.ready.load(Ordering::Acquire)
        || !breakers::display_ready(world)
    {
        return Ok(false);
    }
    let no_lip = effects.input.ranges().is_empty();
    super::validate_projection(world)?;
    world.init_resource::<Fences>();
    world.resource_scope(|world, mut fences: Mut<Fences>| {
        if fences.owner.is_some_and(|current| current != owner) {
            return Err("Water renderer admission already has another owner".into());
        }
        fences.owner = Some(owner);
        let water = family::<source_water_optics::OpticalMaterial>(
            world,
            owner,
            document,
            "surface",
            &mut fences,
            super::owns,
            false,
        )?;
        let lip = family::<crate::source_breaker_render::LipMaterial>(
            world,
            owner,
            document,
            "lip",
            &mut fences,
            breakers::owns_lip,
            no_lip,
        )?;
        let spray = family::<crate::source_spray_render::SprayMaterial>(
            world,
            owner,
            document,
            "spray",
            &mut fences,
            breakers::owns_spray,
            false,
        )?;
        Ok(water && lip && spray)
    })
}

#[allow(
    clippy::too_many_arguments,
    reason = "One typed path retains each family's ownership, empty-case rule and lease"
)]
fn family<M: Material>(
    world: &mut World,
    owner: EntityId,
    document: ContentHash,
    name: &'static str,
    fences: &mut Fences,
    owns: fn(&World, Entity) -> bool,
    allow_empty: bool,
) -> Result<bool, String> {
    let mut meshes = BTreeMap::new();
    let mut materials = BTreeMap::new();
    for (entity, mesh, material) in world
        .query::<(Entity, &Mesh3d, &MeshMaterial3d<M>)>()
        .iter(world)
    {
        if !owns(world, entity) {
            return Err("Foreign material in the admitted water family".into());
        }
        if crate::source_ocean_geometry::inactive_tile(world, entity) {
            continue;
        }
        meshes.insert(mesh.id(), mesh.0.clone());
        materials.insert(material.id(), material.0.clone());
    }
    if std::any::TypeId::of::<M>() == std::any::TypeId::of::<source_water_optics::OpticalMaterial>()
        && let Some(retained) = crate::source_ocean_geometry::retained_meshes(world)
    {
        for mesh in retained {
            meshes.insert(mesh.id(), mesh);
        }
    }
    let scope = EntityId::deterministic(format!("water-view:{owner}:{name}"));
    if meshes.is_empty() {
        if allow_empty {
            render_inventory::release_scoped(world, scope);
            fences.leases.remove(name);
        }
        return Ok(allow_empty);
    }
    let Some(assets) = world.get_resource::<Assets<M>>() else {
        return Ok(false);
    };
    let mut dependencies = BTreeSet::new();
    for material in materials.values() {
        let Some(material) = assets.get(material) else {
            return Ok(false);
        };
        render_inventory::visit_material_dependencies(material, &mut |id| {
            dependencies.insert(format!("{id:?}"));
        });
    }
    let identity = format!(
        "{document}:{:?}:{:?}:{dependencies:?}",
        meshes.keys(),
        materials.keys()
    );
    let key = forge_core::bytes_sha256(identity.as_bytes());
    let meshes: Vec<_> = meshes.into_values().collect();
    let materials: Vec<_> = materials.into_values().collect();
    let published = world
        .get_resource::<bevy::diagnostic::FrameCount>()
        .ok_or("Water frame absent")?
        .0;
    if fences.leases.get(name).is_none_or(|lease| lease.key != key) {
        let generation = fences
            .leases
            .get(name)
            .map_or(Some(1), |lease| lease.generation.checked_add(1))
            .ok_or("Water renderer generation exhausted")?;
        render_inventory::request_scoped(world, scope, key, generation, &meshes, &materials)?;
        fences.leases.insert(
            name,
            Lease {
                key,
                generation,
                published,
            },
        );
    }
    let lease = &fences.leases[name];
    if render_inventory::scoped_evidence(world, scope).is_some_and(|evidence| {
        evidence.identity == Some(key)
            && evidence.generation == Some(lease.generation)
            && evidence.pipelines_failed > 0
    }) {
        return Err(format!("Water {name} render pipeline failed"));
    }
    Ok(
        render_inventory::acquire_scoped(world, scope, key, lease.generation).is_some_and(
            |token| {
                token.covers_sources(world, &meshes, &materials)
                    && token.covers_frame(world, lease.published)
            },
        ),
    )
}
