//! Read-only counterfactual counts; never writes the input documents.
use forge_project::{TerrainDocument, VegetationDocument, VegetationKind};
use forge_world::VegetationGenerationPlan;
use std::{env, error::Error, fs, io};

fn main() -> Result<(), Box<dyn Error>> {
    let args: Vec<_> = env::args().skip(1).collect();
    if args.len() != 6 {
        return Err(
            io::Error::other("terrain.json vegetation.json min_x max_x min_z max_z").into(),
        );
    }
    let terrain: TerrainDocument = serde_json::from_slice(&fs::read(&args[0])?)?;
    let vegetation: VegetationDocument = serde_json::from_slice(&fs::read(&args[1])?)?;
    let bounds = [
        args[2].parse()?,
        args[3].parse()?,
        args[4].parse()?,
        args[5].parse()?,
    ];
    validate_bounds(bounds)?;
    let mut rows = Vec::new();
    for mode in [
        "authored",
        "without_exclusions",
        "without_material_filter",
        "full_acceptance",
        "all_three",
    ] {
        let mut candidate = vegetation.clone();
        if matches!(mode, "without_exclusions" | "all_three") {
            candidate.exclusions.clear();
        }
        for layer in &mut candidate.layers {
            if layer.kind != VegetationKind::Grass {
                continue;
            }
            if matches!(mode, "without_material_filter" | "all_three") {
                layer.material_filter = None;
            }
            if matches!(mode, "full_acceptance" | "all_three") {
                layer.acceptance_probability = 1.0;
                layer.density_pattern = None;
            }
        }
        let generated =
            VegetationGenerationPlan::from_documents(&terrain, &candidate)?.generate()?;
        let count = generated
            .chunks
            .iter()
            .flat_map(|chunk| &chunk.instances)
            .filter(|instance| instance.kind == VegetationKind::Grass)
            .filter(|instance| {
                let p = instance.position_m;
                p.x >= bounds[0] && p.x < bounds[1] && p.z >= bounds[2] && p.z < bounds[3]
            })
            .count();
        rows.push(serde_json::json!({"mode": mode, "grass_centres_in_region": count}));
    }
    println!(
        "{}",
        serde_json::to_string_pretty(&serde_json::json!({
            "bounds_half_open_xz_m": bounds,
            "terrain_hash": terrain.content_hash()?.to_string(),
            "vegetation_hash": vegetation.content_hash()?.to_string(),
            "scope": "CPU counterfactual counts, not rendered coverage; differences are not additive",
            "rows": rows
        }))?
    );
    Ok(())
}

fn validate_bounds(bounds: [f32; 4]) -> Result<(), io::Error> {
    if bounds.iter().all(|value| value.is_finite())
        && bounds[0] < bounds[1]
        && bounds[2] < bounds[3]
    {
        Ok(())
    } else {
        Err(io::Error::other(
            "region requires finite increasing X and Z bounds",
        ))
    }
}

#[cfg(test)]
mod tests {
    use super::validate_bounds;

    #[test]
    fn rejects_invalid_regions() {
        for bounds in [
            [0.0, 0.0, 0.0, 1.0],
            [0.0, 1.0, 2.0, 1.0],
            [f32::NAN, 1.0, 0.0, 1.0],
        ] {
            assert!(validate_bounds(bounds).is_err());
        }
        assert!(validate_bounds([-55.0, -15.0, -205.0, -165.0]).is_ok());
    }
}
