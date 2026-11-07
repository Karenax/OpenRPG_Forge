//! Shared masked-fish shadow geometry with cached immutable mesh conversion.
use super::super::resident::ShadowInstance;
use crate::source_shadows::casters::masked::{Frame, MaskedMesh, gpu::Pose};
use bevy::prelude::*;
use std::sync::Arc;

#[derive(Default)]
pub struct MaskedFishCache {
    key: Vec<(Entity, bevy::asset::AssetId<Mesh>)>,
    geometry: Option<Arc<Vec<MaskedMesh>>>,
}

impl MaskedFishCache {
    /// Invalidate cached geometry after any in-place mesh modification.
    pub fn clear(&mut self) {
        self.key.clear();
        self.geometry = None;
    }

    /// Prepare the caller's current fish contribution without uploading geometry twice.
    /// An empty inventory retires the cached geometry and produces no contribution.
    /// Meshes must remain immutable while cached; call `clear` after in-place edits.
    ///
    /// # Errors
    /// Rejects non-finite time, absent meshes or invalid masked geometry.
    pub fn frame(
        &mut self,
        world: &World,
        inventory: Vec<(ShadowInstance, bool)>,
        target: Handle<Image>,
        shader: Arc<String>,
        time: f32,
    ) -> Result<Option<Frame>, String> {
        if !time.is_finite() {
            return Err("Invalid fish shadow time".into());
        }
        if inventory.is_empty() {
            self.clear();
            return Ok(None);
        }
        let key: Vec<_> = inventory
            .iter()
            .map(|(instance, _)| (instance.root, instance.mesh.id()))
            .collect();
        if self.geometry.is_none() || self.key != key {
            let meshes = world
                .get_resource::<Assets<Mesh>>()
                .ok_or("Fish shadow mesh assets absent")?;
            let geometry = inventory
                .iter()
                .map(|(instance, _)| {
                    super::masked_mesh(
                        meshes
                            .get(&instance.mesh)
                            .ok_or("Fish shadow mesh absent")?,
                    )
                })
                .collect::<Result<Vec<_>, String>>()?;
            self.geometry = Some(Arc::new(geometry));
            self.key = key;
        }
        Ok(Some(Frame {
            target,
            shader,
            meshes: self
                .geometry
                .as_ref()
                .ok_or("Fish shadow geometry absent")?
                .clone(),
            poses: inventory
                .into_iter()
                .map(|(instance, enabled)| Pose {
                    model: instance.world_from_local,
                    enabled,
                })
                .collect(),
            time,
        }))
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::{
        asset::RenderAssetUsages, mesh::Indices, render::render_resource::PrimitiveTopology,
    };

    fn inventory(root: Entity, mesh: Handle<Mesh>, x: f32) -> Vec<(ShadowInstance, bool)> {
        vec![(
            ShadowInstance {
                root,
                mesh,
                world_from_local: Mat4::from_translation(Vec3::X * x),
                time: 1.0,
                frame_index: 2,
            },
            true,
        )]
    }

    #[test]
    fn shared_geometry_survives_pose_edits_and_rejects_bad_replacements() {
        let mut world = World::new();
        world.init_resource::<Assets<Mesh>>();
        let mesh = Mesh::new(
            PrimitiveTopology::TriangleList,
            RenderAssetUsages::default(),
        )
        .with_inserted_attribute(Mesh::ATTRIBUTE_POSITION, vec![[0.0, 0.0, 0.0]; 3])
        .with_inserted_attribute(crate::source_fish::geometry::FISH_DATA, vec![[0.0; 4]; 3])
        .with_inserted_indices(Indices::U32(vec![0, 1, 2]));
        let mesh = world.resource_mut::<Assets<Mesh>>().add(mesh);
        let root = world.spawn_empty().id();
        let mut cache = MaskedFishCache::default();
        let target = Handle::<Image>::default();
        let shader = Arc::new("admitted mask".to_owned());
        let first = cache
            .frame(
                &world,
                inventory(root, mesh.clone(), 1.0),
                target.clone(),
                shader.clone(),
                1.0,
            )
            .unwrap()
            .unwrap();
        let moved = cache
            .frame(
                &world,
                inventory(root, mesh.clone(), 2.0),
                target.clone(),
                shader.clone(),
                2.0,
            )
            .unwrap()
            .unwrap();
        assert!(Arc::ptr_eq(&first.meshes, &moved.meshes));
        assert!((moved.poses[0].model.w_axis.x - 2.0).abs() < f32::EPSILON);
        assert!(
            cache
                .frame(
                    &world,
                    inventory(root, Handle::default(), 3.0),
                    target.clone(),
                    shader.clone(),
                    3.0
                )
                .is_err()
        );
        let retained = cache
            .frame(
                &world,
                inventory(root, mesh.clone(), 4.0),
                target.clone(),
                shader.clone(),
                4.0,
            )
            .unwrap()
            .unwrap();
        assert!(Arc::ptr_eq(&first.meshes, &retained.meshes));
        assert!(
            cache
                .frame(&world, Vec::new(), target.clone(), shader.clone(), 5.0)
                .unwrap()
                .is_none()
        );
        let rebuilt = cache
            .frame(&world, inventory(root, mesh, 6.0), target, shader, 6.0)
            .unwrap()
            .unwrap();
        assert!(!Arc::ptr_eq(&first.meshes, &rebuilt.meshes));
    }
}
