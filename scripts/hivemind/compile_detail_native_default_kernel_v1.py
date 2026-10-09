"""Compile the original Detail DAG with an explicit native default RG input.

No invented texture or guessed RG packet. Native surface equivalence evidence
is retained; actual host binding and complete rendered parity remain required.
"""
from pathlib import Path
import datetime
import hashlib
import json
import math
import subprocess
from generate_nested_material_wgsl import scalar, vec

W = Path('F:/OpenRPG_ImportWork')
ROOT = W/'detail-native-default-kernel-v1'
GUID = 'abc00000000012210656512993269337'

def read(path):
    return json.loads(path.read_text('utf-8-sig'))

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def once(text, old, new):
    assert text.count(old) == 1, old
    return text.replace(old, new)

def main():
    assert not ROOT.exists()
    topology_path = W/'nested-prefab-delta-material-active-ir-v1/RESULT.v1.json'
    source_row = next(row for row in read(topology_path)['rows'] if row['material_guid'] == GUID)
    ir_path = Path(source_row['output'])
    assert sha(ir_path) == source_row['output_sha256']
    ir = read(ir_path)
    native_path = W/'detail-mask-surface-independent-reopen-v3/RESULT.v1.json'
    native = read(native_path)
    assert native['native_surface_default_qualified'] and native['surface_data_only']
    assert all(row['black_control_changed_pixels'] >= 32 and row['maximum_null_white_error'] == 0
        for row in native['modes'].values())
    graph_path = W/'detail-mask-original-graph-paths-v1/RESULT.v1.json'
    graph = read(graph_path)
    emitter_path = W/'delta-vector-node-converter-v3/adapted-node-emitter.v3.py.txt'
    kernels = W/'native-hdrp-nine-source-programs-v1'
    kernel = kernels/'house-on-hill-source-shader-kernel-v1/source_house_graph_kernels.wgsl'
    extra = kernels/'house-on-hill-extra-node-kernels-v2/source_extra_nodes.wgsl'
    validator = kernels/'rock-surface-kernel-port-v1/target/debug/openrpg-rock-kernel-validator.exe'
    assert sha(validator) == 'bb928cfed4c84b4918fc306a23fae8818e8d5a0fc85b0f9444edbc6be9c55e1b'
    pins = {str(path):sha(path) for path in [topology_path, ir_path, native_path, graph_path,
        emitter_path, kernel, extra, validator, Path(__file__).resolve(), Path(__file__).with_name('generate_nested_material_wgsl.py')]}
    for path,digest in graph['input_pins'].items():
        assert sha(Path(path)) == digest
        pins[path] = digest
    defaults = [node for node in ir['nodes'] if node['opcode'] == 'SampleTexture2DNode'
        and next(item['value'] for item in node['inputs'] if item['name']=='Texture')['literal']['guid'] is None]
    assert len(defaults) == 2 and {node['output_slot_id'] for node in defaults} == {4,5}
    for node in defaults:
        texture = next(item['value'] for item in node['inputs'] if item['name']=='Texture')
        assert texture['provenance'] == 'material:_Mask'
        assert node['node_id'] == '740deec615ff4739a43dba4a747e6df2'
        assert texture['literal']['scale'] == [1,1] and texture['literal']['offset'] == [0,0]
    emitter = emitter_path.read_text('utf-8-sig')
    emitter = once(emitter, " elif op=='SampleTexture2DNode':", """ elif op=='SampleTexture2DNode' and ins['Texture']['literal']['guid'] is None:
  assert ins['Texture']['provenance']=='material:_Mask' and node['node_id']=='740deec615ff4739a43dba4a747e6df2'
  assert sid in (4,5) and p['m_TextureType']==0 and ins['Sampler']['literal'] is None
  assert native_default_contract['material_guid']==material_guid and native_default_contract['source_surface_evidence_qualified']
  input_requirements.append({'field':'native_default_mask_rg','width':2,'channel':'r' if sid==4 else 'g','host_qualified':False,'source_texture_reference_preserved':True})
  expr='vec4<f32>(i.native_default_mask_rg.'+('x' if sid==4 else 'y')+')';dim=1
 elif op=='SampleTexture2DNode':""")
    emitter = once(emitter, "fields=[('source_world_to_object',16)", "fields=[('native_default_mask_rg',2),('source_world_to_object',16)")
    resources = []
    normals = set()
    for node in ir['nodes']:
        for item in node['inputs']:
            literal = item['value'].get('literal')
            if isinstance(literal,dict) and 'guid' in literal:
                guid = literal['guid']
                if guid is None:
                    assert node in defaults
                    continue
                if guid not in resources:
                    resources.append(guid)
                if node['opcode']=='SampleTexture2DNode' and node['source_parameters']['m_TextureType']==1:
                    normals.add(guid)
    assert len(resources) == 2 and len(normals) == 1
    base = W/'hivemind-run/native-american-village'
    inventory_path = base/'source.v1.json'
    inventory = read(inventory_path)
    pins[str(inventory_path)] = sha(inventory_path)
    by_path = {row['stored_path'].replace('\\','/'):row for row in inventory['files']}
    bindings = []
    for guid in resources:
        asset_path = inventory['guid_paths'][guid]
        payloads = []
        for suffix in ['', '.meta']:
            relative = 'source/'+asset_path+suffix
            path = base/relative
            expected = by_path[relative]['sha256']
            assert sha(path) == expected
            pins[str(path)] = expected
            payloads.append(dict(path=str(path),sha256=expected))
        bindings.append(dict(guid=guid,source_files=payloads,requires_normal_decode=guid in normals,
            imported_storage_qualified=False,normal_decode_mode_qualified=False))
    ROOT.mkdir()
    adapted = ROOT/'explicit-default-node-emitter.py.txt'
    adapted.write_text(emitter,'utf-8')
    contract = dict(material_guid=GUID,source_surface_evidence_qualified=True,
        property='_Mask',channels=['r','g'],host_binding_qualified=False,
        missing_authored_guid=graph['property']['m_Value']['m_Guid'] if False else 'abc00000000001326820419954783426')
    context = dict(ir=ir,indices={guid:index for index,guid in enumerate(resources)},normal_guids=normals,
        dims={},body=[],input_requirements=[],prefix=kernel.read_text('utf-8-sig')+'\n'+extra.read_text('utf-8-sig'),
        case=ROOT,scalar=scalar,vec=vec,math=math,native_default_contract=contract,material_guid=GUID)
    exec(compile(emitter,str(adapted),'exec'),context)
    output = ROOT/'source_material.wgsl'
    with (ROOT/'naga.stdout.log').open('xb') as stdout,(ROOT/'naga.stderr.log').open('xb') as stderr:
        code = subprocess.run([str(validator),str(output)],stdout=stdout,stderr=stderr,
            creationflags=subprocess.CREATE_NO_WINDOW).returncode
    for path,digest in pins.items():
        assert sha(Path(path)) == digest
    result = dict(status='detail_original_dag_with_explicit_native_default_input_naga_validated' if code==0 else 'refused_or_unverified',
        actual_naga_exit_code=code,material_guid=GUID,active_nodes=len(ir['nodes']),
        original_terminals=len(ir['terminals']),source_texture_bindings=bindings,
        explicit_native_default_input=contract,input_requirements=context['input_requirements'],
        source_material=str(output),source_material_sha256=sha(output),input_pins=pins,
        adapted_emitter_sha256=sha(adapted),gpu_render_parity_qualified=False,
        host_binding_qualified=False,authored_texture_substituted=False,full_import=False,
        finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({key:value for key,value in result.items() if key not in {'input_pins','input_requirements','source_texture_bindings'}}))

if __name__ == '__main__':
    main()
