use super::{
    Admission, Prepared, Selection, StreamedPlantPart, StreamedPlantSelection, Vegetation,
    WorldEnvelopes, shadow_range::PlantShadowBounds,
};
use crate::render_inventory::{self, RenderInventoryToken};
use crate::source_vegetation::world::SourceMaterial;
use bevy::prelude::{Vec3, Visibility, World};
use forge_core::{ContentHash, EntityId};
use forge_world::imported_vegetation::streaming::PlantRepresentation;
use std::{collections::BTreeMap, sync::Mutex};
mod audit;

pub struct Resident<M: SourceMaterial> {
    prepared: Prepared,
    inner: crate::source_vegetation::scene::resident::Resident<M>,
    owner: EntityId,
    generation: u64,
    published: bool,
    selected: usize,
    parts: BTreeMap<(EntityId, u8), PlantRepresentation>,
    indices: Vec<Vec<Vec<usize>>>,
    publication_frame: u32,
    sequence: u64,
    audit: Mutex<audit::Audit<M>>,
}

impl<M: SourceMaterial> Resident<M> {
    /// Warm one real authored instance per source level, under the original ranges.
    /// This warms resources/pipelines, never claims the entire population is published.
    pub fn stage(
        world: &mut World,
        mut prepared: Prepared,
        base: &M,
        owner: EntityId,
    ) -> Result<Self, String> {
        if owner.as_uuid().is_nil() {
            return Err("Streamed plant projection requires a real owner".into());
        }
        if base.params().lod_end_frame.z.to_bits() != 0.0_f32.to_bits()
            || base.params().lod_end_frame.w.to_bits() != 0.0_f32.to_bits()
        {
            return Err(
                "Plant staging requires reference selection and normal surface output".into(),
            );
        }
        if render_inventory::current_owner(world).is_some() {
            return Err(
                "Retire the previous render inventory before staging another plant projection"
                    .into(),
            );
        }
        let scene = std::mem::replace(
            &mut prepared.scene,
            Vegetation {
                head_hash: Some(prepared.head),
                groups: Vec::new(),
            },
        );
        let mut inner =
            crate::source_vegetation::scene::resident::Resident::stage(world, scene, base)?;
        if prepared.pins.is_empty() {
            render_inventory::release(world, owner)?;
        } else {
            let warm: Vec<_> = prepared
                .levels
                .iter()
                .map(|&levels| vec![vec![0]; levels])
                .collect();
            inner.select_indices(world, &warm, Visibility::Visible)?;
            let (meshes, materials) = inner.handles();
            if let Err(error) =
                render_inventory::request(world, owner, prepared.identity, 1, &meshes, &materials)
            {
                inner.retire(world);
                return Err(error);
            }
        }
        Ok(Self {
            prepared,
            inner,
            owner,
            generation: 1,
            published: false,
            selected: 0,
            parts: BTreeMap::new(),
            indices: Vec::new(),
            publication_frame: 0,
            sequence: 0,
            audit: Mutex::new(audit::Audit::new(world)),
        })
    }

    #[must_use]
    pub fn generation(&self) -> u64 {
        self.generation
    }

    #[must_use]
    pub fn head_hash(&self) -> ContentHash {
        self.prepared.head
    }

    /// Exact owned material handles; callers must renew after changing bindings.
    #[must_use]
    pub fn materials(&self) -> Vec<bevy::prelude::Handle<M>> {
        self.inner.materials()
    }

    /// Snapshot the published source instances, never a newly generated LOD set.
    pub fn picking_snapshot<P>(
        &self,
        world: &World,
        material: impl FnMut(&M, u32, u32) -> Result<P, String>,
    ) -> Result<crate::source_vegetation::scene::resident::picking::Snapshot<P>, String> {
        if !self.published || !self.projection_intact(world) {
            return Err("Plant picking requires an intact published selection".into());
        }
        self.inner.picking_snapshot(world, material)
    }

    pub fn renew_bindings(&mut self, world: &mut World) -> Result<(), String> {
        let next = self
            .generation
            .checked_add(1)
            .ok_or("Plant generation overflow")?;
        if !self.prepared.pins.is_empty() {
            let (meshes, materials) = self.inner.handles();
            render_inventory::request(
                world,
                self.owner,
                self.prepared.identity,
                next,
                &meshes,
                &materials,
            )?;
        }
        self.generation = next;
        self.publication_frame = world
            .get_resource::<bevy::diagnostic::FrameCount>()
            .map_or(0, |frame| frame.0);
        Ok(())
    }
    #[must_use]
    pub fn identity(&self) -> ContentHash {
        self.prepared.identity
    }
    #[must_use]
    pub fn selection_sequence(&self) -> u64 {
        self.sequence
    }

    #[must_use]
    pub fn entity_count(&self) -> usize {
        self.inner.entity_count()
    }

    #[must_use]
    pub fn matches(&self, selection: &Selection) -> bool {
        self.published
            && selection.identity == self.prepared.identity
            && self.parts == selection.parts
            && self.indices == selection.indices
    }

    /// CPU identity and fixed LOD controls; owned mesh/buffer payloads stay immutable.
    #[must_use]
    pub fn projection_intact(&self, world: &World) -> bool {
        self.inner.is_intact(world)
            && (!self.published
                || self.inner.entities_with_lod().all(|(entity, lod)| {
                    world.get::<StreamedPlantPart>(entity).is_some_and(|part| {
                        usize::from(part.source_lod) == lod
                            && self.parts.get(&(part.id, part.source_lod))
                                == Some(&part.representation)
                            && world
                                .get::<crate::source_vegetation::scene::ImportedPlant>(entity)
                                .is_some_and(|plant| plant.id == part.id)
                    })
                }))
    }

