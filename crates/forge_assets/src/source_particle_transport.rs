//! Governed source-particle resources, independent of filesystem/renderer.
//! Integrity and structural admission are not native rendering qualification.
use crate::source_particle_emission::SourceSingleBurstLoopParameters;
use forge_core::{ContentHash, bytes_sha256};
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceParticleBlob {
    pub path: String,
    pub sha256: ContentHash,
    pub byte_length: usize,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceParticleProgram {
    pub source: SourceParticleBlob,
    pub entry_point: String,
}

/// Compiler-declared Float32 control lanes in a fixed source uniform bank.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceParticleControlLayout {
    pub delta_offset: usize,
    pub mode_offset: usize,
    pub stamp_offset: usize,
    pub count_offset: usize,
    pub reset_mode: f32,
    pub allocate_mode: f32,
    pub update_mode: f32,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceParticleVertexTransport {
    pub program: SourceParticleProgram,
    pub bank: SourceParticleBlob,
    pub native_vertices: SourceParticleBlob,
    pub vertex_count: usize,
    pub input_stride_bytes: usize,
    pub output_stride_bytes: usize,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub frame_layout: Option<SourceParticleFrameLayout>,
}

/// Compiler-proven native bank offsets, independent of renderer math types.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceParticleFrameLayout {
    pub object_to_world_offset: usize,
    pub world_to_object_offset: usize,
    pub relative_world_to_clip_offset: usize,
    pub camera_position_offset: usize,
    /// Source proof for this layout, separate from the captured bank content.
    pub evidence_sha256: ContentHash,
}
impl SourceParticleFrameLayout {
    pub fn validate(&self) -> Result<(), String> {
        let mut lanes = std::collections::BTreeSet::new();
        for (offset, length) in [
            (self.object_to_world_offset, 64),
            (self.world_to_object_offset, 64),
            (self.relative_world_to_clip_offset, 64),
            (self.camera_position_offset, 16),
        ] {
            let end = offset
                .checked_add(length)
                .ok_or("Projection frame layout overflow")?;
            if !offset.is_multiple_of(16) || end > 1024 {
                return Err("Projection frame layout exceeds bank or alignment".into());
            }
            for lane in (offset..end).step_by(4) {
                if !lanes.insert(lane) {
                    return Err("Projection frame layout overlaps".into());
                }
            }
        }
        Ok(())
    }
}

/// Nodes/primitive/material are original carrier indices, not Bevy identities.
#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceParticleOwnerTransport {
    pub source_component_guid: String,
    pub source_component_file_id: String,
    pub source_node: usize,
    pub source_primitive: usize,
    pub source_material: usize,
    pub compute: SourceParticleProgram,
    pub curve_atlas: SourceParticleBlob,
    pub curve_width: u32,
    pub curve_height: u32,
    pub curve_bank: SourceParticleBlob,
    pub initial_state: SourceParticleBlob,
    pub state_prefix_bytes: usize,
    pub workgroups: u32,
    pub control: SourceParticleControlLayout,
    pub spawner: SourceSingleBurstLoopParameters,
    pub projection: SourceParticleVertexTransport,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceParticleTransport {
    pub schema_version: u32,
    pub carrier_sha256: ContentHash,
    pub source_archive_sha256: ContentHash,
    pub source_graph_sha256: ContentHash,
    pub licence_ref: String,
    pub owners: Vec<SourceParticleOwnerTransport>,
}

pub struct ResolvedSourceParticleTransport {
    pub contract: SourceParticleTransport,
    pub resources: BTreeMap<String, Vec<u8>>,
}

fn relative(path: &str) -> bool {
    !path.is_empty()
        && !path.starts_with('/')
        && !path.contains(['\\', ':'])
        && path
            .split('/')
            .all(|part| !part.is_empty() && part != "." && part != "..")
}

fn program(program: &SourceParticleProgram) -> Result<(), String> {
    let entry = program.entry_point.as_bytes();
    if entry.is_empty()
        || entry[0].is_ascii_digit()
        || !entry
            .iter()
            .all(|b| b.is_ascii_alphanumeric() || *b == b'_')
    {
        return Err("Source particle entry point invalid".into());
    }
    Ok(())
}

