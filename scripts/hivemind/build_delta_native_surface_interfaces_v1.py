"""Carry original Delta UVs into native displaced surface stages, without a Lit substitute."""
from pathlib import Path
import datetime
import hashlib
import json
import re
import subprocess

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'delta-native-surface-interfaces-v1'
OLD = W / 'native-hdrp-nine-source-programs-v1/source-graph-native-surface-bridge-v1'


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def split_arguments(text):
    depth = 0
    start = 0
    output = []
    for index, char in enumerate(text):
        if char == '(':
            depth += 1
        elif char == ')':
            depth -= 1
        elif char == ',' and depth == 0:
            output.append(text[start:index])
            start = index + 1
        assert depth >= 0
    assert depth == 0
    output.append(text[start:])
    return output


def surface_evaluator(program, ir):
    """Select exact emitted statements; never execute vertex displacement in fragment."""
    nodes = {node['id']: node for node in ir['nodes']}
    assert len(nodes) == len(ir['nodes'])
    required, visiting = set(), set()

    def walk(value):
        if 'ref' not in value:
            return
        identity = value['ref']
        assert identity in nodes and identity not in visiting
        if identity in required:
            return
        visiting.add(identity)
        for entry in nodes[identity]['inputs']:
            walk(entry['value'])
        visiting.remove(identity)
        required.add(identity)

    surface_terminals = {key: value for key, value in ir['terminals'].items()
                         if key.startswith('SurfaceDescription.')}
    assert len(surface_terminals) == 10
    for value in surface_terminals.values():
        walk(value)
    fields = re.search(r'struct SourceMaterialOutputs \{ (.*?)\}', program, re.S).group(1).split(',')
    fields = [field for field in fields if field.strip()]
    assert [field.split(':')[0] for field in fields[:3]] == ['position', 'normal', 'tangent']
    head = 'fn evaluate_original_source_material(i:SourceMaterialInputs)->SourceMaterialOutputs {\n'
    assert program.count(head) == 1
    body, rest = program.split(head, 1)[1].split('\nreturn SourceMaterialOutputs(', 1)
    arguments = split_arguments(rest.split(');\n}', 1)[0])
    assert len(arguments) == len(fields) == 13
    selected, declared = [], set()
    for line in body.splitlines():
        if not line.strip():
            continue
        match = re.fullmatch(r'let (?:n|pom)(\d+)=.*;', line)
        assert match, 'Unrecognized source statement'
        declared.add(match[1])
        if match[1] in required:
            selected.append(line)
    assert required <= declared
    used = {identity for expression in arguments[3:]
            for identity in re.findall(r'\bn(\d+)\b', expression)}
    assert used <= required, 'Surface terminal/output correspondence refused'
    evaluator = ('\nstruct SourceMaterialSurfaceOutputs { ' + ','.join(fields[3:]) + ',}\n'
                 'fn evaluate_original_source_surface(i:SourceMaterialInputs)->SourceMaterialSurfaceOutputs {\n'
                 + '\n'.join(selected) + '\nreturn SourceMaterialSurfaceOutputs('
                 + ','.join(arguments[3:]) + ');\n}\n')
    dependencies = sorted(required, key=int)
    return evaluator, dependencies


