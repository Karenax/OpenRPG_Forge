"""Compare authored stencil values with imported values for every held wood LOD."""
from pathlib import Path
from datetime import datetime, timezone
import ast, hashlib, json, subprocess, sys

S=Path(__file__).resolve().parent
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault')
O=W/'pirate-wood-lod-migration-v590'
PROJECT=W/'unity-library-source-thumbnail-pirate-bench-v2/project'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
parent=S/'run_pirate_wood_pending_render_v288.py'
tree=ast.parse(parent.read_text('utf-8'))
selected=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name in {'read','sha','save','run'}]
run_node=next(n for n in selected if isinstance(n,ast.FunctionDef) and n.name=='run')
run_code=ast.unparse(run_node).replace('PirateWoodPendingRenderV288','PirateWoodLodMigrationV590').replace('len(batch[\'cases\']) == 16','len(batch[\'cases\']) == 144').replace('range(4)','range(36)')
selected=[n for n in selected if n is not run_node]
exec(compile(ast.Module(body=selected,type_ignores=[]),str(parent)+'#retained-controller','exec'),globals())
exec(compile(run_code,str(parent)+'#36-exact-lod-comparisons','exec'),globals())

def prepare():
    assert not O.exists()
    assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
    auditpath=W/'pirate-palm-wind-native-reference-v558/HELPER-OWNERSHIP.v1.json';audit=read(auditpath)
    assert not audit['asset_dll_found']
    for h in audit['helpers']:
        for key in ['private_path','controlled_source']:assert sha(h[key])==h['sha256'],h[key]
    assert {str(p) for p in (PROJECT/'Assets').rglob('*.cs')}=={h['private_path'] for h in audit['helpers']}
    assert not any((PROJECT/'Assets').rglob('*.dll'))
    frontierpath=W/'pirate-image-frontier-v589/RESULT.v1.json';frontier=read(frontierpath)
    pointer=V/'metadata/asset-source-support-registry.v1.json';registry=read(pointer)
    assert registry['revision']==frontier['catalog_revision']
    catalogpath=V/registry['path'];assert sha(catalogpath)==registry['sha256']
    catalog={a['id']:a for a in read(catalogpath)['assets']}
    guid='abc00000000004592200770135964921'
    groups=[r for r in frontier['held_groups'] if r['blocked_material_guids']==[guid]]
    assert len(groups)==12
    queuepath=W/'queue/pirate-island/prefabs.json';queue=read(queuepath)
    originals={};source_pins={}
    for row in groups:
        path=row['source_path'];root=Path(queue['source_root']);original=root/path
        assert sha(original)==row['source_sha256'];source_pins[str(original)]=row['source_sha256']
        cards=[a for a in catalog.values() if a.get('pack_id')=='hivemind.pirate-island' and a.get('source_asset_path')==path]
        levels={a['original_lod_level']:a for a in cards};assert set(levels)=={0,1,2}
        for card in cards:
            for field,hfield in [('source_file','source_sha256'),('source_meta_file','source_meta_sha256')]:
                p=V/card[field];assert sha(p)==card[hfield];source_pins[str(p)]=card[hfield]
        import re
        text=original.read_text('utf-8-sig')
        lod_sections=re.findall(r'^--- !u!205.*?(?=^---|\Z)',text,re.M|re.S)
        assert len(lod_sections)==1
        empty=len(re.findall(r'renderer: \{fileID: 0\}',lod_sections[0]))
        for level in range(3):
            card=levels[level]
            originals[(path,level)]=dict(id=card['id'],source_card_id=card['id'],prefab_path=path,prefab_guid=card['source_guid'],requested_lod=level,expected_empty_lod_slots=empty)
    prepare_node=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='prepare')
    assignment=next(n for n in prepare_node.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='replacements' for t in n.targets))
    replacements=ast.literal_eval(assignment.value)
    base=S/'candidates/SourceLibraryLodThumbnailBatchV353.cs';text=base.read_text('utf-8-sig')
    replacements['OpenRPGSourceLibraryLodThumbnailBatchV353']='OpenRPGPirateWoodLodMigrationV590'
    del replacements['OpenRPGSourceLibraryThumbnailBatchV1']
    replacements['public int requested_lod,expected_empty_lod_slots;']='public int requested_lod,expected_empty_lod_slots; public bool restore_prior_mask_and_stencil;public string comparison_material_guid;'
    del replacements['public string id,prefab_path,prefab_guid,output;']
    insertion=replacements.pop('foreach(var lod in fixture.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(0);')
    insertion=insertion.replace('foreach(var lod in fixture.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(0);','')
    anchor='fixture.hideFlags=HideFlags.HideAndDontSave;UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fixture,preview);'
    replacements[anchor]=anchor+'\n'+insertion
    for a,b in replacements.items():
        assert text.count(a)==1,a;text=text.replace(a,b)
    O.mkdir();(O/'temp').mkdir()
    helper=O/'PirateWoodLodMigrationV590.cs';helper.write_text(text,'utf-8')
    cases=[]
    for i,source in enumerate(originals.values()):
        for label,restore in [('warmup',False),('current',False),('prior-observed-semantics',True),('current-repeat',False)]:
            folder=O/f'case-{i:02d}-{label}';folder.mkdir()
            cases.append(dict(source,id=source['id']+':'+label,output=str(folder/'NATIVE-RESULT.v1.json'),restore_prior_mask_and_stencil=restore,comparison_material_guid=guid))
    manifest=O/'manifest.json';manifest.write_text(json.dumps(dict(cases=cases,output=str(O/'NATIVE-RESULT.v1.json')),indent=2),'utf-8')
    pins={str(p):sha(p) for p in (PROJECT/'Assets/Hivemind/PirateIsland').rglob('*') if p.is_file()};pins.update(source_pins)
    for p in [Path(__file__),parent,base,helper,manifest,frontierpath,pointer,catalogpath,queuepath,auditpath,PROJECT/'Packages/manifest.json',PROJECT/'ProjectSettings/ProjectSettings.asset']:
        pins[str(p)]=sha(p)
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,cases=cases,adaptations=replacements,scope='Twelve held wood prefab groups, three exact LOD levels, two views, current/prior stencil/current-repeat comparisons; no persistent material writes or full conversion qualification.'),indent=2),'utf-8')
    save(dict(status='wood_lod_migration_prepared'))
    with (O/'controller.log').open('xb') as log:
        proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid,one_shot=True)),'utf-8')
    print(json.dumps(dict(controller_pid=proc.pid,root=str(O),groups=12,lod_cases=36,captures=144,views=288)))

if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
