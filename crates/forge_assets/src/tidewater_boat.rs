//! Hash-pinned source boat transport. Placement, rendering and controllers are
//! separate consumers; loading this package is not an appearance qualification.
use crate::source_geometry::{
    AssetBounds, AssetBufferReference, AssetFileReference, AssetLocalFrame, ResolvedWaterExclusion,
    WaterExclusionDescriptor,
};
use forge_core::{AssetId, ContentHash, bytes_sha256};
use serde::{Deserialize, Serialize};
use serde_json::Value;
use std::collections::{BTreeMap, BTreeSet};

const COMMIT: &str = "1438b1abfcaee3267092b75573014f4d9b4a983c";
const PACKAGE: &str = "44a9110a0bda0cd319488f088fc1da32f3344f6cad8b9db0a92869df0c49351a";
const MAX_FILE: usize = 32 * 1024 * 1024;
const MAX_TOTAL: usize = 128 * 1024 * 1024;

#[must_use]
pub fn asset_id() -> AssetId {
    AssetId::deterministic(format!("tidewater/boat/{COMMIT}/0"))
}
#[must_use]
pub fn package_hash() -> ContentHash {
    PACKAGE.parse().expect("Pinned boat SHA-256 is valid")
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Entry {
    bytes: usize,
    sha256: ContentHash,
}
#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Manifest {
    schema: String,
    version: u32,
    source_commit: String,
    boat: AssetFileReference,
    materials: AssetFileReference,
    inspection_glb: AssetFileReference,
    license: AssetFileReference,
    files: BTreeMap<String, Entry>,
    qualification: String,
}

#[derive(Clone, Debug, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct BoatNode {
    pub id: String,
    pub parent: Option<String>,
    pub transform: AssetLocalFrame,
    pub matrix_local: [f64; 16],
    pub matrix_world: [f64; 16],
    pub visible: bool,
}
#[derive(Clone, Debug, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct BoatPart {
    pub id: String,
    pub node: String,
    pub material: String,
    pub attributes: BTreeMap<String, AssetBufferReference>,
    pub indices: AssetBufferReference,
    pub triangles: usize,
    pub bounds_local: AssetBounds,
    pub cast_shadow: bool,
    pub receive_shadow: bool,
    pub render_order: i32,
}
#[derive(Clone, Debug, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct BoatSourceFile {
    pub path: String,
    pub sha256: ContentHash,
    pub bytes: usize,
    pub original: AssetFileReference,
    pub role: Option<String>,
    pub executed: Option<bool>,
}
#[derive(Clone, Debug, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct BoatCanonicalPlacement {
    pub translation_m: [f64; 3],
    pub heading_radians: f64,
}
#[derive(Clone, Debug, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct BoatCollider {
    pub id: String,
    pub tag: String,
    pub center_m: [f64; 3],
    pub half_extents_m: [f64; 3],
    pub walkable: bool,
    pub solid: bool,
}
#[derive(Clone, Debug, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct BoatHullSample {
    pub position: [f64; 3],
    pub area: f64,
    pub depth: f64,
    #[serde(rename = "bottomY")]
    pub bottom_y: f64,
}
#[derive(Clone, Debug, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct BoatDescription {
    pub schema: String,
    pub version: u32,
    pub source_commit: String,
    pub source_id: String,
    pub source_graph_sha256: ContentHash,
    pub source_files: Vec<BoatSourceFile>,
    pub source_support_files: Vec<BoatSourceFile>,
    pub source_frame: Value,
    pub canonical_placement: BoatCanonicalPlacement,
    pub nodes: Vec<BoatNode>,
    pub parts: Vec<BoatPart>,
    pub triangles: usize,
    pub material_library: AssetFileReference,
    pub inspection_glb: AssetFileReference,
    pub water_exclusion: WaterExclusionDescriptor,
    pub anchors: BTreeMap<String, Value>,
    pub dimensions: BTreeMap<String, f64>,
    pub hydrostatics: Value,
    pub colliders: Vec<BoatCollider>,
    pub hull_samples: Vec<BoatHullSample>,
    pub controller_observations: Value,
    pub qualification: BTreeMap<String, String>,
}

