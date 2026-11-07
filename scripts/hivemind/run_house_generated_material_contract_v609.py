"""Capture actual SDK generated House graph and six native material bindings."""
from pathlib import Path
from datetime import datetime,timezone
import ast,hashlib,json,re,subprocess,sys
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'house-generated-material-contract-v609'
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
    checkpoint=W/'house-pom-checkpoint-v608/RESULT.v1.json';proof=read(checkpoint);assert proof['all_outputs_byte_exact']
    pins=dict(proof['input_pins'])
    for p,h in pins.items():assert sha(p)==h,p
    ap=W/'house-pom-native-reference-v606/HELPER-OWNERSHIP.v1.json';audit=read(ap)
    assert len(audit['helpers'])==19 and not any((PROJECT/'Assets').rglob('*.dll'))
    assert {str(p) for p in (PROJECT/'Assets').rglob('*.cs')}=={h['private_path'] for h in audit['helpers']}
    for h in audit['helpers']:assert sha(h['private_path'])==sha(h['controlled_source'])==h['sha256']
    base=S/'candidates/TorchMaterialExportV171.cs';code=base.read_text('utf-8-sig')
    replacements={'OpenRPGTorchMaterialExportV171':'OpenRPGHouseGeneratedMaterialContractV609','r.material_guids.Length!=3||r.material_guids.Distinct().Count()!=3':'r.material_guids.Length!=6||r.material_guids.Distinct().Count()!=6','Exactly three original private materials required':'Exactly six House native materials required','Assets/OpenRPGTorchOriginalVariantV134/':'Assets/Hivemind/HouseOnaHill/',' OpenRPGTorchCompressedStorageV159.Export();':''}
    for old,new in replacements.items():assert code.count(old)==1,old;code=code.replace(old,new)
    queuepath=W/'queue/house-on-a-hill/prefabs.json';q=read(queuepath);mp=W/'house-main-material-lot-v603/SOURCE-QUEUE.v1.json';materials=read(mp)['materials']
    graphguid='7dcd0e3bb8cbf2345a1e124f5a095198';graph=q['guid_paths'][graphguid]
    copies=read(W/'house-pom-native-reference-v606/PREPARED.v1.json')['private_copies']
    for row in copies:assert sha(row['copy'])==row['private_before_sha256']
    closure=read(W/'house-main-material-lot-v603/SHADER-SUBGRAPH-CLOSURE.v1.json')
    for g in closure['graphs']:
        original=Path(g['path']);private=PROJECT/original.relative_to(Path(q['source_root']))
        for suffix in ['', '.meta']:
            a=Path(str(original)+suffix);b=Path(str(private)+suffix);assert sha(a)==sha(b),str(b)
            pins[str(a)]=sha(a);copies.append(dict(source=str(a),copy=str(b),source_sha256=sha(a),private_before_sha256=sha(b)))
    O.mkdir();(O/'temp').mkdir();controlled=O/'HouseGeneratedMaterialContractV609.cs';controlled.write_text(code,'utf-8');dst=PROJECT/'Assets/Editor'/controlled.name;assert not dst.exists();dst.write_bytes(controlled.read_bytes())
    audit['helpers'].append(dict(private_path=str(dst),controlled_source=str(controlled),sha256=sha(dst)));audit.update(previous_audit=str(ap),checked_utc=datetime.now(timezone.utc).isoformat())
    auditpath=O/'HELPER-OWNERSHIP.v1.json';auditpath.write_text(json.dumps(audit,indent=2),'utf-8')
    fileids=set()
    for material in materials.values():
        text=(Path(q['source_root'])/material['source']).read_text('utf-8-sig');match=re.search(r'm_Shader: \{fileID: (-?\d+), guid: ([a-f0-9]{32}),',text);assert match and match[2]==graphguid;fileids.add(match[1])
    assert len(fileids)==1
    request=O/'REQUEST.v1.json';request.write_text(json.dumps(dict(output=str(O/'NATIVE-RESULT.v1.json'),graph_path=graph,graph_guid=graphguid,graph_file_id=next(iter(fileids)),generated_shader=str(O/'source-generated.shader'),material_guids=sorted(materials)),indent=2),'utf-8')
    for p in [Path(__file__),checkpoint,ap,base,controlled,dst,auditpath,request,queuepath,mp,S/'run_gladiator_remaining_geometry_v400.py',S/'queue_delta_firepit_particle_behavior_v4.py']:pins[str(p)]=sha(p)
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,private_copies=copies,materials=6,purchased_scripts_copied_or_executed=False,full_import=False),indent=2),'utf-8');save(dict(status='house_generated_material_contract_prepared'))
    with (O/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid,one_shot=True)),'utf-8');print(json.dumps(dict(controller_pid=proc.pid,materials=6)))
def run():
    recipe=S/'run_gladiator_remaining_geometry_v400.py';tree=ast.parse(recipe.read_text('utf-8-sig'));nodes=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name=='run']
    code=ast.unparse(ast.Module(body=nodes,type_ignores=[])).replace('OpenRPGGladiatorLotGeometryV400.Export','OpenRPGHouseGeneratedMaterialContractV609.Export')
    old="assert len(native['cases']) == 5 and sum((len(c['meshes']) for c in native['cases'])) == 13 and all((c['source_components_unchanged'] for c in native['cases']))";assert code.count(old)==1
    code=code.replace(old,"assert len(native['materials'])==6 and native['source_materials_unchanged'] and all(m['source_material_unchanged'] for m in native['materials']) and (O/'source-generated.shader').stat().st_size>0")
    code=code.replace('remaining_thirteen_meshes_native_capture_running','house_generated_material_contract_running').replace('remaining_thirteen_meshes_and_five_root_material_snapshots_captured_review_pending','house_generated_material_contract_captured_review_pending').replace('remaining_geometry_capture_refusal_retained','house_generated_material_contract_refusal_retained').replace('new_meshes=13','materials_captured=6')
    scope=dict(S=S,W=W,O=O,PROJECT=PROJECT,PS=PS,read=read,sha=sha,save=save);exec(compile(code,str(recipe)+'#house-generated-material-contract','exec'),scope);scope['run']()
if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
