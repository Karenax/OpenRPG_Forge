//! CPU projection and visibility evidence; neither draw completion nor pixel parity.
use bevy::prelude::*;
use forge_tidewater::{
    tidewater_beach_props::ResidentBeachProps, tidewater_material::VillageMaterial,
    tidewater_village_props::ResidentVillageProps,
};
use serde_json::{Value, json};

pub(super) fn observe(world: &World) -> Value {
    json!({
        "scope": "CPU placement, propagated transforms and view visibility; not GPU draw evidence",
        "village": world.get_resource::<ResidentVillageProps>()
            .map(|resident| family(world, resident.authored_roots(), resident.render_parts())),
        "beach": world.get_resource::<ResidentBeachProps>()
            .map(|resident| family(world, resident.authored_roots(), resident.render_parts())),
    })
}

fn family(
    world: &World,
    roots: &[(Entity, forge_core::EntityId, forge_core::Transform)],
    parts: &[Entity],
) -> Value {
    let roots = roots.iter().map(|(entity, id, accepted)| {
        json!({
            "id": id.to_string(),
            "name": world.get::<Name>(*entity).map(Name::as_str),
            "accepted_position": [accepted.translation_m.x, accepted.translation_m.y, accepted.translation_m.z],
            "local_position": world.get::<Transform>(*entity).map(|transform| transform.translation.to_array()),
            "world_position": world.get::<GlobalTransform>(*entity).map(|transform| transform.translation().to_array()),
            "inherited_visible": world.get::<InheritedVisibility>(*entity).map(|visibility| visibility.get()),
            "parts": parts.iter().filter(|part| world.get::<ChildOf>(**part)
                .is_some_and(|parent| parent.parent() == *entity))
                .map(|part| part_observation(world, *part)).collect::<Vec<_>>(),
        })
    }).collect::<Vec<_>>();
    json!({"root_count": roots.len(), "part_count": parts.len(), "roots": roots})
}

fn part_observation(world: &World, entity: Entity) -> Value {
    let material = world
        .get::<MeshMaterial3d<VillageMaterial>>(entity)
        .and_then(|handle| {
            world
                .get_resource::<Assets<VillageMaterial>>()?
                .get(&handle.0)
        });
    json!({
        "inherited_visible": world.get::<InheritedVisibility>(entity).map(|visibility| visibility.get()),
        "view_visible": world.get::<ViewVisibility>(entity).map(|visibility| visibility.get()),
        "world_position": world.get::<GlobalTransform>(entity).map(|transform| transform.translation().to_array()),
        "mesh_loaded": world.get::<Mesh3d>(entity).is_some_and(|handle|
            world.get_resource::<Assets<Mesh>>().is_some_and(|meshes| meshes.contains(handle.0.id()))),
        "material": material.map(|material| json!({
            "kind": material.kind,
            "opacity": material.settings.color.w,
            "alpha_test": material.settings.surface.z,
        })),
    })
}
