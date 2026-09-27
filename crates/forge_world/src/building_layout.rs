//! Engine-neutral single-room building layout. Not a persisted authoring format.
//! Local +Y is up, floor surface is Y=0 and the entrance faces -Z.
use forge_core::EntityId;

pub use forge_project::building::RoomDimensions;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum PartRole {
    Floor,
    Roof,
    WestWall,
    EastWall,
    BackWall,
    EntranceLeft,
    EntranceRight,
    EntranceLintel,
}

impl PartRole {
    fn key(self) -> &'static str {
        match self {
            Self::Floor => "floor",
            Self::Roof => "roof",
            Self::WestWall => "wall/west",
            Self::EastWall => "wall/east",
            Self::BackWall => "wall/back",
            Self::EntranceLeft => "entrance/left",
            Self::EntranceRight => "entrance/right",
            Self::EntranceLintel => "entrance/lintel",
        }
    }
}

/// Exact local bounds shared by future mesh and collider adapters.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct LocalBox {
    pub min: [f32; 3],
    pub max: [f32; 3],
}

#[derive(Clone, Debug, PartialEq)]
pub struct BuildingPart {
    pub id: EntityId,
    pub role: PartRole,
    pub bounds: LocalBox,
}

#[derive(Clone, Debug, PartialEq)]
pub struct RoomLayout {
    pub parts: Vec<BuildingPart>,
    /// Empty room volume, excluding slabs and walls.
    pub interior: LocalBox,
    /// Empty opening through the front wall, not a door leaf or navigation proof.
    pub doorway: LocalBox,
    pub doorway_id: EntityId,
}

impl RoomLayout {
    pub fn generate(dimensions: RoomDimensions, building_id: EntityId) -> Result<Self, String> {
        dimensions.validate()?;
        let x = dimensions.width_m / 2.0;
        let z = dimensions.depth_m / 2.0;
        let h = dimensions.clear_height_m;
        let w = dimensions.wall_m;
        let left = dimensions.door_offset_x_m - dimensions.door_width_m / 2.0;
        let right = dimensions.door_offset_x_m + dimensions.door_width_m / 2.0;
        let front = -z + w;
        let definitions = [
            (PartRole::Floor, [-x, -dimensions.slab_m, -z], [x, 0.0, z]),
            (PartRole::Roof, [-x, h, -z], [x, h + dimensions.slab_m, z]),
            (PartRole::WestWall, [-x, 0.0, -z], [-x + w, h, z]),
            (PartRole::EastWall, [x - w, 0.0, -z], [x, h, z]),
            (PartRole::BackWall, [-x + w, 0.0, z - w], [x - w, h, z]),
            (PartRole::EntranceLeft, [-x + w, 0.0, -z], [left, h, front]),
            (PartRole::EntranceRight, [right, 0.0, -z], [x - w, h, front]),
            (
                PartRole::EntranceLintel,
                [left, dimensions.door_height_m, -z],
                [right, h, front],
            ),
        ];
        let parts = definitions
            .into_iter()
            .map(|(role, min, max)| BuildingPart {
                // Dimensions/order never participate in identity: edited parts stay addressable.
                id: EntityId::deterministic(format!(
                    "building-layout/v1:{building_id}:{}",
                    role.key()
                )),
                role,
                bounds: LocalBox { min, max },
            })
            .collect();
        Ok(RoomLayout {
            parts,
            doorway_id: EntityId::deterministic(format!(
                "building-layout/v1:{building_id}:entrance/opening"
            )),
            interior: LocalBox {
                min: [-x + w, 0.0, front],
                max: [x - w, h, z - w],
            },
            doorway: LocalBox {
                min: [left, 0.0, -z],
                max: [right, dimensions.door_height_m, front],
            },
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    fn dimensions() -> RoomDimensions {
        RoomDimensions {
            width_m: 8.0,
            depth_m: 6.0,
            clear_height_m: 3.0,
            wall_m: 0.25,
            slab_m: 0.2,
            door_width_m: 1.4,
            door_height_m: 2.2,
            door_offset_x_m: 0.0,
        }
    }
    fn intersects(a: LocalBox, b: LocalBox) -> bool {
        (0..3).all(|axis| a.min[axis] < b.max[axis] && a.max[axis] > b.min[axis])
    }
    #[test]
    fn room_and_entrance_are_empty_and_solids_do_not_overlap() {
        let layout = RoomLayout::generate(dimensions(), EntityId::deterministic("room")).unwrap();
        assert_eq!(layout.parts.len(), 8);
        for (index, part) in layout.parts.iter().enumerate() {
            assert!((0..3).all(|axis| part.bounds.max[axis] > part.bounds.min[axis]));
            assert!(!intersects(part.bounds, layout.interior));
            assert!(!intersects(part.bounds, layout.doorway));
            for other in &layout.parts[index + 1..] {
                assert_ne!(part.id, other.id);
                assert!(!intersects(part.bounds, other.bounds));
            }
        }
    }
    #[test]
    fn resizing_preserves_semantic_ids_and_parent_scopes_them() {
        let parent = EntityId::deterministic("room");
        let before = RoomLayout::generate(dimensions(), parent).unwrap();
        assert_eq!(before, RoomLayout::generate(dimensions(), parent).unwrap());
        let after = RoomLayout::generate(
            RoomDimensions {
                width_m: 12.0,
                ..dimensions()
            },
            parent,
        )
        .unwrap();
        let other =
            RoomLayout::generate(dimensions(), EntityId::deterministic("other-room")).unwrap();
        for part in &before.parts {
            let resized = after.parts.iter().find(|p| p.role == part.role).unwrap();
            assert_eq!(part.id, resized.id);
            assert!(other.parts.iter().all(|p| p.id != part.id));
        }
        assert_ne!(before.interior, after.interior);
    }
    #[test]
    fn moved_door_preserves_identity_and_keeps_a_clear_passage() {
        let parent = EntityId::deterministic("room");
        let before = RoomLayout::generate(dimensions(), parent).unwrap();
        let moved = RoomLayout::generate(
            RoomDimensions {
                door_offset_x_m: 1.5,
                ..dimensions()
            },
            parent,
        )
        .unwrap();
        assert_eq!(before.doorway_id, moved.doorway_id);
        for (original, part) in before.parts.iter().zip(&moved.parts) {
            assert_eq!(original.id, part.id);
            assert!(!intersects(part.bounds, moved.doorway));
        }
        assert!((moved.doorway.min[0] - before.doorway.min[0] - 1.5).abs() < 0.00001);
        let wider = RoomLayout::generate(
            RoomDimensions {
                width_m: 12.0,
                door_offset_x_m: 1.5,
                ..dimensions()
            },
            parent,
        )
        .unwrap();
        assert_eq!(wider.doorway, moved.doorway);
        assert_eq!(wider.doorway_id, moved.doorway_id);
    }
    #[test]
    fn invalid_dimensions_are_rejected_before_generating_parts() {
        let parent = EntityId::deterministic("room");
        for bad in [f32::NAN, f32::INFINITY, -1.0, 0.0, 129.0] {
            assert!(
                RoomLayout::generate(
                    RoomDimensions {
                        width_m: bad,
                        ..dimensions()
                    },
                    parent
                )
                .is_err()
            );
        }
        assert!(
            RoomLayout::generate(
                RoomDimensions {
                    door_width_m: 4.0,
                    width_m: 4.0,
                    ..dimensions()
                },
                parent
            )
            .is_err()
        );
        assert!(
            RoomLayout::generate(
                RoomDimensions {
                    door_height_m: 3.0,
                    ..dimensions()
                },
                parent
            )
            .is_err()
        );
    }
}