impl SourceParticleOwnerTransport {
    pub fn validate(&self) -> Result<(), String> {
        if let Some(layout) = &self.projection.frame_layout {
            layout.validate()?;
        }
        self.spawner.validate().map_err(str::to_owned)?;
        program(&self.compute)?;
        program(&self.projection.program)?;
        let offsets = [
            self.control.delta_offset,
            self.control.mode_offset,
            self.control.stamp_offset,
            self.control.count_offset,
        ];
        let mut unique = std::collections::BTreeSet::new();
        if self.source_component_guid.len() != 32
            || !self
                .source_component_guid
                .bytes()
                .all(|b| b.is_ascii_digit() || (b'a'..=b'f').contains(&b))
            || self.source_component_file_id.is_empty()
            || !(1..=65535).contains(&self.workgroups)
            || self.curve_bank.byte_length != 1024
            || self.projection.bank.byte_length != 1024
            || self.state_prefix_bytes == 0
            || !self.state_prefix_bytes.is_multiple_of(16)
            || self.initial_state.byte_length != self.state_prefix_bytes
            || self.curve_width == 0
            || self.curve_height == 0
            || u64::from(self.curve_width)
                .checked_mul(u64::from(self.curve_height))
                .and_then(|n| n.checked_mul(8))
                != Some(
                    u64::try_from(self.curve_atlas.byte_length)
                        .map_err(|_| "Atlas size overflow")?,
                )
            || offsets.iter().any(|offset| {
                *offset > 1020 || !offset.is_multiple_of(4) || !unique.insert(*offset)
            })
            || ![
                self.control.reset_mode,
                self.control.allocate_mode,
                self.control.update_mode,
            ]
            .iter()
            .all(|v| v.is_finite())
            || modes_overlap(&self.control)
            || self.projection.vertex_count == 0
            || self.projection.input_stride_bytes == 0
            || self.projection.output_stride_bytes == 0
            || !self.projection.input_stride_bytes.is_multiple_of(4)
            || !self.projection.output_stride_bytes.is_multiple_of(16)
            || self
                .projection
                .vertex_count
                .checked_mul(self.projection.input_stride_bytes)
                != Some(self.projection.native_vertices.byte_length)
            || self
                .projection
                .vertex_count
                .checked_mul(self.projection.output_stride_bytes)
                .and_then(|n| n.checked_add(self.state_prefix_bytes))
                .is_none_or(|n| n > 64 * 1024 * 1024)
        {
            return Err(
                "Source particle owner identity, bank, atlas or projection ABI invalid".into(),
            );
        }
        Ok(())
    }

    #[must_use]
    pub fn blobs(&self) -> [&SourceParticleBlob; 7] {
        [
            &self.compute.source,
            &self.curve_atlas,
            &self.curve_bank,
            &self.initial_state,
            &self.projection.program.source,
            &self.projection.bank,
            &self.projection.native_vertices,
        ]
    }

    /// Preserve every other source byte; stamp encoding is exact Float32 only.
    pub fn control_bank(
        &self,
        base: &[u8],
        delta: f32,
        mode: f32,
        stamp: u64,
        count: f32,
    ) -> Result<Vec<u8>, String> {
        self.validate()?;
        if base.len() != 1024
            || !delta.is_finite()
            || delta < 0.0
            || !mode.is_finite()
            || !count.is_finite()
            || count < 0.0
            || stamp > 16_777_216
            || ![
                self.control.reset_mode,
                self.control.allocate_mode,
                self.control.update_mode,
            ]
            .contains(&mode)
        {
            return Err("Source particle control value outside exact Float32 bank ABI".into());
        }
        let mut bytes = base.to_vec();
        #[allow(
            clippy::cast_precision_loss,
            reason = "Stamp <=2^24 admits exact Float32 integer conversion"
        )]
        let stamp = stamp as f32;
        for (offset, value) in [
            (self.control.delta_offset, delta),
            (self.control.mode_offset, mode),
            (self.control.stamp_offset, stamp),
            (self.control.count_offset, count),
        ] {
            bytes[offset..offset + 4].copy_from_slice(&value.to_le_bytes());
        }
        Ok(bytes)
    }
}

