"""Bound the complete arena mirror-normal lot using original prefab and material hashes."""
from pathlib import Path
from datetime import datetime,timezone
from collections import Counter
import hashlib,json,re,os,sys
V=Path('F:/OpenRPG_AssetVault');W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-mirror-lot-audit-v384'
P=V/'originals/unity-purchased/hivemind/source-support-v1/modular-gladiator-arena-base170-v1'
overlay=os.environ.get('OPENRPG_UNITY_OVERLAY_MANIFEST')
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'assets'))
from unity_shared_overlay import UnitySourceResolver,configured_resolver
resolver=UnitySourceResolver(V,Path(overlay),os.environ.get('OPENRPG_UNITY_OVERLAY_SHA256','')) if overlay else configured_resolver(V,P/'source')
def actual(p):
    path=Path(p).resolve(strict=False)
    return resolver.resolve_absolute(path) if resolver else path
def source_text(p):return actual(p).read_text('utf-8-sig')
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with actual(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
pointer=V/'metadata/asset-source-support-registry.v1.json';registry=read(pointer);catalogpath=V/registry['path'];assert sha(catalogpath)==registry['sha256']
cards=[a for a in read(catalogpath)['assets'] if a['pack_id']=='hivemind.modular-gladiator-arena' and a['support_kind']=='source_support_hdrp_mirror_normals'];assert len(cards)==17
mp=P/'manifest.v1.json';manifest=read(mp);payload={str((P/r['path']).resolve()):r for r in manifest['payload']};guidmap={}
pins={str(p):sha(p) for p in [Path(__file__),pointer,catalogpath,mp]}
for name,row in payload.items():
    path=Path(name)
    if path.suffix!='.meta' or not path.is_relative_to((P/'source').resolve()):continue
    assert sha(path)==row['sha256'];pins[name]=row['sha256']
    m=re.search(r'^guid: ([a-f0-9]{32})$',source_text(path),re.M)
    if m:assert m[1] not in guidmap;guidmap[m[1]]=Path(str(path)[:-5])
materials={};rows=[]
for card in cards:
    path=(V/card['source_file']).resolve();meta=(V/card['source_meta_file']).resolve()
    assert path.is_relative_to(P.resolve()) and sha(path)==card['source_sha256'] and sha(meta)==card['source_meta_sha256']
    assert card['evidence']['package_manifest_sha256']==sha(mp)
    pins[str(path)]=sha(path);pins[str(meta)]=sha(meta);text=source_text(path)
    components=Counter(re.findall(r'^--- !u!(\d+) ',text,re.M));refs=set(re.findall(r'guid:\s*([a-f0-9]{32})',text))
    mg=[];unresolved=[]
    for guid in sorted(refs):
        if guid=='0'*32:continue
        if guid not in guidmap:unresolved.append(guid);continue
        dep=guidmap[guid]
        if dep.suffix not in {'.mat','.fbx'}:continue
        assert sha(dep)==payload[str(dep)]['sha256'];pins[str(dep)]=sha(dep)
        if dep.suffix!='.mat':continue
        mg.append(guid)
        if guid in materials:continue
        raw=source_text(dep);floats={k:float(v) for k,v in re.findall(r'^\s+- (_[A-Za-z0-9_]+): ([-+0-9.eE]+)\s*$',raw,re.M)}
        shaderguid=re.search(r'm_Shader: \{fileID: -?\d+, guid: ([a-f0-9]{32}), type: \d+\}',raw)[1]
        shader=guidmap[shaderguid];assert sha(shader)==payload[str(shader)]['sha256'];pins[str(shader)]=sha(shader)
        materials[guid]=dict(path=str(dep),sha256=sha(dep),shader_guid=shaderguid,shader_sha256=sha(shader),double_sided=floats.get('_DoubleSidedEnable'),normal_mode=floats.get('_DoubleSidedNormalMode'),use_opacity=floats.get('_UseOpacity'),alpha_cutoff_enable=floats.get('_AlphaCutoffEnable'),alpha_cutoff=floats.get('_AlphaCutoff'),surface_type=floats.get('_SurfaceType'))
    rows.append(dict(asset_id=card['id'],source_path=str(path),source_sha256=sha(path),original_lod_level=card.get('original_lod_level'),components=dict(components),static_without_purchased_behaviour='137' not in components and '114' not in components,material_guids=mg,unresolved_prefab_guids=unresolved))
for p,h in pins.items():assert sha(Path(p))==h,p
result=dict(status='seventeen_arena_mirror_source_cases_rehashed',catalog_revision=registry['revision'],cards=len(rows),unique_prefabs=len({r['source_path'] for r in rows}),static_without_purchased_behaviour=sum(r['static_without_purchased_behaviour'] for r in rows),materials=materials,rows=rows,input_pins=pins,same_graph_as_flag=sum(m['shader_sha256']=='70b3f0ea3849a14fe88af3135d775c73a95e5d78b2d3a1fda0547966f863e0ee' for m in materials.values()),native_geometry_qualified=False,material_conversion_qualified=False,converted=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps({k:result[k] for k in ['status','cards','unique_prefabs','static_without_purchased_behaviour','same_graph_as_flag']}));print(json.dumps(materials))
