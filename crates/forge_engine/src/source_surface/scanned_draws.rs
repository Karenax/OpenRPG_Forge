//! Draw records for scanned surfaces and their authored placements.
use crate::distance_lod::fixed_bands::Policy;
use forge_core::{AssetId, ContentHash, EntityId, Transform};
use forge_project::zone_authoring::placements::{ManualPlacement, PlacementChanges};

pub(super) const MAX_PLACEMENTS: usize = 16_384;
/// Distance and cross-fade settings of the acquired scanned-material profile.
pub const SOURCE_POLICY: Policy = match Policy::new(22.0, 70.0, 260.0, 0.12, 6.0, Some(0)) {
    Ok(policy) => policy,
    Err(_) => panic!("Invalid scanned LOD policy"),
};
pub const SOURCE_CASTER_MARGIN_M: f64 = 6.0;

/// Immutable source definition for one asset variation.
#[derive(Clone, Copy)]
pub struct Instance {
    pub asset: u8,
    pub random_variation: f64,
    pub reference_scale: [f64; 3],
    pub radius_m: f64,
}

pub struct Draw {
    pub placement_id: EntityId,
    pub transform: Transform,
    pub asset: u8,
    pub lod: u8,
    /// Index into `Frame::records`, published as the renderer's `MeshTag`.
    pub tag: u32,
    pub casts_shadow: bool,
}

pub struct Frame {
    pub draws: Vec<Draw>,
    /// Source iData: asset ordinal, immutable random variation, fade, outgoing.
    pub records: Vec<[f32; 4]>,
}

/// Resolve the complete draw frame before publishing entities or buffers.
/// Edited scale expands the original world radius conservatively.
///
/// # Errors
/// Rejects malformed placements, camera, variations, revisions or frustum margin.
#[allow(
    clippy::cast_possible_truncation,
    reason = "Validated variation and bounded record index narrow to the shader ABI."
)]
pub fn prepare(
    placements: &[ManualPlacement],
    revision: ContentHash,
    policy: Policy,
    camera: [f64; 3],
    caster_margin_m: f64,
    instance: impl Fn(AssetId) -> Option<Instance>,
    mut intersects: impl FnMut(EntityId, [f64; 3], f64) -> bool,
) -> Result<Frame, String> {
    PlacementChanges::between(&[], placements)?;
    if placements.len() > MAX_PLACEMENTS
        || camera.iter().any(|v| !v.is_finite())
        || !caster_margin_m.is_finite()
        || caster_margin_m < 0.0
    {
        return Err("Invalid scanned frame camera, margin or placement budget".into());
    }
    let mut frame = Frame {
        draws: Vec::new(),
        records: Vec::new(),
    };
    for placement in placements {
        if placement.asset_hash != revision {
            return Err("Scanned placement revision differs".into());
        }
        let owner = instance(placement.asset_id).ok_or("Scanned source variation absent")?;
        if owner.asset >= 4
            || !(0.0..=1.0).contains(&owner.random_variation)
            || owner
                .reference_scale
                .iter()
                .any(|v| !v.is_finite() || *v <= 0.0)
            || !owner.radius_m.is_finite()
            || owner.radius_m < 0.0
        {
            return Err("Invalid scanned source variation".into());
        }
        let p = placement.transform.translation_m;
        let position = [f64::from(p.x), f64::from(p.y), f64::from(p.z)];
        let scale = placement.transform.scale;
        let radius = owner.radius_m
            * (f64::from(scale.x) / owner.reference_scale[0])
                .max(f64::from(scale.y) / owner.reference_scale[1])
                .max(f64::from(scale.z) / owner.reference_scale[2]);
        let frustum_radius = radius + caster_margin_m;
        if !frustum_radius.is_finite() {
            return Err("Scanned frustum radius overflow".into());
        }
        let distance = position
            .into_iter()
            .zip(camera)
            .map(|(a, b)| (a - b).powi(2))
            .sum::<f64>()
            .sqrt();
        for bucket in policy.select(
            distance,
            radius,
            intersects(placement.id, position, frustum_radius),
        )? {
            let tag = frame.records.len() as u32;
            frame.records.push([
                f32::from(owner.asset),
                owner.random_variation as f32,
                bucket.fade,
                if bucket.outgoing { 1.0 } else { 0.0 },
            ]);
            frame.draws.push(Draw {
                placement_id: placement.id,
                transform: placement.transform,
                asset: owner.asset,
                lod: bucket.lod,
                tag,
                casts_shadow: bucket.casts_shadow(),
            });
        }
    }
    Ok(frame)
}
