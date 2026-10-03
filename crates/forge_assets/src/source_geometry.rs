//! Renderer-neutral geometry references and local frames for governed assets.
//! Water exclusions preserve the authored indices and fingerprint and verify
//! exact welded topology. The renderer must still validate its admission policy.
use forge_core::{ContentHash, bytes_sha256};
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssetFileReference {
    pub file: String,
    pub sha256: ContentHash,
    pub bytes: usize,
}

impl AssetFileReference {
    pub fn verify<'a>(&self, bytes: &'a [u8]) -> Result<&'a [u8], String> {
        if bytes.len() != self.bytes || bytes_sha256(bytes) != self.sha256 {
            return Err(format!("Asset dependency pin differs: {}", self.file));
        }
        Ok(bytes)
    }
}

#[derive(Clone, Debug, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssetBufferReference {
    pub file: String,
    pub sha256: ContentHash,
    pub bytes: usize,
    pub component_type: String,
    pub count: usize,
    pub item_size: Option<usize>,
}

impl AssetBufferReference {
    #[must_use]
    pub fn reference(&self) -> AssetFileReference {
        AssetFileReference {
            file: self.file.clone(),
            sha256: self.sha256,
            bytes: self.bytes,
        }
    }

    pub fn floats(&self, bytes: &[u8], width: usize) -> Result<Vec<f32>, String> {
        self.reference().verify(bytes)?;
        let length = self.count.checked_mul(width).and_then(|n| n.checked_mul(4));
        if self.component_type != "f32"
            || self.item_size != Some(width)
            || width == 0
            || self.count == 0
            || length != Some(bytes.len())
        {
            return Err("Asset float attribute layout differs".into());
        }
        let values: Vec<_> = bytes
            .as_chunks::<4>()
            .0
            .iter()
            .map(|b| f32::from_le_bytes([b[0], b[1], b[2], b[3]]))
            .collect();
        if values.iter().any(|value| !value.is_finite()) {
            return Err("Asset attribute contains a nonfinite value".into());
        }
        Ok(values)
    }

    pub fn triangle_indices(&self, bytes: &[u8], vertices: usize) -> Result<Vec<u32>, String> {
        self.reference().verify(bytes)?;
        let width = match self.component_type.as_str() {
            "u16" => 2,
            "u32" => 4,
            _ => return Err("Unsupported asset index encoding".into()),
        };
        if self.item_size.is_some()
            || self.count == 0
            || !self.count.is_multiple_of(3)
            || self.count.checked_mul(width) != Some(bytes.len())
        {
            return Err("Asset triangle index layout differs".into());
        }
        let indices: Vec<_> = bytes
            .chunks_exact(width)
            .map(|b| {
                if width == 2 {
                    u32::from(u16::from_le_bytes([b[0], b[1]]))
                } else {
                    u32::from_le_bytes([b[0], b[1], b[2], b[3]])
                }
            })
            .collect();
        if indices
            .iter()
            .any(|&index| usize::try_from(index).map_or(true, |i| i >= vertices))
        {
            return Err("Asset index references a missing vertex".into());
        }
        Ok(indices)
    }
}

#[derive(Clone, Debug, Deserialize, PartialEq)]
#[serde(deny_unknown_fields)]
pub struct AssetLocalFrame {
    pub translation_m: [f64; 3],
    pub rotation_xyzw: [f64; 4],
    pub scale: [f64; 3],
}

impl AssetLocalFrame {
    // Validate the eventual renderer conversion, including underflow of scale.
    #[allow(clippy::cast_possible_truncation)]
    pub fn validate(&self) -> Result<(), String> {
        let values = self
            .translation_m
            .iter()
            .chain(self.rotation_xyzw.iter())
            .chain(self.scale.iter());
        if values
            .into_iter()
            .any(|&x| !x.is_finite() || !(x as f32).is_finite())
            || self
                .scale
                .iter()
                .any(|&x| (x as f32).abs() <= f32::MIN_POSITIVE)
            || (self.rotation_xyzw.iter().map(|q| q * q).sum::<f64>() - 1.0).abs() > 1e-6
        {
            return Err("Asset local frame is singular, nonfinite or nonunit".into());
        }
        Ok(())
    }

    /// Column-major matrix in the original metre/Y-up frame.
    #[must_use]
    pub fn matrix(&self) -> [f64; 16] {
        let [x, y, z, w] = self.rotation_xyzw;
        let [sx, sy, sz] = self.scale;
        let [px, py, pz] = self.translation_m;
        [
            (1.0 - 2.0 * (y * y + z * z)) * sx,
            2.0 * (x * y + z * w) * sx,
            2.0 * (x * z - y * w) * sx,
            0.0,
            2.0 * (x * y - z * w) * sy,
            (1.0 - 2.0 * (x * x + z * z)) * sy,
            2.0 * (y * z + x * w) * sy,
            0.0,
            2.0 * (x * z + y * w) * sz,
            2.0 * (y * z - x * w) * sz,
            (1.0 - 2.0 * (x * x + y * y)) * sz,
            0.0,
            px,
            py,
            pz,
            1.0,
        ]
    }
}