pub struct ResolvedBoatPart {
    pub id: String,
    pub node: String,
    pub material: String,
    pub attributes: BTreeMap<String, Vec<f32>>,
    pub indices: Vec<u32>,
}

pub struct BoatPackage {
    files: BTreeMap<String, Vec<u8>>,
    description: BoatDescription,
    materials: Value,
    expected: ContentHash,
}

impl BoatPackage {
    /// The caller supplies its governed receipt hash. Studio's canonical source
    /// route must use `package_hash()`; derivatives require their own receipt.
    pub fn load(
        manifest: &[u8],
        expected: ContentHash,
        mut read: impl FnMut(&str, usize) -> Result<Vec<u8>, String>,
    ) -> Result<Self, String> {
        let index = parse(manifest, expected)?;
        let mut files = BTreeMap::new();
        for (name, entry) in &index.files {
            let bytes = read(name, entry.bytes)?;
            verify(entry, &bytes)?;
            files.insert(name.clone(), bytes);
        }
        Self::finish(&index, files, expected)
    }

    pub async fn load_async<F, Fut>(
        manifest: &[u8],
        expected: ContentHash,
        mut read: F,
    ) -> Result<Self, String>
    where
        F: FnMut(String, usize) -> Fut,
        Fut: std::future::Future<Output = Result<Vec<u8>, String>>,
    {
        let index = parse(manifest, expected)?;
        let mut files = BTreeMap::new();
        for (name, entry) in &index.files {
            let bytes = read(name.clone(), entry.bytes).await?;
            verify(entry, &bytes)?;
            files.insert(name.clone(), bytes);
        }
        Self::finish(&index, files, expected)
    }

    fn finish(
        index: &Manifest,
        files: BTreeMap<String, Vec<u8>>,
        expected: ContentHash,
    ) -> Result<Self, String> {
        for (reference, name) in [
            (&index.boat, "boat.json"),
            (&index.materials, "boat-materials.json"),
            (&index.inspection_glb, "lobster-boat.glb"),
            (&index.license, "LICENSE-Tidewater.txt"),
        ] {
            if reference.file != name {
                return Err("Boat root reference name differs".into());
            }
            reference.verify(files.get(name).ok_or("Boat root dependency absent")?)?;
        }
        let license = &files[&index.license.file];
        if !license.starts_with(b"MIT License")
            || !license
                .windows(b"Permission is hereby granted".len())
                .any(|s| s == b"Permission is hereby granted")
        {
            return Err("Boat source licence is incomplete".into());
        }
        let raw: Value =
            serde_json::from_slice(&files[&index.boat.file]).map_err(|e| e.to_string())?;
        let description: BoatDescription =
            serde_json::from_value(raw.clone()).map_err(|e| e.to_string())?;
        let materials: Value =
            serde_json::from_slice(&files[&index.materials.file]).map_err(|e| e.to_string())?;
        validate_numbers(&raw)?;
        validate_numbers(&materials)?;
        let mut referenced = BTreeSet::from([
            index.boat.file.clone(),
            index.materials.file.clone(),
            index.inspection_glb.file.clone(),
            index.license.file.clone(),
        ]);
        references(&raw, &files, &mut referenced, 0)?;
        references(&materials, &files, &mut referenced, 0)?;
        if referenced != files.keys().cloned().collect() {
            return Err("Boat package contains unclaimed payloads".into());
        }
        validate_description(&description, &materials, &files)?;
        validate_glb(&files[&index.inspection_glb.file])?;
        Ok(Self {
            files,
            description,
            materials,
            expected,
        })
    }

