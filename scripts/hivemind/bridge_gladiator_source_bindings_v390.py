"""Read original prefab hierarchy and renderer identities; native comparison remains separate."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,math,re,yaml
W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-source-binding-bridge-v390'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def matrix(t):
    q=t['m_LocalRotation'];x,y,z,w=[q[k] for k in 'xyzw'];assert abs(sum(v*v for v in [x,y,z,w])-1)<1e-5
    s=t['m_LocalScale'];p=t['m_LocalPosition']
    r=[[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)], [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)], [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]]
    result=[r[i][j]*s['xyz'[j]] if j<3 else p['xyz'[i]] for i in range(3) for j in range(4)]+[0,0,0,1]
    assert all(math.isfinite(v) for v in result);return result
def multiply(a,b):return [sum(a[i*4+k]*b[k*4+j] for k in range(4)) for i in range(4) for j in range(4)]
assert not O.exists()
proofpath=W/'gladiator-mirror-lot-audit-v385/RESULT.v1.json';proof=read(proofpath);assert proof['cards']==17 and proof['unique_prefabs']==5
queuepath=W/'queue/modular-gladiator-arena/prefabs.json';queue=read(queuepath)
preflightpath=W/'review/arena-source-support323-preflight.v1.json';preflight=read(preflightpath)
base=next(p for p in preflight['pipelines'] if p['pipeline']=='base')
assert sha(queuepath)==base['source_queue_sha256']
manifestpath=next(Path(p) for p in proof['input_pins'] if p.endswith('/manifest.v1.json') or p.endswith('\\manifest.v1.json'))
assert sha(manifestpath)==proof['input_pins'][str(manifestpath)]
assert sha(preflightpath)==read(manifestpath)['preflight_sha256']
jobs={j['id']:j for j in queue['jobs']};assert len(jobs)==len(queue['jobs'])
for card in proof['rows']:
    job=jobs[card['asset_id']];assert job['source_sha256']==card['source_sha256']
    levels={c['lod']['level'] for c in job['components']};assert len(levels)==1
    card['original_lod_level']=next(iter(levels));card['source_job_components']=job['components']
pins={str(p):sha(p) for p in [proofpath,queuepath,preflightpath,manifestpath,Path(__file__)]};groups={};rows=[]
for card in proof['rows']:groups.setdefault(card['source_path'],[]).append(card)
for name,cards in groups.items():
    path=Path(name);assert all(c['source_sha256']==sha(path) for c in cards);pins[name]=sha(path)
    meta=Path(str(path)+'.meta');assert sha(meta)==proof['input_pins'][str(meta)];pins[str(meta)]=sha(meta)
    pguid=re.search(r'^guid: ([a-f0-9]{32})$',meta.read_text('utf-8-sig'),re.M)[1]
    text=path.read_text('utf-8-sig');parts=re.split(r'^--- !u!(\d+) &(-?\d+)\s*\n',text,flags=re.M);objects={};classes={}
    for i in range(1,len(parts),3):
        kind=int(parts[i]);fid=int(parts[i+1]);assert fid not in objects and kind in {1,4,23,33,65,205},(kind,fid)
        block=yaml.safe_load(parts[i+2]);assert isinstance(block,dict) and len(block)==1
        objects[fid]=next(iter(block.values()));classes[fid]=kind
    transforms={fid:obj for fid,obj in objects.items() if classes[fid]==4}
    roots=[fid for fid,t in transforms.items() if t['m_Father']['fileID']==0];assert len(roots)==1
    seen=set();nodes=[];renderers={};gotos={t['m_GameObject']['fileID']:fid for fid,t in transforms.items()};assert len(gotos)==len(transforms)
    def walk(fid,nodepath,parent_world):
        assert fid not in seen;seen.add(fid);t=transforms[fid];go=t['m_GameObject']['fileID'];assert classes[go]==1
        local=matrix(t);world=multiply(parent_world,local);componentids=[c['component']['fileID'] for c in objects[go]['m_Component']]
        assert fid in componentids and all(c in objects for c in componentids)
        filters=[objects[c] for c in componentids if classes[c]==33];mrs=[c for c in componentids if classes[c]==23];assert len(filters)<=1 and len(mrs)<=1
        renderer=None
        if mrs:
            assert len(filters)==1;rid=mrs[0];mr=objects[rid];assert mr['m_GameObject']['fileID']==go
            mesh=filters[0]['m_Mesh'];assert mesh['fileID']!=0 and 'guid' in mesh
            materials=mr['m_Materials'];assert materials and all(m['guid'] in proof['materials'] for m in materials)
            renderer=dict(source_renderer_file_id=str(rid),prefab_guid=pguid,mesh_guid=mesh['guid'],mesh_file_id=str(mesh['fileID']),material_slots=materials,enabled=mr['m_Enabled'],cast_shadows=mr['m_CastShadows'],receive_shadows=mr['m_ReceiveShadows'],path=nodepath)
            renderers[rid]=renderer
        nodes.append(dict(path=nodepath,name=objects[go]['m_Name'],source_transform_file_id=str(fid),game_object_file_id=str(go),active=objects[go]['m_IsActive'],local_position=t['m_LocalPosition'],local_rotation=t['m_LocalRotation'],local_scale=t['m_LocalScale'],source_local_matrix_row_major=local,source_world_matrix_row_major=world,renderer=renderer,colliders=[dict(source_file_id=str(c),source=objects[c]) for c in componentids if classes[c]==65]))
        for index,child in enumerate(t['m_Children']):
            cfid=child['fileID'];assert cfid in transforms and transforms[cfid]['m_Father']['fileID']==fid
            walk(cfid,nodepath+'/'+str(index),world)
    walk(roots[0],'root',[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]);assert seen==set(transforms)
    lods=[]
    for fid,obj in objects.items():
        if classes[fid]!=205:continue
        levels=[]
        for level,lod in enumerate(obj['m_LODs']):
            refs=[r['renderer']['fileID'] for r in lod['renderers']]
            assert all(r==0 or r in renderers for r in refs)
            levels.append(dict(level=level,screen_relative_height=lod['screenRelativeHeight'],fade_width=lod['fadeTransitionWidth'],explicit_empty_renderer_slots=refs.count(0),renderers=[renderers[r] for r in refs if r]))
        lods.append(dict(source_file_id=str(fid),reference_point=obj['m_LocalReferencePoint'],size=obj['m_Size'],fade_mode=obj['m_FadeMode'],levels=levels))
    assert len(lods)==1
    for c in cards:
        level=c['original_lod_level'];assert isinstance(level,int) and 0<=level<len(lods[0]['levels']) and lods[0]['levels'][level]['renderers']
        for component in c['source_job_components']:
            assert component['lod']['group']==int(lods[0]['source_file_id'])
            node=next(n for n in nodes if n['source_transform_file_id']==str(component['transform_id']))
            renderer=node['renderer'];assert renderer in lods[0]['levels'][level]['renderers']
            assert renderer['mesh_guid']==component['mesh_guid'] and renderer['mesh_file_id']==str(component['mesh_fileid'])
            assert [m['guid'] for m in renderer['material_slots']]==component['material_guids']
    rows.append(dict(source_path=name,source_sha256=sha(path),prefab_guid=pguid,cards=[dict(id=c['asset_id'],lod=c['original_lod_level']) for c in cards],nodes=nodes,lod_groups=lods,renderer_count=len(renderers)))
for p,h in pins.items():assert sha(Path(p))==h,p
result=dict(status='five_original_prefab_hierarchies_and_seventeen_lod_bindings_bridged',prefabs=len(rows),source_cards=sum(len(r['cards']) for r in rows),renderer_bindings=sum(r['renderer_count'] for r in rows),rows=rows,input_pins=pins,native_import_identity_comparison_qualified=False,native_geometry_qualified=False,converted=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps({k:result[k] for k in ['status','prefabs','source_cards','renderer_bindings']}))