#[derive(Clone, Debug, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct AssetBounds {
    pub min: [f64; 3],
    pub max: [f64; 3],
}

impl AssetBounds {
    // JSON decimal decoding may round by one f64 ULP. This tolerance is far
    // below a single f32 vertex ULP and does not admit a different geometry.
    pub fn verify(&self, positions: &[[f32; 3]]) -> Result<(), String> {
        if positions.is_empty() || positions.iter().flatten().any(|x| !x.is_finite()) {
            return Err("Asset bounds require finite nonempty positions".into());
        }
        let mut min = [f32::INFINITY; 3];
        let mut max = [f32::NEG_INFINITY; 3];
        for position in positions {
            for i in 0..3 {
                min[i] = min[i].min(position[i]);
                max[i] = max[i].max(position[i]);
            }
        }
        let differs = |declared: f64, actual: f32| {
            let actual = f64::from(actual);
            !declared.is_finite() || (declared - actual).abs() > f64::EPSILON * actual.abs()
        };
        if (0..3).any(|i| differs(self.min[i], min[i]) || differs(self.max[i], max[i])) {
            return Err(format!(
                "Authored asset bounds differ from its positions: declared {:?}/{:?}, decoded {:?}/{:?}",
                self.min, self.max, min, max
            ));
        }
        Ok(())
    }
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq)]
#[serde(deny_unknown_fields)]
pub struct SourceProxyTopology {
    pub kind: String,
    pub boundary_edges: u32,
    pub welded_vertices: usize,
    pub edges: usize,
    pub nonmanifold_edges: usize,
    pub inconsistent_edges: usize,
    pub degenerate_triangles: usize,
    pub repairs_applied: bool,
}

#[derive(Clone, Debug, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct WaterExclusionDescriptor {
    pub id: String,
    pub role: String,
    pub parent: String,
    pub parent_reference_kind: String,
    pub transform: AssetLocalFrame,
    pub positions: AssetBufferReference,
    pub indices: AssetBufferReference,
    pub geometry_fingerprint: ContentHash,
    pub topology: SourceProxyTopology,
    pub bounds_local: AssetBounds,
    pub source_method: String,
}

pub struct ResolvedWaterExclusion {
    pub id: String,
    pub parent_node: String,
    pub local_frame: AssetLocalFrame,
    pub positions: Vec<[f32; 3]>,
    pub indices: Vec<u32>,
    pub fingerprint: ContentHash,
    pub topology: SourceProxyTopology,
    /// Some identifies a hash-pinned original proxy; zero/None requires closed
    /// topology. This does not imply completeness of a scene's hull registry.
    pub source_boundary_edges: Option<u32>,
}

impl WaterExclusionDescriptor {
    pub fn resolve<'a>(
        &self,
        mut file: impl FnMut(&str) -> Option<&'a [u8]>,
    ) -> Result<ResolvedWaterExclusion, String> {
        self.transform.validate()?;
        if self.id.is_empty()
            || self.parent.is_empty()
            || self.role != "water_exclusion"
            || self.parent_reference_kind != "descriptor_node_id"
            || self.topology.repairs_applied
            || self.topology.nonmanifold_edges != 0
            || self.topology.inconsistent_edges != 0
            || self.topology.degenerate_triangles != 0
        {
            return Err("Unsupported water exclusion descriptor".into());
        }
        let boundary = match self.topology.kind.as_str() {
            "verified_source_proxy" => Some(self.topology.boundary_edges),
            "closed" if self.topology.boundary_edges == 0 => None,
            _ => return Err("Unsupported water exclusion topology policy".into()),
        };
        let floats = self.positions.floats(
            file(&self.positions.file).ok_or("Water exclusion positions absent")?,
            3,
        )?;
        let positions: Vec<[f32; 3]> = floats.as_chunks::<3>().0.to_vec();
        let indices = self.indices.triangle_indices(
            file(&self.indices.file).ok_or("Water exclusion indices absent")?,
            positions.len(),
        )?;
        self.bounds_local.verify(&positions)?;
        if source_geometry_hash(&positions, &indices) != self.geometry_fingerprint {
            return Err("Water exclusion fingerprint differs from decoded geometry".into());
        }
        self.topology.verify(&positions, &indices)?;
        Ok(ResolvedWaterExclusion {
            id: self.id.clone(),
            parent_node: self.parent.clone(),
            local_frame: self.transform.clone(),
            positions,
            indices,
            fingerprint: self.geometry_fingerprint,
            topology: self.topology.clone(),
            source_boundary_edges: boundary,
        })
    }
}