impl SourceParticleTransport {
    /// Check original carrier identities before resolving or registering any
    /// effect resources. Vertex count is necessary, but does not prove vertex
    /// order or source-to-carrier coordinate equivalence.
    pub fn validate_carrier(&self, bytes: &[u8]) -> Result<(), String> {
        use crate::source_graph_glb::{SourceGraphGlbReadBudget, read_glb_document};
        if bytes_sha256(bytes) != self.carrier_sha256 {
            return Err("Source particle carrier SHA differs".into());
        }
        let (document, _) = read_glb_document(bytes, SourceGraphGlbReadBudget::Standard)?;
        self.validate_carrier_document(&document)
    }

    fn validate_carrier_document(&self, document: &serde_json::Value) -> Result<(), String> {
        let index = |value: &serde_json::Value, field: &str| -> Result<usize, String> {
            value
                .get(field)
                .and_then(serde_json::Value::as_u64)
                .and_then(|n| usize::try_from(n).ok())
                .ok_or_else(|| format!("Source particle carrier {field} missing or invalid"))
        };
        for owner in &self.owners {
            owner.validate()?;
            let node = document
                .get("nodes")
                .and_then(|v| v.get(owner.source_node))
                .ok_or("Source particle carrier node missing")?;
            let mesh = index(node, "mesh")?;
            let primitive = document
                .get("meshes")
                .and_then(|v| v.get(mesh))
                .and_then(|v| v.get("primitives"))
                .and_then(|v| v.get(owner.source_primitive))
                .ok_or("Source particle carrier primitive missing")?;
            if index(primitive, "material")? != owner.source_material {
                return Err("Source particle carrier primitive material differs".into());
            }
            let position = primitive
                .get("attributes")
                .ok_or("Source particle carrier attributes missing")?;
            let accessor = index(position, "POSITION")?;
            let accessor = document
                .get("accessors")
                .and_then(|v| v.get(accessor))
                .ok_or("Source particle carrier POSITION accessor missing")?;
            let actual = index(accessor, "count")?;
            if actual != owner.projection.vertex_count {
                return Err(format!(
                    "Source particle carrier vertex count differs: carrier={actual}, projection={}",
                    owner.projection.vertex_count
                ));
            }
            if accessor.get("type").and_then(serde_json::Value::as_str) != Some("VEC3")
                || index(accessor, "componentType")? != 5126
            {
                return Err("Source particle carrier POSITION layout invalid".into());
            }
            let material = document
                .get("materials")
                .and_then(|v| v.get(owner.source_material))
                .ok_or("Source particle carrier material missing")?;
            if material
                .get("extensions")
                .and_then(|v| v.get(crate::source_graph_bundle::SOURCE_GRAPH_EXTENSION))
                .is_none()
            {
                return Err(
                    "Source particle carrier requires an explicit source graph material".into(),
                );
            }
        }
        Ok(())
    }

    /// The library supplies the manifest pin; the actual GLB supplies the
    /// carrier pin. Resource resolution starts only after identity checks.
    pub fn decode_for_carrier(
        wire: &[u8],
        expected_manifest: ContentHash,
        carrier: &[u8],
        resolve: impl FnMut(&str) -> Result<Vec<u8>, String>,
    ) -> Result<ResolvedSourceParticleTransport, String> {
        if wire.len() > 1024 * 1024 || bytes_sha256(wire) != expected_manifest {
            return Err("Source particle manifest SHA or budget differs".into());
        }
        let contract: Self = serde_json::from_slice(wire).map_err(|e| e.to_string())?;
        contract.validate_carrier(carrier)?;
        let source = crate::source_graph_glb::read_source_graph_glb(carrier)?
            .ok_or("Source particle carrier source graph declaration missing")?;
        for owner in &contract.owners {
            let (_, material) = source
                .materials
                .iter()
                .find(|(index, _)| *index == owner.source_material)
                .ok_or("Source particle carrier material was not admitted")?;
            for stage in [&material.forward, &material.prepass] {
                let bytes = source
                    .views
                    .get(stage.buffer_view)
                    .ok_or("Source particle carrier material stage missing")?;
                let program = std::str::from_utf8(bytes)
                    .map_err(|_| "Source particle carrier material stage is not UTF-8")?;
                if !crate::source_graph_bundle::stage_requires_state_buffer(program)
                    .map_err(|e| format!("Source particle carrier state ABI invalid: {e:?}"))?
                {
                    return Err(
                        "Source particle carrier material does not consume the state buffer".into(),
                    );
                }
            }
        }
        Self::decode_pinned(wire, expected_manifest, bytes_sha256(carrier), resolve)
    }

