"""Reopen captured SDK bytes and retain an explicit native binding overlay for ORM."""
from pathlib import Path
from datetime import datetime, timezone
import base64, copy, hashlib, json, re
W=Path('F:/OpenRPG_ImportWork');O=W/'pirate-palm-native-default-binding-v536';B=W/'pirate-palm-native-contract-v535'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
prep=read(B/'PREPARED.v1.json');run=read(B/'RUN-RESULT.v1.json');native=read(B/'NATIVE-RESULT.v1.json')
assert run['status']=='palm_native_shader_generated_contract_review_pending' and run['unity_exit_code']==0
assert not run['source_changes'] and not run['private_importer_changes'] and native['source_materials_unchanged']
assert sha(B/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256']
assert native['sdk_white_pixels_checked']
assert (native['sdk_white_width'],native['sdk_white_height'],native['sdk_white_mips'])==(4,4,1)
assert native['sdk_white_format']=='RGBA32' and native['sdk_white_graphics_format']=='R8G8B8A8_SRGB'
payload=base64.b64decode(native['sdk_white_base64'],validate=True)
assert len(payload)==64 and set(payload)=={255}
props={p['name']:p for p in native['materials'][0]['properties']};orm=props['_ORMMap']
assert orm['texture_null'] and orm['default_texture_name']=='white'
shader=(B/'source-generated.shader').read_text('utf-8')
assert re.search(r'_ORMMap\("ORMMap",\s*2D\)\s*=\s*"white"\s*\{\}',shader)
manifest_path=W/'hivemind-run/pirate-island/source.v1.json';manifest=read(manifest_path)
missing='34307d11fc1ff8d46a03b56ec522f4a7';assert missing not in manifest['guid_paths']
ip=W/'pirate-palm-active-material-ir-v531/RESULT.v1.json';proof=read(ip)
assert sha(proof['ir_path'])==proof['ir_sha256'];original=read(proof['ir_path']);ir=copy.deepcopy(original)
key='sdk-native:unity-white:'+hashlib.sha256(payload).hexdigest()
overlays=[]
for node in ir['nodes']:
    if node['opcode']!='SampleTexture2DNode':continue
    value=next(i['value'] for i in node['inputs'] if i['name']=='Texture')
    if value.get('provenance')!='source-property-default:_ORMMap':continue
    declared=json.loads(value['literal']['m_SerializedTexture'])['texture']
    assert declared['guid']==missing and declared['fileID']==2800000
    overlays.append(dict(node_id=node['node_id'],ir_id=node['id'],original_declared_value=copy.deepcopy(value),native_default_property=orm))
    value['original_declared_value']=copy.deepcopy(value)
    value['literal']=dict(native_sdk_resource=dict(key=key,kind='native_sdk_declared_white_default',receipt=str(B/'NATIVE-RESULT.v1.json')),scale=[orm['texture_scale'][k] for k in 'xy'],offset=[orm['texture_offset'][k] for k in 'xy'])
assert len(overlays)==3 and len(ir['nodes'])==len(original['nodes'])==36 and ir['terminals']==original['terminals']
ir['native_binding_overlay']=dict(original_ir=proof['ir_path'],original_ir_sha256=proof['ir_sha256'],missing_authored_graph_texture_guid_preserved=missing,resource_key=key,source_version_render_equivalence_qualified=False,shader_sampling_gpu_qualified=False)
pins=dict(prep['input_pins'])
for p,h in pins.items():assert sha(p)==h,p
for r in prep['private_copies']:assert sha(r['copy'])==r['private_before_sha256'],r['copy']
for p in [Path(__file__),B/'PREPARED.v1.json',B/'RUN-RESULT.v1.json',B/'NATIVE-RESULT.v1.json',B/'source-generated.shader',manifest_path,ip,Path(proof['ir_path'])]:pins[str(p)]=sha(p)
O.mkdir();raw=O/'native-sdk-white.rgba32';raw.write_bytes(payload)
effective=O/'native-bound-material-ir.json';effective.write_text(json.dumps(ir,indent=2),'utf-8')
result=dict(status='native_sdk_white_bytes_reopened_and_explicit_orm_binding_overlay_retained',resource=dict(key=key,path=str(raw),sha256=sha(raw),width=4,height=4,mips=1,format='RGBA32',graphics_format='R8G8B8A8_SRGB',origin='captured_Unity_Texture2D.whiteTexture',cpu_pixels_verified=True,shader_sampling_gpu_qualified=False),native_property=orm,original_missing_reference=missing,binding_overlays=overlays,effective_ir_path=str(effective),effective_ir_sha256=sha(effective),original_source_ir_unchanged=True,input_pins=pins,artist_texture_generated=False,full_import=False,thumbnail_admission_qualified=False,finished_utc=datetime.now(timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(status=result['status'],native_bytes=64,binding_overlays=3,full_import=False)))