def main():
    assert not ROOT.exists()
    parent_path = W / 'delta-vector-bevy-vertex-stages-v2/RESULT.v1.json'
    parent, old = read(parent_path), read(OLD / 'RESULT.v1.json')
    assert parent['native_vertex_programs_composed'] == 6 and not parent['failures']
    interface_path = OLD / 'source_graph_native_surface_interface.wgsl'
    assert sha(interface_path) == old['native_interface_sha256']
    interface = interface_path.read_text('utf-8-sig')
    declarations = interface.split('// SPDX-License-Identifier: MIT', 1)[0]
    declarations = declarations.replace('@location(8) source_uv0:vec4<f32>,',
        '@location(8) source_uv0:vec4<f32>,\n    @location(9) source_uv1:vec4<f32>,')
    declarations = declarations.replace('@location(10) source_uv0:vec4<f32>,',
        '@location(10) source_uv0:vec4<f32>,\n    @location(11) source_uv1:vec4<f32>,')
    bridge = interface.split('#ifdef PREPASS_PIPELINE\nalias SourceGraphSurfaceVertex=', 1)[1]
    bridge = '#ifdef PREPASS_PIPELINE\nalias SourceGraphSurfaceVertex=' + bridge
    bridge = bridge.replace('HouseOutputs', 'SourceMaterialSurfaceOutputs')
    bridge = bridge.replace('source_house_material(', 'evaluate_original_source_surface(')
    bridge = bridge.replace(' packet.source_vertex_color=in.color;',
        ' packet.source_vertex_color=vec4<f32>(0.0);\n packet.source_uv1=in.source_uv1;\n packet.target_world_from_object=world_from_local;')
    topology_path = W / 'nested-prefab-delta-material-active-ir-v1/RESULT.v1.json'
    ir_rows = {row['material_guid']: row for row in read(topology_path)['rows']}
    validator = Path(parent['validator']['path']) if isinstance(parent.get('validator'), dict) else OLD / 'validate_bevy_vertex.exe'
    # This is the original 13-module validator preserved by the bridge receipt.
    validator = OLD / 'validate_bevy_vertex.exe'
    modules = OLD / 'module-list.txt'
    pins = {str(path): sha(path) for path in [parent_path, OLD / 'RESULT.v1.json',
        interface_path, validator, modules, topology_path, Path(__file__).resolve()]}
    vertex_old = read(W / 'native-hdrp-nine-source-programs-v1/source-graph-bevy-vertex-wrapper-v4/RESULT.v1.json')
    assert sha(validator) == vertex_old['validator_sha256']
    for module in old['bevy_primary_module_pins']:
        assert sha(module['copy']) == module['sha256']
        pins[module['copy']] = module['sha256']
    assert set(modules.read_text().splitlines()) == set(row['copy'] for row in old['bevy_primary_module_pins'])
    configs = {'forward': [], 'depth': ['PREPASS_PIPELINE', 'DEPTH_PREPASS'],
        'normal_depth': ['PREPASS_PIPELINE', 'DEPTH_PREPASS', 'NORMAL_PREPASS', 'NORMAL_PREPASS_OR_DEFERRED_PREPASS'],
        'ortho_normal_depth': ['PREPASS_PIPELINE', 'DEPTH_PREPASS', 'NORMAL_PREPASS', 'NORMAL_PREPASS_OR_DEFERRED_PREPASS', 'UNCLIPPED_DEPTH_ORTHO_EMULATION']}
    ROOT.mkdir()
    rows, failures = [], []
    for row in parent['rows']:
        case = ROOT / row['material_guid']
        case.mkdir()
        try:
            source_path, ir_path = Path(row['shader']), Path(ir_rows[row['material_guid']]['output'])
            assert sha(source_path) == row['sha256']
            assert sha(ir_path) == ir_rows[row['material_guid']]['output_sha256']
            pins[str(source_path)], pins[str(ir_path)] = sha(source_path), sha(ir_path)
            text, ir = source_path.read_text('utf-8-sig'), read(ir_path)
            evaluator, dependencies = surface_evaluator(text, ir)
            marker = '// SPDX-License-Identifier: MIT\n// Native static source-graph vertex candidate.'
            assert text.count(marker) == 1
            prefix, wrapper = text.split(marker, 1)
            wrapper = marker + wrapper
            wrapper = wrapper.replace(',forward_io::VertexOutput', '')
            wrapper = wrapper.replace('#import bevy_pbr::prepass_io::VertexOutput as SourceGraphPrepassOutput\n', '')
            wrapper = re.sub(r'\bVertexOutput\b', 'SourceGraphForwardOutput', wrapper)
            # Original bridge retains normal/tangent in depth-only passes too.
            guard = '#ifdef NORMAL_PREPASS_OR_DEFERRED_PREPASS\n out.world_normal=frame.world_normal;\n#ifdef VERTEX_TANGENTS\n out.world_tangent=frame.world_tangent;\n#endif\n#endif'
            assert wrapper.count(guard) == 1
            wrapper = wrapper.replace(guard, ' out.world_normal=frame.world_normal;\n#ifdef VERTEX_TANGENTS\n out.world_tangent=frame.world_tangent;\n#endif')
            wrapper = wrapper.replace(' out.world_position=mesh_functions::mesh_position_local_to_world',
                ' out.source_uv0=vertex.source_uv0;\n out.source_uv1=vertex.source_uv1;\n out.world_position=mesh_functions::mesh_position_local_to_world')
            wrapper = wrapper.replace(' out.world_position=frame.world_position;',
                ' out.source_uv0=frame.source_uv0;\n out.source_uv1=frame.source_uv1;\n out.world_position=frame.world_position;')
            assert wrapper.count('out.source_uv0=') == wrapper.count('out.source_uv1=') == 2
            native = case / 'native_vertex_and_surface_functions.wgsl'
            native.write_text(prefix + evaluator + declarations + wrapper + bridge, 'utf-8')
            qualifier = case / 'surface_interface_qualification.wgsl'
            qualifier.write_text(native.read_text() + '\n// Legality probe, never an installed material.\n@fragment fn surface_probe(in:SourceGraphSurfaceVertex)->@location(0)vec4<f32>{let s=source_graph_surface_from_native_frame(in);return vec4<f32>(s.base_color,s.alpha);}\n', 'utf-8')
            receipts = []
            for name, defines in configs.items():
                with (case / (name + '.stdout.log')).open('xb') as out, (case / (name + '.stderr.log')).open('xb') as err:
                    process = subprocess.run([str(validator), str(modules), str(qualifier), '!VERTEX_COLORS', *defines], stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
                receipt = dict(permutation=name, actual_exit_code=process.returncode,
                    stdout_sha256=sha(case / (name + '.stdout.log')), stderr_sha256=sha(case / (name + '.stderr.log')))
                receipts.append(receipt)
                assert process.returncode == 0, (case / (name + '.stderr.log')).read_text()[:2400]
            rows.append(dict(material_guid=row['material_guid'], native=str(native), native_sha256=sha(native),
                qualifier=str(qualifier), qualifier_sha256=sha(qualifier), surface_dependency_nodes=dependencies,
                actual_compositions=receipts, raw_source_uv0_and_uv1_interpolated=True,
                surface_evaluator_excludes_vertex_wind=True, fragment_lighting_adapter_present=False))
        except Exception as error:
            failures.append(dict(material_guid=row['material_guid'], error=str(error)))
    assert all(sha(path) == digest for path, digest in pins.items())
    result = dict(schema='hivemind.delta-native-surface-interfaces.v1', observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        requested=6, native_surface_interfaces_composed=len(rows), actual_positive_compositions=sum(len(row['actual_compositions']) for row in rows),
        rows=rows, failures=failures, input_pins=pins, qualifier_must_never_be_installed=True,
        gpu_rendered=False, renderer_installed=False, source_texture_storage_qualified=False, full_import=False)
    with (ROOT / 'RESULT.v1.json').open('x', encoding='utf-8') as stream:
        json.dump(result, stream, indent=2)
    print(json.dumps(dict(composed=len(rows), failures=failures, receipt=str(ROOT / 'RESULT.v1.json'))))


if __name__ == '__main__':
    main()
