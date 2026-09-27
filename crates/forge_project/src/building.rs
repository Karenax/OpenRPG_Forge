//! Validated building parameters for the zone-authoring integration.
//! Used by the building domain of `ZoneCorrections`; geometry remains derived.
use serde::{Deserialize, Serialize};

#[derive(Clone, Copy, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct RoomDimensions {
    pub width_m: f32,
    pub depth_m: f32,
    pub clear_height_m: f32,
    pub wall_m: f32,
    pub slab_m: f32,
    pub door_width_m: f32,
    pub door_height_m: f32,
    /// Absolute local X coordinate of the opening centre, not a width fraction.
    pub door_offset_x_m: f32,
}

impl RoomDimensions {
    pub fn validate(self) -> Result<(), String> {
        let ranges = [
            (self.width_m, 3.0..=128.0),
            (self.depth_m, 3.0..=128.0),
            (self.clear_height_m, 2.0..=16.0),
            (self.wall_m, 0.1..=1.0),
            (self.slab_m, 0.1..=1.0),
            (self.door_width_m, 0.8..=4.0),
            (self.door_height_m, 1.8..=4.0),
        ];
        if ranges
            .into_iter()
            .any(|(v, range)| !v.is_finite() || !range.contains(&v))
            || !self.door_offset_x_m.is_finite()
        {
            return Err("Dimensions de pièce finies et dans les limites requises".into());
        }
        let remaining = self.width_m / 2.0 - self.wall_m - self.door_width_m / 2.0;
        if remaining - self.door_offset_x_m.abs() < 0.1
            || self.clear_height_m - self.door_height_m < 0.1
        {
            return Err("Porte incompatible : conserver au moins 0,1 m de montant de chaque côté et de linteau".into());
        }
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    fn room() -> RoomDimensions {
        RoomDimensions {
            width_m: 8.0,
            depth_m: 6.0,
            clear_height_m: 3.0,
            wall_m: 0.25,
            slab_m: 0.2,
            door_width_m: 1.4,
            door_height_m: 2.2,
            door_offset_x_m: 1.0,
        }
    }
    #[test]
    fn parameters_round_trip_without_engine_data() {
        let source = room();
        source.validate().unwrap();
        let bytes = serde_json::to_vec(&source).unwrap();
        let result: RoomDimensions = serde_json::from_slice(&bytes).unwrap();
        result.validate().unwrap();
        assert_eq!(source, result);
        let mut value = serde_json::to_value(source).unwrap();
        value["entity"] = serde_json::json!(123);
        assert!(serde_json::from_value::<RoomDimensions>(value).is_err());
    }
    #[test]
    fn door_offset_is_absolute_and_incompatible_resize_is_rejected() {
        let source = room();
        RoomDimensions {
            width_m: 12.0,
            ..source
        }
        .validate()
        .unwrap();
        assert!(
            RoomDimensions {
                width_m: 3.0,
                ..source
            }
            .validate()
            .is_err()
        );
        for offset in [f32::NAN, f32::INFINITY, -4.0, 4.0] {
            assert!(
                RoomDimensions {
                    door_offset_x_m: offset,
                    ..source
                }
                .validate()
                .is_err()
            );
        }
    }
}
