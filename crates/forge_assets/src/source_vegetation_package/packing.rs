//! Tidewater placement-to-GPU adapter, independent of the renderer.
//! Derived from dgreenheck/tidewater 1438b1ab: Vegetation.js, InstanceLOD.js,
//! Matrix4.js. Copyright 2026 DRG Software Solutions LLC, MIT (retained notices).

use super::profile::PlantPrototype;

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
    position_m: [f64; 3],
    rotation_xyzw: [f64; 4],
    scale: [f64; 3],
    parameters: &[f64],
) -> Result<PackedPlant, String> {
    PlantPrototype::validate_parameters(parameter_names)?;
    if parameters.len() != parameter_names.len() {
        return Err("Unsupported Tidewater prototype parameter contract".into());
    }
    let source_order = if let Some(&order) = parameters.get(5) {
        if !order.is_finite() || !(0.0..131_072.0).contains(&order) || order.fract() != 0.0 {
            return Err("Invalid Tidewater source order".into());
        }
        Some(order as u32)
    } else {
        None
    };
    let [la, lean, height, seed, shape_scale]: [f64; 5] =
        parameters[..5].try_into().map_err(|_| "Parameter count")?;
    let prototype = PlantPrototype::from_key(prototype)?;
    let group = prototype.group();
    let (kind, radius) = prototype.shader_kind_and_radius(seed);
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
    let [x, y, z, w] = rotation_xyzw;
    let (x2, y2, z2) = (x + x, y + y, z + z);
    let (xx, xy, xz, yy, yz, zz) = (x * x2, x * y2, x * z2, y * y2, y * z2, z * z2);
    let (wx, wy, wz) = (w * x2, w * y2, w * z2);
    let [sx, sy, sz] = scale;
    let sy = sy
        * if shape_scale == 0.0 {
            1.0
        } else {
            shape_scale.abs()
        };
    let [px, py, pz] = position_m;
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
    use super::PackedPlant;
    use crate::source_vegetation_package::profile::PARAMETERS;
    fn pack(
        prototype: &str,
        names: &[String],
        entity: &ImportedVegetationInstance,
    ) -> Result<PackedPlant, String> {
        super::pack(
            prototype,
            names,
            entity.position_m,
            entity.rotation_xyzw,
            entity.scale,
            &entity.parameters,
        )
    }
    use forge_core::ContentHash;
    use forge_project::imported_vegetation::ImportedVegetationInstance;
    #[test]
    fn shared_source_profile_preserves_original_groups_shader_tags_and_query_radii() {
        let names: Vec<String> = PARAMETERS.iter().map(|name| (*name).into()).collect();
        let mut entity = ImportedVegetationInstance {
            source_key: ContentHash::from_bytes([1; 32]),
            position_m: [0.0; 3],
            rotation_xyzw: [0.0, 0.0, 0.0, 1.0],
            scale: [1.0; 3],
            parameters: vec![0.0, 0.0, 1.0, 0.25, 1.0],
        };
        for (key, group, tag, radius_squared) in [
            ("tidewater_palms", "palms", 0.25_f32, 0.0_f32),
            ("tidewater_bananas", "bananas", 2.24975, 16900.0),
            ("tidewater_young_palms", "understory", 1.24975, 22500.0),
            ("tidewater_ferns", "understory", 3.24975, 4356.0),
            ("tidewater_monsteras", "monsteras", 11.24975, 13225.0),
            ("tidewater_elephant_ears", "broadleaf", 12.24975, 13225.0),
            ("tidewater_heliconias", "broadleaf", 13.24975, 13225.0),
            ("tidewater_strelitzias", "broadleaf", 14.24975, 13225.0),
            ("tidewater_trees", "canopy", 0.25, 5625.0),
            ("tidewater_shrubs", "canopy", 0.25, 3025.0),
        ] {
            let packed = pack(key, &names, &entity).unwrap();
            assert_eq!(packed.group, group);
            assert_eq!(packed.data[3].to_bits(), tag.to_bits());
            assert_eq!(
                packed.query_radius_squared.to_bits(),
                radius_squared.to_bits()
            );
        }
        entity.parameters[3] = 0.5;
        assert_eq!(
            pack("tidewater_bananas", &names, &entity).unwrap().data[3].to_bits(),
            4.4995_f32.to_bits()
        );
    }
    #[test]
    #[allow(
        clippy::float_cmp,
        reason = "Adding source-order metadata must leave the existing packed float payload exactly unchanged."
    )]
    fn order_is_optional_for_legacy_and_strict_for_renderable_imports() {
        let mut entity = ImportedVegetationInstance {
            source_key: ContentHash::from_bytes([1; 32]),
            position_m: [0.0; 3],
            rotation_xyzw: [0.0, 0.0, 0.0, 1.0],
            scale: [1.0; 3],
            parameters: vec![0.0, 0.0, 1.0, 0.5, 1.0],
        };
        let mut names: Vec<String> = PARAMETERS.iter().map(|v| (*v).to_owned()).collect();
        let legacy = pack("tidewater_palms", &names, &entity).unwrap();
        assert_eq!(legacy.source_order, None);
        names.push("source_order".into());
        entity.parameters.push(12.0);
        let ordered = pack("tidewater_palms", &names, &entity).unwrap();
        assert_eq!(ordered.source_order, Some(12));
        assert_eq!(ordered.matrix, legacy.matrix);
        assert_eq!(ordered.data, legacy.data);
        for bad in [-1.0, 0.5, 131_072.0, f64::NAN, f64::INFINITY] {
            entity.parameters[5] = bad;
            assert!(pack("tidewater_palms", &names, &entity).is_err());
        }
        entity.parameters[5] = 12.0;
        names[5] = "unknown".into();
        assert!(pack("tidewater_palms", &names, &entity).is_err());
    }
}
