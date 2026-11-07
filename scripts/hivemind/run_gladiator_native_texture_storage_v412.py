"""Capture twelve actual material textures, including uncompressed alpha/ORM."""
from pathlib import Path
from datetime import datetime,timezone
import ast,hashlib,json,subprocess,sys,shutil
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-native-texture-storage-v412'
PROJECT=W/'gladiator-native-geometry-v380/project';PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(v):
    v.update(updated_utc=datetime.now(timezone.utc).isoformat(),converted=False,full_import=False)
    p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
def prepare():
    assert not O.exists()
    assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
    seal=W/'gladiator-material-defaults-seal-v411/RESULT.v1.json'
    for p,h in read(seal)['input_pins'].items():assert sha(p)==h,p
    capture=W/'gladiator-remaining-native-geometry-v400/NATIVE-RESULT.v1.json'
    textures={}
    for c in read(capture)['cases']:
        for m in c['materials']:
            for t in m['textures']:
                guid=t.get('identity',{}).get('guid')
                if not guid:continue
                assert t['format'] in {'DXT1','DXT5','RGB24','RGBA32'} and not t['readable']
                if guid in textures:
                    for k in ['format','graphics_format','width','height','mips','filter_mode','aniso_level']:assert textures[guid][k]==t[k]
                textures[guid]=t
    assert len(textures)==12
    O.mkdir();(O/'temp').mkdir();(O/'private-before').mkdir()
    base=S/'candidates/SourceTownsmithCompressedStorageV2.cs';code=base.read_text('utf-8-sig')
    edits={
      'OpenRPGSourceTownsmithCompressedStorageV2':'OpenRPGGladiatorTextureStorageV412',
      'input.rows.Length!=8':'input.rows.Length!=12',
      'Eight compressed textures required':'Twelve original material textures required',
      'row.format!="DXT1"&&row.format!="DXT5"':'row.format!="DXT1"&&row.format!="DXT5"&&row.format!="RGB24"&&row.format!="RGBA32"',
      'if(texture.graphicsFormat.ToString()!=row.graphics_format':'if(texture.format.ToString()!=row.format||texture.graphicsFormat.ToString()!=row.graphics_format',
      'Math.Max(1,(w+3)/4)*Math.Max(1,(h+3)/4)*(row.format=="DXT1"?8:16)':'((row.format=="RGB24"||row.format=="RGBA32")?w*h*(row.format=="RGB24"?3:4):Math.Max(1,(w+3)/4)*Math.Max(1,(h+3)/4)*(row.format=="DXT1"?8:16))',
      'eight_compressed_native_mip_chains_extracted_in_isolated_readable_copy':'twelve_native_texture_mip_chains_extracted_in_isolated_readable_copy'
    }
    for a,b in edits.items():assert code.count(a)==1,a;code=code.replace(a,b)
    helper=PROJECT/'Assets/Editor/GladiatorTextureStorageV412.cs';assert not helper.exists();helper.write_text(code,'utf-8')
    compute=S/'candidates/SourceImportedTextureMipLoadV1.compute';target=PROJECT/'Assets/Editor'/compute.name
    if target.exists():assert sha(target)==sha(compute)
    else:shutil.copyfile(compute,target)
    request=O/'INPUT.v1.json';request.write_text(json.dumps(dict(rows=[dict(guid=g,authored_texture_index=i) for i,g in enumerate(sorted(textures))]),indent=2),'utf-8')
    pins={str(p):sha(p) for p in [Path(__file__),base,compute,target,helper,request,seal,capture,S/'run_gladiator_remaining_geometry_v400.py',S/'queue_delta_firepit_particle_behavior_v4.py']}
    copies=[]
    for r in read(W/'gladiator-lot-dependency-closure-v394/RESULT.v1.json')['files']:
        source=Path(r['source']);private=PROJECT/r['relative_project_path'];assert sha(source)==r['sha256']
        pins[str(source)]=r['sha256'];snap=O/'private-before'/r['relative_project_path'];snap.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(private,snap)
        copies.append(dict(source=str(source),copy=str(private),private_before_sha256=sha(private),snapshot=str(snap)))
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,private_copies=copies,expected_textures=textures,purchased_scripts_copied_or_executed=False),indent=2),'utf-8')
    save(dict(status='twelve_native_material_textures_prepared'))
    with (O/'controller.log').open('xb') as log:p=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=p.pid,one_shot=True)),'utf-8');print(json.dumps(dict(controller_pid=p.pid,textures=12)))
def run():
    recipe=S/'run_gladiator_remaining_geometry_v400.py';tree=ast.parse(recipe.read_text('utf-8'))
    nodes=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name=='run']
    code=ast.unparse(ast.Module(body=nodes,type_ignores=[]))
    code=code.replace('OpenRPGGladiatorLotGeometryV400.Export','OpenRPGGladiatorTextureStorageV412.Export')
    needle="assert len(native['cases']) == 5 and sum((len(c['meshes']) for c in native['cases'])) == 13 and all((c['source_components_unchanged'] for c in native['cases']))"
    assert code.count(needle)==1
    code=code.replace(needle,"assert len(native['rows']) == 12 and native['status'] == 'twelve_native_texture_mip_chains_extracted_in_isolated_readable_copy' and all(not r['error'] and r['settings_restored_equal'] and r['gpu_samples_bit_exact'] for r in native['rows'])")
    code=code.replace("UPM_CACHE_ROOT=str(W / 'unity-package-cache')","UPM_CACHE_ROOT=str(W / 'unity-package-cache'), OPENRPG_COMPRESSED_OUT=str(O)")
    assert 'OPENRPG_COMPRESSED_OUT=str(O)' in code
    code=code.replace('remaining_thirteen_meshes_native_capture_running','twelve_texture_native_storage_capture_running').replace('remaining_thirteen_meshes_and_five_root_material_snapshots_captured_review_pending','twelve_native_texture_chains_captured_review_pending').replace('new_meshes=13','native_textures=12')
    ns=dict(S=S,W=W,O=O,PROJECT=PROJECT,PS=PS,read=read,sha=sha,save=save)
    exec(compile(code,str(recipe)+'#native-texture-controller','exec'),ns);ns['run']()
if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