    pub fn admit(&self, selection: &StreamedPlantSelection<'_>) -> Result<Admission, String> {
        self.prepared.admit(selection)
    }

    pub(super) fn admit_verified(
        &self,
        selection: &super::bounds::VerifiedSelection<'_>,
    ) -> Result<Admission, String> {
        self.prepared.admit_verified(selection)
    }

    pub fn select(&self, admitted: &Admission, camera: Vec3) -> Result<Selection, String> {
        self.prepared
            .plan(admitted, self.inner.selection_indices(camera)?)
    }

    pub fn select_excluding(
        &self,
        admitted: &Admission,
        camera: Vec3,
        replaced: &std::collections::BTreeSet<EntityId>,
    ) -> Result<Selection, String> {
        self.prepared.plan(
            admitted,
            self.inner.selection_indices_excluding(camera, replaced)?,
        )
    }

    /// Atomic CPU visibility publication after the previous inventory is GPU-ready.
    /// Gameplay remains held until `ready` sees a fresh fence for the new generation
    /// and the host independently verifies the canonical collision/backend state.
    pub fn publish(
        &mut self,
        world: &mut World,
        selection: Selection,
        token: Option<&RenderInventoryToken>,
    ) -> Result<(), String> {
        self.publish_with_bounds(world, selection, token, None)
    }

    pub(super) fn publish_with_bounds(
        &mut self,
        world: &mut World,
        selection: Selection,
        token: Option<&RenderInventoryToken>,
        bounds: Option<&WorldEnvelopes>,
    ) -> Result<(), String> {
        if selection.identity != self.prepared.identity {
            return Err("Selection belongs to another plant projection".into());
        }
        if bounds.is_some_and(|bounds| bounds.identity() != self.prepared.identity) {
            return Err("Shadow bounds belong to another plant projection".into());
        }
        if self.published
            && self.parts == selection.parts
            && self.indices == selection.indices
            && self.projection_intact(world)
        {
            return Ok(());
        }
        let empty = self.prepared.pins.is_empty();
        if !empty
            && !token.is_some_and(|token| {
                token.is_current(world, self.owner, self.prepared.identity, self.generation)
            })
        {
            return Err("Current plant GPU inventory is not admitted".into());
        }
        if !self.projection_intact(world) {
            return Err("Plant warm/live entities changed before publication".into());
        }
        let next = if self.published {
            self.generation
        } else {
            self.generation
                .checked_add(1)
                .ok_or("Plant generation overflow")?
        };
        let sequence = self
            .sequence
            .checked_add(1)
            .ok_or("Plant selection sequence overflow")?;
        if !empty && !self.published {
            let (meshes, materials) = self.inner.handles();
            render_inventory::request(
                world,
                self.owner,
                self.prepared.identity,
                next,
                &meshes,
                &materials,
            )?;
        }
        self.inner
            .select_indices(world, &selection.indices, Visibility::Visible)?;
        for (entity, lod) in self.inner.entities_with_lod() {
            let plant = world
                .get::<crate::source_vegetation::scene::ImportedPlant>(entity)
                .ok_or("Published plant identity absent")?;
            let id = plant.id;
            let source_lod = u8::try_from(lod).map_err(|_| "LOD index overflow")?;
            let &representation = selection
                .parts
                .get(&(id, source_lod))
                .ok_or("Published plant not in the complete selection")?;
            world.entity_mut(entity).insert(StreamedPlantPart {
                id,
                representation,
                source_lod,
            });
            if world.get::<PlantShadowBounds>(entity).is_none()
                && let Some(bound) = bounds.and_then(|bounds| bounds.plant_shadow_bounds(id))
            {
                world.entity_mut(entity).insert(bound);
            }
        }
        // Selection changes reuse the already warmed immutable meshes/buffers.
        // The first publication fences the resource generation; readiness still
        // acquires its current GPU inventory on every call, including empty views.
        if !self.published {
            self.publication_frame = world
                .get_resource::<bevy::diagnostic::FrameCount>()
                .map_or(0, |frame| frame.0);
        }
        self.generation = next;
        self.published = true;
        self.selected = selection.parts.len();
        self.parts = selection.parts;
        self.indices = selection.indices;
        self.sequence = sequence;
        Ok(())
    }

    #[must_use]
    pub fn ready(&self, world: &World) -> bool {
        self.published
            && self.selected == self.parts.len()
            && self.inner.entity_count() == self.selected
            && self.audited_projection_intact(world)
            && (self.prepared.pins.is_empty()
                || render_inventory::acquire(
                    world,
                    self.owner,
                    self.prepared.identity,
                    self.generation,
                )
                .is_some_and(|token| token.covers_frame(world, self.publication_frame)))
    }

    pub(super) fn audited_projection_intact(&self, world: &World) -> bool {
        self.audit
            .lock()
            .is_ok_and(|mut audit| audit.intact(world, self))
    }

    pub fn retire(self, world: &mut World) -> Result<(), String> {
        render_inventory::release(world, self.owner)?;
        self.inner.retire(world);
        Ok(())
    }
}
