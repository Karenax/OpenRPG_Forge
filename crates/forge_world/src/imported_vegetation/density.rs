//! Stable thinning of the imported layout by authored density masks.
use super::{ImportedPopulation, ImportedVisibleLayer};
use forge_core::{ContentHash, EntityId, bytes_sha256};
use forge_project::{VegetationDensityMask, VegetationExtent, zone_authoring::ZoneAuthoringHead};

pub struct ImportedAuthoredView<'a> {
    pub head_hash: ContentHash,
    pub excluded_count: usize,
    pub density_removed_count: usize,
    pub layers: Vec<ImportedVisibleLayer<'a>>,
}

impl ImportedPopulation {
    /// Exclusions/promotions followed by authored mask thinning. Imported base
    /// placements are authoritative: procedural acceptance, spacing and habitat
    /// recipe fields do not regenerate them. Clearing an override restores the
    /// imported layout, subject to the remaining authored exclusions.
    /// This does not yet apply road/building clearances or terrain reprojection.
    pub fn authored_view(
        &self,
        head: &ZoneAuthoringHead,
    ) -> Result<ImportedAuthoredView<'_>, String> {
        let mut view = self.exclusion_view(head)?;
        let mut density_removed_count = 0;
        for layer in &mut view.layers {
            let Some(correction) = head
                .corrections
                .density
                .iter()
                .find(|c| c.layer_id.as_str() == layer.layer_id)
            else {
                continue;
            };
            let before = layer.instances.len();
            layer.instances.retain(|entity| {
                let [x, _, z] = entity.source.position_m;
                let weight = weight(&correction.mask, self.document().extent_m, x, z);
                weight >= 1.0 || (weight > 0.0 && rank(entity.id) < weight)
            });
            density_removed_count += before - layer.instances.len();
        }
        Ok(ImportedAuthoredView {
            head_hash: view.head_hash,
            excluded_count: view.excluded_count,
            density_removed_count,
            layers: view.layers,
        })
    }
}

/// Midpoint of a 32-bit bin: strictly between zero and one, identical on WASM/native.
fn rank(id: EntityId) -> f64 {
    let hash = bytes_sha256(format!("imported-density-v1/{id}").as_bytes());
    let b = hash.as_bytes();
    (f64::from(u32::from_le_bytes([b[0], b[1], b[2], b[3]])) + 0.5) / 4_294_967_296.0
}

#[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
fn weight(mask: &VegetationDensityMask, extent: VegetationExtent, x: f64, z: f64) -> f64 {
    // Head validation proves mask dimensions and length; coordinates are validated
    // by the imported block decoder. Preserve f64 positions through interpolation.
    let last = mask.vertices_per_axis - 1;
    let u = ((x / f64::from(extent.size_x_m) + 0.5) * f64::from(last)).clamp(0.0, f64::from(last));
    let v = ((z / f64::from(extent.size_z_m) + 0.5) * f64::from(last)).clamp(0.0, f64::from(last));
    let ix = u.floor() as u16;
    let iz = v.floor() as u16;
    let sample = |x: u16, z: u16| {
        f64::from(
            mask.samples_u8[usize::from(z) * usize::from(mask.vertices_per_axis) + usize::from(x)],
        ) / 255.0
    };
    let lerp = |a: f64, b: f64, t: f64| a + (b - a) * t;
    lerp(
        lerp(
            sample(ix, iz),
            sample((ix + 1).min(last), iz),
            u - f64::from(ix),
        ),
        lerp(
            sample(ix, (iz + 1).min(last)),
            sample((ix + 1).min(last), (iz + 1).min(last)),
            u - f64::from(ix),
        ),
        v - f64::from(iz),
    )
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn masks_are_exact_at_corners_and_bilinear_at_center() {
        let mask = VegetationDensityMask {
            vertices_per_axis: 2,
            samples_u8: vec![0, 255, 255, 0],
        };
        let extent = VegetationExtent {
            size_x_m: 2048.0,
            size_z_m: 2048.0,
        };
        assert_eq!(weight(&mask, extent, -1024.0, -1024.0), 0.0);
        assert_eq!(weight(&mask, extent, 1024.0, -1024.0), 1.0);
        assert_eq!(weight(&mask, extent, 0.0, 0.0), 0.5);
        assert_eq!(weight(&mask, extent, 1024.0, 1024.0), 0.0);
    }
}
