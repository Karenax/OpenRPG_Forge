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
    visible_ids: BTreeSet<u32>,
}

impl Snapshot {
    #[must_use]
    pub fn identity_count(&self) -> usize {
        self.identities.len()
    }

    #[must_use]
    pub fn visible_count(&self) -> usize {
        self.visible_ids.len()
    }

    /// The complete request must also bind camera, viewport and library epoch.
    /// This check only binds the canonical document revision.
    pub fn resolve(&self, id: u32, current_head: ContentHash) -> Result<Option<EntityId>, String> {
        if current_head != self.head {
            return Err("Stale plant picking document".into());
        }
        if id == 0 {
            return Ok(None);
        }
        if !self.visible_ids.contains(&id) {
            return Err("Plant picking ID is not visible in this snapshot".into());
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
        let mut visible_ids = BTreeSet::new();
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
                        let id = first.checked_add(tag).ok_or("Picking ID overflow")?;
                        if id > MAX_ID {
                            return Err("Picking ID overflow".into());
                        }
                        visible_ids.insert(id);
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
            visible_ids,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn a_snapshot_resolves_only_drawn_ids_from_the_same_head() {
        let head = "0101010101010101010101010101010101010101010101010101010101010101"
            .parse()
            .unwrap();
        let stale = "0202020202020202020202020202020202020202020202020202020202020202"
            .parse()
            .unwrap();
        let visible = EntityId::deterministic("visible");
        let hidden = EntityId::deterministic("hidden");
        let snapshot = Snapshot {
            head,
            batches: Vec::new(),
            identities: vec![visible, hidden],
            visible_ids: BTreeSet::from([1]),
        };
        assert_eq!(snapshot.resolve(1, head), Ok(Some(visible)));
        assert_eq!(snapshot.resolve(0, head), Ok(None));
        assert!(snapshot.resolve(2, head).is_err());
        assert!(snapshot.resolve(3, head).is_err());
        assert!(snapshot.resolve(1, stale).is_err());
    }
}