    #[must_use]
    pub fn description(&self) -> &BoatDescription {
        &self.description
    }
    #[must_use]
    pub fn materials(&self) -> &Value {
        &self.materials
    }
    #[must_use]
    pub fn receipt_hash(&self) -> ContentHash {
        self.expected
    }
    #[must_use]
    pub fn file(&self, name: &str) -> Option<&[u8]> {
        self.files.get(name).map(Vec::as_slice)
    }
    #[must_use]
    pub fn file_count(&self) -> usize {
        self.files.len()
    }
    pub fn water_exclusion(&self) -> Result<ResolvedWaterExclusion, String> {
        self.description
            .water_exclusion
            .resolve(|name| self.file(name))
    }
    pub fn resolve_part(&self, id: &str) -> Result<ResolvedBoatPart, String> {
        let part = self
            .description
            .parts
            .iter()
            .find(|p| p.id == id)
            .ok_or("Boat part absent")?;
        decode_part(part, &self.files)
    }
}

fn safe_name(name: &str) -> bool {
    let stem = name
        .split('.')
        .next()
        .unwrap_or_default()
        .to_ascii_uppercase();
    let reserved = matches!(stem.as_str(), "CON" | "PRN" | "AUX" | "NUL")
        || (stem.len() == 4
            && (stem.starts_with("COM") || stem.starts_with("LPT"))
            && matches!(stem.as_bytes()[3], b'1'..=b'9'));
    !name.is_empty()
        && name.len() <= 128
        && !name.starts_with('.')
        && !name.ends_with('.')
        && !reserved
        && name
            .bytes()
            .all(|c| c.is_ascii_alphanumeric() || matches!(c, b'-' | b'_' | b'.'))
}
fn parse(manifest: &[u8], expected: ContentHash) -> Result<Manifest, String> {
    if manifest.len() > 2 * 1024 * 1024 || bytes_sha256(manifest) != expected {
        return Err("Boat manifest pin mismatch or size limit".into());
    }
    let index: Manifest = serde_json::from_slice(manifest).map_err(|e| e.to_string())?;
    if index.schema != "forge.tidewater.boat-package"
        || index.version != 1
        || index.source_commit != COMMIT
        || index.qualification.is_empty()
        || index.files.is_empty()
        || index.files.len() > 4096
        || index
            .files
            .iter()
            .any(|(name, e)| !safe_name(name) || e.bytes == 0 || e.bytes > MAX_FILE)
    {
        return Err("Unsupported boat manifest or payload index".into());
    }
    let total = index
        .files
        .values()
        .try_fold(0usize, |n, e| n.checked_add(e.bytes))
        .ok_or("Boat package size overflow")?;
    if total > MAX_TOTAL {
        return Err("Boat package exceeds total size limit".into());
    }
    let folded: BTreeSet<_> = index
        .files
        .keys()
        .map(|name| name.to_ascii_lowercase())
        .collect();
    if folded.len() != index.files.len() {
        return Err("Boat payload names collide on a case-insensitive filesystem".into());
    }
    for r in [
        &index.boat,
        &index.materials,
        &index.inspection_glb,
        &index.license,
    ] {
        let entry = index
            .files
            .get(&r.file)
            .ok_or("Boat root reference is outside the file index")?;
        if r.bytes != entry.bytes || r.sha256 != entry.sha256 {
            return Err("Boat root reference differs from its index".into());
        }
    }
    Ok(index)
}
fn verify(entry: &Entry, bytes: &[u8]) -> Result<(), String> {
    if bytes.len() != entry.bytes || bytes_sha256(bytes) != entry.sha256 {
        return Err("Boat payload size or pin mismatch".into());
    }
    Ok(())
}
fn references(
    value: &Value,
    files: &BTreeMap<String, Vec<u8>>,
    used: &mut BTreeSet<String>,
    depth: usize,
) -> Result<(), String> {
    if depth > 32 {
        return Err("Boat dependency tree exceeds depth limit".into());
    }
    match value {
        Value::Object(object) => {
            if object.contains_key("file") {
                let reference = AssetFileReference {
                    file: object["file"]
                        .as_str()
                        .ok_or("Boat dependency filename absent")?
                        .into(),
                    bytes: object
                        .get("bytes")
                        .and_then(Value::as_u64)
                        .and_then(|x| usize::try_from(x).ok())
                        .ok_or("Boat dependency size absent")?,
                    sha256: object
                        .get("sha256")
                        .and_then(Value::as_str)
                        .ok_or("Boat dependency pin absent")?
                        .parse::<ContentHash>()
                        .map_err(|e| e.to_string())?,
                };
                reference.verify(
                    files
                        .get(&reference.file)
                        .ok_or("Boat transitive dependency absent")?,
                )?;
                used.insert(reference.file);
            }
            for child in object.values() {
                references(child, files, used, depth + 1)?;
            }
        }
        Value::Array(array) => {
            for child in array {
                references(child, files, used, depth + 1)?;
            }
        }
        _ => {}
    }
    Ok(())
}
// The canonical placement is an exact source value, not a computed pose.
#[allow(clippy::float_cmp)]
fn validate_description(
    d: &BoatDescription,
    materials: &Value,
    files: &BTreeMap<String, Vec<u8>>,
) -> Result<(), String> {
    if d.schema != "forge.tidewater.boat-source"
        || d.version != 1
        || d.source_commit != COMMIT
        || d.source_id != format!("tidewater:{COMMIT}:boat:0")
        || d.source_files.len() != 54
        || d.source_support_files.len() != 10
        || d.nodes.len() != 14
        || d.parts.len() != 12
        || d.triangles != 46_845
        || d.colliders.len() != 23
        || d.hull_samples.len() != 32
        || d.source_frame
            != serde_json::json!({"units":"metres","axes":{"x":"port","y":"up","z":"forward"},"source_to_asset":"identity"})
        || d.canonical_placement.translation_m != [64.5, 0.0, 36.5]
        || d.canonical_placement.heading_radians != 0.0
        || d.controller_observations["curves_baked_into_glb"] != false
        || d.material_library.file != "boat-materials.json"
        || d.inspection_glb.file != "lobster-boat.glb"
    {
        return Err("Unexpected boat source description".into());
    }
    validate_hierarchy(&d.nodes)?;
    let nodes: BTreeSet<_> = d.nodes.iter().map(|n| n.id.as_str()).collect();
    let material_ids = validate_materials(materials, files)?;
    let mut ids = BTreeSet::new();
    let mut node_meshes = BTreeSet::new();
    let mut triangles = 0usize;
    for p in &d.parts {
        if p.id.is_empty()
            || !ids.insert(&p.id)
            || !node_meshes.insert(&p.node)
            || !nodes.contains(p.node.as_str())
            || !material_ids.contains(p.material.as_str())
        {
            return Err("Boat part identity or binding differs".into());
        }
        decode_part(p, files)?;
        triangles = triangles
            .checked_add(p.triangles)
            .ok_or("Boat triangle count overflow")?;
    }
    if triangles != d.triangles
        || !nodes.contains(d.water_exclusion.parent.as_str())
        || ids.contains(&d.water_exclusion.id)
    {
        return Err("Boat total or exclusion identity differs".into());
    }
    d.water_exclusion
        .resolve(|name| files.get(name).map(Vec::as_slice))?;
    let mut paths = BTreeSet::new();
    for sources in [&d.source_files, &d.source_support_files] {
        for s in sources {
            if s.path.is_empty()
                || s.path.starts_with('/')
                || s.path.contains(['\\', ':'])
                || s.path
                    .split('/')
                    .any(|c| c.is_empty() || matches!(c, "." | ".."))
                || !paths.insert(&s.path)
                || s.sha256 != s.original.sha256
                || s.bytes != s.original.bytes
                || s.executed == Some(true)
            {
                return Err("Boat source provenance differs".into());
            }
            let original = &files[&s.original.file];
            if !std::path::Path::new(&s.path)
                .extension()
                .is_some_and(|ext| ext.eq_ignore_ascii_case("ogg"))
            {
                std::str::from_utf8(original).map_err(|e| e.to_string())?;
            }
        }
    }
    validate_provenance(d)?;
    validate_anchors(&d.anchors)?;
    validate_controls(&d.controller_observations, &d.nodes, materials)?;
    validate_buoyancy(d)
}

