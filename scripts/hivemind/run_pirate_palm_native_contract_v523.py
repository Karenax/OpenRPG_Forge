"""Read the actual palm material defaults and generated shader in the owned bench."""
from pathlib import Path
from datetime import datetime, timezone
import ast, hashlib, json, subprocess, sys
S=Path(__file__).resolve().parent
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault')
O=W/'pirate-palm-native-contract-v523'
PROJECT=W/'unity-library-source-thumbnail-pirate-bench-v2/project'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
MATERIAL='Assets/Hivemind/PirateIsland/HDRP(Default)/Art/Materials/M_PalmTreeFronds.mat'
GRAPH='Assets/Hivemind/PirateIsland/HDRP(Default)/Art/ShaderGraphs/S_Vegetation.shadergraph'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(v):
    v.update(updated_utc=datetime.now(timezone.utc).isoformat(),converted=False,full_import=False)
    p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
def prepare():
    assert not O.exists()
    assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
    auditpath=W/'pirate-lod-thumbnail-batch06-v513/HELPER-OWNERSHIP.v1.json'
    audit=read(auditpath);assert not audit['asset_dll_found']
    for h in audit['helpers']:
        assert sha(h['private_path'])==sha(h['controlled_source'])==h['sha256']
    assert {str(p) for p in (PROJECT/'Assets').rglob('*.cs')}=={h['private_path'] for h in audit['helpers']}
    assert not any((PROJECT/'Assets').rglob('*.dll'))
    pointer=V/'metadata/asset-source-support-registry.v1.json';registry=read(pointer)
    catalogpath=V/registry['path'];assert sha(catalogpath)==registry['sha256']
    card=next(c for c in read(catalogpath)['assets'] if c['id']=='hivemind.pirate-island.prefab-sm-palmtree-01-abc00000000006839218276228297273-lod1')
    source=V/card['source_file'];assert sha(source)==card['source_sha256']
    root=source
    for part in Path(card['source_asset_path']).parts:root=root.parent
    original_material=root/MATERIAL;original_graph=root/GRAPH
    assert original_material.is_file() and original_graph.is_file()
    assert 'abc00000000004284671107665666980' in (PROJECT/(MATERIAL+'.meta')).read_text('utf-8-sig')
    assert '8a71d1ff6799ee7429ba5af1b299c193' in (PROJECT/(GRAPH+'.meta')).read_text('utf-8-sig')
    O.mkdir();(O/'temp').mkdir()
    base=S/'candidates/TorchMaterialExportV171.cs';code=base.read_text('utf-8-sig')
    for old,new in {
        'OpenRPGTorchMaterialExportV171':'OpenRPGPiratePalmContractV523',
        'r.material_guids.Length!=3||r.material_guids.Distinct().Count()!=3':'r.material_guids.Length!=1||r.material_guids.Distinct().Count()!=1',
        'Exactly three original private materials required':'Exactly one original private palm material required',
        'Assets/OpenRPGTorchOriginalVariantV134/':'Assets/Hivemind/PirateIsland/HDRP(Default)/',
        ' OpenRPGTorchCompressedStorageV159.Export();':'',
        'public string[] keywords;':'public string[] keywords,declared_keywords;public bool alpha_to_mask_keyword_declared;',
        'keywords=m.shaderKeywords,properties=':'keywords=m.shaderKeywords,declared_keywords=shader.keywordSpace.keywords.Select(k=>k.name).ToArray(),alpha_to_mask_keyword_declared=shader.keywordSpace.keywords.Any(k=>k.name=="_ALPHATOMASK_ON"),properties=',
    }.items():
        assert code.count(old)==1,old
        code=code.replace(old,new)
    controlled=O/'PiratePalmContractV523.cs';controlled.write_text(code,'utf-8')
    helper=PROJECT/'Assets/Editor/PiratePalmContractV523.cs';assert not helper.exists()
    helper.write_bytes(controlled.read_bytes());assert sha(helper)==sha(controlled)
    audit['helpers'].append(dict(private_path=str(helper),controlled_source=str(controlled),sha256=sha(helper)))
    audit.update(checked_utc=datetime.now(timezone.utc).isoformat(),asset_dll_found=False,previous_audit=str(auditpath))
    (O/'HELPER-OWNERSHIP.v1.json').write_text(json.dumps(audit,indent=2),'utf-8')
    request=O/'REQUEST.v1.json'
    request.write_text(json.dumps(dict(output=str(O/'NATIVE-RESULT.v1.json'),generated_shader=str(O/'source-generated.shader'),graph_path=GRAPH,graph_guid='8a71d1ff6799ee7429ba5af1b299c193',graph_file_id='-6465566751694194690',material_guids=['abc00000000004284671107665666980']),indent=2),'utf-8')
    pins={str(p):sha(p) for p in [Path(__file__),base,controlled,helper,request,auditpath,O/'HELPER-OWNERSHIP.v1.json',S/'run_gladiator_remaining_geometry_v400.py',S/'queue_delta_firepit_particle_behavior_v4.py']}
    for h in audit['helpers']:
        for key in ['private_path','controlled_source']:pins[h[key]]=h['sha256']
    copies=[]
    for p in (PROJECT/'Assets/Hivemind/PirateIsland').rglob('*'):
        if not p.is_file():continue
        original=root/p.relative_to(PROJECT)
        assert original.is_file(),str(original)
        pins[str(original)]=sha(original)
        copies.append(dict(source=str(original),copy=str(p),source_sha256=pins[str(original)],private_before_sha256=sha(p)))
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,private_copies=copies,material_count=1,purchased_scripts_copied_or_executed=False,shader_gpu_qualified=False,original_material=str(original_material),original_graph=str(original_graph)),indent=2),'utf-8')
    save(dict(status='palm_native_shader_contract_prepared'))
    with (O/'controller.log').open('xb') as log:
        proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid,one_shot=True)),'utf-8')
    print(json.dumps(dict(controller_pid=proc.pid,materials=1)))
def run():
    recipe=S/'run_gladiator_remaining_geometry_v400.py'
    tree=ast.parse(recipe.read_text('utf-8'))
    nodes=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name=='run']
    code=ast.unparse(ast.Module(body=nodes,type_ignores=[]))
    code=code.replace('OpenRPGGladiatorLotGeometryV400.Export','OpenRPGPiratePalmContractV523.Export')
    old="assert len(native['cases']) == 5 and sum((len(c['meshes']) for c in native['cases'])) == 13 and all((c['source_components_unchanged'] for c in native['cases']))"
    assert code.count(old)==1
    code=code.replace(old,"assert len(native['materials']) == 1 and native['source_materials_unchanged'] and (O / 'source-generated.shader').is_file()")
    code=code.replace('remaining_thirteen_meshes_native_capture_running','palm_native_shader_contract_running').replace('remaining_thirteen_meshes_and_five_root_material_snapshots_captured_review_pending','palm_native_shader_generated_contract_review_pending').replace('new_meshes=13','native_materials=1')
    scope=dict(S=S,W=W,O=O,PROJECT=PROJECT,PS=PS,read=read,sha=sha,save=save)
    exec(compile(code,str(recipe)+'#palm-native-contract','exec'),scope);scope['run']()
if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
