"""Resolve and rehash one real source flag's material/mesh/shader dependency closure."""
from pathlib import Path
from collections import Counter
from datetime import datetime,timezone
import hashlib,json,re
V=Path('F:/OpenRPG_AssetVault');W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-mirror-source-case-v379'
P=V/'originals/unity-purchased/hivemind/source-support-v1/modular-gladiator-arena-base170-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
manifestpath=P/'manifest.v1.json';manifest=read(manifestpath)
pointer=V/'metadata/asset-source-support-registry.v1.json';sr=read(pointer);catalogpath=V/sr['path'];assert sha(catalogpath)==sr['sha256']
card=next(a for a in read(catalogpath)['assets'] if a['id']=='hivemind.modular-gladiator-arena.prefab-sm-bigflag-a-abc00000000001793096621998943855')
assert sha(manifestpath)==card['evidence']['package_manifest_sha256'] and manifest['source_archive_sha256']==card['source_archive_sha256']
payload={str((P/r['path']).resolve()):r for r in manifest['payload']}
pins={str(p):sha(p) for p in [Path(__file__),manifestpath,pointer,catalogpath]}
metas={};duplicates=[]
for name,row in payload.items():
    path=Path(name)
    if not path.is_relative_to((P/'source').resolve()) or path.suffix!='.meta':continue
    assert sha(path)==row['sha256'];pins[name]=row['sha256']
    match=re.search(r'^guid: ([a-f0-9]{32})$',path.read_text('utf-8-sig'),re.M)
    if match:
        guid=match[1];asset=Path(str(path)[:-5])
        if guid in metas and metas[guid]!=asset:duplicates.append(guid)
        metas[guid]=asset
assert not duplicates
source=V/card['source_file'];assert sha(source)==card['source_sha256']
source_meta=V/card['source_meta_file'];assert sha(source_meta)==card['source_meta_sha256']
queue=[source.resolve()];seen=set();unresolved=set();materials=[];graphs=[];files=[]
while queue:
    path=queue.pop()
    if path in seen:continue
    seen.add(path);assert path.is_relative_to(P.resolve()) and str(path) in payload,path
    row=payload[str(path)];assert sha(path)==row['sha256'];pins[str(path)]=row['sha256'];files.append(dict(path=str(path),sha256=row['sha256'],bytes=path.stat().st_size,suffix=path.suffix))
    meta=Path(str(path)+'.meta')
    if meta.is_file():assert sha(meta)==payload[str(meta)]['sha256'];pins[str(meta)]=sha(meta)
    if path.suffix not in {'.prefab','.mat','.shadergraph','.shadersubgraph','.asset','.shader','.hlsl'}:continue
    text=path.read_text('utf-8-sig')
    if path.suffix=='.mat':
        floats={k:float(v) for k,v in re.findall(r'^\s+- (_[A-Za-z0-9_]+): ([-+0-9.eE]+)\s*$',text,re.M)}
        materials.append(dict(path=str(path),source_properties={k:v for k,v in floats.items() if any(term in k.lower() for term in ['double','alpha','opacity','mask','surface','blend','wind','time','color'])}))
    if path.suffix in {'.shadergraph','.shadersubgraph'}:
        types=Counter(re.findall(r'"m_Type"\s*:\s*"([^"]+)"',text))
        graphs.append(dict(path=str(path),node_types=dict(types),time_nodes=[k for k in types if 'TimeNode' in k],vertex_related_nodes=[k for k in types if any(t in k for t in ['PositionNode','VertexColorNode','NormalVectorNode','TangentVectorNode'])]))
    for guid in set(re.findall(r'(?:guid:\s*|"guid"\s*:\s*")([a-f0-9]{32})',text)):
        if guid=='0'*32:continue
        if guid in metas:queue.append(metas[guid])
        else:unresolved.add(guid)
components=Counter(re.findall(r'^--- !u!(\d+) ',source.read_text('utf-8-sig'),re.M))
for p,h in pins.items():assert sha(Path(p))==h,p
result=dict(status='real_flag_source_dependency_closure_rehashed',asset_id=card['id'],pack_id=card['pack_id'],license=card['license'],source_archive_sha256=card['source_archive_sha256'],source_components=dict(components),skinned_renderer_present='137' in components,mono_behaviour_present='114' in components,materials=materials,shader_graphs=graphs,resolved_files=files,unresolved_external_guids=sorted(unresolved),native_shader_qualified=False,graph_time_nodes_detected=any(g['time_nodes'] for g in graphs),input_pins=pins,vault_modified=False,converted=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps({k:result[k] for k in ['asset_id','source_components','skinned_renderer_present','mono_behaviour_present','graph_time_nodes_detected','unresolved_external_guids']}))