fn validate_buoyancy(d: &BoatDescription) -> Result<(), String> {
    let mut collider_ids = BTreeSet::new();
    for collider in &d.colliders {
        if collider.id.is_empty()
            || collider.tag.is_empty()
            || !collider_ids.insert(&collider.id)
            || collider
                .center_m
                .iter()
                .chain(collider.half_extents_m.iter())
                .any(|x| !x.is_finite())
            || collider.half_extents_m.iter().any(|&x| x < 0.0)
        {
            return Err("Boat collider is invalid".into());
        }
    }
    if d.hull_samples.iter().any(|s| {
        s.position
            .iter()
            .chain([&s.area, &s.depth, &s.bottom_y])
            .any(|x| !x.is_finite())
            || s.area <= 0.0
            || s.depth < 0.0
    }) {
        return Err("Boat flotation sample is invalid".into());
    }
    Ok(())
}

fn validate_provenance(d: &BoatDescription) -> Result<(), String> {
    #[derive(Serialize)]
    struct Pin<'a> {
        path: &'a str,
        sha256: ContentHash,
        bytes: usize,
    }
    if d.source_files.windows(2).any(|s| s[0].path >= s[1].path)
        || d.source_files
            .iter()
            .any(|s| s.role.is_some() || s.executed.is_some())
        || d.source_support_files.iter().any(|s| {
            s.executed != Some(false)
                || !matches!(
                    s.role.as_deref(),
                    Some("integration_reference_only" | "source_credits" | "source_audio")
                )
        })
    {
        return Err("Boat source graph order or support execution policy differs".into());
    }
    let pins: Vec<_> = d
        .source_files
        .iter()
        .map(|s| Pin {
            path: &s.path,
            sha256: s.sha256,
            bytes: s.bytes,
        })
        .collect();
    let mut encoded = serde_json::to_vec_pretty(&pins).map_err(|e| e.to_string())?;
    encoded.push(b'\n');
    if bytes_sha256(&encoded) != d.source_graph_sha256 {
        return Err("Boat source graph fingerprint differs from its records".into());
    }
    Ok(())
}

