"""Retain BC blocks with full decoded-mip checks and restored private importers."""
from pathlib import Path
from datetime import datetime,timezone
import ast,hashlib,json,subprocess,sys
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'house-compressed-storage-v641';PROJECT=W/'unity-library-source-thumbnail-house-bench-v2/project'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(v):
    v.update(updated_utc=datetime.now(timezone.utc).isoformat(),converted=False,full_import=False);p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
def prepare():
    assert not O.exists();assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
    cp=W/'house-native-texture-mips-checkpoint-v620/RESULT.v1.json';proof=read(cp);pins={}
    for key in ['input_pins','output_pins']:
        for p,h in proof[key].items():assert sha(p)==h,p;pins[p]=h
    ap=W/'house-dxc-surface-reference-v638/HELPER-OWNERSHIP.v1.json';audit=read(ap);assert len(audit['helpers'])==30
    assert not any((PROJECT/'Assets').rglob('*.dll')) and {str(p) for p in (PROJECT/'Assets').rglob('*.cs')}=={h['private_path'] for h in audit['helpers']}
    for h in audit['helpers']:assert sha(h['private_path'])==sha(h['controlled_source'])==h['sha256']
    copies=read(W/'house-dxc-surface-reference-v638/PREPARED.v1.json')['private_copies']
    for c in copies:assert sha(c['copy'])==c['private_before_sha256']
    O.mkdir();(O/'temp').mkdir();base=S/'candidates/HouseCompressedStorageV641.cs';helper=PROJECT/'Assets/Editor'/base.name;assert not helper.exists();helper.write_bytes(base.read_bytes());audit['helpers'].append(dict(private_path=str(helper),controlled_source=str(base),sha256=sha(helper)));audit.update(previous_audit=str(ap),checked_utc=datetime.now(timezone.utc).isoformat());owned=O/'HELPER-OWNERSHIP.v1.json';owned.write_text(json.dumps(audit,indent=2),'utf-8')
    shader=PROJECT/'Assets/Editor/HouseTextureMipsV619.compute';assert shader.exists()
    textures=read(W/'house-native-texture-mips-v619/NATIVE-RESULT.v1.json')['textures'];request=O/'REQUEST.v1.json';request.write_text(json.dumps(dict(output=str(O/'NATIVE-RESULT.v1.json'),shader_path=shader.relative_to(PROJECT).as_posix(),textures=[dict(guid=t['guid'],folder=str(O/t['guid']),levels=t['levels']) for t in textures]),indent=2),'utf-8')
    for p in [Path(__file__),cp,ap,base,helper,owned,shader,request,S/'run_gladiator_remaining_geometry_v400.py',S/'queue_delta_firepit_particle_behavior_v4.py']:pins[str(p)]=sha(p)
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,private_copies=copies,textures=18,mip_levels=216,private_readability_temporary=True,meta_bytes_restoration_required=True,all_decoded_mips_before_readable_after_readable_after_restoration_required=True,purchased_scripts_copied_or_executed=False,full_import=False),indent=2),'utf-8');save(dict(status='house_compressed_storage_prepared'))
    with (O/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid,one_shot=True)),'utf-8');print(json.dumps(dict(controller_pid=proc.pid,textures=18,mip_levels=216)))
def run():
    recipe=S/'run_gladiator_remaining_geometry_v400.py';tree=ast.parse(recipe.read_text('utf-8-sig'));nodes=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name=='run'];code=ast.unparse(ast.Module(body=nodes,type_ignores=[])).replace('OpenRPGGladiatorLotGeometryV400.Export','OpenRPGHouseCompressedStorageV641.Export')
    old="assert len(native['cases']) == 5 and sum((len(c['meshes']) for c in native['cases'])) == 13 and all((c['source_components_unchanged'] for c in native['cases']))";assert code.count(old)==1
    code=code.replace(old,"assert not state['private_importer_changes'] and len(native['textures'])==18 and all(t['native_decoded_mips_equal'] and t['restored_decoded_mips_equal'] and t['importer_restored_equal'] and t['meta_bytes_restored_equal'] and len(t['levels'])==12 for t in native['textures'])")
    code=code.replace('remaining_thirteen_meshes_native_capture_running','house_compressed_storage_running').replace('remaining_thirteen_meshes_and_five_root_material_snapshots_captured_review_pending','house_compressed_storage_captured_review_pending').replace('remaining_geometry_capture_refusal_retained','house_compressed_storage_refusal_retained').replace('new_meshes=13','native_textures=18')
    scope=dict(S=S,W=W,O=O,PROJECT=PROJECT,PS=PS,read=read,sha=sha,save=save);exec(compile(code,str(recipe)+'#house-native-BC-storage','exec'),scope);scope['run']()
if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
