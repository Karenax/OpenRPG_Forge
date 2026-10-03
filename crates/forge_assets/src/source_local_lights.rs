//! Hash-pinned source lighting descriptors, without renderer or filesystem state.
//!
//! Positions remain in their declared owner frame. Acquired world poses are
//! checks on the capture, never a replacement for a placement's live transform.
//! A complete inventory declaration is provenance, not a visual qualification.

use forge_core::{ContentHash, bytes_sha256};
use serde::{Deserialize, Deserializer, Serialize};
use std::collections::{BTreeMap, BTreeSet};
use thiserror::Error;

pub mod ownership;
pub mod package;
pub use package::SourceLocalLightsPackage;

const MAX_BYTES: usize = 8 * 1024 * 1024;
const MAX_RECORDS: usize = 65_536;
const IDENTITY: [f64; 16] = [
    1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0,
];

#[derive(Debug, Error)]
pub enum SourceLocalLightsError {
    #[error("Local-light descriptor size or caller-selected SHA-256 differs")]
    PinMismatch,
    #[error("Local-light package size or caller-selected SHA-256 differs")]
    PackagePinMismatch,
    #[error("Invalid local-light package: {0}")]
    Package(String),
    #[error("Cannot decode local-light descriptor: {0}")]
    Decode(#[from] serde_json::Error),
    #[error("Unsupported local-light descriptor schema or version")]
    Schema,
    #[error("Invalid local-light descriptor: {0}")]
    Invalid(String),
    #[error("Local-light source dependency absent or different: {0}")]
    SourceDependency(String),
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
// These independent provenance facts mirror the acquired source schema.
#[allow(clippy::struct_excessive_bools)]
pub struct SourceLocalLightDocument {
    pub schema: String,
    pub version: u32,
    pub source_commit: String,
    pub capture_mode: String,
    pub gpu_initialized: bool,
    pub browser_required: bool,
    pub registration_scope: String,
    pub full_application_inventory: bool,
    pub missing_owners: Vec<String>,
    pub phase_reacquisition_reproducible: bool,
    pub source_defaults: SourceLocalLightDefaults,
    pub coordinate_system: SourceLightCoordinates,
    pub frames: Vec<SourceLightFrame>,
    pub sets: BTreeMap<String, SourceLightSet>,
    pub source_inputs: Vec<SourceLightInput>,
    pub lights: Vec<SourceLocalLightRecord>,
    pub runtime_selection_executed: bool,
    pub renderer_parity: bool,
    pub studio_qualified: bool,
}

#[derive(Clone, Copy, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceLocalLightDefaults {
    pub enabled: bool,
    pub scale: f64,
    pub flicker: f64,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceLightCoordinates {
    pub units: String,
    pub up: String,
    pub handedness: String,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceLightFrame {
    pub id: String,
    pub source_object: String,
    #[serde(deserialize_with = "nullable")]
    pub parent: Option<String>,
    /// Column-major capture matrix. Only a root may omit it, meaning identity.
    #[serde(skip_serializing_if = "Option::is_none")]
    pub world_from_local: Option<[f64; 16]>,
}

#[derive(Clone, Copy, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceLightSet {
    pub start_order: usize,
    pub count: usize,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceLightInput {
    pub path: String,
    pub sha256: ContentHash,
    pub bytes: usize,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceLocalLightRecord {
    pub id: ContentHash,
    pub registration_order: u64,
    pub owner_frame: String,
    /// Descriptive source provenance. Consumers must not dispatch on this label.
    pub source_kind: String,
    pub source_function: String,
    pub position_local_m: [f64; 3],
    #[serde(deserialize_with = "nullable")]
    pub direction_local: Option<[f64; 3]>,
    pub acquired_position_world_m: [f64; 3],
    #[serde(deserialize_with = "nullable")]
    pub acquired_direction_world: Option<[f64; 3]>,
    pub color_linear_rgb: [f64; 3],
    pub intensity_at_1m: f64,
    pub range_m: f64,
    #[serde(deserialize_with = "nullable")]
    pub cos_inner: Option<f64>,
    #[serde(deserialize_with = "nullable")]
    pub cos_outer: Option<f64>,
    #[serde(deserialize_with = "nullable")]
    pub enabled: Option<bool>,
    #[serde(deserialize_with = "nullable")]
    pub scale: Option<f64>,
    #[serde(deserialize_with = "nullable")]
    pub flicker: Option<f64>,
    /// Required even when flicker is currently zero; no renderer re-randomization.
    pub phase: f64,
    pub phase_source: String,
    pub source_has_pose_update: bool,
}

// Missing nullable capture fields must not silently become absent source values.
fn nullable<'de, D, T>(deserializer: D) -> Result<Option<T>, D::Error>
where
    D: Deserializer<'de>,
    T: Deserialize<'de>,
{
    Option::deserialize(deserializer)
}

/// Validated immutable payload; the expected hash must come from its caller's
/// selected package. This reader does not execute acquisition or infer completeness.
pub struct SourceLocalLights {
    document: SourceLocalLightDocument,
    content_hash: ContentHash,
}

impl SourceLocalLights {
    pub fn load(bytes: &[u8], expected: ContentHash) -> Result<Self, SourceLocalLightsError> {
        if bytes.len() > MAX_BYTES || bytes_sha256(bytes) != expected {
            return Err(SourceLocalLightsError::PinMismatch);
        }
        let document: SourceLocalLightDocument = serde_json::from_slice(bytes)?;
        document.validate()?;
        Ok(Self {
            document,
            content_hash: expected,
        })
    }

    #[must_use]
    pub fn document(&self) -> &SourceLocalLightDocument {
        &self.document
    }

    #[must_use]
    pub const fn content_hash(&self) -> ContentHash {
        self.content_hash
    }

    /// Package adapters use this with already-closed original source bytes.
    /// Checking source hashes does not attest that the declared capture ran.
    pub fn verify_source_inputs<'a>(
        &self,
        mut source: impl FnMut(&str) -> Option<&'a [u8]>,
    ) -> Result<(), SourceLocalLightsError> {
        for input in &self.document.source_inputs {
            let bytes = source(&input.path)
                .ok_or_else(|| SourceLocalLightsError::SourceDependency(input.path.clone()))?;
            if bytes.len() != input.bytes || bytes_sha256(bytes) != input.sha256 {
                return Err(SourceLocalLightsError::SourceDependency(input.path.clone()));
            }
        }
        Ok(())
    }
}

impl SourceLocalLightDocument {
    pub fn validate(&self) -> Result<(), SourceLocalLightsError> {
        if self.schema != "forge.source-local-lights" || self.version != 1 {
            return Err(SourceLocalLightsError::Schema);
        }
        if self.source_commit.len() != 40
            || !self.source_commit.bytes().all(|c| c.is_ascii_hexdigit())
            || !label(&self.capture_mode)
            || !label(&self.registration_scope)
            || self.coordinate_system.units != "metres"
            || self.coordinate_system.up != "+Y"
            || self.coordinate_system.handedness != "right"
            || self.lights.len() > MAX_RECORDS
            || self.frames.is_empty()
            || self.frames.len() > MAX_RECORDS
            || self.source_inputs.is_empty()
        {
            return invalid("capture metadata or coordinate system");
        }
        let mut missing = BTreeSet::new();
        if self
            .missing_owners
            .iter()
            .any(|s| !label(s) || !missing.insert(s))
            || self.full_application_inventory != self.missing_owners.is_empty()
        {
            return invalid("inventory declaration contradicts missing owners");
        }
        nonnegative(self.source_defaults.scale, "default scale")?;
        finite(self.source_defaults.flicker, "default flicker")?;
        let mut inputs = BTreeSet::new();
        for input in &self.source_inputs {
            if !source_path(&input.path) || input.bytes == 0 || !inputs.insert(&input.path) {
                return invalid("source dependency path, size or duplicate");
            }
        }
        let frames = self.validate_frames()?;
        self.validate_sets()?;
        let mut ids = BTreeSet::new();
        for (order, light) in self.lights.iter().enumerate() {
            if usize::try_from(light.registration_order).ok() != Some(order)
                || !ids.insert(light.id)
            {
                return invalid("source light identity or registration order");
            }
            let frame = frames.get(light.owner_frame.as_str()).ok_or_else(|| {
                SourceLocalLightsError::Invalid(format!(
                    "unknown owner frame: {}",
                    light.owner_frame
                ))
            })?;
            light.validate(frame.world_from_local.unwrap_or(IDENTITY))?;
        }
        Ok(())
    }

    fn validate_frames(&self) -> Result<BTreeMap<&str, &SourceLightFrame>, SourceLocalLightsError> {
        let mut frames = BTreeMap::new();
        for frame in &self.frames {
            if !label(&frame.id)
                || !label(&frame.source_object)
                || frames.insert(frame.id.as_str(), frame).is_some()
                || (frame.parent.is_some() && frame.world_from_local.is_none())
            {
                return invalid("frame identity or capture matrix");
            }
            affine(frame.world_from_local.unwrap_or(IDENTITY))?;
        }
        if self
            .frames
            .iter()
            .filter(|frame| frame.parent.is_none())
            .count()
            != 1
        {
            return invalid("capture must have one root coordinate frame");
        }
        for frame in &self.frames {
            let mut seen = BTreeSet::from([frame.id.as_str()]);
            let mut current = frame;
            while let Some(parent) = current.parent.as_deref() {
                if !seen.insert(parent) {
                    return invalid("cycle in light owner frames");
                }
                current = frames.get(parent).copied().ok_or_else(|| {
                    SourceLocalLightsError::Invalid(format!("missing frame parent: {parent}"))
                })?;
            }
        }
        Ok(frames)
    }

    fn validate_sets(&self) -> Result<(), SourceLocalLightsError> {
        let mut covered = vec![false; self.lights.len()];
        for (name, set) in &self.sets {
            if !label(name) {
                return invalid("registration set identity");
            }
            let end = set.start_order.checked_add(set.count).ok_or_else(|| {
                SourceLocalLightsError::Invalid("registration set overflow".into())
            })?;
            let range = covered.get_mut(set.start_order..end).ok_or_else(|| {
                SourceLocalLightsError::Invalid("registration set outside inventory".into())
            })?;
            if range.iter().any(|&value| value) {
                return invalid("overlapping light registration sets");
            }
            range.fill(true);
        }
        if covered.iter().any(|&value| !value) {
            return invalid("light absent from registration sets");
        }
        Ok(())
    }
}

impl SourceLocalLightRecord {
    fn validate(&self, matrix: [f64; 16]) -> Result<(), SourceLocalLightsError> {
        if !label(&self.source_kind) || !label(&self.source_function) || !label(&self.phase_source)
        {
            return invalid("light provenance");
        }
        for value in self
            .position_local_m
            .iter()
            .chain(self.acquired_position_world_m.iter())
        {
            finite(*value, "light position")?;
        }
        for value in self.color_linear_rgb {
            nonnegative(value, "linear light color")?;
        }
        nonnegative(self.intensity_at_1m, "source light intensity")?;
        finite(self.range_m, "light range")?;
        // The GPU packet stores range squared, not just range.
        if self.range_m <= 0.0 || self.range_m * self.range_m > f64::from(f32::MAX) {
            return invalid("nonpositive or unpackable light range");
        }
        finite(self.phase, "acquired light phase")?;
        if let Some(scale) = self.scale {
            nonnegative(scale, "light scale")?;
        }
        if let Some(flicker) = self.flicker {
            finite(flicker, "light flicker")?;
        }
        for cosine in [self.cos_inner, self.cos_outer].into_iter().flatten() {
            if !cosine.is_finite() || !(-1.0..=1.0).contains(&cosine) {
                return invalid("light cone cosine");
            }
        }
        if !near3(
            transform(matrix, self.position_local_m, true),
            self.acquired_position_world_m,
        ) {
            return invalid("acquired world position differs from owner-local position");
        }
        match (self.direction_local, self.acquired_direction_world) {
            (None, None) => {}
            (Some(local), Some(world)) => {
                unit(local)?;
                unit(world)?;
                let transformed = transform(matrix, local, false);
                let length = transformed.iter().map(|v| v * v).sum::<f64>().sqrt();
                if !length.is_finite()
                    || length <= 0.0
                    || !near3(transformed.map(|v| v / length), world)
                {
                    return invalid("acquired world direction differs from owner-local axis");
                }
            }
            _ => return invalid("local/world direction presence differs"),
        }
        Ok(())
    }
}

fn invalid<T>(reason: &str) -> Result<T, SourceLocalLightsError> {
    Err(SourceLocalLightsError::Invalid(reason.into()))
}

fn label(value: &str) -> bool {
    !value.is_empty() && value.len() <= 512 && !value.chars().any(char::is_control)
}

fn source_path(value: &str) -> bool {
    label(value)
        && !value.contains(['\\', ':'])
        && value
            .split('/')
            .all(|part| !part.is_empty() && !matches!(part, "." | ".."))
}

fn finite(value: f64, name: &str) -> Result<(), SourceLocalLightsError> {
    if !value.is_finite() || value.abs() > f64::from(f32::MAX) {
        return invalid(name);
    }
    Ok(())
}

fn nonnegative(value: f64, name: &str) -> Result<(), SourceLocalLightsError> {
    finite(value, name)?;
    if value < 0.0 {
        return invalid(name);
    }
    Ok(())
}

fn unit(vector: [f64; 3]) -> Result<(), SourceLocalLightsError> {
    if vector.iter().any(|v| !v.is_finite())
        || (vector.iter().map(|v| v * v).sum::<f64>() - 1.0).abs() > 1e-6
    {
        return invalid("nonunit light axis");
    }
    Ok(())
}

fn affine(matrix: [f64; 16]) -> Result<(), SourceLocalLightsError> {
    for value in matrix {
        finite(value, "frame matrix")?;
    }
    let det = matrix[0] * (matrix[5] * matrix[10] - matrix[9] * matrix[6])
        - matrix[4] * (matrix[1] * matrix[10] - matrix[9] * matrix[2])
        + matrix[8] * (matrix[1] * matrix[6] - matrix[5] * matrix[2]);
    if matrix[3].abs() > 1e-12
        || matrix[7].abs() > 1e-12
        || matrix[11].abs() > 1e-12
        || (matrix[15] - 1.0).abs() > 1e-12
        || !det.is_finite()
        || det.abs() <= f64::MIN_POSITIVE
    {
        return invalid("singular or nonaffine owner frame");
    }
    Ok(())
}

fn transform(matrix: [f64; 16], point: [f64; 3], position: bool) -> [f64; 3] {
    std::array::from_fn(|row| {
        matrix[row] * point[0]
            + matrix[4 + row] * point[1]
            + matrix[8 + row] * point[2]
            + if position { matrix[12 + row] } else { 0.0 }
    })
}

fn near3(left: [f64; 3], right: [f64; 3]) -> bool {
    left.into_iter().zip(right).all(|(a, b)| {
        a.is_finite() && b.is_finite() && (a - b).abs() <= 1e-9 * a.abs().max(b.abs()).max(1.0)
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::{Value, json};

    pub(super) fn fixture() -> Value {
        let mut frame = IDENTITY;
        frame[12] = 12.0;
        json!({
            "schema":"forge.source-local-lights", "version":1,
            "source_commit":"1438b1abfcaee3267092b75573014f4d9b4a983c",
            "capture_mode":"source-export-headless-gpu", "gpu_initialized":true,
            "browser_required":false, "registration_scope":"source-prefix",
            "full_application_inventory":false,"missing_owners":["unacquired-owner"],
            "phase_reacquisition_reproducible":false,
            "source_defaults":{"enabled":true,"scale":1.0,"flicker":0.0},
            "coordinate_system":{"units":"metres","up":"+Y","handedness":"right"},
            "frames":[{"id":"world","source_object":"world","parent":null},
                {"id":"moving","source_object":"group","parent":"world","world_from_local":frame}],
            "sets":{"emitters":{"start_order":0,"count":1}},
            "source_inputs":[{"path":"src/lighting.js","sha256":bytes_sha256(b"source"),"bytes":6}],
            "lights":[{
                "id":bytes_sha256(b"stable-id"),"registration_order":0,"owner_frame":"moving",
                "source_kind":"descriptive-only","source_function":"original-function",
                "position_local_m":[1.0,2.0,3.0],"direction_local":[0.0,-1.0,0.0],
                "acquired_position_world_m":[13.0,2.0,3.0],"acquired_direction_world":[0.0,-1.0,0.0],
                "color_linear_rgb":[1.0,0.7,0.4],"intensity_at_1m":3.2,"range_m":14.0,
                "cos_inner":0.8,"cos_outer":0.45,"enabled":null,"scale":0.0,"flicker":null,
                "phase":28.375,"phase_source":"upstream_LocalLights.add","source_has_pose_update":true
            }],
            "runtime_selection_executed":false,"renderer_parity":false,"studio_qualified":false
        })
    }

    fn load(value: &Value) -> Result<SourceLocalLights, SourceLocalLightsError> {
        let bytes = serde_json::to_vec(value).unwrap();
        SourceLocalLights::load(&bytes, bytes_sha256(&bytes))
    }

    #[test]
    fn source_local_lights_preserve_pose_phase_and_explicit_absence() {
        let loaded = load(&fixture()).unwrap();
        let document = loaded.document();
        let light = &document.lights[0];
        assert_eq!(light.position_local_m, [1.0, 2.0, 3.0]);
        assert_eq!(light.acquired_position_world_m, [13.0, 2.0, 3.0]);
        assert_eq!(light.phase, 28.375);
        assert_eq!(light.scale, Some(0.0));
        assert_eq!(light.enabled, None);
        assert_eq!(light.flicker, None);
        assert!(!document.full_application_inventory);
        assert_eq!(serde_json::to_value(document).unwrap(), fixture());
        loaded
            .verify_source_inputs(|path| {
                (path == "src/lighting.js").then_some(b"source".as_slice())
            })
            .unwrap();
        assert!(
            loaded
                .verify_source_inputs(|_| Some(b"edited".as_slice()))
                .is_err()
        );
    }

    #[test]
    fn source_local_lights_keep_original_f64_phase_bits_from_json() {
        // The source clock oracle and acquired phases use JavaScript f64 values.
        // The default approximate JSON float parser changes this number by a ULP.
        let expected = 0.915_999_999_999_999_9_f64;
        let mut value = fixture();
        value["lights"][0]["phase"] = json!(expected);
        let loaded = load(&value).unwrap();
        assert_eq!(
            loaded.document().lights[0].phase.to_bits(),
            expected.to_bits()
        );
    }

    #[test]
    fn source_local_lights_verify_pin_before_decode_and_require_capture_fields() {
        assert!(matches!(
            SourceLocalLights::load(b"invalid", bytes_sha256(b"different")),
            Err(SourceLocalLightsError::PinMismatch)
        ));
        for field in [
            "phase",
            "enabled",
            "scale",
            "flicker",
            "direction_local",
            "cos_outer",
        ] {
            let mut value = fixture();
            value["lights"][0].as_object_mut().unwrap().remove(field);
            assert!(
                matches!(load(&value), Err(SourceLocalLightsError::Decode(_))),
                "{field}"
            );
        }
        let mut value = fixture();
        value["lights"][0]["unknown"] = json!(true);
        assert!(matches!(
            load(&value),
            Err(SourceLocalLightsError::Decode(_))
        ));
    }

    #[test]
    fn source_local_lights_refuse_missing_cyclic_and_inconsistent_owner_frames() {
        for change in [0, 1, 2, 3, 4, 5] {
            let mut value = fixture();
            match change {
                0 => value["lights"][0]["owner_frame"] = json!("missing"),
                1 => value["frames"][1]["parent"] = json!("moving"),
                2 => value["frames"][1]["parent"] = json!("missing"),
                3 => value["frames"][1]["world_from_local"][0] = json!(0.0),
                4 => value["lights"][0]["acquired_position_world_m"][0] = json!(14.0),
                _ => value["lights"][0]["acquired_direction_world"] = json!([1.0, 0.0, 0.0]),
            }
            assert!(load(&value).is_err(), "frame case {change}");
        }
    }

    #[test]
    fn source_local_lights_refuse_bad_inventory_and_photometric_values() {
        for change in 0..10 {
            let mut value = fixture();
            match change {
                0 => value["lights"][0]["registration_order"] = json!(1),
                1 => {
                    let duplicate = value["lights"][0].clone();
                    value["lights"].as_array_mut().unwrap().push(duplicate);
                    value["sets"]["emitters"]["count"] = json!(2);
                }
                2 => value["sets"]["emitters"]["count"] = json!(0),
                3 => value["sets"]["other"] = json!({"start_order":0,"count":1}),
                4 => value["lights"][0]["range_m"] = json!(0.0),
                5 => value["lights"][0]["intensity_at_1m"] = json!(-1.0),
                6 => value["lights"][0]["cos_inner"] = json!(1.1),
                7 => value["lights"][0]["direction_local"] = json!([0.0, -2.0, 0.0]),
                8 => value["source_inputs"][0]["path"] = json!("../source.js"),
                _ => value["full_application_inventory"] = json!(true),
            }
            assert!(load(&value).is_err(), "inventory case {change}");
        }
    }
}
