//! Saved attachment to an existing immutable source-light acquisition.
//! These are authoring inputs, never live entities or renderer resource IDs.
use forge_core::{ContentHash, Transform};
use serde::{Deserialize, Serialize};

// Match the source descriptor's inventory budget, independently of its eight GPU slots.
const MAX_RECORDS: usize = 65_536;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct LocalLightAnchor {
    pub frame_id: String,
    /// Relative to the descriptor's parent frame, or to the placement root for
    /// a descriptor root. Acquisition world matrices are never live placements.
    pub transform: Transform,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct LocalLightPackageReference {
    pub schema_version: u32,
    pub carrier_sha256: ContentHash,
    /// The caller-selected manifest hash of `SourceLocalLightsPackage`.
    pub manifest_sha256: ContentHash,
    pub descriptor_sha256: ContentHash,
    /// Sorted source IDs. Original registration orders and phases stay in the package.
    pub light_ids: Vec<ContentHash>,
    /// Sorted frame IDs, including the ancestors required by the selected emitters.
    pub anchors: Vec<LocalLightAnchor>,
}

impl LocalLightPackageReference {
    /// Domain validation is deliberately independent of a filesystem or asset
    /// loader. The package consumer additionally checks the pins, selection and
    /// frame closure against the actual acquired descriptor.
    pub fn validate(&self, carrier: ContentHash) -> Result<(), String> {
        if self.schema_version != 1 || self.carrier_sha256 != carrier {
            return Err("Local-light reference must pin the placement carrier".into());
        }
        if self.light_ids.is_empty()
            || self.light_ids.len() > MAX_RECORDS
            || self.light_ids.windows(2).any(|ids| ids[0] >= ids[1])
            || self.anchors.is_empty()
            || self.anchors.len() > MAX_RECORDS
            || self
                .anchors
                .windows(2)
                .any(|a| a[0].frame_id >= a[1].frame_id)
        {
            return Err(
                "Local-light reference requires bounded, unique sorted sources and anchors".into(),
            );
        }
        for anchor in &self.anchors {
            if anchor.frame_id.is_empty()
                || anchor.frame_id.len() > 256
                || anchor.frame_id.chars().any(char::is_control)
            {
                return Err("Local-light anchor identity is empty or invalid".into());
            }
            anchor
                .transform
                .validate()
                .map_err(|error| error.to_string())?;
        }
        Ok(())
    }
}
