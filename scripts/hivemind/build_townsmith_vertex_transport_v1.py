"""Compose static source vertex stages with lossless original UV0 transport."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import re
import subprocess
from source_graph_instance_index_transport_v2 import repair_vertex_instance_transport

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'townsmith-s-paint-vertex-transport-v1'
OLD = W / 'native-hdrp-nine-source-programs-v1/source-graph-bevy-vertex-wrapper-v4'
TOOLS = W / 'native-hdrp-nine-source-programs-v1/source-graph-real-lighting-composer-v2'
BEVY = Path('C:/Users/karen/.cargo/registry/src/index.crates.io-1949cf8c6b5b557f/bevy_pbr-0.19.1/src')
HOST = W / 'townsmith-s-paint-material-host-adapter-v1'
SDK = W / 'townsmith-s-paint-sdk-generated-source-v2/S_Paint.generated.shader.txt'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as handle:
        return hashlib.file_digest(handle, 'sha256').hexdigest()


def output_struct(path, name):
    source = path.read_text(encoding='utf-8')
    start = source.index('struct VertexOutput {')
    end = source.index('\n}', start)
    text = source[start:end].replace('struct VertexOutput {', 'struct ' + name + ' {', 1)
    return text + '\n    @location(10) source_uv0:vec4<f32>,\n}\n'


def main():
    assert not ROOT.exists()
    host_receipt = read(HOST / 'RESULT.v1.json')
    assert host_receipt['status'] == 'material_common_uniform_host_adapter_composed'
    program = HOST / 'material-host-program.wgsl'
    assert sha(program) == host_receipt['input_pins'][str(program)]
    assert host_receipt['vertex_defaults_are_mesh_inputs'] is True
    wrapper_path = OLD / 'source_graph_bevy_vertex_and_prepass.wgsl'
    reference = read(W / 'delta-vector-bevy-vertex-stages-v3/RESULT.v1.json')
    assert sha(wrapper_path) == reference['input_pins'][str(wrapper_path)]
    sdk = SDK.read_text(encoding='utf-8')
    assert re.search(r'output\.TangentSpaceNormal\s*=\s*float3\(0\.0f,\s*0\.0f,\s*1\.0f\);', sdk)
    assert re.search(r'output\.WorldSpaceNormal\s*=\s*normalize\(input\.tangentToWorld\[2\]\.xyz\);', sdk)
    receipt = read(TOOLS / 'RESULT.v1.json')
    tool, modules = TOOLS / 'validate_real_bevy_closure.exe', TOOLS / 'module-map.tsv'
    assert sha(tool) == receipt['exe_sha256'] and sha(modules) == receipt['module_map_sha256']
    forward = BEVY / 'render/forward_io.wgsl'
    prepass = BEVY / 'prepass/prepass_io.wgsl'
    inputs = [program, HOST / 'RESULT.v1.json', wrapper_path, SDK, forward, prepass, tool, modules,
              S / 'source_graph_instance_index_transport_v1.py', S / 'source_graph_instance_index_transport_v2.py', Path(__file__)]
    pins = {str(p): sha(p) for p in inputs}
    wrapper = repair_vertex_instance_transport(wrapper_path.read_text(encoding='utf-8'))
    assert wrapper.count('forward_io::VertexOutput,') == 1
    wrapper = wrapper.replace('forward_io::VertexOutput,', '')
    wrapper = wrapper.replace('->VertexOutput', '->SourceGraphForwardOutput').replace('var out:VertexOutput;', 'var out:SourceGraphForwardOutput;')
    declaration = '#import bevy_pbr::prepass_io::VertexOutput as SourceGraphPrepassOutput'
    assert wrapper.count(declaration) == 1
    wrapper = wrapper.replace(declaration, '')
    assert wrapper.count(' @location(11) source_color:vec4<f32>,') == 1
    wrapper = wrapper.replace(' @location(11) source_color:vec4<f32>,', '')
    wrapper = wrapper.replace(' out.color=vertex.source_color;', ' source_graph_active_vertex_color_requires_separate_admission();')
    start, end = wrapper.index(' var packet:SourceGraphHostPacket;'), wrapper.index(' out.world_position=mesh_functions::mesh_position_local_to_world(')
    # These source slots are disconnected object-space defaults: the SDK consumes
    # the mesh frame. Serialized zero values must not flatten the geometry.
    wrapper = wrapper[:start] + ''' let displaced_target_position=vertex.position;
 let displaced_target_normal=target_normal_object;
 let displaced_target_tangent=target_tangent_object;
 out.source_uv0=vertex.source_uv0;
''' + wrapper[end:]
    assert wrapper.count(' out.world_position=frame.world_position;') == 1
    wrapper = wrapper.replace(' out.world_position=frame.world_position;', ' out.world_position=frame.world_position;\n out.source_uv0=frame.source_uv0;')
    assert 'source_house_vertex' not in wrapper and 'SourceGraphHostPacket' not in wrapper
    assert 'get_world_from_local(source_instance_index)' in wrapper
    linear = '\nfn source_graph_linear(m:mat4x4<f32>)->mat3x3<f32> {return mat3x3<f32>(m[0].xyz,m[1].xyz,m[2].xyz);}\n'
    declarations = output_struct(forward, 'SourceGraphForwardOutput') + output_struct(prepass, 'SourceGraphPrepassOutput')
    shader = program.read_text(encoding='utf-8') + linear + declarations + wrapper
    ROOT.mkdir()
    output = ROOT / 'source-vertex-and-prepass.wgsl'
    output.write_text(shader, encoding='utf-8')
    probe = ROOT / 'source-vertex-surface-type-probe.wgsl'
    # A diagnostic fragment verifies that the full UV0 reaches the evaluator.
    # It is not a lighting pass and will never be registered as a material.
    probe.write_text(shader + '''
#ifndef PREPASS_PIPELINE
@fragment fn original_surface_input_probe(input:SourceGraphForwardOutput)->@location(0) vec4<f32> {
 let world_from_object=mesh_functions::get_world_from_local(input.instance_index);
 // Exact generated HDRP FragInputsToSurfaceDescriptionInputs value, not a fallback.
 let source_ts_normal=vec3<f32>(0.0,0.0,1.0);
 let material=evaluate_source_material_from_host(SourceMaterialHostInputs(input.source_uv0,input.world_position.xyz,input.world_normal,world_from_object,source_ts_normal));
 return vec4<f32>(material.base_color,material.alpha);
}
#endif
''', encoding='utf-8')
    configs = {
        'forward_vertex_and_surface_inputs': [],
        'depth_prepass_vertex': ['PREPASS_PIPELINE', 'DEPTH_PREPASS'],
        'normal_prepass_vertex': ['PREPASS_PIPELINE', 'DEPTH_PREPASS', 'NORMAL_PREPASS', 'NORMAL_PREPASS_OR_DEFERRED_PREPASS'],
        'orthographic_normal_prepass_vertex': ['PREPASS_PIPELINE', 'DEPTH_PREPASS', 'NORMAL_PREPASS', 'NORMAL_PREPASS_OR_DEFERRED_PREPASS', 'UNCLIPPED_DEPTH_ORTHO_EMULATION'],
    }
    cases = []
    for label, defines in configs.items():
        process = subprocess.run([str(tool), str(modules), str(probe), '!VERTEX_COLORS', *defines], capture_output=True, creationflags=subprocess.CREATE_NO_WINDOW)
        (ROOT / (label + '.stdout.log')).write_bytes(process.stdout)
        (ROOT / (label + '.stderr.log')).write_bytes(process.stderr)
        cases.append({'permutation': label, 'actual_exit_code': process.returncode, 'shader_defines': ['!VERTEX_COLORS', *defines]})
    for path, expected in pins.items():
        assert sha(path) == expected, path
    pins.update({str(p): sha(p) for p in (output, probe)})
    result = {'status': 'original_static_vertex_and_uv0_transport_composed' if all(c['actual_exit_code'] == 0 for c in cases) else 'vertex_transport_composition_refusal_retained',
              'finished_utc': datetime.now(timezone.utc).isoformat(), 'cases': cases, 'input_pins': pins,
              'original_uv0_components_transported': 4, 'uv0_vertex_attribute_location': 10, 'uv0_varying_location': 10,
              'source_tangent_normal_from_generated_sdk': [0, 0, 1],
              'entrypoint_instance_index_explicitly_transported': True,
              'source_vertex_slots_use_original_mesh_frame': True,
              'vertex_shader': str(output), 'diagnostic_probe': str(probe),
              'actual_material_lighting_fragment_created': False,
              'complete_render_stages_qualified': False, 'actual_engine_uniform_binding_qualified': False,
              'renderer_installed': False, 'gpu_rendered': False, 'forge_editor_verified': False,
              'full_import': False, 'complete_assets_delivered_increment': 0,
              'limits': ['Vertex stages composed, not executed.', 'The probe fragment returns base color for type admission; it is not a render replacement.', 'Matching lighting/prepass fragments, actual host bindings and derivative sampling still require qualification.', 'Skinning/morph/motion/deferred routes remain refused by this static source stage.']}
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in result.items() if k != 'input_pins'}))


if __name__ == '__main__':
    main()