    /// Manifest pin is supplied by the governed library revision, not read
    /// from the envelope itself. Resolve all bytes before renderer mutation.
    pub fn decode_pinned(
        wire: &[u8],
        expected_manifest: ContentHash,
        expected_carrier: ContentHash,
        mut resolve: impl FnMut(&str) -> Result<Vec<u8>, String>,
    ) -> Result<ResolvedSourceParticleTransport, String> {
        if wire.len() > 1024 * 1024 || bytes_sha256(wire) != expected_manifest {
            return Err("Source particle manifest SHA or budget differs".into());
        }
        let contract: Self = serde_json::from_slice(wire).map_err(|e| e.to_string())?;
        if contract.schema_version != 1
            || contract.carrier_sha256 != expected_carrier
            || contract.licence_ref.trim().is_empty()
            || contract.owners.is_empty()
            || contract.owners.len() > 4096
        {
            return Err(
                "Source particle transport version, carrier, licence or owner budget differs"
                    .into(),
            );
        }
        let mut resources = BTreeMap::new();
        let mut pins = BTreeMap::new();
        let mut identities = std::collections::BTreeSet::new();
        let mut total = 0usize;
        for owner in &contract.owners {
            owner.validate()?;
            if !identities.insert((
                &owner.source_component_guid,
                &owner.source_component_file_id,
            )) {
                return Err("Duplicated source particle component identity".into());
            }
            for blob in owner.blobs() {
                if !relative(&blob.path)
                    || blob.byte_length == 0
                    || blob.byte_length > 64 * 1024 * 1024
                {
                    return Err("Source particle blob path or budget invalid".into());
                }
                if let Some(previous) = pins.get(&blob.path) {
                    if *previous != (blob.sha256, blob.byte_length) {
                        return Err("Source particle path has conflicting pins".into());
                    }
                    continue;
                }
                total = total
                    .checked_add(blob.byte_length)
                    .ok_or("Source particle resource budget overflow")?;
                if total > 128 * 1024 * 1024 {
                    return Err("Source particle total resource budget exceeded".into());
                }
                let bytes = resolve(&blob.path)?;
                if bytes.len() != blob.byte_length || bytes_sha256(&bytes) != blob.sha256 {
                    return Err(format!("Source particle resource differs: {}", blob.path));
                }
                pins.insert(blob.path.clone(), (blob.sha256, blob.byte_length));
                resources.insert(blob.path.clone(), bytes);
            }
        }
        for owner in &contract.owners {
            for program in [&owner.compute, &owner.projection.program] {
                std::str::from_utf8(&resources[&program.source.path])
                    .map_err(|_| "Source particle program is not UTF-8 WGSL")?;
            }
        }
        Ok(ResolvedSourceParticleTransport {
            contract,
            resources,
        })
    }
}

#[allow(
    clippy::float_cmp,
    reason = "Mode values are exact source ABI selectors; signed zero aliases must be refused"
)]
fn modes_overlap(layout: &SourceParticleControlLayout) -> bool {
    layout.reset_mode == layout.allocate_mode
        || layout.reset_mode == layout.update_mode
        || layout.allocate_mode == layout.update_mode
}

#[cfg(test)]
mod tests {
    use super::*;

