"""Assemble an integrity-only schema-6 material packet; never register it as delivered."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import subprocess
import sys

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'townsmith-s-paint-material-packet-v1'


def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))


def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


def main():
    assert not ROOT.exists()
    ROOT.mkdir()
    set_path = W / 'townsmith-s-paint-texture-set-v1/TEXTURE-SET.v1.json'
    state_path = W / 'townsmith-s-paint-original-render-contract-v1/RESULT.v1.json'
    lit_path = W / 'townsmith-s-paint-lit-fragments-v1/RESULT.v1.json'
    textures = read(set_path)
    render = read(state_path)['rows'][0]
    assert read(lit_path)['status'] == 'townsmith_original_arithmetic_native_lit_and_prepass_composed'
    pins = {str(p): sha(p) for p in (set_path, state_path, lit_path, Path(__file__))}
    views = list(textures['buffer_views'])
    for view in views:
        path = Path(view['file'])
        assert path.stat().st_size == view['bytes'] and sha(path) == view['sha256'], path
        pins[str(path)] = view['sha256']
    for key in ('material', 'graph'):
        path = Path(render['original_' + key])
        assert sha(path) == render['original_' + key + '_sha256']
        pins[str(path)] = sha(path)

    stages = []
    for name in ('forward', 'prepass'):
        path = W / f'townsmith-s-paint-lit-fragments-v1/source-native-{name}.wgsl'
        assert sha(path) == read(lit_path)['input_pins'][str(path)]
        stages.append({'buffer_view': len(views), 'sha256': sha(path)})
        views.append({'file': str(path), 'bytes': path.stat().st_size, 'sha256': sha(path)})
        pins[str(path)] = sha(path)
    floats = render['source_floats']
    assert floats['_CullModeForward'] == 2 and floats['_CullMode'] == 2
    assert floats['_ZTestDepthEqualForOpaque'] == 3 and floats['_ZWrite'] == 1
    assert not render['source_keyword_transparent'] and not render['source_keyword_alpha_test']
    contract = {
        'schema_version': 6,
        'source_program_sha256': render['original_graph_sha256'],
        'source_material_sha256': render['original_material_sha256'],
        'forward': stages[0], 'prepass': stages[1], 'libraries': [],
        'textures': textures['textures'],
        'state': {'color_write_mask': 15, 'color_blend': [int(floats['_SrcBlend']), int(floats['_DstBlend'])],
                  'alpha_blend': [int(floats['_AlphaSrcBlend']), int(floats['_AlphaDstBlend'])],
                  'forward_cull': 2, 'prepass_cull': 2, 'depth_compare': 3, 'write_depth': True,
                  'transparent': False, 'alpha_test': False,
                  'alpha_to_coverage': render['source_keyword_alpha_to_mask'],
                  'render_queue': render['source_custom_render_queue'],
                  'disabled_passes': render['source_disabled_shader_passes']},
        'requires_original_vertex_color': False,
        'normal_decode_modes': textures['normal_decode_modes'], 'scalar_parameters': None,
    }
    sdk = W / 'townsmith-s-paint-sdk-generated-source-v2/S_Paint.generated.shader.txt'
    text = sdk.read_text(encoding='utf-8-sig')
    start = text.index('Name "Forward"')
    end = text.index('HLSLPROGRAM', start)
    source_states = text[start:end]
    assert 'ZTest [_ZTestDepthEqualForOpaque]' in source_states
    assert 'Blend 4 One OneMinusSrcAlpha' in source_states and 'Stencil' in source_states
    (ROOT / 'original-forward-shaderlab-state.txt').write_text(source_states, encoding='utf-8')
    pins[str(sdk)] = sha(sdk)
    packet = {'status': 'complete_material_wire_packet_prepared_integrity_only',
              'prepared_utc': datetime.now(timezone.utc).isoformat(), 'contract': contract, 'buffer_views': views,
              'input_pins': pins, 'original_render_state_receipt': str(state_path),
              'source_state_limits': ['Wire carries primary target RGBA mask; additional MRT blends/masks and stencil are preserved in sidecar, not qualified by this wire.',
                                    'Source Equal depth comparison requires an actual matching prepass.',
                                    'Source full vertex-color branch is refused by the shader if enabled; this packet has no enabled vertex-color input.'],
              'original_forward_shaderlab_state': str(ROOT / 'original-forward-shaderlab-state.txt'),
              'source_pipeline_state_qualified': False, 'actual_resource_bind_groups_created': False,
              'gpu_draw_executed': False, 'source_unity_render_parity_qualified': False,
              'forge_editor_verified': False, 'full_import': False, 'complete_assets_delivered_increment': 0}
    (ROOT / 'MATERIAL-PACKET.v1.json').write_text(json.dumps(packet, indent=2), encoding='utf-8')
    helper = (S / 'check_normal_decode_swizzle_v1.py').read_text(encoding='utf-8')
    helper = helper.replace('normal-decode-swizzle-check-v1', 'townsmith-s-paint-material-packet-check-v1')
    marker = ' #[test] fn ten_actual_resource_chains_and_identity_sampler_refusals() {'
    extra = ''' #[test] fn full_schema6_material_packet_validates_and_refuses_missing_normal_profiles() {
  use crate::source_graph_bundle::{SourceGraphMaterialContract,SourceGraphContractError};
  let packet:Value=serde_json::from_slice(&fs::read("F:/OpenRPG_ImportWork/townsmith-s-paint-material-packet-v1/MATERIAL-PACKET.v1.json").unwrap()).unwrap();
  let buffers:Vec<Vec<u8>>=packet["buffer_views"].as_array().unwrap().iter().map(|view|{
   let bytes=fs::read(view["file"].as_str().unwrap()).unwrap();assert_eq!(bytes.len() as u64,view["bytes"].as_u64().unwrap());assert_eq!(serde_json::to_value(bytes_sha256(&bytes)).unwrap(),view["sha256"]);bytes
  }).collect();
  let views:Vec<&[u8]>=buffers.iter().map(|v|v.as_slice()).collect();
  let original:SourceGraphMaterialContract=serde_json::from_value(packet["contract"].clone()).unwrap();
  assert_eq!(original.schema_version,6);assert_eq!(original.textures.len(),10);assert_eq!(original.state.depth_compare,3);
  assert_eq!(original.validate_integrity(&views),Ok(()));
  for index in 0..3 {let mut missing=original.clone();missing.normal_decode_modes[index]=None;assert_eq!(missing.validate_integrity(&views),Err(SourceGraphContractError::UnqualifiedNormalDecode));}
  let mut corrupt=buffers.clone();corrupt[original.forward.buffer_view][0]^=1;
  let altered:Vec<&[u8]>=corrupt.iter().map(|v|v.as_slice()).collect();assert!(original.validate_integrity(&altered).is_err());
 }
'''
    assert helper.count(marker) == 1
    helper = helper.replace(marker, extra + marker)
    target = S / 'check_townsmith_material_packet_v1.py'
    assert not target.exists()
    target.write_text(helper, encoding='utf-8')
    subprocess.run([sys.executable, '-B', str(target)], check=True)


if __name__ == '__main__':
    main()
