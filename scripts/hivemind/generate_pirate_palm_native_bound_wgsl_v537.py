"""Generate WGSL with an explicit measured SDK resource, preserving source provenance."""
from pathlib import Path
import hashlib, json
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork')
I=W/'pirate-palm-wgsl-input-v537';O=W/'pirate-palm-wgsl-v537'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not I.exists() and not O.exists()
bp=W/'pirate-palm-native-default-binding-v536/RESULT.v1.json';binding=read(bp)
assert binding['status']=='native_sdk_white_bytes_reopened_and_explicit_orm_binding_overlay_retained'
assert sha(binding['effective_ir_path'])==binding['effective_ir_sha256']
assert sha(binding['resource']['path'])==binding['resource']['sha256']
pins=dict(binding['input_pins'])
for p,h in pins.items():assert sha(p)==h,p
generator=S/'generate_nested_material_wgsl.py'
emitter=W/'dungeon-original-material-wgsl-v1/adapted-node-emitter.py.txt'
for p in [generator,emitter,bp,Path(__file__),Path(binding['effective_ir_path']),Path(binding['resource']['path'])]:pins[str(p)]=sha(p)
source_emitter=emitter.read_text('utf-8-sig')
needle="index=indices[t['guid']]";assert source_emitter.count(needle)==1
source_emitter=source_emitter.replace(needle,"index=indices[t['guid'] if 'guid' in t else t['native_sdk_resource']['key']]")
code=generator.read_text('utf-8')
for old,new in [
    ('ROOT = W / "nested-prefab-material-wgsl-v1"','ROOT = W / "pirate-palm-wgsl-v537"'),
    ('EMITTER = W / "dungeon-original-material-wgsl-v1/adapted-node-emitter.py.txt"','EMITTER = W / "pirate-palm-wgsl-input-v537/native-resource-emitter.py.txt"'),
    ('batch_path = W / "nested-prefab-material-active-ir-v1/RESULT.v1.json"','batch_path = W / "pirate-palm-wgsl-input-v537/BATCH.v1.json"'),
    ('batch["compiled_materials"] != 16','batch["compiled_materials"] != 1'),
    ('Closed sixteen-material active topology required','Exact native-bound palm topology required'),
    ('"total": 16','"total": 1'),
    ('"requested_materials": 16','"requested_materials": 1'),
    ('"existing_node_emitter_unchanged": True','"existing_node_emitter_unchanged": False, "emitter_change": "Select an explicit measured SDK resource key for the native ORM default; all node equations unchanged"'),
]:
    assert old in code,old;code=code.replace(old,new)
needle='                    if not isinstance(literal, dict) or "guid" not in literal:'
addition="""                    if isinstance(literal,dict) and 'native_sdk_resource' in literal:
                        key=literal['native_sdk_resource']['key']
                        assert key==native_binding['resource']['key'] and node['source_parameters'].get('m_TextureType',0)==0
                        if key not in resources:resources.append(key)
                        continue
"""+needle
assert code.count(needle)==1;code=code.replace(needle,addition)
needle='            for guid in resources:\n                asset_path = manifest["guid_paths"][guid]'
addition="""            for guid in resources:
                if guid==native_binding['resource']['key']:
                    resource=native_binding['resource']
                    assert sha(resource['path'])==resource['sha256']
                    pins[resource['path']]=resource['sha256']
                    bindings.append(dict(resource_key=guid,kind='measured_native_sdk_default',source_files=[dict(path=resource['path'],sha256=resource['sha256'])],native_resource=resource,original_missing_graph_reference=native_binding['original_missing_reference'],requires_normal_decode=False,imported_storage_qualified=False,shader_sampling_gpu_qualified=False))
                    continue
                asset_path = manifest["guid_paths"][guid]"""
assert code.count(needle)==1;code=code.replace(needle,addition)
code=code.replace('    emitter = EMITTER.read_text("utf-8-sig")','    native_binding=read(W/"pirate-palm-native-default-binding-v536/RESULT.v1.json")\n    emitter = EMITTER.read_text("utf-8-sig")')
I.mkdir()
adapted=I/'native-resource-emitter.py.txt';adapted.write_text(source_emitter,'utf-8');pins[str(adapted)]=sha(adapted)
configured=I/'configured_existing_generator.py';configured.write_text(code,'utf-8');pins[str(configured)]=sha(configured)
batch=dict(status='material_active_topology_compiled',compiled_materials=1,source_pins=pins,rows=[dict(pack_id='hivemind.pirate-island',material_guid='abc00000000004284671107665666980',output=binding['effective_ir_path'],output_sha256=binding['effective_ir_sha256'])])
(I/'BATCH.v1.json').write_text(json.dumps(batch,indent=2),'utf-8')
exec(compile(code,str(generator)+'#explicit-measured-sdk-binding','exec'),globals())
