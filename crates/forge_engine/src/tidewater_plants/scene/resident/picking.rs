//! Immutable plant draw snapshot and canonical ID table for an offscreen pass.
use super::{ImportedPlant, Resident};
use crate::tidewater_plants::{
    PlantMaterial,
    picking::{MAX_ID, PickingExtension, PickingMaterial},
};
use bevy::{mesh::MeshTag, prelude::*};
use forge_core::{ContentHash, EntityId};
use std::collections::BTreeSet;

pub struct Batch {
    pub mesh: Handle<Mesh>,
    pub material: PickingMaterial,
    /// Original storage-buffer indices, in the visible renderer's draw order.
    pub tags: Vec<u32>,
}

pub struct Snapshot {
    pub head: ContentHash,
    pub batches: Vec<Batch>,
    identities: Vec<EntityId>,
}

impl Snapshot {
    /// The complete request must also bind camera, viewport and library epoch.
    /// This check only binds the canonical document revision.
    pub fn resolve(&self, id: u32, current_head: ContentHash) -> Result<Option<EntityId>, String> {
        if current_head != self.head {
            return Err("Stale plant picking document".into());
        }
        if id == 0 {
            return Ok(None);
        }
        self.identities
            .get((id - 1) as usize)
            .copied()
            .map(Some)
            .ok_or_else(|| "Unknown plant picking ID".into())
    }
}

impl Resident {
    /// Capture the existing selection without rerunning LOD or allocating GPU
    /// resources. Each level reuses the exact current buffers and uniforms.
    pub fn picking_snapshot(&self, world: &World) -> Result<Snapshot, String> {
        let head = self
            .scene
            .head_hash
            .ok_or("Plant picking requires a canonical head")?;
        let materials = world
            .get_resource::<Assets<PlantMaterial>>()
            .ok_or("Plant materials unavailable")?;
        let mut identities = Vec::new();
        let mut unique = BTreeSet::new();
        let mut batches = Vec::new();
        for (group, levels) in self.scene.groups.iter().zip(&self.levels) {
            let ids = group
                .identities
                .as_ref()
                .ok_or("Missing plant identities")?;
            if ids.len() != group.records.len() || ids.iter().any(|id| !unique.insert(*id)) {
                return Err("Invalid plant picking identity table".into());
            }
            if ids.is_empty() {
                continue;
            }
            let first = u32::try_from(identities.len()).map_err(|_| "Picking ID overflow")? + 1;
            let count = u32::try_from(ids.len()).map_err(|_| "Picking group overflow")?;
            let extension = PickingExtension::new(first, count)?;
            if identities.len() + ids.len() > MAX_ID as usize {
                return Err("Picking capacity exceeded".into());
            }
            identities.extend_from_slice(ids);
            for level in levels {
                let mut tags = Vec::new();
                for &entity in &level.order {
                    let plant = world
                        .get::<ImportedPlant>(entity)
                        .ok_or("Picking entity disappeared")?;
                    let tag = world
                        .get::<MeshTag>(entity)
                        .ok_or("Picking tag disappeared")?
                        .0;
                    if plant.head_hash != Some(head)
                        || ids.get(tag as usize) != Some(&plant.id)
                        || world.get::<Mesh3d>(entity).map(|m| &m.0) != Some(&level.mesh)
                        || world
                            .get::<MeshMaterial3d<PlantMaterial>>(entity)
                            .map(|m| &m.0)
                            != Some(&level.material)
                    {
                        return Err("Plant picking projection changed".into());
                    }
                    if world.get::<Visibility>(entity) == Some(&Visibility::Visible) {
                        tags.push(tag);
                    }
                }
                if !tags.is_empty() {
                    batches.push(Batch {
                        mesh: level.mesh.clone(),
                        material: PickingMaterial {
                            base: materials
                                .get(&level.material)
                                .ok_or("Picking material disappeared")?
                                .clone(),
                            extension: extension.clone(),
                        },
                        tags,
                    });
                }
            }
        }
        Ok(Snapshot {
            head,
            batches,
            identities,
        })
    }
}
