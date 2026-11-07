//! Model component watches using the same guard as source plant residence.
use super::{ImportedTreePresentation, OwnedPrimitive};
use crate::{
    render_inventory::audit::Guard, source_vegetation::scene::ImportedPlant,
    vegetation::VisualValleyVegetationMaterial,
};
use bevy::{
    camera::{
        primitives::Aabb,
        visibility::{NoAutoAabb, NoFrustumCulling, VisibilityRange},
    },
    ecs::query::QueryState,
    light::NotShadowCaster,
    mesh::MeshTag,
    prelude::*,
};

type Shape = (
    With<OwnedPrimitive>,
    With<Mesh3d>,
    Or<(
        With<MeshMaterial3d<VisualValleyVegetationMaterial>>,
        With<MeshMaterial3d<crate::source_lighting::surfaces::VegetationMaterial>>,
    )>,
    With<ImportedPlant>,
    With<Transform>,
    With<MeshTag>,
    With<Visibility>,
    With<Aabb>,
    With<NoAutoAabb>,
    With<VisibilityRange>,
    Without<ChildOf>,
    Without<NoFrustumCulling>,
);
type Changes = Or<(
    Changed<OwnedPrimitive>,
    Changed<Mesh3d>,
    Changed<MeshMaterial3d<VisualValleyVegetationMaterial>>,
    Changed<MeshMaterial3d<crate::source_lighting::surfaces::VegetationMaterial>>,
    Changed<crate::source_lighting::surfaces::host::Origin>,
    Changed<ImportedPlant>,
    Changed<Transform>,
    Changed<MeshTag>,
    Changed<Visibility>,
    Changed<Aabb>,
    Changed<NoAutoAabb>,
    Changed<VisibilityRange>,
    Changed<ChildOf>,
    Changed<NoFrustumCulling>,
    Changed<NotShadowCaster>,
)>;

pub(super) struct Audit {
    shape: QueryState<Entity, Shape>,
    changes: QueryState<Entity, (With<OwnedPrimitive>, Changes)>,
    guard: Guard,
    #[cfg(test)]
    pub complete_audits: usize,
}

impl Audit {
    pub(super) fn new(world: &mut World) -> Self {
        let components = [
            world.register_component::<OwnedPrimitive>(),
            world.register_component::<Mesh3d>(),
            world.register_component::<MeshMaterial3d<VisualValleyVegetationMaterial>>(),
            world.register_component::<MeshMaterial3d<crate::source_lighting::surfaces::VegetationMaterial>>(),
            world.register_component::<crate::source_lighting::surfaces::host::Origin>(),
            world.register_component::<ImportedPlant>(),
            world.register_component::<Transform>(),
            world.register_component::<MeshTag>(),
            world.register_component::<Visibility>(),
            world.register_component::<Aabb>(),
            world.register_component::<NoAutoAabb>(),
            world.register_component::<VisibilityRange>(),
            world.register_component::<ChildOf>(),
            world.register_component::<NoFrustumCulling>(),
            world.register_component::<NotShadowCaster>(),
        ];
        Self {
            shape: world.query_filtered(),
            changes: world.query_filtered(),
            guard: Guard::new(components),
            #[cfg(test)]
            complete_audits: 0,
        }
    }

    pub(super) fn intact(&mut self, world: &World, model: &ImportedTreePresentation) -> bool {
        self.shape.update_archetypes(world);
        self.changes.update_archetypes(world);
        let count = self.shape.iter_manual(world).len();
        self.guard.intact(
            world,
            count,
            |previous, now| !self.changes.is_empty(world, previous, now),
            || {
                #[cfg(test)]
                {
                    self.complete_audits += 1;
                }
                count == model.entities.len() && model.complete_intact(world)
            },
        ) && count == model.entities.len()
    }
}