// This cast tests whether a preserved f64 value fits the renderer's finite f32 range.
#[allow(clippy::cast_possible_truncation)]
fn validate_numbers(value: &Value) -> Result<(), String> {
    match value {
        Value::Number(n)
            if n.as_f64()
                .is_none_or(|x| !x.is_finite() || !(x as f32).is_finite()) =>
        {
            return Err("Boat numeric value is outside the finite renderer range".into());
        }
        Value::Array(a) => {
            for v in a {
                validate_numbers(v)?;
            }
        }
        Value::Object(o) => {
            for v in o.values() {
                validate_numbers(v)?;
            }
        }
        _ => {}
    }
    Ok(())
}

fn vector3(value: &Value) -> Result<[f64; 3], String> {
    serde_json::from_value(value.clone()).map_err(|e| format!("Boat vector differs: {e}"))
}

fn validate_anchors(anchors: &BTreeMap<String, Value>) -> Result<(), String> {
    if anchors.len() != 6 {
        return Err("Boat anchor set differs".into());
    }
    for key in ["helmEye", "boardPoint", "propeller", "rudder"] {
        vector3(anchors.get(key).ok_or("Boat anchor absent")?)?;
    }
    for (key, count) in [("exitPoints", 6), ("bowSprayPoints", 8)] {
        let points = anchors
            .get(key)
            .and_then(Value::as_array)
            .ok_or("Boat anchor array absent")?;
        if points.len() != count {
            return Err("Boat anchor count differs".into());
        }
        for point in points {
            vector3(point)?;
        }
    }
    Ok(())
}

