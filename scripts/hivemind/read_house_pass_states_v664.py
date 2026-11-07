"""Preserve generated pass states and compare native render properties to source."""
from pathlib import Path
import datetime
import hashlib
import json
import re
import struct

W = Path('F:/OpenRPG_ImportWork')
O = W / 'house-native-pass-states-v664'
assert not O.exists()

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def bits(value):
    return struct.pack('<f', float(value))

contract_path = W / 'house-render-contracts-v661/RESULT.v1.json'
capture_path = W / 'house-generated-material-contract-v609/NATIVE-RESULT.v1.json'
shader_path = capture_path.with_name('source-generated.shader')
audit_path = W / 'house-generated-contract-audit-v610/RESULT.v1.json'
contracts, capture, audit = read(contract_path), read(capture_path), read(audit_path)
pins = {str(p): sha(p) for p in [contract_path, capture_path, shader_path, audit_path, Path(__file__).resolve()]}
for proof in [contracts, audit]:
    for path, digest in proof['input_pins'].items():
        assert sha(path) == digest, path
        pins[path] = digest
assert capture['source_materials_unchanged']
text = shader_path.read_text('utf-8-sig')
passes = []
for match in re.finditer(r'\bPass\s*\{', text):
    opening = text.index('{', match.start())
    depth = 1
    for i in range(opening + 1, len(text)):
        depth += (text[i] == '{') - (text[i] == '}')
        if depth == 0:
            block = text[opening + 1:i]
            break
    else:
        raise ValueError('Unclosed generated pass')
    name = re.search(r'\bName\s+"([^"]+)"', block)
    assert name
    header = block.split('HLSLPROGRAM', 1)[0]
    states = {}
    for key in ['Cull', 'Blend', 'ZTest', 'ZWrite', 'ColorMask', 'AlphaToMask']:
        values = re.findall(r'^\s*' + key + r'\s+([^\r\n]+)', header, re.M)
        if values:
            states[key] = [value.strip() for value in values]
    passes.append(dict(name=name[1], authored_states=states, original_header=header))
assert len({p['name'] for p in passes}) == len(passes)
for name in ['Forward', 'DepthOnly', 'ShadowCaster']:
    assert sum(p['name'] == name for p in passes) == 1
native = {m['guid']: m for m in capture['materials']}
fields = ['_SurfaceType', '_AlphaCutoffEnable', '_DoubleSidedEnable', '_CullMode', '_CullModeForward', '_ZWrite', '_ZTestDepthEqualForOpaque', '_ZTestGBuffer', '_SrcBlend', '_DstBlend', '_AlphaSrcBlend', '_AlphaDstBlend']
rows = []
for source in contracts['rows']:
    m = native[source['material_guid']]
    props = {p['name']: p for p in m['properties']}
    resolved = {name: props[name]['value_float'] for name in fields}
    for name, value in resolved.items():
        assert bits(value) == bits(source['source_floats'][name]), (source['material_guid'], name)
    assert resolved['_SurfaceType'] == 0 and resolved['_ZWrite'] == 1
    assert set(m['keywords']) == set(source['source_valid_keywords'])
    assert '_DEPTHOFFSET_ON' in m['keywords']
    assert m['render_queue'] == source['source_custom_render_queue']
    rows.append(dict(material_guid=source['material_guid'], native_resolved_state=resolved, native_render_queue=m['render_queue'], generated_pass_states=passes, original_source_contract=source, omitted_pass_states_require_default_resolution=True, indexed_blends_and_masks_preserved=True, stencil_headers_preserved=True, actual_pipeline_admission=False))
assert len(rows) == 6
assert all(sha(path) == digest for path, digest in pins.items())
O.mkdir()
result = dict(schema='hivemind.house-native-pass-states.v664', utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), materials=6, generated_passes=len(passes), native_render_scalar_checks=6*len(fields), rows=rows, input_pins=pins, renderer_installed=False, full_import=False)
(O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
print(json.dumps(dict(materials=6, generated_passes=len(passes), render_scalar_checks=6*len(fields), receipt=str(O / 'RESULT.v1.json'))))
