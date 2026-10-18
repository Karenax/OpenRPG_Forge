//! Engine-neutral decoded scanned prototypes and original placement frames.
use super::ScannedPackage;
use serde::Deserialize;
use std::collections::{BTreeMap, BTreeSet};

#[derive(Deserialize)]
struct Reference {
    file: String,
}

#[derive(Deserialize)]
struct MeshSpec {
    asset: u8,
    lod: u8,
    attributes: BTreeMap<String, Reference>,
    atlas_uv: Reference,
    indices: Reference,
    triangles: usize,
}

#[derive(Clone, Debug, Deserialize)]
pub struct SourceRecord {
    pub asset: u8,
    pub x: f64,
    pub y: f64,
    pub z: f64,
    pub sx: f64,
    pub sy: f64,
    pub sz: f64,
    pub rnd: f64,
}

#[derive(Clone, Debug, Deserialize)]
pub struct Instance {
    pub source_id: String,
    pub source: SourceRecord,
    pub matrix_column_major: [f64; 16],
    pub quaternion_xyzw: [f64; 4],
    pub radius_m: f64,
}

#[derive(Deserialize)]
struct Document {
    meshes: Vec<MeshSpec>,
    instances: Vec<Instance>,
}

pub struct Prototype {
    pub asset: u8,
    pub lod: u8,
    pub positions: Vec<[f32; 3]>,
    pub normals: Vec<[f32; 3]>,
    pub atlas_uv: Vec<[f32; 2]>,
    pub indices: Vec<u32>,
}

pub struct Geometry {
    pub prototypes: Vec<Prototype>,
    pub instances: Vec<Instance>,
}

/// Decode each immutable prototype once; instances retain their source TRS.
///
/// # Errors
/// Refuses missing buffers, nonfinite values, invalid topology and duplicate identities.
pub fn prepare(package: &ScannedPackage) -> Result<Geometry, String> {
    let document: Document = serde_json::from_value(
        package
            .document("geometry")
            .ok_or("Scanned geometry document absent")?
            .clone(),
    )
    .map_err(|e| e.to_string())?;
    if document.meshes.len() != 12 || document.instances.len() != 141 {
        return Err("Scanned geometry population differs".into());
    }
    let mut pairs = BTreeSet::new();
    let mut prototypes = Vec::with_capacity(12);
    for spec in document.meshes {
        if spec.asset >= 4 || spec.lod >= 3 || !pairs.insert((spec.asset, spec.lod)) {
            return Err("Scanned prototype identity differs".into());
        }
        prototypes.push(prototype(package, &spec)?);
    }
    let mut identities = BTreeSet::new();
    for instance in &document.instances {
        validate_instance(instance)?;
        if !identities.insert(&instance.source_id) {
            return Err("Duplicate scanned placement identity".into());
        }
    }
    Ok(Geometry {
        prototypes,
        instances: document.instances,
    })
}

fn payload<'a>(package: &'a ScannedPackage, reference: &Reference) -> Result<&'a [u8], String> {
    package
        .file(&format!("geometry/{}", reference.file))
        .ok_or_else(|| "Scanned geometry payload absent".into())
}

fn vectors<const N: usize>(bytes: &[u8]) -> Result<Vec<[f32; N]>, String> {
    if bytes.is_empty() || !bytes.len().is_multiple_of(N * 4) || bytes.len() > 2 * 1024 * 1024 {
        return Err("Invalid scanned vertex buffer size".into());
    }
    bytes
        .as_chunks::<4>()
        .0
        .as_chunks::<N>()
        .0
        .iter()
        .map(|chunk| {
            let value: [f32; N] = std::array::from_fn(|i| f32::from_le_bytes(chunk[i]));
            if value.iter().any(|v| !v.is_finite()) {
                Err("Nonfinite scanned vertex".into())
            } else {
                Ok(value)
            }
        })
        .collect()
}

fn prototype(package: &ScannedPackage, spec: &MeshSpec) -> Result<Prototype, String> {
    let attr = |name| {
        spec.attributes
            .get(name)
            .ok_or("Scanned vertex attribute absent")
    };
    let positions = vectors::<3>(payload(package, attr("position")?)?)?;
    let normals = vectors::<3>(payload(package, attr("normal")?)?)?;
    let atlas_uv = vectors::<2>(payload(package, &spec.atlas_uv)?)?;
    let raw = payload(package, &spec.indices)?;
    if positions.len() != normals.len()
        || positions.len() != atlas_uv.len()
        || spec.triangles == 0
        || spec.triangles > 20_000
        || raw.len() != spec.triangles * 12
        || atlas_uv.iter().flatten().any(|v| !(0.0..=1.0).contains(v))
    {
        return Err("Scanned attribute or topology dimensions differ".into());
    }
    let indices: Vec<_> = raw
        .as_chunks::<4>()
        .0
        .iter()
        .map(|b| u32::from_le_bytes(*b))
        .collect();
    if indices.iter().any(|i| {
        usize::try_from(*i)
            .ok()
            .is_none_or(|i| i >= positions.len())
    }) {
        return Err("Scanned index out of bounds".into());
    }
    Ok(Prototype {
        asset: spec.asset,
        lod: spec.lod,
        positions,
        normals,
        atlas_uv,
        indices,
    })
}

fn validate_instance(instance: &Instance) -> Result<(), String> {
    let record = &instance.source;
    let values = [
        record.x,
        record.y,
        record.z,
        record.sx,
        record.sy,
        record.sz,
        record.rnd,
        instance.radius_m,
    ];
    let rotation_length: f64 = instance.quaternion_xyzw.iter().map(|v| v * v).sum();
    if instance.source_id.is_empty()
        || record.asset >= 4
        || values
            .iter()
            .chain(&instance.matrix_column_major)
            .chain(&instance.quaternion_xyzw)
            .any(|v| !v.is_finite())
        || [record.sx, record.sy, record.sz, instance.radius_m]
            .iter()
            .any(|v| *v <= 0.0)
        || !(0.0..=1.0).contains(&record.rnd)
        || (rotation_length - 1.0).abs() > 1e-10
    {
        return Err("Invalid scanned placement frame".into());
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    #[ignore = "Requires the acquired standalone scanned package"]
    fn decoded_prototypes_preserve_all_source_lods() {
        let root = std::path::PathBuf::from(std::env::var("FORGE_DEBRIS_SCANNED_FIXTURE").unwrap());
        let raw = std::fs::read(root.join("package.json")).unwrap();
        let package = ScannedPackage::load(&raw, |name, _| {
            std::fs::read(root.join(name)).map_err(|e| e.to_string())
        })
        .unwrap();
        let geometry = prepare(&package).unwrap();
        assert_eq!(geometry.instances.len(), 141);
        for (lod, expected) in [(0, 10_598), (1, 1_999), (2, 478)] {
            assert_eq!(
                geometry
                    .prototypes
                    .iter()
                    .filter(|p| p.lod == lod)
                    .map(|p| p.indices.len() / 3)
                    .sum::<usize>(),
                expected
            );
        }
        assert_eq!(geometry.prototypes.len(), 12);
    }
}
