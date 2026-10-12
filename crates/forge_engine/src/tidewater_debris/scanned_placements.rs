//! Initial scanned content expressed through the common authoring placement contract.
use forge_assets::tidewater_debris::scanned::{self, ScannedPackage, geometry};
use forge_core::{AssetId, EntityId};
use forge_project::zone_authoring::placements::{ManualPlacement, PlacementChanges};
use std::collections::BTreeMap;

/// Each source variation is a common asset identity; its GPU prototype remains shared.
pub struct SourcePlacements {
    pub definitions: BTreeMap<AssetId, geometry::Instance>,
    pub placements: Vec<ManualPlacement>,
}

/// Prepare source defaults without changing an authoring session or persisted document.
/// Duplicating a common placement reuses its asset variation and mesh prototypes.
///
/// # Errors
/// Refuses invalid acquired geometry or common transforms and duplicate identities.
pub fn prepare(package: &ScannedPackage) -> Result<SourcePlacements, String> {
    let geometry = geometry::prepare(package)?;
    from_geometry(&geometry)
}

/// Bind common variations from geometry already verified by the shared input reader.
///
/// # Errors
/// Refuses invalid common transforms or duplicate identities.
pub fn from_geometry(geometry: &geometry::Geometry) -> Result<SourcePlacements, String> {
    let mut definitions = BTreeMap::new();
    let mut placements = Vec::with_capacity(geometry.instances.len());
    for template in scanned::templates::prepare(geometry)? {
        let owner = template.instance;
        let asset_id = template.asset_id;
        placements.push(ManualPlacement {
            physics: None,
            id: EntityId::deterministic(format!("acquired-scanned-placement:{}", owner.source_id)),
            name: template.name,
            asset_id,
            asset_hash: template.asset_hash,
            transform: template.transform,
            locked: false,
        });
        if definitions.insert(asset_id, owner).is_some() {
            return Err("Duplicate scanned source variation".into());
        }
    }
    placements.sort_by_key(|placement| placement.id);
    PlacementChanges::between(&[], &placements)?;
    Ok(SourcePlacements {
        definitions,
        placements,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::{UnitQuaternion, Vec3};

    #[test]
    #[ignore = "Requires the acquired scanned package; no Studio gestures in this test"]
    #[allow(
        clippy::cast_possible_truncation,
        clippy::float_cmp,
        reason = "Compare exact f32 domain conversions of immutable acquired frame values."
    )]
    fn source_frames_enter_common_documents_without_losing_variation() {
        let root = std::path::PathBuf::from(std::env::var("FORGE_DEBRIS_SCANNED_FIXTURE").unwrap());
        let raw = std::fs::read(root.join("package.json")).unwrap();
        let package = ScannedPackage::load(&raw, |name, _| {
            std::fs::read(root.join(name)).map_err(|e| e.to_string())
        })
        .unwrap();
        let content = prepare(&package).unwrap();
        assert_eq!(content.placements.len(), 141);
        assert_eq!(content.definitions.len(), 141);
        for placement in &content.placements {
            let owner = &content.definitions[&placement.asset_id];
            let source = &owner.source;
            assert_eq!(
                placement.transform.translation_m,
                Vec3::new(source.x as f32, source.y as f32, source.z as f32)
            );
            assert_eq!(
                placement.transform.scale,
                Vec3::new(source.sx as f32, source.sy as f32, source.sz as f32)
            );
            let [x, y, z, w] = owner.quaternion_xyzw.map(|v| v as f32);
            assert_eq!(
                placement.transform.rotation,
                UnitQuaternion::new(x, y, z, w)
            );
            assert_eq!(placement.asset_hash, scanned::revision());
        }
        let original = content.placements[0].clone();
        let source_owner = &content.definitions[&original.asset_id];
        let random = source_owner.source.rnd;
        let mut edited = content.placements.clone();
        edited[0].transform.translation_m.x += 3.5;
        let mut duplicate = edited[0].clone();
        duplicate.id = EntityId::deterministic("scanned-common-document-copy-test");
        edited.push(duplicate);
        edited.sort_by_key(|p| p.id);
        PlacementChanges::between(&content.placements, &edited).unwrap();
        let saved = serde_json::to_vec(&edited).unwrap();
        let reopened: Vec<ManualPlacement> = serde_json::from_slice(&saved).unwrap();
        assert_eq!(reopened, edited);
        assert_eq!(
            reopened
                .iter()
                .filter(|p| p.asset_id == original.asset_id)
                .count(),
            2
        );
        assert_eq!(content.definitions[&original.asset_id].source.rnd, random);
        assert_eq!(content.placements[0], original);
    }
}
