//! Tidewater inputs for the shared scanned draw projection.
use super::{scanned_lod, scanned_placements::SourcePlacements};
use forge_core::EntityId;
use forge_engine::source_surface::scanned_draws::{self, Instance};
pub use forge_engine::source_surface::scanned_draws::{Draw, Frame};
use forge_project::zone_authoring::placements::ManualPlacement;
use forge_tidewater_assets::tidewater_debris::scanned;

/// Project the acquired placements using Tidewater's LOD and frustum settings.
///
/// # Errors
/// Propagates violations of the common scanned draw contract.
pub fn prepare(
    source: &SourcePlacements,
    placements: &[ManualPlacement],
    camera: [f64; 3],
    intersects: impl FnMut(EntityId, [f64; 3], f64) -> bool,
) -> Result<Frame, String> {
    scanned_draws::prepare(
        placements,
        scanned::revision(),
        scanned_lod::POLICY,
        camera,
        scanned_draws::SOURCE_CASTER_MARGIN_M,
        |asset| {
            source.definitions.get(&asset).map(|owner| Instance {
                asset: owner.source.asset,
                random_variation: owner.source.rnd,
                reference_scale: [owner.source.sx, owner.source.sy, owner.source.sz],
                radius_m: owner.radius_m,
            })
        },
        intersects,
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    #[ignore = "Requires the acquired scanned package"]
    #[allow(
        clippy::float_cmp,
        clippy::cast_possible_truncation,
        reason = "Check exact source-to-shader ABI and bounded tags."
    )]
    fn edited_duplicates_keep_variation_and_transition_records_separate() {
        let root = std::path::PathBuf::from(std::env::var("FORGE_DEBRIS_SCANNED_FIXTURE").unwrap());
        let package = scanned::ScannedPackage::load(
            &std::fs::read(root.join("package.json")).unwrap(),
            |name, _| std::fs::read(root.join(name)).map_err(|e| e.to_string()),
        )
        .unwrap();
        let source = super::super::scanned_placements::prepare(&package).unwrap();
        let mut original = source.placements[0].clone();
        // Place the camera at the edited object's exact f32 centre plus the source
        // radius compensation and midpoint of the near/middle transition.
        original.transform.translation_m.x += 3.5;
        let mut duplicate = original.clone();
        duplicate.id = EntityId::deterministic("scanned-draw-duplicate");
        let owner = &source.definitions[&original.asset_id];
        let s = original.transform.scale;
        let radius = owner.radius_m
            * (f64::from(s.x) / owner.source.sx)
                .max(f64::from(s.y) / owner.source.sy)
                .max(f64::from(s.z) / owner.source.sz);
        let p = original.transform.translation_m;
        let camera = [
            f64::from(p.x),
            f64::from(p.y),
            f64::from(p.z) + radius * 6.0 + 22.0,
        ];
        let placements = [original.clone(), duplicate.clone()];
        let frame = prepare(&source, &placements, camera, |_, _, r| {
            assert_eq!(r, radius + 6.0);
            true
        })
        .unwrap();
        assert_eq!(frame.draws.len(), 4);
        for (index, draw) in frame.draws.iter().enumerate() {
            assert_eq!(draw.tag, index as u32);
            assert_eq!(draw.transform, original.transform);
            assert_eq!(draw.lod, (index % 2) as u8);
            assert_eq!(draw.casts_shadow, index % 2 == 0);
            let record = frame.records[index];
            assert_eq!(record[1], owner.source.rnd as f32);
            assert!((record[2] - 0.5).abs() < 1e-5);
            assert_eq!(record[3], if index % 2 == 0 { 1.0 } else { 0.0 });
        }
        assert_eq!(frame.draws[0].placement_id, original.id);
        assert_eq!(frame.draws[2].placement_id, duplicate.id);
        assert!(
            prepare(&source, &placements, camera, |_, _, _| false)
                .unwrap()
                .draws
                .is_empty()
        );
        assert_eq!(
            prepare(&source, &[duplicate], camera, |_, _, _| true)
                .unwrap()
                .draws
                .len(),
            2
        );
        let mut wrong = original;
        wrong.asset_hash = forge_core::ContentHash::from_bytes([0; 32]);
        assert!(prepare(&source, &[wrong], camera, |_, _, _| true).is_err());
        assert!(prepare(&source, &placements, [f64::NAN, 0.0, 0.0], |_, _, _| true).is_err());
    }
}
