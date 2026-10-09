"""Stage the actual wire/program/key/specialization path, preserving preimages."""
from pathlib import Path
import hashlib
import json

R = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'source-graph-production-color-mask-v1'
FILES = [
    'crates/forge_assets/src/source_graph_bundle.rs',
    'crates/forge_engine/src/source_graph.rs',
    'crates/forge_engine/src/source_graph_program.rs',
    'crates/forge_engine/src/source_graph_glb.rs',
    'crates/forge_engine/src/source_graph_shadow_routing.rs',
]

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def replace(text, before, after, count=1):
    assert text.count(before) == count, 'Unexpected source shape: ' + before
    return text.replace(before, after)

def main():
    assert not ROOT.exists()
    proof_path = W / 'source-graph-color-write-mask-native-check-v2/RUN-RESULT.v1.json'
    proof = json.loads(proof_path.read_text('utf-8-sig'))
    assert proof['actual_exit_code'] == 0 and proof['tests_passed'] == 6
    inputs = {str(proof_path): sha(proof_path), str(Path(__file__).resolve()): sha(Path(__file__).resolve())}
    ROOT.mkdir(); transformed = {}
    for relative in FILES:
        source = R / relative; raw = source.read_bytes(); inputs[str(source)] = sha(source)
        backup = ROOT / 'before' / relative; backup.parent.mkdir(parents=True, exist_ok=True); backup.write_bytes(raw)
        transformed[relative] = raw.decode('utf-8').replace('\r\n', '\n')
    rel = FILES[0]; text = transformed[rel]
    text = replace(text, '    pub color_blend: [u8; 2],', '''    /// Renderer channel bits RED=1, GREEN=2, BLUE=4, ALPHA=8.
    /// Mandatory in schema 4; absent in schemas 2/3, whose renderer wrote ALL.
    /// Producers translate named source channels; Unity numeric masks differ.
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub color_write_mask: Option<u32>,
    pub color_blend: [u8; 2],''')
    text = replace(text, 'if !matches!(self.schema_version, 2 | 3)', 'if !matches!(self.schema_version, 2 | 3 | 4)')
    text = replace(text, '|| (self.schema_version == 3', '|| (matches!(self.schema_version, 3 | 4)')
    text = replace(text, '        if self.textures.len() > 7', '''        if (self.schema_version < 4 && self.state.color_write_mask.is_some())
            || (self.schema_version == 4 && self.state.color_write_mask.is_none())
        {
            return Err(SourceGraphContractError::Schema);
        }
        if self.textures.len() > 7''')
    text = replace(text, '        if state.forward_cull > 2', '        if state.color_write_mask.is_some_and(|mask| mask > 15)\n            || state.forward_cull > 2')
    text = replace(text, '                color_blend: [1, 0],', '                color_write_mask: None,\n                color_blend: [1, 0],')
    transformed[rel] = text
    rel = FILES[1]; text = transformed[rel]
    text = replace(text, 'pub use source_render_state::SourceGraphForwardState;', '''pub use source_render_state::SourceGraphForwardState;
#[path = "source_graph_color_write_mask.rs"]
mod source_color_write_mask;
pub use source_color_write_mask::SourceGraphForwardPassState;''')
    text = replace(text, '    pub forward_state: SourceGraphForwardState,', '''    /// Explicit renderer channel mask; included in Eq/Hash pipeline caching.
    pub color_write_mask: u32,
    pub forward_state: SourceGraphForwardState,''')
    text = replace(text, '        key.forward_state.validate()?;', '''        SourceGraphForwardPassState { forward: key.forward_state,
            color_write_mask: key.color_write_mask }.validate()?;''')
    text = replace(text, 'key.bind_group_data.forward_state.apply(descriptor).is_err()', '''(SourceGraphForwardPassState { forward: key.bind_group_data.forward_state,
            color_write_mask: key.bind_group_data.color_write_mask }).apply(descriptor).is_err()''')
    transformed[rel] = text
    rel = FILES[2]; text = transformed[rel]
    text = replace(text, '    pub forward_state:SourceGraphForwardState,', '    pub color_write_mask:u32,\n    pub forward_state:SourceGraphForwardState,')
    text = replace(text, 'forward_state:program.forward_state};', 'color_write_mask:program.color_write_mask,forward_state:program.forward_state};')
    text = replace(text, 'SourceGraphMaterialProgram{prepass_cull,shaders,', 'SourceGraphMaterialProgram{color_write_mask:15,prepass_cull,shaders,')
    text = replace(text, 'SourceGraphMaterialProgram{prepass_cull:0,', 'SourceGraphMaterialProgram{color_write_mask:15,prepass_cull:0,')
    transformed[rel] = text
    rel = FILES[3]; text = transformed[rel]
    text = replace(text, 'contract.schema_version == 3', 'matches!(contract.schema_version, 3 | 4)')
    text = replace(text, '            prepass_cull: state.prepass_cull,', '''            // Legacy transport explicitly retained the old ALL-writes ABI.
            // Schema 4 requires a measured/translated mask during validation.
            color_write_mask: state.color_write_mask.unwrap_or(15),
            prepass_cull: state.prepass_cull,''')
    text = replace(text, '                color_blend:', '                color_write_mask: None,\n                color_blend:', count=2)
    text += '''
#[cfg(test)]
mod color_mask_transport_tests {
    use super::*;
    use forge_assets::source_graph_bundle::{SourceGraphBlob, SourceGraphPipelineState};
    fn contract(version: u32, mask: Option<u32>) -> SourceGraphMaterialContract {
        let hash = forge_core::bytes_sha256(b"fn test() -> f32 { return 0.0; }");
        SourceGraphMaterialContract {
            schema_version: version, source_program_sha256: hash, source_material_sha256: hash,
            forward: SourceGraphBlob { buffer_view: 0, sha256: hash },
            prepass: SourceGraphBlob { buffer_view: 0, sha256: hash }, libraries: vec![], textures: vec![],
            state: SourceGraphPipelineState { color_write_mask: mask, color_blend: [5,10], alpha_blend: [5,10],
                forward_cull: 0, prepass_cull: 0, depth_compare: 4, write_depth: false,
                transparent: true, alpha_test: false, alpha_to_coverage: false,
                render_queue: 3000, disabled_passes: vec![] },
            requires_original_vertex_color: false, normal_decode_modes: [None; 8],
        }
    }
    #[test]
    fn schema_four_rgb_reaches_actual_material_program_and_legacy_keeps_all() {
        let bytes = b"fn test() -> f32 { return 0.0; }";
        let current = source_graph_program_from_glb(&contract(4, Some(7)), &[bytes]).unwrap();
        assert_eq!(current.program.color_write_mask, 7);
        assert!(current.requires_captured_images());
        let old = source_graph_program_from_glb(&contract(3, None), &[bytes]).unwrap();
        assert_eq!(old.program.color_write_mask, 15);
    }
    #[test]
    fn unknown_mask_missing_mask_or_mask_in_legacy_contract_refuse() {
        let bytes = b"fn test() -> f32 { return 0.0; }";
        for value in [contract(4, None), contract(4, Some(16)), contract(3, Some(7)), contract(99, Some(7))] {
            assert!(source_graph_program_from_glb(&value, &[bytes]).is_err());
        }
    }
}
'''
    transformed[rel] = text
    rel = FILES[4]; text = transformed[rel]
    text = replace(text, 'let key=SourceGraphKey{prepass_cull:2,', 'let key=SourceGraphKey{color_write_mask:15,prepass_cull:2,')
    transformed[rel] = text
    source = R / 'scripts/hivemind/candidates/source_graph_color_write_mask.rs'
    inputs[str(source)] = sha(source)
    text = source.read_text('utf-8-sig')
    text = replace(text, 'use crate::source_graph_render_state::SourceGraphForwardState;', 'use super::SourceGraphForwardState;')
    text = replace(text, '    pub fn apply(self, descriptor:', '''    pub fn validate(self) -> Result<(), &'static str> {
        ColorWrites::from_bits(self.color_write_mask).ok_or("Unknown original color channel mask")?;
        self.forward.validate()
    }
    pub fn apply(self, descriptor:''')
    transformed['crates/forge_engine/src/source_graph_color_write_mask.rs'] = text
    outputs = {}
    for relative, content in transformed.items():
        p = ROOT / 'candidate' / relative; p.parent.mkdir(parents=True, exist_ok=True); p.write_text(content, 'utf-8')
        outputs[str(p)] = sha(p)
    for p, digest in inputs.items(): assert sha(Path(p)) == digest, 'Source changed while staging: ' + p
    (ROOT / 'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=inputs, output_pins=outputs,
        changed_paths=list(transformed), source_contract_schema=4, existing_schemas_preserved=[2,3],
        mask_participates_in_real_material_key=True, actual_engine_compiled=False, installed=False,
        gpu_shader_parity_qualified=False, full_import=False), indent=2), 'utf-8')
    print(json.dumps(dict(staged_files=len(transformed), installed=False, actual_engine_compiled=False)))

if __name__ == '__main__': main()
