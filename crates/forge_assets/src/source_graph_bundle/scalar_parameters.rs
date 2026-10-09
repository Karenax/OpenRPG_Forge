//! Integrity-checked compiler slots, independent of material or pack identity.
use super::{SourceGraphBlob, SourceGraphContractError, SourceGraphMaterialContract, blob};
use crate::source_material_parameters::{SOURCE_SCALAR_CAPACITY, SourceMaterialScalarParameters};
use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct SourceGraphScalarParameterContract {
    pub names: Vec<String>,
    /// Complete little-endian 16-vec4 ABI, including zero unused slots.
    pub values: SourceGraphBlob,
}

#[cfg(test)]
mod tests {
    use super::*;
    use forge_core::bytes_sha256;

    const STAGE: &[u8] =
        b"@group(#{MATERIAL_BIND_GROUP}) @binding(120) var<uniform> values:vec4<f32>;";
    fn contract(packet: &[u8]) -> SourceGraphMaterialContract {
        let hash = bytes_sha256(STAGE);
        serde_json::from_value(serde_json::json!({
            "schema_version":5,"source_program_sha256":hash,"source_material_sha256":hash,
            "forward":{"buffer_view":0,"sha256":hash},"prepass":{"buffer_view":0,"sha256":hash},
            "libraries":[],"textures":[],"requires_original_vertex_color":false,
            "normal_decode_modes":[null,null,null,null,null,null,null,null],
            "scalar_parameters":{"names":["measured_mask_r","measured_mask_g"],"values":{"buffer_view":1,"sha256":bytes_sha256(packet)}},
            "state":{"color_write_mask":15,"color_blend":[1,0],"alpha_blend":[1,0],"forward_cull":0,"prepass_cull":0,
                "depth_compare":4,"write_depth":true,"transparent":false,"alpha_test":false,"alpha_to_coverage":false,
                "render_queue":2000,"disabled_passes":[]}
        })).unwrap()
    }
    fn packet() -> [u8; 256] {
        let mut packet = [0; 256];
        packet[..4].copy_from_slice(&1f32.to_le_bytes());
        packet[4..8].copy_from_slice(&1f32.to_le_bytes());
        packet
    }
    #[test]
    fn source_graph_scalar_transport_preserves_packet_and_compiler_order() {
        let p = packet();
        let c = contract(&p);
        c.validate_integrity(&[STAGE, &p]).unwrap();
        let values = c.read_scalar_parameters(&[STAGE, &p]).unwrap().unwrap();
        assert_eq!(values.little_endian_uniform_bytes(), p);
        assert_eq!(values.slot("measured_mask_g"), Some(1));
        assert_eq!(values.vec4_lanes()[0], [1., 1., 0., 0.]);
        let json = serde_json::to_vec(&c).unwrap();
        let reopened: SourceGraphMaterialContract = serde_json::from_slice(&json).unwrap();
        assert_eq!(reopened, c);
    }
    #[test]
    fn source_graph_scalar_transport_refuses_absent_packet_or_legacy_schema() {
        let p = packet();
        let mut c = contract(&p);
        c.scalar_parameters = None;
        assert!(c.validate_integrity(&[STAGE, &p]).is_err());
        let mut c = contract(&p);
        c.schema_version = 4;
        assert!(c.validate_integrity(&[STAGE, &p]).is_err());
    }
    #[test]
    fn source_graph_scalar_transport_refuses_changed_truncated_nonfinite_and_undeclared_values() {
        let p = packet();
        let c = contract(&p);
        let mut changed = p;
        changed[0] ^= 1;
        assert_eq!(
            c.validate_integrity(&[STAGE, &changed]),
            Err(SourceGraphContractError::ChangedBlob)
        );
        for invalid in [
            p[..252].to_vec(),
            {
                let mut b = p;
                b[..4].copy_from_slice(&f32::NAN.to_le_bytes());
                b.to_vec()
            },
            {
                let mut b = p;
                b[252..].copy_from_slice(&1f32.to_le_bytes());
                b.to_vec()
            },
        ] {
            assert!(
                contract(&invalid)
                    .validate_integrity(&[STAGE, &invalid])
                    .is_err()
            );
        }
    }
    #[test]
    fn source_graph_scalar_transport_refuses_duplicate_names_and_stage_abi_mismatch() {
        let p = packet();
        let mut c = contract(&p);
        c.scalar_parameters.as_mut().unwrap().names[1] = "measured_mask_r".into();
        assert!(c.validate_integrity(&[STAGE, &p]).is_err());
        let mut c = contract(&p);
        let other = b"fn unused() {}";
        c.prepass = SourceGraphBlob {
            buffer_view: 2,
            sha256: bytes_sha256(other),
        };
        assert!(c.validate_integrity(&[STAGE, &p, other]).is_err());
    }
}

impl SourceGraphMaterialContract {
    pub fn read_scalar_parameters(
        &self,
        views: &[&[u8]],
    ) -> Result<Option<SourceMaterialScalarParameters>, SourceGraphContractError> {
        let declares_scalars = |stage: &SourceGraphBlob| {
            let source = std::str::from_utf8(blob(stage, views)?)
                .map_err(|_| SourceGraphContractError::InvalidShader)?;
            Ok::<_, SourceGraphContractError>(source.lines().any(|line| {
                let code = line.split("//").next().unwrap_or_default();
                let compact: String = code.split_whitespace().collect();
                compact.contains("@binding(120)") && compact.contains("var<uniform>")
            }))
        };
        let forward = declares_scalars(&self.forward)?;
        let prepass = declares_scalars(&self.prepass)?;
        if forward != prepass || forward != self.scalar_parameters.is_some() {
            return Err(SourceGraphContractError::ScalarParameters);
        }
        let Some(contract) = &self.scalar_parameters else {
            return Ok(None);
        };
        if self.schema_version != 5
            || contract.names.is_empty()
            || contract.names.len() > SOURCE_SCALAR_CAPACITY
            || contract.names.iter().any(|name| name.len() > 256)
        {
            return Err(SourceGraphContractError::ScalarParameters);
        }
        let bytes = blob(&contract.values, views)?;
        if bytes.len() != SOURCE_SCALAR_CAPACITY * 4 {
            return Err(SourceGraphContractError::ScalarParameters);
        }
        let slots = contract
            .names
            .iter()
            .enumerate()
            .map(|(index, name)| {
                let offset = index * 4;
                (
                    name.clone(),
                    f32::from_le_bytes(
                        bytes[offset..offset + 4]
                            .try_into()
                            .expect("checked packet length"),
                    ),
                )
            })
            .collect();
        let parameters = SourceMaterialScalarParameters::from_compiler_slots(slots)
            .map_err(|_| SourceGraphContractError::ScalarParameters)?;
        // Reject nonfinite values, undeclared trailing values, and noncanonical padding.
        if parameters.little_endian_uniform_bytes().as_slice() != bytes {
            return Err(SourceGraphContractError::ScalarParameters);
        }
        Ok(Some(parameters))
    }
}