    fn fixture() -> (SourceParticleTransport, BTreeMap<String, Vec<u8>>) {
        let mut resources = BTreeMap::new();
        let mut blob = |path: &str, bytes: Vec<u8>| {
            let pin = SourceParticleBlob {
                path: path.into(),
                sha256: bytes_sha256(&bytes),
                byte_length: bytes.len(),
            };
            resources.insert(path.into(), bytes);
            pin
        };
        let program = SourceParticleProgram {
            source: blob(
                "program.wgsl",
                b"@compute @workgroup_size(1) fn advance() {}".to_vec(),
            ),
            entry_point: "advance".into(),
        };
        let owner = SourceParticleOwnerTransport {
            source_component_guid: "a".repeat(32),
            source_component_file_id: "11400000".into(),
            source_node: 0,
            source_primitive: 0,
            source_material: 0,
            compute: program.clone(),
            curve_atlas: blob("curve.bin", vec![0; 8]),
            curve_width: 1,
            curve_height: 1,
            curve_bank: blob("bank.bin", vec![0; 1024]),
            initial_state: blob("state.bin", vec![0; 16]),
            state_prefix_bytes: 16,
            workgroups: 1,
            control: SourceParticleControlLayout {
                delta_offset: 1008,
                mode_offset: 1012,
                stamp_offset: 1016,
                count_offset: 1020,
                reset_mode: 3.0,
                allocate_mode: 2.0,
                update_mode: 1.0,
            },
            spawner: SourceSingleBurstLoopParameters {
                loop_duration_seconds: 3.0,
                loop_count: 1,
                burst_delay_seconds: 0.0,
                burst_count: 1.0,
                burst_active: true,
                initial_play: true,
                prewarm_steps: 0,
            },
            projection: SourceParticleVertexTransport {
                frame_layout: None,
                program,
                bank: blob("projection-bank.bin", vec![0; 1024]),
                native_vertices: blob("vertices.bin", vec![0; 64]),
                vertex_count: 1,
                input_stride_bytes: 64,
                output_stride_bytes: 112,
            },
        };
        (
            SourceParticleTransport {
                schema_version: 1,
                carrier_sha256: bytes_sha256(b"carrier"),
                source_archive_sha256: bytes_sha256(b"archive"),
                source_graph_sha256: bytes_sha256(b"graph"),
                licence_ref: "LicenseRef-private-source".into(),
                owners: vec![owner],
            },
            resources,
        )
    }

    #[test]
    fn pins_paths_and_component_identity_are_checked_before_renderer_admission() {
        let (contract, resources) = fixture();
        let wire = serde_json::to_vec(&contract).unwrap();
        let resolved = SourceParticleTransport::decode_pinned(
            &wire,
            bytes_sha256(&wire),
            contract.carrier_sha256,
            |path| resources.get(path).cloned().ok_or("missing".into()),
        )
        .unwrap();
        assert_eq!(resolved.contract, contract);
        assert_eq!(resolved.resources, resources);
        assert!(
            SourceParticleTransport::decode_pinned(
                &wire,
                bytes_sha256(b"changed"),
                contract.carrier_sha256,
                |_| panic!("must not resolve changed manifest")
            )
            .is_err()
        );
        assert!(
            SourceParticleTransport::decode_pinned(
                &wire,
                bytes_sha256(&wire),
                bytes_sha256(b"other carrier"),
                |_| panic!("must not resolve wrong carrier")
            )
            .is_err()
        );
        assert!(
            SourceParticleTransport::decode_pinned(
                &wire,
                bytes_sha256(&wire),
                contract.carrier_sha256,
                |path| {
                    let mut bytes = resources[path].clone();
                    bytes[0] ^= 1;
                    Ok(bytes)
                }
            )
            .is_err()
        );
        let mut bad = contract.clone();
        bad.owners[0].compute.source.path = "../program.wgsl".into();
        let bytes = serde_json::to_vec(&bad).unwrap();
        assert!(
            SourceParticleTransport::decode_pinned(
                &bytes,
                bytes_sha256(&bytes),
                bad.carrier_sha256,
                |_| panic!("must not resolve traversal")
            )
            .is_err()
        );
        let mut duplicated = contract;
        duplicated.owners.push(duplicated.owners[0].clone());
        let bytes = serde_json::to_vec(&duplicated).unwrap();
        assert!(
            SourceParticleTransport::decode_pinned(
                &bytes,
                bytes_sha256(&bytes),
                duplicated.carrier_sha256,
                |path| Ok(resources[path].clone())
            )
            .is_err()
        );
    }

