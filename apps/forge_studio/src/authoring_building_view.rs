//! Disposable camera framing, derived from the accepted room, never persisted.
use forge_core::Vec3;
use forge_project::zone_authoring::buildings::AuthoredBuilding;

/// Accepted provenance, independent of the editable form's pending values.
pub(super) fn door_origin(recipe: f32, correction: Option<f32>, locked: bool) -> String {
    let source = correction.map_or_else(
        || format!("Recette : {recipe} m · retouche : aucune · valeur effective : {recipe} m"),
        |value| {
            format!(
                "Recette : {recipe} m · retouche absolue : {value} m · valeur effective : {value} m"
            )
        },
    );
    format!(
        "Porte acceptée — {source}. Repère local du bâtiment, axe X. Verrou du bâtiment entier : {} ; pas de verrou indépendant de la porte. Les champs ci-dessous sont un brouillon jusqu’à Appliquer.",
        if locked { "actif" } else { "inactif" }
    )
}

/// Temporary terrain start, independent of camera framing and never a saved spawn.
pub(super) fn entrance_start(
    building: &AuthoredBuilding,
) -> forge_project::authoring::playtest::PlaytestStart {
    let local = Vec3::new(
        building
            .door_offset_override_m
            .unwrap_or(building.dimensions.door_offset_x_m),
        0.0,
        -building.dimensions.depth_m * 0.5 - 2.0,
    );
    let world = to_world(building, local);
    forge_project::authoring::playtest::PlaytestStart {
        x_m: world.x,
        z_m: world.z,
        facing_degrees: Some((building.yaw_degrees + 180.0).rem_euclid(360.0)),
    }
}

pub(super) fn frame(building: &AuthoredBuilding, doorway: bool) -> (Vec3, Vec3) {
    let d = building.dimensions;
    let (pivot, offset) = if doorway {
        (
            Vec3::new(
                building.door_offset_override_m.unwrap_or(d.door_offset_x_m),
                d.door_height_m * 0.5,
                -d.depth_m * 0.5,
            ),
            Vec3::new(0.0, 0.4, -d.door_height_m.max(d.door_width_m) * 2.0),
        )
    } else {
        let distance = d.width_m.max(d.depth_m).max(d.clear_height_m) * 1.8;
        (
            Vec3::new(0.0, d.clear_height_m * 0.5, 0.0),
            Vec3::new(distance * 0.5, distance * 0.3, -distance),
        )
    };
    let eye = Vec3::new(pivot.x + offset.x, pivot.y + offset.y, pivot.z + offset.z);
    (to_world(building, pivot), to_world(building, eye))
}

fn to_world(building: &AuthoredBuilding, p: Vec3) -> Vec3 {
    let (sin, cos) = building.yaw_degrees.to_radians().sin_cos();
    Vec3::new(
        building.origin_m.x + cos * p.x + sin * p.z,
        building.origin_m.y + p.y,
        building.origin_m.z - sin * p.x + cos * p.z,
    )
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::EntityId;
    use forge_project::building::RoomDimensions;

    #[test]
    fn door_origin_distinguishes_an_explicit_zero_from_inheritance() {
        let inherited = door_origin(1.5, None, false);
        assert!(inherited.contains("retouche : aucune"));
        assert!(inherited.contains("valeur effective : 1.5 m"));
        let corrected = door_origin(1.5, Some(0.0), true);
        assert!(corrected.contains("Recette : 1.5 m"));
        assert!(corrected.contains("retouche absolue : 0 m"));
        assert!(corrected.contains("valeur effective : 0 m"));
        assert!(corrected.contains("bâtiment entier : actif"));
        assert!(corrected.contains("pas de verrou indépendant"));
    }

    #[test]
    fn camera_faces_the_rotated_front_and_tracks_the_door_override() {
        let mut building = AuthoredBuilding {
            id: EntityId::deterministic("camera-room"),
            name: "Room".into(),
            generator_version: 1,
            origin_m: Vec3::new(20.0, 3.0, 20.0),
            yaw_degrees: 0.0,
            locked: false,
            door_offset_override_m: Some(1.5),
            dimensions: RoomDimensions {
                width_m: 8.0,
                depth_m: 6.0,
                clear_height_m: 3.0,
                wall_m: 0.25,
                slab_m: 0.2,
                door_width_m: 1.4,
                door_height_m: 2.2,
                door_offset_x_m: 0.0,
            },
        };
        for yaw in [0.0_f32, 90.0, 180.0, -90.0] {
            building.yaw_degrees = yaw;
            let original = building.clone();
            let (sin, cos) = yaw.to_radians().sin_cos();
            let start = entrance_start(&building);
            let start_x = cos * (start.x_m - 20.0) - sin * (start.z_m - 20.0);
            let start_z = sin * (start.x_m - 20.0) + cos * (start.z_m - 20.0);
            assert!((start_x - 1.5).abs() < 0.0001);
            assert!((start_z + 5.0).abs() < 0.0001);
            let heading = start.facing_degrees.unwrap().to_radians();
            // The opening is two metres along local +Z from the start.
            assert!((-heading.sin() - sin).abs() < 0.0001);
            assert!((-heading.cos() - cos).abs() < 0.0001);
            for doorway in [false, true] {
                let (pivot, eye) = frame(&building, doorway);
                // Inverse rotation: the eye must be outside the entrance (-Z) wall.
                let local_eye_z = sin * (eye.x - 20.0) + cos * (eye.z - 20.0);
                assert!(local_eye_z < -3.0);
                if doorway {
                    let local_pivot_x = cos * (pivot.x - 20.0) - sin * (pivot.z - 20.0);
                    let local_pivot_z = sin * (pivot.x - 20.0) + cos * (pivot.z - 20.0);
                    assert!((local_pivot_x - 1.5).abs() < 0.0001);
                    assert!((local_pivot_z + 3.0).abs() < 0.0001);
                }
            }
            assert_eq!(building, original);
        }
        building.door_offset_override_m = None;
        building.yaw_degrees = 0.0;
        assert!((frame(&building, true).0.x - 20.0).abs() < 0.0001);
    }
}