fn validate_controls(
    observations: &Value,
    nodes: &[BoatNode],
    library: &Value,
) -> Result<(), String> {
    let table: BTreeMap<_, _> = nodes.iter().map(|n| (n.id.as_str(), n)).collect();
    let check_frame = |node: &Value| -> Result<(), String> {
        if !table.contains_key(node["id"].as_str().ok_or("Boat control node id absent")?) {
            return Err("Boat control references a missing node".into());
        }
        let frame: AssetLocalFrame =
            serde_json::from_value(node["transform"].clone()).map_err(|e| e.to_string())?;
        frame.validate()
    };
    let controls = observations["controls"]
        .as_array()
        .ok_or("Boat control observations absent")?;
    if controls.len() != 3 {
        return Err("Boat control observation count differs".into());
    }
    for (control, input) in controls.iter().zip([-1, 0, 1]) {
        if control["input"] != input {
            return Err("Boat control input differs".into());
        }
        let frames = control["nodes"]
            .as_array()
            .ok_or("Boat control frames absent")?;
        if frames.len() != 3 {
            return Err("Boat control frame count differs".into());
        }
        for frame in frames {
            check_frame(frame)?;
        }
    }
    let running = observations["running"]["nodes"]
        .as_array()
        .ok_or("Boat running frames absent")?;
    if running.len() != 2 {
        return Err("Boat running frame count differs".into());
    }
    for frame in running {
        check_frame(frame)?;
    }
    let parts = observations["part_controls"]
        .as_array()
        .ok_or("Boat part controls absent")?;
    if parts.len() != 5 {
        return Err("Boat part control count differs".into());
    }
    let mut ids = BTreeSet::new();
    for part in parts {
        if !ids.insert(part["id"].as_str().ok_or("Boat part control id absent")?) {
            return Err("Boat control ids are duplicated".into());
        }
        let node = table
            .get(
                part["node"]
                    .as_str()
                    .ok_or("Boat part control node absent")?,
            )
            .ok_or("Boat part control node unknown")?;
        let frame: AssetLocalFrame =
            serde_json::from_value(part["node_frame"].clone()).map_err(|e| e.to_string())?;
        frame.validate()?;
        if frame != node.transform {
            return Err("Boat part control rest frame differs from its node".into());
        }
        if let Some(pivot) = part.get("pivot_node")
            && node.parent.as_deref() != pivot.as_str()
        {
            return Err("Boat control pivot differs from hierarchy".into());
        }
        let axis = vector3(&part["axis_local"])?;
        if (axis.iter().map(|x| x * x).sum::<f64>() - 1.0).abs() > 1e-6 {
            return Err("Boat control axis is not unit length".into());
        }
    }
    let materials = library["materials"]
        .as_array()
        .ok_or("Boat control materials absent")?;
    let lights = observations["navigation_lights"]
        .as_array()
        .ok_or("Boat light controls absent")?;
    if lights.len() != 2 {
        return Err("Boat light control count differs".into());
    }
    for (light, input) in lights.iter().zip([false, true]) {
        if light["input"] != input || light["value"] != u8::from(input) {
            return Err("Boat light control values differ".into());
        }
        let material = materials
            .iter()
            .find(|m| m["id"] == light["material"])
            .ok_or("Boat light material absent")?;
        if light["uniform"] != "navOn"
            || material["uniforms"]
                .as_array()
                .is_none_or(|u| !u.iter().any(|u| u["name"] == light["uniform"]))
        {
            return Err("Boat light uniform absent".into());
        }
    }
    Ok(())
}
fn decode_part(
    part: &BoatPart,
    files: &BTreeMap<String, Vec<u8>>,
) -> Result<ResolvedBoatPart, String> {
    if part.attributes.len() != 5 || part.triangles == 0 {
        return Err("Boat part attribute set differs".into());
    }
    let mut attributes = BTreeMap::new();
    let mut vertices = None;
    for (name, width) in [
        ("position", 3),
        ("normal", 3),
        ("uv", 2),
        ("color", 3),
        ("aux", 4),
    ] {
        let reference = part.attributes.get(name).ok_or("Boat attribute absent")?;
        if let Some(n) = vertices {
            if reference.count != n {
                return Err("Boat attribute vertex counts differ".into());
            }
        } else {
            vertices = Some(reference.count);
        }
        let bytes = files
            .get(&reference.file)
            .ok_or("Boat attribute dependency absent")?;
        attributes.insert(name.to_string(), reference.floats(bytes, width)?);
    }
    let indices = part.indices.triangle_indices(
        files.get(&part.indices.file).ok_or("Boat indices absent")?,
        vertices.ok_or("Boat vertex count absent")?,
    )?;
    if indices.len() / 3 != part.triangles {
        return Err("Boat part triangle count differs".into());
    }
    let positions: Vec<_> = attributes["position"]
        .as_chunks::<3>()
        .0
        .iter()
        .map(|v| [v[0], v[1], v[2]])
        .collect();
    part.bounds_local.verify(&positions)?;
    Ok(ResolvedBoatPart {
        id: part.id.clone(),
        node: part.node.clone(),
        material: part.material.clone(),
        attributes,
        indices,
    })
}
fn validate_hierarchy(nodes: &[BoatNode]) -> Result<(), String> {
    let table: BTreeMap<_, _> = nodes.iter().map(|n| (n.id.as_str(), n)).collect();
    if table.len() != nodes.len()
        || nodes.iter().any(|n| n.id.is_empty())
        || nodes.iter().filter(|n| n.parent.is_none()).count() != 1
    {
        return Err("Boat node identities or root count differ".into());
    }
    for node in nodes {
        node.transform.validate()?;
        matrix_matches(node.matrix_local, node.transform.matrix())?;
        let mut ancestors = BTreeSet::from([node.id.as_str()]);
        let mut current = node;
        while let Some(parent) = &current.parent {
            if !ancestors.insert(parent.as_str()) {
                return Err("Boat hierarchy contains a cycle".into());
            }
            current = table
                .get(parent.as_str())
                .ok_or("Boat node parent absent")?;
        }
        let expected = if let Some(parent) = &node.parent {
            multiply(table[parent.as_str()].matrix_world, node.matrix_local)
        } else {
            node.matrix_local
        };
        matrix_matches(node.matrix_world, expected)?;
    }
    Ok(())
}
fn multiply(a: [f64; 16], b: [f64; 16]) -> [f64; 16] {
    std::array::from_fn(|i| {
        let row = i % 4;
        let col = i / 4;
        (0..4).map(|k| a[k * 4 + row] * b[col * 4 + k]).sum()
    })
}
fn matrix_matches(a: [f64; 16], b: [f64; 16]) -> Result<(), String> {
    if a.iter()
        .zip(b)
        .any(|(&x, y)| !x.is_finite() || !y.is_finite() || (x - y).abs() > 1e-8 * (1.0 + y.abs()))
    {
        return Err("Boat node matrix differs from its local hierarchy".into());
    }
    Ok(())
}
fn validate_materials<'a>(
    library: &'a Value,
    files: &BTreeMap<String, Vec<u8>>,
) -> Result<BTreeSet<&'a str>, String> {
    if library["schema"] != "forge.tidewater.boat-materials"
        || library["version"] != 1
        || library["source_commit"] != COMMIT
    {
        return Err("Unsupported boat material library".into());
    }
    let materials = library["materials"]
        .as_array()
        .ok_or("Boat materials absent")?;
    if materials.len() != 7 {
        return Err("Boat material count differs".into());
    }
    let mut ids = BTreeSet::new();
    for m in materials {
        let id = m["id"].as_str().ok_or("Boat material identity absent")?;
        if id.is_empty()
            || !ids.insert(id)
            || m["attributes"] != serde_json::json!({"aux":"vec4f"})
            || m["varyings"] != serde_json::json!({"vAux":"vec4f","vLocal":"vec3f"})
        {
            return Err("Boat material identity or attribute layout differs".into());
        }
        let snippets = m["snippets"]
            .as_object()
            .ok_or("Boat material snippets absent")?;
        if snippets.len() != 4 {
            return Err("Boat material snippet set differs".into());
        }
        for key in ["vertex", "surface", "output", "shadow"] {
            let ref_value = snippets.get(key).ok_or("Boat shader snippet absent")?;
            if !ref_value.is_null() {
                let r: AssetFileReference =
                    serde_json::from_value(ref_value.clone()).map_err(|e| e.to_string())?;
                std::str::from_utf8(&files[&r.file]).map_err(|e| e.to_string())?;
            }
        }
        let modules = m["modules"]
            .as_array()
            .ok_or("Boat shader modules absent")?;
        validate_modules(modules, &mut BTreeSet::new(), files)?;
        let uniforms = m["uniforms"]
            .as_array()
            .ok_or("Boat material uniforms absent")?;
        let mut names = BTreeSet::new();
        for u in uniforms {
            let name = u["name"].as_str().ok_or("Boat uniform name absent")?;
            let ty = u["type"].as_str().ok_or("Boat uniform type absent")?;
            if !names.insert(name) || !matches!(ty, "f32" | "vec2f" | "vec3f" | "vec4f") {
                return Err("Boat uniform identity/type differs".into());
            }
            let count = match ty {
                "f32" => 1,
                "vec2f" => 2,
                "vec3f" => 3,
                _ => 4,
            };
            if count == 1 {
                if u["value"].as_f64().is_none() {
                    return Err("Boat scalar uniform differs".into());
                }
            } else if u["value"]
                .as_array()
                .is_none_or(|a| a.len() != count || a.iter().any(|x| x.as_f64().is_none()))
            {
                return Err("Boat vector uniform differs".into());
            }
        }
    }
    Ok(ids)
}

