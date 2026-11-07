use super::{
    CachedMesh, MAX_PRIMITIVES, MAX_SOURCE_BYTES, Request, Routes, Source, State,
    StaticHlodMaterial, StaticHlodSettings, StaticHlodSource, selection,
};
use bevy::{
    asset::AssetId,
    camera::visibility::RenderLayers,
    light::{NotShadowCaster, NotShadowReceiver},
    mesh::MeshTag,
    prelude::*,
};
use forge_core::EntityId;
use std::{
    collections::{BTreeMap, BTreeSet},
    fmt::Write,
    sync::Arc,
};

pub(super) fn request(world: &mut World, state: &mut State) -> Result<Option<Request>, String> {
    let raw: Vec<_> = world
        .query::<(Entity, &StaticHlodSource, &Mesh3d)>()
        .iter(world)
        .map(|(entity, source, mesh)| (entity, *source, mesh.id()))
        .collect();
    if raw.len() > MAX_PRIMITIVES {
        return Err("Static HLOD source primitive budget exceeded".into());
    }
    let used: BTreeSet<_> = raw.iter().map(|(_, _, mesh)| *mesh).collect();
    state.cache.retain(|id, _| used.contains(id));
    let modified: BTreeSet<_> = world
        .get_resource::<Messages<AssetEvent<Mesh>>>()
        .map(|messages| {
            state
                .mesh_events
                .read(messages)
                .filter_map(|event| match event {
                    AssetEvent::Modified { id } => Some(*id),
                    _ => None,
                })
                .collect()
        })
        .unwrap_or_default();
    if state
        .live
        .iter()
        .any(|live| modified.contains(&live.mesh.id()))
    {
        return Err("Static HLOD owned derived mesh was changed outside its admission".into());
    }
    state.modified_meshes.extend(modified);
    state.modified_meshes.retain(|id| used.contains(id));
    let routes = world.resource::<Routes>().0.clone();
    let families: BTreeSet<_> = routes.iter().map(|route| route.family).collect();
    if families.len() != routes.len() {
        return Err("Static HLOD material routes contain a duplicate family".into());
    }
    let mut sources = Vec::with_capacity(raw.len());
    let mut bounds: BTreeMap<EntityId, (Vec3, Vec3, bool)> = BTreeMap::new();
    for (entity, identity, mesh) in raw {
        cache(world, state, mesh, state.modified_meshes.contains(&mesh))?;
        let source = inspect(world, state, &routes, entity, identity, mesh)?;
        let cached = &state.cache[&mesh];
        let (minimum, maximum) = transformed_bounds(cached, source.pose);
        if !minimum.is_finite() || !maximum.is_finite() {
            return Err("Static HLOD transformed bounds are not finite".into());
        }
        // One retained/deforming part keeps the complete authored object in
        // detail. Coverage still tracks every original primitive and pin.
        let visible =
            is_visible(world, entity, &state.hidden) && source.can_bake && !source.retained_motion;
        bounds
            .entry(identity.object)
            .and_modify(|(min, max, all_visible)| {
                *min = min.min(minimum);
                *max = max.max(maximum);
                *all_visible &= visible;
            })
            .or_insert((minimum, maximum, visible));
        sources.push(source);
    }
    sources.sort_by_key(|source| (source.identity.object, source.entity));
    let Some(selection) = crate::cell_streaming::render_selection(world) else {
        return Ok(None);
    };
    let enabled = world.resource::<StaticHlodSettings>().enabled;
    for source in &mut sources {
        let (min, max, visible) = bounds[&source.identity.object];
        source.node = (visible && enabled)
            .then(|| selection::node(&selection, min, max))
            .flatten();
    }
    let groups = selection::groups(&selection, &sources, &state.cache);
    Ok(Some(Request {
        enabled,
        terrain: selection.source,
        sources,
        groups,
    }))
}

fn cache(
    world: &World,
    state: &mut State,
    id: AssetId<Mesh>,
    modified: bool,
) -> Result<(), String> {
    let mesh = world
        .get_resource::<Assets<Mesh>>()
        .and_then(|assets| assets.get(id))
        .ok_or("Static HLOD pinned source mesh is missing")?;
    if let Some(cached) = state.cache.get(&id) {
        if modified && *cached.mesh != *mesh {
            return Err(
                "Static HLOD pinned mesh changed; replace the admitted source handle".into(),
            );
        }
        state.modified_meshes.remove(&id);
        return Ok(());
    }
    crate::static_hlod_geometry::validate_mesh(mesh).map_err(|error| error.to_string())?;
    let bytes = crate::static_hlod_geometry::buffer_bytes(mesh);
    if state.cache.values().map(|cached| cached.bytes).sum::<u64>() + bytes > MAX_SOURCE_BYTES {
        return Err("Static HLOD source snapshot buffer budget exceeded".into());
    }
    let positions = mesh
        .attribute(Mesh::ATTRIBUTE_POSITION)
        .and_then(bevy::mesh::VertexAttributeValues::as_float3)
        .ok_or("Static HLOD positions are missing")?;
    let (minimum, maximum) = positions.iter().fold(
        (Vec3::splat(f32::INFINITY), Vec3::splat(f32::NEG_INFINITY)),
        |(min, max), value| {
            let point = Vec3::from_array(*value);
            (min.min(point), max.max(point))
        },
    );
    let mut layout = String::new();
    for (attribute, values) in mesh.attributes() {
        write!(
            &mut layout,
            "{:?}:{:?};",
            attribute.id,
            bevy::render::render_resource::VertexFormat::from(values)
        )
        .expect("writing a String cannot fail");
    }
    state.cache.insert(
        id,
        CachedMesh {
            mesh: Arc::new(mesh.clone()),
            minimum,
            maximum,
            bytes,
            triangles: mesh
                .indices()
                .map_or(mesh.count_vertices(), bevy::mesh::Indices::len)
                / 3,
            layout,
        },
    );
    Ok(())
}

