//! Tidewater placement-to-GPU adapter, independent of the renderer.
//! Derived from dgreenheck/tidewater 1438b1ab: Vegetation.js, InstanceLOD.js,
//! Matrix4.js. Copyright 2026 DRG Software Solutions LLC, MIT (retained notices).
use crate::imported_vegetation::ImportedEntity;

pub const PARAMETERS: [&str; 5] = [
    "lean_azimuth_radians",
    "lean_amount",
    "stem_height_m",
    "variation_seed",
    "vertical_shape_scale",
];

#[derive(Debug)]
pub struct PackedPlant {
    pub group: &'static str,
    pub matrix: [f32; 16],
    pub position: [f32; 4],
    pub data: [f32; 4],
    pub query_radius_squared: f32,
    pub source_order: Option<u32>,
}

/// Preserve source double-precision arithmetic until the original `Float32Array`
/// boundary. The caller validates canonical blocks before exposing entities.
#[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
pub fn pack(
    prototype: &str,
    parameter_names: &[String],
    entity: &ImportedEntity,
) -> Result<PackedPlant, String> {
    let record = &entity.source;
    if !(5..=6).contains(&parameter_names.len())
        || record.parameters.len() != parameter_names.len()
        || parameter_names[..5]
            .iter()
            .map(String::as_str)
            .ne(PARAMETERS)
        || (parameter_names.len() == 6 && parameter_names[5] != "source_order")
    {
        return Err("Unsupported Tidewater prototype parameter contract".into());
    }
    let source_order = if let Some(&order) = record.parameters.get(5) {
        if !order.is_finite() || !(0.0..131_072.0).contains(&order) || order.fract() != 0.0 {
            return Err("Invalid Tidewater source order".into());
        }
        Some(order as u32)
    } else {
        None
    };
    let [la, lean, height, seed, shape_scale]: [f64; 5] = record.parameters[..5]
        .try_into()
        .map_err(|_| "Parameter count")?;
    let (group, kind, radius) = match prototype {
        "tidewater_palms" => ("palms", 0.0, 0.0),
        "tidewater_bananas" => ("bananas", if seed < 0.5 { 2.0 } else { 4.0 }, 130.0),
        "tidewater_young_palms" => ("understory", 1.0, 150.0),
        "tidewater_ferns" => ("understory", 3.0, 66.0),
        "tidewater_monsteras" => ("monsteras", 11.0, 115.0),
        "tidewater_elephant_ears" => ("broadleaf", 12.0, 115.0),
        "tidewater_heliconias" => ("broadleaf", 13.0, 115.0),
        "tidewater_strelitzias" => ("broadleaf", 14.0, 115.0),
        "tidewater_trees" => ("canopy", 0.0, 75.0),
        "tidewater_shrubs" => ("canopy", 0.0, 55.0),
        _ => return Err("Unknown Tidewater prototype".into()),
    };
    let seed = if kind == 0.0 {
        seed
    } else {
        kind + seed * 0.999
    };
    let height = if group == "bananas" {
        0.02
    } else if height == 0.0 {
        1.0
    } else {
        height
    };
    let [x, y, z, w] = record.rotation_xyzw;
    let (x2, y2, z2) = (x + x, y + y, z + z);
    let (xx, xy, xz, yy, yz, zz) = (x * x2, x * y2, x * z2, y * y2, y * z2, z * z2);
    let (wx, wy, wz) = (w * x2, w * y2, w * z2);
    let [sx, sy, sz] = record.scale;
    let sy = sy
        * if shape_scale == 0.0 {
            1.0
        } else {
            shape_scale.abs()
        };
    let [px, py, pz] = record.position_m;
    let matrix = [
        (1.0 - (yy + zz)) * sx,
        (xy + wz) * sx,
        (xz - wy) * sx,
        0.0,
        (xy - wz) * sy,
        (1.0 - (xx + zz)) * sy,
        (yz + wx) * sy,
        0.0,
        (xz + wy) * sz,
        (yz - wx) * sz,
        (1.0 - (xx + yy)) * sz,
        0.0,
        px,
        py,
        pz,
        1.0,
    ]
    .map(|v| v as f32);
    let result = PackedPlant {
        source_order,
        group,
        matrix,
        position: [px as f32, py as f32, pz as f32, sx as f32],
        data: [la as f32, lean as f32, height as f32, seed as f32],
        query_radius_squared: (radius * radius) as f32,
    };
    if result
        .matrix
        .iter()
        .chain(&result.position)
        .chain(&result.data)
        .any(|v| !v.is_finite())
    {
        return Err("Tidewater packed value overflow".into());
    }
    Ok(result)
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::{ContentHash, EntityId};
    use forge_project::imported_vegetation::ImportedVegetationInstance;
    #[test]
    fn order_is_optional_for_legacy_and_strict_for_renderable_imports() {
        let mut entity = ImportedEntity {
            id: EntityId::deterministic("order-test"),
            source: ImportedVegetationInstance {
                source_key: ContentHash::from_bytes([1; 32]),
                position_m: [0.0; 3],
                rotation_xyzw: [0.0, 0.0, 0.0, 1.0],
                scale: [1.0; 3],
                parameters: vec![0.0, 0.0, 1.0, 0.5, 1.0],
            },
        };
        let mut names: Vec<String> = PARAMETERS.iter().map(|v| (*v).to_owned()).collect();
        let legacy = pack("tidewater_palms", &names, &entity).unwrap();
        assert_eq!(legacy.source_order, None);
        names.push("source_order".into());
        entity.source.parameters.push(12.0);
        let ordered = pack("tidewater_palms", &names, &entity).unwrap();
        assert_eq!(ordered.source_order, Some(12));
        assert_eq!(ordered.matrix, legacy.matrix);
        assert_eq!(ordered.data, legacy.data);
        for bad in [-1.0, 0.5, 131_072.0, f64::NAN, f64::INFINITY] {
            entity.source.parameters[5] = bad;
            assert!(pack("tidewater_palms", &names, &entity).is_err());
        }
        entity.source.parameters[5] = 12.0;
        names[5] = "unknown".into();
        assert!(pack("tidewater_palms", &names, &entity).is_err());
    }
}
