//! Derived keep-clear corridors. Never erase painted density masks or manual objects.
use forge_project::{
    VegetationDocument, VegetationExclusion, VegetationExclusionId,
    zone_authoring::paths::AuthoredPath,
};

/// Derived keep-clear geometry, separate from the quota of authored exclusions.
/// Not serialized into a zone or vegetation document.
#[derive(Clone, Debug)]
pub struct PreparedPathExclusions {
    pub(crate) exclusions: Vec<VegetationExclusion>,
    pub(crate) source_hash: forge_core::ContentHash,
}

impl PreparedPathExclusions {
    /// Projection identity; with no paths it is exactly the source document hash.
    #[must_use]
    pub const fn source_hash(&self) -> forge_core::ContentHash {
        self.source_hash
    }
}

pub fn prepare(
    source: &VegetationDocument,
    paths: &[AuthoredPath],
) -> Result<PreparedPathExclusions, String> {
    source.validate().map_err(|e| e.to_string())?;
    forge_project::zone_authoring::paths::validate(
        paths,
        forge_project::TerrainExtent {
            size_x_m: source.extent_m.size_x_m,
            size_z_m: source.extent_m.size_z_m,
        },
    )?;
    let source_hash = source.content_hash().map_err(|e| e.to_string())?;
    let source_hash = if paths.is_empty() {
        source_hash
    } else {
        forge_core::canonical_json_sha256(&("path-vegetation-projection-v1", source_hash, paths))
            .map_err(|e| e.to_string())?
    };
    Ok(PreparedPathExclusions {
        exclusions: exclusions(paths)?,
        source_hash,
    })
}

pub fn project(
    source: &VegetationDocument,
    paths: &[AuthoredPath],
) -> Result<VegetationDocument, String> {
    source.validate().map_err(|e| e.to_string())?;
    let mut derived = source.clone();
    derived.exclusions.extend(exclusions(paths)?);
    // Compatibility adapter for the existing renderer. Kept until it consumes
    // PreparedPathExclusions separately; its authored-document quota still applies.
    derived
        .validate()
        .map_err(|e| format!("Path vegetation projection refused: {e}"))?;
    Ok(derived)
}

fn exclusions(paths: &[AuthoredPath]) -> Result<Vec<VegetationExclusion>, String> {
    let mut derived = Vec::new();
    for path in paths {
        // Geometry validation remains mandatory even when called without a zone head.
        if !(0.1..=64.0).contains(&path.width_m)
            || !(0.0..=32.0).contains(&path.shoulder_m)
            || !(2..=128).contains(&path.points.len())
        {
            return Err("Invalid path exclusion profile".into());
        }
        let sections = crate::path_surface::sections(path)?;
        let outer = path.width_m * 0.5 + path.shoulder_m;
        derived.push(VegetationExclusion::PolylineCorridor {
            id: VegetationExclusionId::new(format!("forge_path_{}", path.id).replace('-', "_")),
            points_xz_m: path
                .points
                .iter()
                .map(|point| [point.x_m, point.z_m])
                .collect(),
            half_width_m: outer,
        });
        // The outside of a miter lies beyond the round corridor end. Keep that
        // corner clear too, without modifying authored masks or manual objects.
        for (index, section) in sections
            .iter()
            .enumerate()
            .skip(1)
            .take(path.points.len() - 2)
        {
            let stretch = section[0].hypot(section[1]);
            if stretch > 1.0001 {
                let point = path.points[index];
                derived.push(VegetationExclusion::Circle {
                    id: VegetationExclusionId::new(
                        format!("forge_path_{}_join_{index}", path.id).replace('-', "_"),
                    ),
                    center_x_m: point.x_m,
                    center_z_m: point.z_m,
                    radius_m: outer * stretch,
                });
            }
        }
    }
    Ok(derived)
}
