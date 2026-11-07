//! Incremental CPU identity audit; GPU fences are still acquired on every ready call.
use super::{Resident, SourceMaterial, StreamedPlantPart};
use crate::source_vegetation::scene::ImportedPlant;
use bevy::{
    camera::visibility::NoFrustumCulling, ecs::query::QueryState, light::NotShadowCaster,
    mesh::MeshTag, prelude::*,
};

// Required and forbidden components make removals/archetype moves observable
// without visiting each entity's component values. Added replacements are also
// Changed, so a balanced removal/insertion cannot conceal a missing owned part.
type Shape<M> = (
    With<ImportedPlant>,
    With<Mesh3d>,
    With<MeshMaterial3d<M>>,
    With<MeshTag>,
    With<Transform>,
    With<Visibility>,
    With<NoFrustumCulling>,
    With<StreamedPlantPart>,
    Without<ChildOf>,
);
type Changes<M> = Or<(
    Changed<ImportedPlant>,
    Changed<Mesh3d>,
    Changed<MeshMaterial3d<M>>,
    Changed<MeshTag>,
    Changed<Transform>,
    Changed<Visibility>,
    Changed<StreamedPlantPart>,
    Changed<ChildOf>,
    Changed<NoFrustumCulling>,
    Changed<NotShadowCaster>,
)>;

pub(super) struct Audit<M: SourceMaterial> {
    shadowed: QueryState<Entity, (Shape<M>, Without<NotShadowCaster>)>,
    unshadowed: QueryState<Entity, (Shape<M>, With<NotShadowCaster>)>,
    changes: QueryState<Entity, (With<ImportedPlant>, Changes<M>)>,
    guard: crate::render_inventory::audit::Guard,
    #[cfg(test)]
    complete_audits: usize,
}

impl<M: SourceMaterial> Audit<M> {
    pub(super) fn new(world: &mut World) -> Self {
        let removed = [
            world.register_component::<ImportedPlant>(),
            world.register_component::<Mesh3d>(),
            world.register_component::<MeshMaterial3d<M>>(),
            world.register_component::<MeshTag>(),
            world.register_component::<Transform>(),
            world.register_component::<Visibility>(),
            world.register_component::<NoFrustumCulling>(),
            world.register_component::<StreamedPlantPart>(),
            world.register_component::<ChildOf>(),
            world.register_component::<NotShadowCaster>(),
        ];
        Self {
            shadowed: world.query_filtered(),
            unshadowed: world.query_filtered(),
            changes: world.query_filtered(),
            guard: crate::render_inventory::audit::Guard::new(removed),
            #[cfg(test)]
            complete_audits: 0,
        }
    }

    pub(super) fn intact(&mut self, world: &World, resident: &Resident<M>) -> bool {
        self.shadowed.update_archetypes(world);
        self.unshadowed.update_archetypes(world);
        self.changes.update_archetypes(world);
        let count = if M::enable_shadows() {
            self.shadowed.iter_manual(world).len()
        } else {
            self.unshadowed.iter_manual(world).len()
        };
        self.guard.intact(
            world,
            count,
            |previous, now| !self.changes.is_empty(world, previous, now),
            || {
                #[cfg(test)]
                {
                    self.complete_audits += 1;
                }
                resident.projection_intact(world)
            },
        ) && resident.inner.sources_intact(world)
    }
}

#[cfg(test)]
mod tests;
