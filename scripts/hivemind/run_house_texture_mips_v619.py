"""Retain native GPU-decoded mip chains without recapturing proven mip zero."""
from pathlib import Path
from datetime import datetime,timezone
import ast,hashlib,json,subprocess,sys
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'house-native-texture-mips-v619';PROJECT=W/'unity-library-source-thumbnail-house-bench-v2/project'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(v):
    v.update(updated_utc=datetime.now(timezone.utc).isoformat(),converted=False,full_import=False);p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
def prepare():
    assert not O.exists()
    assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
    checkpoint=W/'house-full-surface-numeric-checkpoint-v618/RESULT.v1.json';proof=read(checkpoint);assert proof['failed_components']==0
    pins=dict(proof['input_pins'])
    for p,h in pins.items():assert sha(p)==h,p
    ap=W/'house-full-surface-reference-v616/HELPER-OWNERSHIP.v1.json';audit=read(ap);assert len(audit['helpers'])==22
    assert not any((PROJECT/'Assets').rglob('*.dll')) and {str(p) for p in (PROJECT/'Assets').rglob('*.cs')}=={h['private_path'] for h in audit['helpers']}
    for h in audit['helpers']:assert sha(h['private_path'])==sha(h['controlled_source'])==h['sha256']
    nativepath=W/'house-full-surface-reference-v616/NATIVE-RESULT.v1.json';native=read(nativepath);textures={}
    for material in native['materials']:
        for t in material['textures']:
            if t['guid'] in textures:
                old=textures[t['guid']];assert all(old[k]==t[k] for k in ['width','height','mips','format','graphics_format']);assert sha(old['raw_output'])==sha(t['raw_output'])
            else:textures[t['guid']]=t
    assert textures and all(t['mips']>1 for t in textures.values())
    copies=read(W/'house-full-surface-reference-v616/PREPARED.v1.json')['private_copies']
    for row in copies:assert sha(row['copy'])==row['private_before_sha256']
    O.mkdir();(O/'temp').mkdir();controlled=O/'HouseTextureMipsV619.compute';controlled.write_text('''#pragma kernel CopyMip
Texture2D<float4> _Texture;RWStructuredBuffer<float4> _Output;uint _Width,_Height,_Mip;
[numthreads(8,8,1)]void CopyMip(uint3 id:SV_DispatchThreadID){if(id.x>=_Width||id.y>=_Height)return;_Output[id.y*_Width+id.x]=_Texture.Load(int3(id.xy,_Mip));}
''','utf-8');dst=PROJECT/'Assets/Editor'/controlled.name;assert not dst.exists();dst.write_bytes(controlled.read_bytes())
    base=S/'candidates/HouseTextureMipsV619.cs';helper=PROJECT/'Assets/Editor'/base.name;assert not helper.exists();helper.write_bytes(base.read_bytes());audit['helpers'].append(dict(private_path=str(helper),controlled_source=str(base),sha256=sha(helper)));audit.update(previous_audit=str(ap),checked_utc=datetime.now(timezone.utc).isoformat());ap2=O/'HELPER-OWNERSHIP.v1.json';ap2.write_text(json.dumps(audit,indent=2),'utf-8')
    cases=[]
    for guid,t in sorted(textures.items()):
        folder=O/guid;folder.mkdir();pins[t['raw_output']]=sha(t['raw_output']);cases.append(dict(guid=guid,folder=str(folder),mip0_path=t['raw_output'],width=t['width'],height=t['height'],mips=t['mips'],format=t['format'],graphics_format=t['graphics_format']))
    request=O/'REQUEST.v1.json';request.write_text(json.dumps(dict(output=str(O/'NATIVE-RESULT.v1.json'),shader_path=dst.relative_to(PROJECT).as_posix(),textures=cases),indent=2),'utf-8')
    for p in [Path(__file__),checkpoint,ap,nativepath,controlled,dst,base,helper,ap2,request,S/'run_gladiator_remaining_geometry_v400.py',S/'queue_delta_firepit_particle_behavior_v4.py']:pins[str(p)]=sha(p)
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,private_copies=copies,textures_expected=len(cases),mip_levels_expected=sum(c['mips'] for c in cases),mip_zero_reused_without_recapture=True,purchased_scripts_copied_or_executed=False,full_import=False,implicit_derivative_render_verified=False),indent=2),'utf-8');save(dict(status='house_native_mips_prepared'))
    with (O/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid,one_shot=True)),'utf-8');print(json.dumps(dict(controller_pid=proc.pid,textures=len(cases),mip_levels=sum(c['mips'] for c in cases))))
def run():
    recipe=S/'run_gladiator_remaining_geometry_v400.py';tree=ast.parse(recipe.read_text('utf-8-sig'));nodes=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name=='run'];code=ast.unparse(ast.Module(body=nodes,type_ignores=[])).replace('OpenRPGGladiatorLotGeometryV400.Export','OpenRPGHouseTextureMipsV619.Export')
    old="assert len(native['cases']) == 5 and sum((len(c['meshes']) for c in native['cases'])) == 13 and all((c['source_components_unchanged'] for c in native['cases']))";assert code.count(old)==1
    code=code.replace(old,"assert len(native['textures'])==prep['textures_expected'] and sum(len(t['levels']) for t in native['textures'])==prep['mip_levels_expected'] and native['source_texture_settings_unchanged'] and native['graphics_api']=='Direct3D11' and all(Path(m['path']).stat().st_size==m['bytes']==m['width']*m['height']*16 for t in native['textures'] for m in t['levels'])")
    code=code.replace('remaining_thirteen_meshes_native_capture_running','house_native_mips_running').replace('remaining_thirteen_meshes_and_five_root_material_snapshots_captured_review_pending','house_native_mips_captured_review_pending').replace('remaining_geometry_capture_refusal_retained','house_native_mips_refusal_retained').replace('new_meshes=13',"native_mip_levels=prep['mip_levels_expected']")
    scope=dict(S=S,W=W,O=O,PROJECT=PROJECT,PS=PS,read=read,sha=sha,save=save);exec(compile(code,str(recipe)+'#house-native-mip-chain','exec'),scope);scope['run']()
if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
