"""Capture SDK float POM with actual House textures; not full material qualification."""
from pathlib import Path
from datetime import datetime,timezone
import ast,hashlib,json,struct,subprocess,sys
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'house-pom-native-reference-v606'
PROJECT=W/'unity-library-source-thumbnail-house-bench-v2/project'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(v):
    v.update(updated_utc=datetime.now(timezone.utc).isoformat(),converted=False,full_import=False)
    p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
def prepare():
    assert not O.exists()
    assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
    proofpath=W/'house-main-material-lot-v603/RESULT.v1.json';proof=read(proofpath)
    assert proof['materials_compiled']==6 and not proof['errors']
    pins=dict(proof['input_pins'])
    for p,h in pins.items():assert sha(p)==h,p
    pool=list((S.parent).rglob('*.cs'))+[W/'hivemind-prefab-instance-source-closure-v1/PrefabHierarchySnapshot.cs']
    for folder in W.iterdir():
        if folder.is_dir() and folder.name.startswith(('house-','unity-library-source-thumbnail-house')):pool.extend(folder.glob('*.cs'))
    index={}
    for p in pool:index.setdefault(sha(p),[]).append(p)
    helpers=[]
    for p in (PROJECT/'Assets').rglob('*.cs'):
        h=sha(p);assert h in index,('Unowned helper',str(p));controlled=index[h][0]
        helpers.append(dict(private_path=str(p),controlled_source=str(controlled),sha256=h));pins[str(p)]=h;pins[str(controlled)]=h
    assert len(helpers)==18 and not any((PROJECT/'Assets').rglob('*.dll'))
    queuepath=W/'queue/house-on-a-hill/prefabs.json';queue=read(queuepath);pins[str(queuepath)]=sha(queuepath)
    sourcequeue=W/'house-main-material-lot-v603/SOURCE-QUEUE.v1.json';materials=read(sourcequeue)['materials'];pins[str(sourcequeue)]=sha(sourcequeue)
    sdk=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/BuiltInPackages/com.unity.render-pipelines.core/ShaderLibrary/PerPixelDisplacement.hlsl');pins[str(sdk)]=sha(sdk)
    copies=[]
    for m in materials.values():
        for rel in [m['source'],queue['guid_paths'][m['textures']['_L1_Height']['guid']]]:
            for suffix in ['', '.meta']:
                original=Path(queue['source_root'])/(rel+suffix);private=PROJECT/(rel+suffix)
                assert original.is_file() and private.is_file(),str(private)
                pins[str(original)]=sha(original);copies.append(dict(source=str(original),copy=str(private),source_sha256=sha(original),private_before_sha256=sha(private)))
    O.mkdir();(O/'temp').mkdir()
    for name in ['HousePomReferenceV606.cs','HousePomReferenceV606.compute']:
        source=S/'candidates'/name;dst=PROJECT/'Assets/Editor'/name;assert not dst.exists();dst.write_bytes(source.read_bytes());pins[str(source)]=sha(source);pins[str(dst)]=sha(dst)
        if name.endswith('.cs'):helpers.append(dict(private_path=str(dst),controlled_source=str(source),sha256=sha(dst)))
    audit=O/'HELPER-OWNERSHIP.v1.json';audit.write_text(json.dumps(dict(helpers=helpers,asset_dll_found=False,all_asset_cs_owned=True,checked_utc=datetime.now(timezone.utc).isoformat()),indent=2),'utf-8');pins[str(audit)]=sha(audit)
    uvs=[[-.01,.125],[0,0],[.001,.999],[.125,.25],[.333,.667],[.5,.5],[.875,.125],[.999,.001],[1,1],[1.01,-.01],[2.375,-1.25],[-2.5,3.75]]
    views=[[0,0,1],[.5,.25,1],[-.5,.75,1],[1,-1,.5],[.75,.25,.1],[-.25,-.5,.25]];scales=[[1,1],[.5,2],[2,.25]]
    cases=[]
    for guid,m in materials.items():
        folder=O/guid;folder.mkdir();t=m['textures']['_L1_Height'];amp=m['floats']['_PomHeight'];samples=[]
        for uv in uvs:
            for view in views:
                for scale in scales:samples.append(uv+scale+view+[0]+[1,1,0,0]+t['scale']+t['offset']+[amp,32,0,0])
        assert len(samples)==216 and all(len(s)==20 for s in samples)
        inputpath=folder/'INPUT.f32';inputpath.write_bytes(b''.join(struct.pack('<20f',*s) for s in samples));pins[str(inputpath)]=sha(inputpath)
        cases.append(dict(guid=guid,height_guid=t['guid'],input=str(inputpath),raw_output=str(folder/'native-output.f32'),height_output=str(folder/'height-mip0.f32'),count=len(samples),amplitude=amp,texture_scale=dict(x=t['scale'][0],y=t['scale'][1]),texture_offset=dict(x=t['offset'][0],y=t['offset'][1])))
    request=O/'REQUEST.v1.json';request.write_text(json.dumps(dict(output=str(O/'NATIVE-RESULT.v1.json'),shader_path='Assets/Editor/HousePomReferenceV606.compute',cases=cases),indent=2),'utf-8');pins[str(request)]=sha(request)
    for p in [Path(__file__),S/'run_gladiator_remaining_geometry_v400.py',S/'queue_delta_firepit_particle_behavior_v4.py',proofpath]:pins[str(p)]=sha(p)
    prep=dict(input_pins=pins,private_copies=copies,cases=cases,samples=1296,sample_domains=dict(uv=uvs,tangent_view=views,inverse_object_row_lengths=scales),steps=32,lod=0,primitive_size=[1,1],precision='Explicit SDK float reference; actual generated graph precision not yet qualified',absolute_comparison_tolerance=1e-5,relative_comparison_tolerance=0,actual_prefab_render_verified=False,full_import=False,purchased_scripts_copied_or_executed=False)
    (O/'PREPARED.v1.json').write_text(json.dumps(prep,indent=2),'utf-8');save(dict(status='house_pom_native_reference_prepared'))
    with (O/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid,one_shot=True)),'utf-8');print(json.dumps(dict(controller_pid=proc.pid,samples=1296,materials=6)))