fn validate_modules(
    modules: &[Value],
    ancestors: &mut BTreeSet<String>,
    files: &BTreeMap<String, Vec<u8>>,
) -> Result<(), String> {
    let mut names = BTreeSet::new();
    for module in modules {
        let fields = module.as_object().ok_or("Boat module object absent")?;
        let name = module["name"].as_str().ok_or("Boat module name absent")?;
        if fields.len() != 3 || name.is_empty() || !names.insert(name) || ancestors.contains(name) {
            return Err("Boat module identity/tree differs".into());
        }
        let code: AssetFileReference =
            serde_json::from_value(module["code"].clone()).map_err(|e| e.to_string())?;
        std::str::from_utf8(files.get(&code.file).ok_or("Boat module code absent")?)
            .map_err(|e| e.to_string())?;
        let dependencies = module["dependencies"]
            .as_array()
            .ok_or("Boat module dependencies absent")?;
        ancestors.insert(name.to_owned());
        validate_modules(dependencies, ancestors, files)?;
        ancestors.remove(name);
    }
    Ok(())
}
fn validate_glb(bytes: &[u8]) -> Result<(), String> {
    if bytes.len() < 28 {
        return Err("Boat inspection GLB is truncated".into());
    }
    let word = |i| u32::from_le_bytes([bytes[i], bytes[i + 1], bytes[i + 2], bytes[i + 3]]);
    let header: &[u8; 20] = bytes[..20]
        .try_into()
        .map_err(|e| format!("Boat GLB header: {e}"))?;
    let length =
        crate::governed_glb_json_length(header, bytes.len() as u64).map_err(|e| e.to_string())?;
    let end = 20usize
        .checked_add(length)
        .ok_or("Boat GLB size overflow")?;
    if end.checked_add(8).is_none_or(|size| size > bytes.len()) {
        return Err("Boat inspection GLB JSON is truncated".into());
    }
    crate::validate_governed_glb_json(&bytes[20..end]).map_err(|e| e.to_string())?;
    if word(end + 4) != 0x004e_4942
        || usize::try_from(word(end)).ok() != Some(bytes.len() - end - 8)
    {
        return Err("Boat inspection GLB binary size differs".into());
    }
    Ok(())
}
