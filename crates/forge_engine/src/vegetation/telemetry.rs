//! Counts of live mesh bindings, not GPU draw calls or post-occlusion triangle counts.
use bevy::{
    asset::{AssetId, Assets},
    mesh::{Indices, Mesh},
    prelude::Handle,
    render::render_resource::PrimitiveTopology,
};
use serde::Serialize;
use std::collections::HashMap;

#[derive(Clone, Debug, Default, Eq, PartialEq, Serialize)]
pub struct VegetationLiveMeshStats {
    pub mesh_entities: u64,
    pub unique_mesh_assets: u64,
    pub unique_mesh_triangles: u64,
    pub all_lods_instance_triangles: u64,
    pub cpu_visible_triangles: u64,
    pub uncountable_mesh_assets: u64,
}

pub(super) fn count_mesh(
    stats: &mut VegetationLiveMeshStats,
    cache: &mut HashMap<AssetId<Mesh>, Option<u64>>,
    meshes: &Assets<Mesh>,
    handle: &Handle<Mesh>,
    cpu_visible: bool,
) {
    stats.mesh_entities += 1;
    let triangles = *cache.entry(handle.id()).or_insert_with(|| {
        stats.unique_mesh_assets += 1;
        let count = meshes.get(handle).and_then(triangle_count);
        if let Some(count) = count {
            stats.unique_mesh_triangles += count;
        } else {
            stats.uncountable_mesh_assets += 1;
        }
        count
    });
    if let Some(triangles) = triangles {
        stats.all_lods_instance_triangles += triangles;
        if cpu_visible {
            stats.cpu_visible_triangles += triangles;
        }
    }
}

fn triangle_count(mesh: &Mesh) -> Option<u64> {
    if mesh.primitive_topology() != PrimitiveTopology::TriangleList {
        return None;
    }
    let corners = mesh
        .indices()
        .map_or_else(|| mesh.count_vertices(), Indices::len);
    if !corners.is_multiple_of(3) {
        return None;
    }
    u64::try_from(corners / 3).ok()
}

#[cfg(test)]
mod tests {
    use super::super::{
        GovernedTreePrimitive, VegetationChunkBatchMarker, VegetationPlacementMarker,
        VegetationProjectionLod, VegetationProjectionStats, VegetationVisibilityObservationStatus,
        observe_vegetation_visibility,
    };
    use super::*;
    use bevy::{
        asset::RenderAssetUsages,
        camera::visibility::{VisibleEntities, VisibleEntityRanges},
        prelude::{App, Camera3d, Cuboid, Mesh3d, Update},
    };
    use forge_project::VegetationKind;
    use forge_world::VegetationInstanceId;
    use std::any::TypeId;

    #[test]
    fn shared_assets_are_counted_once_but_instances_and_visibility_are_separate() {
        let mut meshes = Assets::<Mesh>::default();
        let handle = meshes.add(Cuboid::default());
        let mut stats = VegetationLiveMeshStats::default();
        let mut cache = HashMap::new();
        count_mesh(&mut stats, &mut cache, &meshes, &handle, true);
        count_mesh(&mut stats, &mut cache, &meshes, &handle, false);
        assert_eq!(stats.mesh_entities, 2);
        assert_eq!(stats.unique_mesh_assets, 1);
        assert_eq!(stats.unique_mesh_triangles, 12);
        assert_eq!(stats.all_lods_instance_triangles, 24);
        assert_eq!(stats.cpu_visible_triangles, 12);
        assert_eq!(stats.uncountable_mesh_assets, 0);
    }

    #[test]
    fn visibility_joins_live_governed_meshes_and_excludes_replaced_placeholders() {
        let mut app = App::new();
        app.init_resource::<Assets<Mesh>>()
            .init_resource::<VegetationProjectionStats>()
            .init_resource::<VisibleEntityRanges>()
            .add_systems(Update, observe_vegetation_visibility);
        let handle = app
            .world_mut()
            .resource_mut::<Assets<Mesh>>()
            .add(Cuboid::default());
        let id: VegetationInstanceId =
            serde_json::from_str("\"11111111-1111-4111-8111-111111111111\"").unwrap();
        let near = app
            .world_mut()
            .spawn((
                Mesh3d(handle.clone()),
                GovernedTreePrimitive {
                    placement_id: id,
                    lod: 0,
                },
            ))
            .id();
        app.world_mut().spawn((
            Mesh3d(handle.clone()),
            GovernedTreePrimitive {
                placement_id: id,
                lod: 2,
            },
        ));
        app.world_mut().spawn((
            Mesh3d(handle.clone()),
            VegetationChunkBatchMarker {
                chunk_x: 0,
                chunk_z: 0,
                lod: VegetationProjectionLod::GrassBatch,
                logical_placements: 1,
            },
        ));
        // This collider/placement placeholder has no mesh after governed projection.
        app.world_mut().spawn(VegetationPlacementMarker {
            id,
            layer_id: forge_project::VegetationLayerId::new("trees"),
            kind: VegetationKind::Tree,
            lod: VegetationProjectionLod::Near,
        });
        // Unrelated scene geometry must not inflate vegetation counts.
        app.world_mut().spawn(Mesh3d(handle));
        let mut visible = VisibleEntities::default();
        visible.get_mut(TypeId::of::<Mesh3d>()).push(near);
        let camera = app.world_mut().spawn((Camera3d::default(), visible)).id();
        app.update();
        let stats = app.world().resource::<VegetationProjectionStats>();
        assert_eq!(stats.visibility.total.total, 3);
        assert_eq!(stats.visibility.near.total, 1);
        assert_eq!(stats.visibility.hlod.total, 1);
        assert_eq!(stats.visibility.grass_batches.total, 1);
        let meshes = stats.live_meshes.as_ref().unwrap();
        assert_eq!(meshes.mesh_entities, 3);
        assert_eq!(meshes.unique_mesh_assets, 1);
        assert_eq!(meshes.all_lods_instance_triangles, 36);
        assert_eq!(meshes.cpu_visible_triangles, 12);
        let second = app.world_mut().spawn(Camera3d::default()).id();
        app.update();
        assert!(
            app.world()
                .resource::<VegetationProjectionStats>()
                .live_meshes
                .is_none()
        );
        app.world_mut().despawn(camera);
        app.world_mut().despawn(second);
        app.update();
        assert!(
            app.world()
                .resource::<VegetationProjectionStats>()
                .live_meshes
                .is_none()
        );
        assert_eq!(
            app.world()
                .resource::<VegetationProjectionStats>()
                .visibility
                .status,
            VegetationVisibilityObservationStatus::UnavailableNoActiveCamera
        );
    }

    #[test]
    fn missing_or_non_triangle_meshes_are_not_reported_as_complete_zero_geometry() {
        let mut meshes = Assets::<Mesh>::default();
        let handle = meshes.add(Cuboid::default());
        meshes.remove(handle.id());
        let line = meshes.add(Mesh::new(
            PrimitiveTopology::LineList,
            RenderAssetUsages::default(),
        ));
        let mut stats = VegetationLiveMeshStats::default();
        let mut cache = HashMap::new();
        count_mesh(&mut stats, &mut cache, &meshes, &handle, true);
        count_mesh(&mut stats, &mut cache, &meshes, &handle, true);
        count_mesh(&mut stats, &mut cache, &meshes, &line, true);
        assert_eq!(stats.mesh_entities, 3);
        assert_eq!(stats.unique_mesh_assets, 2);
        assert_eq!(stats.uncountable_mesh_assets, 2);
        assert_eq!(stats.all_lods_instance_triangles, 0);
    }
}