def run():
    recipe=S/'run_gladiator_remaining_geometry_v400.py';tree=ast.parse(recipe.read_text('utf-8-sig'));nodes=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name=='run']
    code=ast.unparse(ast.Module(body=nodes,type_ignores=[])).replace('OpenRPGGladiatorLotGeometryV400.Export','OpenRPGHousePomReferenceV606.Export')
    old="assert len(native['cases']) == 5 and sum((len(c['meshes']) for c in native['cases'])) == 13 and all((c['source_components_unchanged'] for c in native['cases']))";assert code.count(old)==1
    code=code.replace(old,"assert len(native['materials'])==6 and sum(c['samples'] for c in native['materials'])==1296 and native['source_materials_unchanged'] and native['graphics_api']=='Direct3D11' and all(Path(c['raw_output']).stat().st_size==c['output_bytes']==c['samples']*16 and Path(c['height_output']).stat().st_size==c['width']*c['height']*4 for c in native['materials'])")
    code=code.replace('remaining_thirteen_meshes_native_capture_running','house_pom_native_reference_running').replace('remaining_thirteen_meshes_and_five_root_material_snapshots_captured_review_pending','house_pom_native_reference_captured_comparison_pending').replace('remaining_geometry_capture_refusal_retained','house_pom_native_reference_refusal_retained').replace('new_meshes=13','native_pom_samples=1296')
    scope=dict(S=S,W=W,O=O,PROJECT=PROJECT,PS=PS,read=read,sha=sha,save=save);exec(compile(code,str(recipe)+'#house-sdk-pom-reference','exec'),scope);scope['run']()
if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
