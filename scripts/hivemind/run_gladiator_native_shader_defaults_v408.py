"""Reuse the native material exporter for six defaults and actual generated source shader."""
from pathlib import Path
from datetime import datetime,timezone
import ast,hashlib,json,subprocess,sys
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-native-shader-defaults-v408'
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
    sourceproof=W/'gladiator-native-material-bindings-v406/RESULT.v1.json';proof=read(sourceproof);assert len(proof['rows'])==6
    for p,h in proof['input_pins'].items():assert sha(p)==h,p
    O.mkdir();(O/'temp').mkdir()
    base=S/'candidates/TorchMaterialExportV171.cs';code=base.read_text('utf-8-sig')
    replacements={'OpenRPGTorchMaterialExportV171':'OpenRPGGladiatorMaterialDefaultsV408','r.material_guids.Length!=3||r.material_guids.Distinct().Count()!=3':'r.material_guids.Length!=6||r.material_guids.Distinct().Count()!=6','Exactly three original private materials required':'Exactly six original private materials required','Assets/OpenRPGTorchOriginalVariantV134/':'Assets/Hivemind/GladitorArena/HDRP(Default)/',' OpenRPGTorchCompressedStorageV159.Export();':''}
    for before,after in replacements.items():assert code.count(before)==1,before;code=code.replace(before,after)
    needle='public string[] keywords;';assert code.count(needle)==1;code=code.replace(needle,'public string[] keywords,declared_keywords;public bool alpha_to_mask_keyword_declared;')
    needle='keywords=m.shaderKeywords,properties=';assert code.count(needle)==1
    code=code.replace(needle,'keywords=m.shaderKeywords,declared_keywords=shader.keywordSpace.keywords.Select(k=>k.name).ToArray(),alpha_to_mask_keyword_declared=shader.keywordSpace.keywords.Any(k=>k.name=="_ALPHATOMASK_ON"),properties=')
    helper=PROJECT/'Assets/Editor/GladiatorMaterialDefaultsV408.cs';assert not helper.exists();helper.write_text(code,'utf-8')
    request=O/'REQUEST.v1.json';request.write_text(json.dumps(dict(output=str(O/'NATIVE-RESULT.v1.json'),generated_shader=str(O/'source-generated.shader'),graph_path='Assets/Hivemind/GladitorArena/HDRP(Default)/Art/Shaders/S_BasicTextured.shadergraph',graph_guid='5a14110ac97e4694887098700a513fbe',graph_file_id='-6465566751694194690',material_guids=[r['material_guid'] for r in proof['rows']]),indent=2),'utf-8')
    pins={str(p):sha(p) for p in [sourceproof,base,helper,request,Path(__file__),S/'run_gladiator_remaining_geometry_v400.py',S/'queue_delta_firepit_particle_behavior_v4.py']}
    copies=[]
    for row in read(W/'gladiator-lot-dependency-closure-v394/RESULT.v1.json')['files']:
        source=Path(row['source']);target=PROJECT/row['relative_project_path'];assert sha(source)==row['sha256']
        pins[str(source)]=row['sha256'];copies.append(dict(source=str(source),copy=str(target),source_sha256=row['sha256'],private_before_sha256=sha(target)))
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,private_copies=copies,material_count=6,purchased_scripts_copied_or_executed=False,shader_gpu_qualified=False),indent=2),'utf-8')
    save(dict(status='six_material_defaults_and_generated_shader_prepared'))
    with (O/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid,one_shot=True)),'utf-8');print(json.dumps(dict(controller_pid=proc.pid,materials=6)))
def run():
    recipe=S/'run_gladiator_remaining_geometry_v400.py';tree=ast.parse(recipe.read_text('utf-8'))
    nodes=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name=='run']
    code=ast.unparse(ast.Module(body=nodes,type_ignores=[]))
    code=code.replace('OpenRPGGladiatorLotGeometryV400.Export','OpenRPGGladiatorMaterialDefaultsV408.Export')
    needle="assert len(native['cases']) == 5 and sum((len(c['meshes']) for c in native['cases'])) == 13 and all((c['source_components_unchanged'] for c in native['cases']))"
    assert code.count(needle)==1
    code=code.replace(needle,"assert len(native['materials']) == 6 and native['source_materials_unchanged'] and (O / 'source-generated.shader').is_file()")
    code=code.replace('remaining_thirteen_meshes_native_capture_running','six_material_defaults_and_original_shader_generation_running').replace('remaining_thirteen_meshes_and_five_root_material_snapshots_captured_review_pending','six_native_material_defaults_and_original_shader_generated_review_pending').replace('new_meshes=13','native_materials=6')
    namespace=dict(S=S,W=W,O=O,PROJECT=PROJECT,PS=PS,read=read,sha=sha,save=save)
    exec(compile(code,str(recipe)+'#material-default-controller','exec'),namespace);namespace['run']()
if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