impl SourceProxyTopology {
    /// Welds exact f32 positions (normalizing signed zero), without repair or
    /// tolerance. This matches the preserved source proxy's topology receipt.
    fn verify(&self, positions: &[[f32; 3]], indices: &[u32]) -> Result<(), String> {
        let mut unique = BTreeMap::new();
        let mut welded = Vec::with_capacity(positions.len());
        for position in positions {
            let key = position.map(|x| if x == 0.0 { 0 } else { x.to_bits() });
            let next = unique.len();
            welded.push(*unique.entry(key).or_insert(next));
        }
        let mut edges: BTreeMap<(usize, usize), (usize, i64)> = BTreeMap::new();
        for triangle in indices.as_chunks::<3>().0 {
            let points: Vec<_> = triangle
                .iter()
                .map(|&i| {
                    positions[usize::try_from(i).expect("decoded index fits usize")].map(f64::from)
                })
                .collect();
            let ab: [f64; 3] = std::array::from_fn(|i| points[1][i] - points[0][i]);
            let ac: [f64; 3] = std::array::from_fn(|i| points[2][i] - points[0][i]);
            let cross = [
                ab[1] * ac[2] - ab[2] * ac[1],
                ab[2] * ac[0] - ab[0] * ac[2],
                ab[0] * ac[1] - ab[1] * ac[0],
            ];
            if cross.iter().all(|&x| x == 0.0) {
                return Err("Water exclusion triangle is degenerate".into());
            }
            for (a, b) in [
                (triangle[0], triangle[1]),
                (triangle[1], triangle[2]),
                (triangle[2], triangle[0]),
            ] {
                let a = welded[usize::try_from(a).expect("decoded index fits usize")];
                let b = welded[usize::try_from(b).expect("decoded index fits usize")];
                let entry = edges.entry((a.min(b), a.max(b))).or_default();
                entry.0 += 1;
                entry.1 += if a < b { 1 } else { -1 };
            }
        }
        let boundary = edges.values().filter(|&&(count, _)| count == 1).count();
        if unique.len() != self.welded_vertices
            || edges.len() != self.edges
            || usize::try_from(self.boundary_edges).ok() != Some(boundary)
            || edges
                .values()
                .any(|&(count, direction)| count > 2 || (count == 2 && direction != 0))
        {
            return Err("Water exclusion topology differs from its receipt".into());
        }
        Ok(())
    }
}

/// Exact f32 LE positions and widened u32 LE indices with independent u64
/// length prefixes. This is the Engine Geometry fingerprint representation.
#[must_use]
pub fn source_geometry_hash(positions: &[[f32; 3]], indices: &[u32]) -> ContentHash {
    let mut bytes = Vec::with_capacity(16 + positions.len() * 12 + indices.len() * 4);
    bytes.extend_from_slice(&(positions.len() as u64).to_le_bytes());
    bytes.extend_from_slice(&(indices.len() as u64).to_le_bytes());
    bytes.extend(positions.iter().flatten().flat_map(|x| x.to_le_bytes()));
    bytes.extend(indices.iter().flat_map(|x| x.to_le_bytes()));
    bytes_sha256(&bytes)
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn bounds_preserve_f32_geometry_across_decimal_json() {
        let mut bounds: AssetBounds = serde_json::from_str(
            r#"{"min":[-1.4498131275177002,0,0],"max":[1.4498131275177002,0,4.300000190734863]}"#,
        )
        .unwrap();
        let positions = [[-1.449_813_1, 0.0, 0.0], [1.449_813_1, 0.0, 4.3]];
        assert!(bounds.verify(&positions).is_ok());
        bounds.max[2] += 1e-7;
        assert!(bounds.verify(&positions).is_err());
    }
    #[test]
    fn frame_rejects_unusable_transforms() {
        let mut frame = AssetLocalFrame {
            translation_m: [0.0; 3],
            rotation_xyzw: [0.0, 0.0, 0.0, 1.0],
            scale: [1.0; 3],
        };
        assert!(frame.validate().is_ok());
        frame.scale[1] = 0.0;
        assert!(frame.validate().is_err());
        frame.scale[1] = 1.0;
        frame.rotation_xyzw[3] = 2.0;
        assert!(frame.validate().is_err());
        frame.rotation_xyzw[3] = 1.0;
        frame.translation_m[0] = f64::MAX;
        assert!(frame.validate().is_err());
    }
    #[test]
    fn index_decode_checks_encoding_and_vertex_limit() {
        let bytes = [0, 0, 1, 0, 2, 0];
        let mut reference = AssetBufferReference {
            file: "indices.bin".into(),
            sha256: bytes_sha256(&bytes),
            bytes: 6,
            component_type: "u16".into(),
            count: 3,
            item_size: None,
        };
        assert_eq!(
            reference.triangle_indices(&bytes, 3).unwrap(),
            vec![0, 1, 2]
        );
        assert!(reference.triangle_indices(&bytes, 2).is_err());
        reference.component_type = "u32".into();
        assert!(reference.triangle_indices(&bytes, 3).is_err());
    }
}
