//! Disposable draw records projected from accepted common placements.
use super::{scanned_lod, scanned_placements::SourcePlacements};
use forge_assets::tidewater_debris::scanned;
use forge_core::{EntityId, Transform};
use forge_project::zone_authoring::placements::{ManualPlacement, PlacementChanges};

pub struct Draw {
    pub placement_id: EntityId,
    pub transform: Transform,
    pub asset: u8,
    pub lod: u8,
    /// Index into `Frame::records`, to publish as the renderer's `MeshTag`.
    pub tag: u32,
    pub casts_shadow: bool,
}

pub struct Frame {
    pub draws: Vec<Draw>,
    /// Source iData: asset ordinal, immutable random variation, fade, outgoing.
    pub records: Vec<[f32; 4]>,
}

/// Resolve a complete frame before changing renderer entities or storage buffers.
/// The camera adapter supplies sphere/frustum intersection, including six metres
/// of caster margin. Edited scale expands the original world radius conservatively.
///
/// # Errors
/// Rejects malformed placements/camera, absent variations, wrong revisions and
/// frames exceeding 16,384 instances. Failure publishes no partial frame.
#[allow(
    clippy::cast_possible_truncation,
    reason = "Bounded tags and acquired random values are converted to the renderer's u32/f32 ABI."
)]
pub fn prepare(
    source: &SourcePlacements,
    placements: &[ManualPlacement],
    camera: [f64; 3],
    mut intersects: impl FnMut(EntityId, [f64; 3], f64) -> bool,
) -> Result<Frame, String> {
    PlacementChanges::between(&[], placements)?;
    if placements.len() > 16_384 || camera.iter().any(|v| !v.is_finite()) {
        return Err("Invalid scanned frame camera or placement budget".into());
    }
    let mut frame = Frame {
        draws: Vec::new(),
        records: Vec::new(),
    };
    for placement in placements {
        if placement.asset_hash != scanned::revision() {
            return Err("Scanned placement revision differs".into());
        }
        let owner = source
            .definitions
            .get(&placement.asset_id)
            .ok_or("Scanned source variation absent")?;
        let p = placement.transform.translation_m;
        let position = [f64::from(p.x), f64::from(p.y), f64::from(p.z)];
        let scale = placement.transform.scale;
        let radius = owner.radius_m
            * (f64::from(scale.x) / owner.source.sx)
                .max(f64::from(scale.y) / owner.source.sy)
                .max(f64::from(scale.z) / owner.source.sz);
        let distance = position
            .into_iter()
            .zip(camera)
            .map(|(a, b)| (a - b).powi(2))
            .sum::<f64>()
            .sqrt();
        for bucket in scanned_lod::select(
            distance,
            radius,
            intersects(placement.id, position, radius + 6.0),
        )? {
            let tag = frame.records.len() as u32;
            frame.records.push([
                f32::from(owner.source.asset),
                owner.source.rnd as f32,
                bucket.fade,
                if bucket.outgoing { 1.0 } else { 0.0 },
            ]);
            frame.draws.push(Draw {
                placement_id: placement.id,
                transform: placement.transform,
                asset: owner.source.asset,
                lod: bucket.lod,
                tag,
                casts_shadow: bucket.casts_shadow(),
            });
        }
    }
    Ok(frame)
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
