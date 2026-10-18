//! Shared asset variations and source poses, independent of zone identity and ECS.
use super::{geometry, revision};
use forge_core::{AssetId, ContentHash, Transform, UnitQuaternion, Vec3};
use std::collections::BTreeSet;

/// A reusable variation; the consuming zone assigns its common placement identity.
pub struct Template {
    pub asset_id: AssetId,
    pub asset_hash: ContentHash,
    pub name: String,
    pub transform: Transform,
    pub instance: geometry::Instance,
}

/// Retain acquired random variation and exact source pose without decoding meshes again.
///
/// # Errors
/// Refuses invalid source kinds, common transforms or duplicate asset identities.
#[allow(
    clippy::cast_possible_truncation,
    reason = "Verified f64 source frames narrow to the validated common f32 transform domain."
)]
pub fn prepare(geometry: &geometry::Geometry) -> Result<Vec<Template>, String> {
    let names = [
        "Tronc échoué",
        "Branche échouée",
        "Branche échouée",
        "Coquillage",
    ];
    let mut assets = BTreeSet::new();
    geometry
        .instances
        .iter()
        .map(|owner| {
            let source = &owner.source;
            let name = names
                .get(usize::from(source.asset))
                .ok_or("Invalid scanned source kind")?;
            let [x, y, z, w] = owner.quaternion_xyzw.map(|v| v as f32);
            let transform = Transform {
                translation_m: Vec3::new(source.x as f32, source.y as f32, source.z as f32),
                rotation: UnitQuaternion::new(x, y, z, w),
                scale: Vec3::new(source.sx as f32, source.sy as f32, source.sz as f32),
            };
            transform.validate().map_err(|error| error.to_string())?;
            let asset_id =
                AssetId::deterministic(format!("acquired-scanned-variation:{}", owner.source_id));
            if !assets.insert(asset_id) {
                return Err("Duplicate scanned source variation".into());
            }
            Ok(Template {
                asset_id,
                asset_hash: revision(),
                name: (*name).into(),
                transform,
                instance: owner.clone(),
            })
        })
        .collect()
}
