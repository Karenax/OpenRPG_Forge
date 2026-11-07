"""Resolve the complete data-only dependency union of all five source prefabs."""
from pathlib import Path
from datetime import datetime,timezone
from collections import Counter
import hashlib,json,re
W=Path('F:/OpenRPG_ImportWork');V=Path('F:/OpenRPG_AssetVault');O=W/'gladiator-lot-dependency-closure-v394'
P=V/'originals/unity-purchased/hivemind/source-support-v1/modular-gladiator-arena-base170-v1'
SDK=W/'unity-library-source-thumbnail-pirate-bench-v2/project/Library/PackageCache'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
bp=W/'gladiator-source-binding-bridge-v390/RESULT.v1.json';bridge=read(bp);assert bridge['prefabs']==5 and bridge['source_cards']==17
mp=P/'manifest.v1.json';manifest=read(mp);payload={str((P/r['path']).resolve()):r for r in manifest['payload']}
pins={str(p):sha(p) for p in [bp,mp,Path(__file__)]}
for p,h in bridge['input_pins'].items():assert sha(p)==h;pins[p]=h
guidmap={}
for name,r in payload.items():
    path=Path(name)
    if path.suffix!='.meta' or not path.is_relative_to((P/'source').resolve()):continue
    assert sha(path)==r['sha256'];pins[name]=r['sha256']
    m=re.search(r'^guid: ([a-f0-9]{32})$',path.read_text('utf-8-sig'),re.M)
    if m:assert m[1] not in guidmap;guidmap[m[1]]=Path(str(path)[:-5])
sdkrefs={}
for guid,package in [('da692e001514ec24dbc4cca1949ff7e8','com.unity.render-pipelines.high-definition@fc55a8abadc2'),('d0353a89b1f911e48b9e16bdc9f2e058','com.unity.render-pipelines.universal@73b4c4ff130e'),('625f186215c104763be7675aa2d941aa','com.unity.shadergraph@8d7376ec43ee')]:
    meta=SDK/package/('Editor/Importers/ShaderGraphImporter.cs.meta' if package.startswith('com.unity.shadergraph@') else 'Editor/AssetVersion.cs.meta' if package.startswith('com.unity.render-pipelines.universal@') else 'Editor/AssetProcessors/AssetVersion.cs.meta');source=Path(str(meta)[:-5])
    assert re.search(r'^guid: ([a-f0-9]{32})$',meta.read_text('utf-8-sig'),re.M)[1]==guid
    assert source.is_file();pins[str(meta)]=sha(meta);pins[str(source)]=sha(source)
    sdkrefs[guid]=dict(meta=str(meta),meta_sha256=sha(meta),source=str(source),source_sha256=sha(source),copied_or_executed_by_this_audit=False)
queue=[Path(r['source_path']).resolve() for r in bridge['rows']];seen=set();files=[];external=set()
while queue:
    path=queue.pop()
    if path in seen:continue
    seen.add(path);assert path.is_relative_to(P.resolve()) and str(path) in payload and path.is_file(),path
    assert path.suffix in {'.prefab','.fbx','.mat','.shadergraph','.shadersubgraph','.asset','.png','.tga','.TGA','.jpg','.jpeg'},path
    texts=[]
    for p in [path,Path(str(path)+'.meta')]:
        assert str(p) in payload and sha(p)==payload[str(p)]['sha256'];pins[str(p)]=sha(p)
        files.append(dict(source=str(p),relative_project_path=p.relative_to(P/'source').as_posix(),sha256=sha(p),bytes=p.stat().st_size))
        if p.suffix in {'.meta','.prefab','.mat','.shadergraph','.shadersubgraph','.asset'}:texts.append(p.read_text('utf-8-sig'))
    for text in texts:
        for guid in set(re.findall(r'(?:guid:\s*|"guid"\s*:\s*")([a-f0-9]{32})',text)):
            if guid=='0'*32:continue
            if guid in guidmap:
                dependency=guidmap[guid]
                if dependency.is_file():queue.append(dependency)
                else:assert False,('Directory dependency needs explicit handling',guid,str(dependency))
            else:external.add(guid)
assert external<=set(sdkrefs),external-set(sdkrefs)
assert len({r['source'] for r in files})==len(files)
for p,h in pins.items():assert sha(p)==h,p
counts=dict(Counter(Path(r['source']).suffix for r in files))
result=dict(status='five_prefab_data_dependency_union_closed_and_rehashed',prefabs=5,source_cards=17,files=files,file_count=len(files),file_bytes=sum(r['bytes'] for r in files),suffix_counts=counts,external_sdk_asset_version_references={g:sdkrefs[g] for g in sorted(external)},input_pins=pins,purchased_scripts_copied_or_executed=False,unity_native_capture_complete=False,converted=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:result[k] for k in ['status','prefabs','source_cards','file_count','file_bytes','suffix_counts']}))
