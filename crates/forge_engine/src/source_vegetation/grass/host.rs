//! Shared playable grass host. CPU publication never substitutes for renderer admission.
use super::{Grid, Resident, ViewCadence, fields::PreparedFields};
use crate::{render_inventory, source_vegetation::world::WorldPlantMaterial};
use bevy::prelude::*;
use forge_core::{ContentHash, EntityId};

#[derive(Default, Clone, Debug, PartialEq, Eq, serde::Serialize, serde::Deserialize)]
pub struct Status {
    pub identity: Option<ContentHash>,
    pub ready: bool,
    pub counts: [usize; 3],
    pub generation: u64,
    pub sequence: u64,
    pub publication_frame: u32,
    pub gpu_epoch: u64,
    pub gpu_observed: u32,
    pub gpu_completed: Option<u32>,
    pub gpu_meshes: [usize; 2],
    pub gpu_materials: [usize; 2],
    pub gpu_images: [usize; 2],
    pub gpu_buffers: [usize; 2],
    pub pipelines: [usize; 3],
}

pub struct Host {
    resident: Resident<WorldPlantMaterial>,
    grid: Grid,
    owner: EntityId,
    identity: ContentHash,
    generation: u64,
    sequence: u64,
    publication_frame: u32,
    published: bool,
    camera: ViewCadence,
    counts: [usize; 3],
}

impl Host {
    #[must_use]
    pub fn materials(&self) -> Vec<Handle<WorldPlantMaterial>> {
        self.resident.handles().1
    }

    pub fn renew_bindings(&mut self, world: &mut World) -> Result<(), String> {
        let next = self
            .generation
            .checked_add(1)
            .ok_or("Grass generation overflow")?;
        let (meshes, materials) = self.resident.handles();
        render_inventory::request_scoped(
            world,
            self.owner,
            self.identity,
            next,
            &meshes,
            &materials,
        )?;
        self.generation = next;
        self.publication_frame = world.resource::<bevy::diagnostic::FrameCount>().0;
        Ok(())
    }

    pub fn retire(self, world: &mut World) {
        render_inventory::release_scoped(world, self.owner);
        self.resident.retire(world);
    }
    pub fn stage(
        world: &mut World,
        prepared: PreparedFields,
        base: &WorldPlantMaterial,
        mask: Handle<Image>,
        owner: EntityId,
    ) -> Result<Self, String> {
        if owner.as_uuid().is_nil() {
            return Err("Grass requires an exclusive real owner".into());
        }
        let mut material = base.clone();
        material.grass_field = prepared.uniform;
        material.grass_mask = Some(mask);
        {
            let mut images = world
                .get_resource_mut::<Assets<Image>>()
                .ok_or("Grass images unavailable")?;
            material.grass_heights = Some(images.add(prepared.heights));
            material.grass_deltas = Some(images.add(prepared.deltas));
        }
        let mut resident = Resident::stage(world, &material, prepared.meshes)?;
        // These are warm-up draw slots, not a published authored selection.
        resident.update(
            world,
            &std::array::from_fn(|_| vec![[0.0; 4]]),
            Visibility::Visible,
        )?;
        let (meshes, materials) = resident.handles();
        if let Err(error) = render_inventory::request_scoped(
            world,
            owner,
            prepared.identity,
            1,
            &meshes,
            &materials,
        ) {
            resident.retire(world);
            return Err(error);
        }
        Ok(Self {
            resident,
            grid: prepared.grid,
            owner,
            identity: prepared.identity,
            generation: 1,
            sequence: 0,
            publication_frame: 0,
            published: false,
            camera: ViewCadence::default(),
            counts: [0; 3],
        })
    }

    pub fn refresh(&mut self, world: &mut World, camera: Entity) -> Result<Status, String> {
        if !self.resident.intact(world) {
            return Err("Owned grass projection changed".into());
        }
        let transform = *world
            .get::<Transform>(camera)
            .ok_or("Grass camera unavailable")?;
        let Some(Projection::Perspective(projection)) = world.get::<Projection>(camera) else {
            return Err("Grass requires its finite perspective camera".into());
        };
        let projection = projection.clone();
        if self.camera.needs_refresh(camera, &transform, &projection) {
            let view = crate::scene_camera::source_view(&transform, &projection)?;
            let cells = self.grid.select(view)?;
            if self.published
                || render_inventory::acquire_scoped(
                    world,
                    self.owner,
                    self.identity,
                    self.generation,
                )
                .is_some()
            {
                let generation = if self.published {
                    self.generation
                } else {
                    self.generation
                        .checked_add(1)
                        .ok_or("Grass generation overflow")?
                };
                let sequence = self
                    .sequence
                    .checked_add(1)
                    .ok_or("Grass selection sequence exhausted")?;
                let report = self.resident.update(world, &cells, Visibility::Visible)?;
                let (meshes, materials) = self.resident.handles();
                if !self.published {
                    render_inventory::request_scoped(
                        world,
                        self.owner,
                        self.identity,
                        generation,
                        &meshes,
                        &materials,
                    )?;
                    self.publication_frame = world
                        .get_resource::<bevy::diagnostic::FrameCount>()
                        .map_or(0, |f| f.0);
                }
                self.counts = report.counts;
                self.sequence = sequence;
                self.generation = generation;
                self.published = true;
                self.camera.record(camera, &transform, &projection);
            }
        }
        let ready = self.published
            && render_inventory::acquire_scoped(world, self.owner, self.identity, self.generation)
                .is_some_and(|token| token.covers_frame(world, self.publication_frame));
        let mut status = Status {
            identity: Some(self.identity),
            ready,
            counts: self.counts,
            generation: self.generation,
            sequence: self.sequence,
            publication_frame: self.publication_frame,
            ..Default::default()
        };
        if let Some(gpu) = render_inventory::scoped_evidence(world, self.owner) {
            status.gpu_epoch = gpu.epoch;
            status.gpu_observed = gpu.observed;
            status.gpu_completed = gpu.completed;
            status.gpu_meshes = [gpu.assets.meshes_expected, gpu.assets.meshes_prepared];
            status.gpu_materials = [gpu.assets.materials_expected, gpu.assets.materials_bound];
            status.gpu_images = [gpu.assets.images_expected, gpu.assets.images_prepared];
            status.gpu_buffers = [gpu.assets.buffers_expected, gpu.assets.buffers_prepared];
            status.pipelines = [
                gpu.pipelines_total,
                gpu.pipelines_pending,
                gpu.pipelines_failed,
            ];
        }
        Ok(status)
    }
}