    #[test]
    fn carrier_mismatches_refuse_before_effect_resource_resolution() {
        let (mut contract, _) = fixture();
        let document = serde_json::json!({
            "nodes": [{"mesh": 0}],
            "meshes": [{"primitives": [{"material": 0, "attributes": {"POSITION": 0}}]}],
            "accessors": [{"count": 1, "type": "VEC3", "componentType": 5126}],
            "materials": [{"extensions": {"OPENRPG_source_graph": {}}}]
        });
        assert!(contract.validate_carrier_document(&document).is_ok());
        for pointer in [
            "/nodes/0/mesh",
            "/meshes/0/primitives/0/material",
            "/accessors/0/count",
        ] {
            let mut bad = document.clone();
            *bad.pointer_mut(pointer).unwrap() = serde_json::json!(2);
            assert!(contract.validate_carrier_document(&bad).is_err());
        }
        let mut ordinary = document.clone();
        ordinary["materials"][0] = serde_json::json!({});
        assert!(contract.validate_carrier_document(&ordinary).is_err());
        let mut json = serde_json::to_vec(&document).unwrap();
        while !json.len().is_multiple_of(4) {
            json.push(b' ');
        }
        let mut carrier = b"glTF".to_vec();
        carrier.extend(2_u32.to_le_bytes());
        carrier.extend(u32::try_from(20 + json.len()).unwrap().to_le_bytes());
        carrier.extend(u32::try_from(json.len()).unwrap().to_le_bytes());
        carrier.extend(b"JSON");
        carrier.extend(json);
        contract.carrier_sha256 = bytes_sha256(&carrier);
        contract.owners[0].projection.vertex_count = 2;
        contract.owners[0].projection.native_vertices.byte_length = 128;
        let wire = serde_json::to_vec(&contract).unwrap();
        let error = SourceParticleTransport::decode_for_carrier(
            &wire,
            bytes_sha256(&wire),
            &carrier,
            |_| panic!("wrong carrier must not resolve any effect resource"),
        )
        .err()
        .unwrap();
        assert!(error.contains("carrier=1, projection=2"));
        assert!(contract.validate_carrier(b"modified").is_err());
        contract.carrier_sha256 = bytes_sha256(b"modified");
        assert!(contract.validate_carrier(b"modified").is_err());
    }

    #[test]
    fn control_lanes_preserve_author_bytes_and_invalid_layouts_do_not_panic() {
        let (mut contract, _) = fixture();
        let owner = &mut contract.owners[0];
        let original = vec![0xA5; 1024];
        let bank = owner
            .control_bank(&original, 0.125, 1.0, 16_777_216, 0.0)
            .unwrap();
        assert_eq!(bank[..1008], original[..1008]);
        assert_eq!(&bank[1008..1012], &0.125_f32.to_le_bytes());
        assert!(
            owner
                .control_bank(&original, 0.0, 1.0, 16_777_217, 0.0)
                .is_err()
        );
        assert!(owner.control_bank(&original, 0.0, 99.0, 1, 0.0).is_err());
        owner.curve_width = u32::MAX;
        owner.curve_height = u32::MAX;
        assert!(owner.validate().is_err());
        owner.curve_width = 1;
        owner.curve_height = 1;
        owner.control.reset_mode = -0.0;
        owner.control.allocate_mode = 0.0;
        assert!(owner.validate().is_err());
        owner.control.reset_mode = 3.0;
        owner.control.allocate_mode = 2.0;
        owner.control.count_offset = owner.control.delta_offset;
        assert!(owner.validate().is_err());
    }
}
