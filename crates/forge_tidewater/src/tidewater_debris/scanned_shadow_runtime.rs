//! Tidewater asset bindings for the common scanned shadow renderer.
use super::{scanned_draws, scanned_placements::SourcePlacements};
use forge_engine::source_shadows::casters::textured::Pose;
use forge_project::zone_authoring::placements::ManualPlacement;

pub use forge_engine::source_surface::scanned_shadow::geometry;

/// Derive caster poses from the authored placements and their Tidewater asset ordinals.
///
/// # Errors
/// Propagates the shared renderer's validation of variations and storage tags.
pub fn poses(
    source: &SourcePlacements,
    placements: &[ManualPlacement],
    frame: &scanned_draws::Frame,
) -> Result<Vec<Pose>, String> {
    forge_engine::source_surface::scanned_shadow::poses(placements, frame, |asset| {
        source
            .definitions
            .get(&asset)
            .map(|owner| owner.source.asset)
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    #[ignore = "Requires acquired scanned package"]
    #[allow(
        clippy::float_cmp,
        reason = "Exact copied fixture values and GPU wire fields must remain identical; an epsilon would weaken this regression."
    )]
    fn near_casters_share_four_prototypes_and_retain_draw_coverage() {
        let root = std::path::PathBuf::from(std::env::var("FORGE_DEBRIS_SCANNED_FIXTURE").unwrap());
        let package = forge_tidewater_assets::tidewater_debris::scanned::ScannedPackage::load(
            &std::fs::read(root.join("package.json")).unwrap(),
            |name, _| std::fs::read(root.join(name)).map_err(|e| e.to_string()),
        )
        .unwrap();
        let meshes = super::super::scanned_meshes::prepare(&package).unwrap();
        let near = geometry(&meshes).unwrap();
        assert_eq!(near.len(), 4);
        assert_eq!(
            near.iter().map(|m| m.indices.len() / 12).sum::<usize>(),
            10_598
        );
        let source = super::super::scanned_placements::prepare(&package).unwrap();
        let p = source.placements[0].transform.translation_m;
        let draws = scanned_draws::prepare(
            &source,
            &source.placements,
            [f64::from(p.x), f64::from(p.y), f64::from(p.z)],
            |_, _, _| true,
        )
        .unwrap();
        let casters = poses(&source, &source.placements, &draws).unwrap();
        assert_eq!(casters.len(), 141);
        for (caster, placement) in casters.iter().zip(&source.placements) {
            let draw = draws
                .draws
                .iter()
                .find(|d| d.placement_id == placement.id && d.lod == 0);
            assert_eq!(caster.enabled, draw.is_some());
            if let Some(draw) = draw {
                assert_eq!(
                    caster.data,
                    draws.records[usize::try_from(draw.tag).unwrap()]
                );
            }
        }
        let empty =
            scanned_draws::prepare(&source, &source.placements, [0.0; 3], |_, _, _| false).unwrap();
        assert!(
            poses(&source, &source.placements, &empty)
                .unwrap()
                .iter()
                .all(|p| !p.enabled)
        );
    }
}
