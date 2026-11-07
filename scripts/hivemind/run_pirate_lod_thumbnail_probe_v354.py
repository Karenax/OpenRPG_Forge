"""Capture LOD1/2 of three new variants, plus source LOD0 geometry controls."""
from pathlib import Path
from datetime import datetime,timezone
import ast,hashlib,json,shutil,subprocess,sys,re
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault')
O=W/'pirate-lod-thumbnail-probe-v354';PROJECT=W/'unity-library-source-thumbnail-pirate-bench-v2/project'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(v):
    v.update(updated_utc=datetime.now(timezone.utc).isoformat(),full_import=False,images_installed_in_vault=False)
    p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
def prepare():
    assert not O.exists()
    assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
    pointer=V/'metadata/asset-source-support-registry.v1.json';registry=read(pointer);catalogpath=V/registry['path'];assert sha(catalogpath)==registry['sha256']
    catalog=read(catalogpath)['assets'];pool={}
    for card in catalog:
        if card.get('pack_id')=='hivemind.pirate-island' and '/HDRP' in card.get('source_asset_path','') and card['source_asset_path'].endswith('.prefab'):
            pool.setdefault(card['source_asset_path'],{})[card.get('original_lod_level')]=card
    groups=[rows for path,rows in sorted(pool.items()) if all(level in rows for level in [0,1,2]) and not rows[1].get('preview') and not rows[2].get('preview')][:3]
    assert len(groups)==3
    base=S/'candidates/SourceLibraryLodThumbnailBatchV353.cs';installed=PROJECT/'Assets/Editor'/base.name
    assert not installed.exists();shutil.copyfile(base,installed);assert sha(base)==sha(installed)
    O.mkdir();(O/'temp').mkdir();(O/'private-input-epoch').mkdir()
    pins={str(p):sha(p) for p in (PROJECT/'Assets/Hivemind/PirateIsland').rglob('*') if p.is_file()};snapshots=[]
    for name,h in list(pins.items()):
        p=Path(name)
        if p.suffix in {'.mat','.meta','.shadergraph','.asset'}:
            copy=O/'private-input-epoch'/p.relative_to(PROJECT);copy.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,copy);assert sha(copy)==h
            snapshots.append(dict(live_path=name,snapshot=str(copy),sha256=h));pins[str(copy)]=h
    cases=[];variants=[]
    for i,rows in enumerate(groups):
        for level in [0,1,2]:
            card=rows[level]
            for field,hfield in [('source_file','source_sha256'),('source_meta_file','source_meta_sha256')]:
                p=V/card[field];assert sha(p)==card[hfield];pins[str(p)]=card[hfield]
            assert sha(PROJECT/card['source_asset_path'])==card['source_sha256']
            source_text=(PROJECT/card['source_asset_path']).read_text('utf-8-sig')
            references=re.findall(r'^\s+- renderer: \{fileID: (-?\d+)\}\s*$',source_text,re.M)
            declared=set(re.findall(r'^--- !u!23 &(-?\d+)',source_text,re.M))
            assert references and all(ref=='0' or ref in declared for ref in references),'Nonzero source LOD renderer reference unresolved'
            empty_slots=references.count('0')
            folder=O/f'prefab-{i:02d}-lod-{level}';folder.mkdir()
            cases.append(dict(id=card['id'] if level else card['id']+':lod0-control',prefab_path=card['source_asset_path'],prefab_guid=card['source_guid'],requested_lod=level,expected_empty_lod_slots=empty_slots,index_candidate=level>0,source_card_id=card['id'],output=str(folder/'NATIVE-RESULT.v1.json')))
            if level:variants.append(card['id'])
    (O/'REQUEST.v1.json').write_text(json.dumps(dict(cases=cases,output=str(O/'NATIVE-RESULT.v1.json')),indent=2),'utf-8')
    (O/'PRIVATE-EPOCH.v1.json').write_text(json.dumps(dict(private_input_snapshots=snapshots),indent=2),'utf-8')
    for p in [Path(__file__),base,installed,catalogpath,pointer,O/'REQUEST.v1.json',O/'PRIVATE-EPOCH.v1.json',S/'queue_delta_firepit_particle_behavior_v4.py',S/'run_pirate_thumbnail_batch_0193_0199_v334.py',PROJECT/'Packages/manifest.json',PROJECT/'ProjectSettings/ProjectSettings.asset']:pins[str(p)]=sha(p)
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(sub_batch='L08.IMAGE.PIRATE.LOD.01',expected_cards=6,expected_capture_cases=9,expected_images=12,cases=cases,candidate_ids=variants,input_pins=pins,catalog_revision=registry['revision'],source_version_parity_qualified=False,full_import=False),indent=2),'utf-8')
    save(dict(status='requested_lod_probe_prepared'))
    with (O/'controller.log').open('xb') as log:
        proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=proc.pid,one_shot=True)),'utf-8')
    print(json.dumps(dict(controller_pid=proc.pid,cards=6,capture_cases=9,names=[Path(g[0]['source_asset_path']).stem for g in groups])))
def run():
    recipe=S/'run_pirate_thumbnail_batch_0193_0199_v334.py';tree=ast.parse(recipe.read_text('utf-8-sig'))
    selected=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name=='run']
    text=ast.unparse(ast.Module(body=selected,type_ignores=[])).replace('OpenRPGSourceLibraryThumbnailBatchV1.Export','OpenRPGSourceLibraryLodThumbnailBatchV353.Export').replace("len(batch['cases']) == 7","len(batch['cases']) == 9").replace('seven_pirate_source_cards','nine_lod_probe_cases')
    scope=dict(S=S,W=W,V=V,O=O,PROJECT=PROJECT,PS=PS,read=read,sha=sha,save=save)
    exec(compile(text,str(recipe)+'#requested-lod-controller','exec'),scope);scope['run']()
if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
