"""Capture four source normal families using the existing Unity reference bench."""
from pathlib import Path
from datetime import datetime,timezone
import ast,hashlib,json,subprocess,sys,shutil
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-normal-reference-v415'
PROJECT=W/'gladiator-native-geometry-v380/project';PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe';OLD=W/'unity-villa-normal-sampling-reference-v1'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(v):
    v.update(updated_utc=datetime.now(timezone.utc).isoformat(),converted=False,full_import=False)
    p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
def prepare():
    assert not O.exists()
    assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
    kernels=W/'gladiator-material-kernels-v414/RESULT.v1.json';k=read(kernels)
    assert len(k['rows'])==6 and not k['failures']
    bindings={b['guid']:b for r in k['rows'] for b in r['source_texture_bindings'] if b['requires_normal_decode']};assert len(bindings)==4
    storage=W/'gladiator-native-texture-storage-v412/NATIVE-RESULT.v1.json';textures={r['guid']:r for r in read(storage)['rows']}
    seal=W/'gladiator-native-texture-seal-v413/RESULT.v1.json'
    for p,h in read(seal)['output_pins'].items():assert sha(p)==h,p
    O.mkdir();(O/'temp').mkdir()
    helper=PROJECT/'Assets/Editor/GladiatorNormalReferenceV415.cs';assert not helper.exists()
    code=(OLD/'NormalSamplingSnapshot.cs').read_text('utf-8-sig')
    code=code.replace('namespace OpenRPGNormalSamplingProbe','namespace OpenRPGGladiatorNormalSamplingProbeV415').replace('Assets/SourceTextures/','Assets/Hivemind/GladitorArena/HDRP(Default)/Art/Textures/')
    helper.write_text(code,'utf-8')
    shader=PROJECT/'Assets/Editor/GladiatorNormalReferenceV415.shader';assert not shader.exists();shutil.copyfile(OLD/'NormalSamplingReference.shader',shader)
    verifier=O/'verify_reference.py';code=(OLD/'verify_reference.py').read_text('utf-8-sig');assert code.count('==14')==1 and code.count("'verified_textures':14")==1
    verifier.write_text(code.replace('==14','==4').replace("'verified_textures':14","'verified_textures':4"),'utf-8')
    pins={str(p):sha(p) for p in [Path(__file__),kernels,storage,seal,helper,shader,verifier,OLD/'NormalSamplingSnapshot.cs',OLD/'NormalSamplingReference.shader',OLD/'verify_reference.py',S/'run_gladiator_remaining_geometry_v400.py',S/'queue_delta_firepit_particle_behavior_v4.py']}
    copies=[];cases=[];private=[]
    for r in read(W/'gladiator-lot-dependency-closure-v394/RESULT.v1.json')['files']:
        source=Path(r['source']);target=PROJECT/r['relative_project_path'];assert sha(source)==r['sha256'];pins[str(source)]=r['sha256'];pins[str(target)]=sha(target)
        private.append(dict(source=str(source),copy=str(target),private_before_sha256=sha(target)))
    for guid,b in sorted(bindings.items()):
        row=textures[guid];assert row['format']=='DXT5'
        original=next(Path(f['path']) for f in b['source_files'] if not f['path'].endswith('.meta'))
        origmeta=Path(str(original)+'.meta')
        relative=next(r['relative_project_path'] for r in read(W/'gladiator-lot-dependency-closure-v394/RESULT.v1.json')['files'] if r['source']==str(original));target=PROJECT/relative
        cases.append(dict(asset_id=guid,asset_path=relative,expected_guid=guid,expected_file_id='2800000'))
        copies.append(dict(original=str(original),original_sha256=sha(original),original_meta=str(origmeta),original_meta_sha256=sha(origmeta),project_copy=str(target),project_copy_sha256=sha(target),expected_storage_row=dict(asset_id=guid,unity_graphics_format=row['graphics_format'],width=row['width'],height=row['height'],mips=row['mips'],resolved_sampler=dict(filter_mode=row['filter_mode'],wrap_u=row['wrap_u'],wrap_v=row['wrap_v'],wrap_w=row['wrap_w'],anisotropy=row['aniso'],mip_bias=row['mip_bias']))))
    manifest=O/'REQUEST.v1.json';manifest.write_text(json.dumps(dict(schema_version=1,output_root=str(O/'export'),cases=cases),indent=2),'utf-8');shutil.copyfile(manifest,O/'manifest.json')
    pins[str(manifest)]=sha(manifest);pins[str(O/'manifest.json')]=sha(O/'manifest.json')
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,input_proofs=pins,private_copies=private,copies=copies,manifest_sha256=sha(O/'manifest.json'),expected_mips=52,purchased_scripts_copied_or_executed=False),indent=2),'utf-8')
    save(dict(status='four_source_normal_references_prepared'))
    with (O/'controller.log').open('xb') as log:p=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=p.pid,one_shot=True)),'utf-8');print(json.dumps(dict(controller_pid=p.pid,normal_textures=4,mips=52)))
def run():
    recipe=S/'run_gladiator_remaining_geometry_v400.py';tree=ast.parse(recipe.read_text('utf-8'))
    nodes=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name=='run'];code=ast.unparse(ast.Module(body=nodes,type_ignores=[]))
    code=code.replace('OpenRPGGladiatorLotGeometryV400.Export','OpenRPGGladiatorNormalSamplingProbeV415.NormalSamplingSnapshot.Export').replace("O / 'NATIVE-RESULT.v1.json'","O / 'export/RESULT.json'")
    needle="assert len(native['cases']) == 5 and sum((len(c['meshes']) for c in native['cases'])) == 13 and all((c['source_components_unchanged'] for c in native['cases']))";assert code.count(needle)==1
    code=code.replace(needle,"assert native['total'] == native['matched'] == 4 and native['failed'] == 0 and all(c['identity_matches'] for c in native['cases'])")
    code=code.replace('remaining_thirteen_meshes_native_capture_running','four_native_normal_references_running').replace('remaining_thirteen_meshes_and_five_root_material_snapshots_captured_review_pending','four_native_normal_references_captured_review_pending').replace('new_meshes=13','normal_textures=4')
    ns=dict(S=S,W=W,O=O,PROJECT=PROJECT,PS=PS,read=read,sha=sha,save=save);exec(compile(code,str(recipe)+'#normal-reference-controller','exec'),ns);ns['run']()
if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
