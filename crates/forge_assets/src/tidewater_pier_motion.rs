//! Immutable acquired motion phases joined to the pinned pier geometry.
use crate::tidewater_pier::PierPackage;
use forge_core::{ContentHash, bytes_sha256};
use serde::Deserialize;
use std::collections::BTreeMap;

const COMMIT: &str = "1438b1abfcaee3267092b75573014f4d9b4a983c";
const MANIFEST: &str = "2a93e08b6ae31548d76643214b1fb12de091d2c369e3f999554d2c1d2ad7ca54";
const ACQUISITION: &str = "e02acf64cbd1ddb2cacd9cd2ca10d2d71856181550ebe8da9afd374abf21097f";
const GEOMETRY: &str = "f3d89e2a8b92d7f99044b40500d28525a223955b28c1deffe0e9f1deeb8c5e73";
const LICENSE: &str = "bf7d406711a4cbf9af0b9eb5f2cf464ae88053e999e0e64a92b3423494dd8699";

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Entry {
    bytes: usize,
    sha256: ContentHash,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Reference {
    file: String,
    bytes: usize,
    sha256: ContentHash,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Manifest {
    schema: String,
    version: u32,
    source_commit: String,
    descriptor: Reference,
    license: Reference,
    files: BTreeMap<String, Entry>,
    qualification: String,
    runtime_integrated: bool,
    visual_parity: bool,
}

#[derive(Clone, Debug, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct LanternInitial {
    pub ordinal: usize,
    pub owner_frame: String,
    pub t: f64,
    pub ph: f64,
    pub x: f64,
    pub z: f64,
    pub vx: f64,
    pub vz: f64,
    pub pivot_world_m: [f64; 3],
    pub rest_world_m: [f64; 3],
    pub live_world_m: [f64; 3],
    pub rotation_xyzw: [f64; 4],
}
#[derive(Clone, Debug, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SignInitial {
    pub t: f64,
    pub a: f64,
    pub av: f64,
    pub b: f64,
    pub bv: f64,
    pub rotation_xyzw: [f64; 4],
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Initial {
    lanterns: Vec<LanternInitial>,
    sign: SignInitial,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Descriptor {
    schema: String,
    version: u32,
    source_commit: String,
    source_object_id: String,
    geometry_package_sha256: String,
    acquisition_sha256: String,
    initial: Initial,
    sign_pivot_local_m: [f64; 3],
}

/// Validated initial states; authored transforms remain owned by placements.
pub struct PierMotionPackage {
    descriptor: Descriptor,
}
impl PierMotionPackage {
    /// Read the closed, pinned runtime payload and join its anchors to geometry.
    pub fn load(
        manifest: &[u8],
        geometry: &PierPackage,
        mut read: impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<Self, String> {
        let index = parse_manifest(manifest)?;
        let mut files = BTreeMap::new();
        for (name, entry) in &index.files {
            let bytes = read(name, entry.bytes)?;
            verify(entry, &bytes)?;
            files.insert(name.clone(), bytes);
        }
        Self::finish(index, files, geometry)
    }

    pub async fn load_async<F, Fut>(
        manifest: &[u8],
        geometry: &PierPackage,
        mut read: F,
    ) -> Result<Self, String>
    where
        F: FnMut(String, usize) -> Fut,
        Fut: std::future::Future<Output = Result<Vec<u8>, String>>,
    {
        let index = parse_manifest(manifest)?;
        let mut files = BTreeMap::new();
        for (name, entry) in &index.files {
            let bytes = read(name.clone(), entry.bytes).await?;
            verify(entry, &bytes)?;
            files.insert(name.clone(), bytes);
        }
        Self::finish(index, files, geometry)
    }

    fn finish(
        index: Manifest,
        files: BTreeMap<String, Vec<u8>>,
        geometry: &PierPackage,
    ) -> Result<Self, String> {
        let license = &files[&index.license.file];
        if bytes_sha256(license).to_string() != LICENSE || !license.starts_with(b"MIT License") {
            return Err("Pier motion original license differs".into());
        }
        let descriptor: Descriptor =
            serde_json::from_slice(&files[&index.descriptor.file]).map_err(|e| e.to_string())?;
        if descriptor.schema != "forge.tidewater.pier-motion-runtime"
            || descriptor.version != 1
            || descriptor.source_commit != COMMIT
            || descriptor.source_object_id != format!("tidewater:{COMMIT}:pier:0")
            || descriptor.geometry_package_sha256 != GEOMETRY
            || descriptor.acquisition_sha256 != ACQUISITION
            || descriptor.initial.lanterns.len() != 11
        {
            return Err("Pier motion source identity differs".into());
        }
        let geo: serde_json::Value =
            serde_json::from_slice(geometry.description()).map_err(|e| e.to_string())?;
        let pier = &geo["pier"];
        if pier["source_id"] != descriptor.source_object_id || geo["source_commit"] != COMMIT {
            return Err("Pier motion geometry identity differs".into());
        }
        let origin: [f64; 3] =
            serde_json::from_value(pier["source_frame"]["translation_m"].clone())
                .map_err(|e| e.to_string())?;
        let groups = pier["lanterns"]
            .as_array()
            .ok_or("Pier lantern anchors absent")?;
        if groups.len() != 11 {
            return Err("Pier motion anchor count differs".into());
        }
        for (ordinal, state) in descriptor.initial.lanterns.iter().enumerate() {
            if state.ordinal != ordinal
                || state.owner_frame != format!("village-lantern-{ordinal}")
                || [state.t, state.ph, state.x, state.z, state.vx, state.vz]
                    .iter()
                    .any(|v| !v.is_finite())
                || [state.x, state.z, state.vx, state.vz] != [0.0; 4]
                || state.rotation_xyzw != [0.0, 0.0, 0.0, 1.0]
                || state.live_world_m != state.rest_world_m
            {
                return Err("Pier motion initial lantern state differs".into());
            }
            for (captured, key) in [(state.pivot_world_m, "pivot"), (state.rest_world_m, "rest")] {
                let local: [f64; 3] = serde_json::from_value(groups[ordinal][key].clone())
                    .map_err(|e| e.to_string())?;
                if captured.iter().zip(origin).zip(local).any(|((&a, o), b)| {
                    !a.is_finite() || !o.is_finite() || !b.is_finite() || (a - o - b).abs() > 1e-12
                }) {
                    return Err("Pier motion lantern anchor differs".into());
                }
            }
        }
        let sign = &descriptor.initial.sign;
        let pivot: [f64; 3] =
            serde_json::from_value(pier["sign"]["pivot"].clone()).map_err(|e| e.to_string())?;
        if [sign.t, sign.a, sign.av, sign.b, sign.bv] != [0.0; 5]
            || sign.rotation_xyzw != [0.0, 0.0, 0.0, 1.0]
            || pivot != descriptor.sign_pivot_local_m
            || pivot.iter().any(|v| !v.is_finite())
        {
            return Err("Pier motion sign state or pivot differs".into());
        }
        Ok(Self { descriptor })
    }
    #[must_use]
    pub fn lanterns(&self) -> &[LanternInitial] {
        &self.descriptor.initial.lanterns
    }
    #[must_use]
    pub fn sign(&self) -> &SignInitial {
        &self.descriptor.initial.sign
    }
    #[must_use]
    pub fn sign_pivot_local_m(&self) -> [f64; 3] {
        self.descriptor.sign_pivot_local_m
    }
}

fn parse_manifest(bytes: &[u8]) -> Result<Manifest, String> {
    if bytes_sha256(bytes).to_string() != MANIFEST {
        return Err("Pier motion manifest hash differs".into());
    }
    let m: Manifest = serde_json::from_slice(bytes).map_err(|e| e.to_string())?;
    if m.schema != "forge.tidewater.pier-motion-runtime-package"
        || m.version != 1
        || m.source_commit != COMMIT
        || m.qualification != "derived-package-only"
        || m.runtime_integrated
        || m.visual_parity
        || m.descriptor.file != "runtime-motion.json"
        || m.license.file != "source/LICENSE"
        || m.files.keys().map(String::as_str).collect::<Vec<_>>()
            != ["runtime-motion.json", "source/LICENSE"]
    {
        return Err("Pier motion manifest closure differs".into());
    }
    for r in [&m.descriptor, &m.license] {
        let entry = m.files.get(&r.file).ok_or("Pier motion reference absent")?;
        if entry.bytes != r.bytes || entry.sha256 != r.sha256 {
            return Err("Pier motion reference differs".into());
        }
    }
    Ok(m)
}
fn verify(entry: &Entry, bytes: &[u8]) -> Result<(), String> {
    if bytes.len() != entry.bytes || bytes_sha256(bytes) != entry.sha256 {
        return Err("Pier motion payload differs".into());
    }
    Ok(())
}
