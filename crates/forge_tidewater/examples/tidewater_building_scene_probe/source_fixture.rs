//! Explicit qualification fixture from the admitted package; no authored-map claim.
use forge_assets::source_surface_package::SurfacePackage;
use forge_core::{EntityId, Transform, UnitQuaternion, Vec3};
use forge_project::zone_authoring::placements::ManualPlacement;
use serde::Deserialize;

#[derive(Deserialize)]
struct Descriptor {
    building: Building,
}
#[derive(Deserialize)]
struct Building {
    source_id: String,
    source_frame: Frame,
}
#[derive(Deserialize)]
struct Frame {
    translation_m: [f32; 3],
    rotation_xyzw: [f32; 4],
    scale: [f32; 3],
}

pub fn placements(package: &SurfacePackage) -> Result<Vec<ManualPlacement>, String> {
    let mut result = Vec::new();
    for asset in &package.catalog().assets {
        let bytes = package
            .file(&format!("{}.json", asset.content_hash))
            .ok_or("Missing admitted building descriptor")?;
        if forge_core::bytes_sha256(bytes) != asset.content_hash {
            return Err("Building fixture descriptor pin mismatch".into());
        }
        let descriptor: Descriptor = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
        if descriptor.building.source_id != asset.logical_name {
            return Err("Building fixture identity mismatch".into());
        }
        let Frame {
            translation_m: t,
            rotation_xyzw: q,
            scale: s,
        } = descriptor.building.source_frame;
        let transform = Transform {
            translation_m: Vec3 {
                x: t[0],
                y: t[1],
                z: t[2],
            },
            rotation: UnitQuaternion {
                x: q[0],
                y: q[1],
                z: q[2],
                w: q[3],
            },
            scale: Vec3 {
                x: s[0],
                y: s[1],
                z: s[2],
            },
        };
        transform.validate().map_err(|e| e.to_string())?;
        result.push(ManualPlacement {
            id: EntityId::deterministic(format!("package-source-fixture:{}", asset.id)),
            name: asset.logical_name.clone(),
            asset_id: asset.id,
            asset_hash: asset.content_hash,
            transform,
            locked: false,
            particles: None,
            physics: None,
            local_lights: None,
        });
    }
    result.sort_by_key(|p| p.id);
    Ok(result)
}