pub(super) fn inspect(
    world: &World,
    state: &State,
    routes: &[StaticHlodMaterial],
    entity: Entity,
    identity: StaticHlodSource,
    mesh: AssetId<Mesh>,
) -> Result<Source, String> {
    if world.get::<MeshTag>(entity).is_some_and(|tag| tag.0 != 0)
        || world
            .get::<bevy::camera::visibility::VisibilityRange>(entity)
            .is_some()
        || world
            .get::<bevy::mesh::skinning::SkinnedMesh>(entity)
            .is_some()
    {
        return Err(
            "Static HLOD cannot bake per-instance tags, skinning or visibility ranges".into(),
        );
    }
    let mut matches = routes.iter().enumerate().filter_map(|(index, route)| {
        (route.inspect)(world, entity).map(|material| (index, material))
    });
    let (route, material) = matches
        .next()
        .ok_or("Static HLOD has no registered material route")?;
    if matches.next().is_some() {
        return Err("Static HLOD source has ambiguous material routes".into());
    }
    let material = material?;
    let global = crate::physics::current_global(world, entity)
        .ok_or("Static HLOD source hierarchy is invalid")?;
    let retained_motion = super::moving_object_retained(world, identity.object);
    let actual_pose = global.compute_transform();
    let original = global.affine().to_cols_array();
    let rebuilt = GlobalTransform::from(actual_pose).affine().to_cols_array();
    if original.iter().any(|value| !value.is_finite())
        || (!retained_motion
            && (!actual_pose.translation.is_finite()
                || !actual_pose.scale.is_finite()
                || actual_pose.scale.min_element() <= 0.0
                || !actual_pose.rotation.is_normalized()
                || original
                    .iter()
                    .zip(rebuilt)
                    .any(|(a, b)| !b.is_finite() || (a - b).abs() > 1.0e-4 * a.abs().max(1.0))))
    {
        return Err("Static HLOD source pose contains unsupported shear or reflection".into());
    }
    let visibility = state
        .hidden
        .get(&entity)
        .copied()
        .or_else(|| world.get::<Visibility>(entity).copied())
        .ok_or("Static HLOD source has no visibility component")?;
    Ok(Source {
        entity,
        identity,
        mesh,
        material: material.handle,
        images: material.images,
        route,
        can_bake: material.can_bake,
        // Retained objects are never baked. Their changing poses must not
        // invalidate unrelated static batches on every animation frame.
        pose: if retained_motion {
            Transform::IDENTITY
        } else {
            actual_pose
        },
        retained_motion,
        visibility,
        layers: world
            .get::<RenderLayers>(entity)
            .cloned()
            .unwrap_or_default(),
        flags: u8::from(world.get::<NotShadowCaster>(entity).is_some())
            | (u8::from(world.get::<NotShadowReceiver>(entity).is_some()) << 1),
        custom: (routes[route].flags)(world, entity),
        node: None,
    })
}

fn transformed_bounds(cached: &CachedMesh, pose: Transform) -> (Vec3, Vec3) {
    let mut minimum = Vec3::splat(f32::INFINITY);
    let mut maximum = Vec3::splat(f32::NEG_INFINITY);
    for x in [cached.minimum.x, cached.maximum.x] {
        for y in [cached.minimum.y, cached.maximum.y] {
            for z in [cached.minimum.z, cached.maximum.z] {
                let point = pose.transform_point(Vec3::new(x, y, z));
                minimum = minimum.min(point);
                maximum = maximum.max(point);
            }
        }
    }
    (minimum, maximum)
}

pub(super) fn is_visible(
    world: &World,
    mut entity: Entity,
    hidden: &BTreeMap<Entity, Visibility>,
) -> bool {
    for _ in 0..128 {
        let visibility = hidden
            .get(&entity)
            .copied()
            .or_else(|| world.get::<Visibility>(entity).copied());
        match visibility {
            Some(Visibility::Hidden) => return false,
            Some(Visibility::Visible) => return true,
            _ => {}
        }
        let Some(parent) = world.get::<ChildOf>(entity) else {
            return true;
        };
        entity = parent.parent();
    }
    false
}

pub(super) fn source_is_intact(world: &World, state: &State, source: &Source) -> bool {
    if world.get::<StaticHlodSource>(source.entity) != Some(&source.identity)
        || world
            .get::<Mesh3d>(source.entity)
            .is_none_or(|mesh| mesh.id() != source.mesh)
    {
        return false;
    }
    let Some(routes) = world.get_resource::<Routes>() else {
        return false;
    };
    inspect(
        world,
        state,
        &routes.0,
        source.entity,
        source.identity,
        source.mesh,
    )
    .is_ok_and(|mut current| {
        current.node = source.node;
        current == *source
            && (source.node.is_none() || is_visible(world, source.entity, &state.hidden))
    })
}
