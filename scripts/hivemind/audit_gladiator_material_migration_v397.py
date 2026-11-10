"""Classify private SDK material migrations and guard every active source property."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,re,yaml
W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-material-migration-audit-v397';P=W/'gladiator-native-geometry-v380'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def material(p):
    text=p.read_text('utf-8-sig');text=re.sub(r'^%[^\n]*\n|^--- !u!\d+ &-?\d+[^\n]*\n','',text,flags=re.M)
    value=yaml.safe_load(text)['Material'];saved=value['m_SavedProperties']
    props={}
    for source,kind in [('m_Floats','floats'),('m_Colors','colors'),('m_TexEnvs','textures')]:
        result={}
        for item in saved[source]:
            assert len(item)==1;name,v=next(iter(item.items()));assert name not in result;result[name]=v
        props[kind]=result
    return value,props
def refs(value):
    found=set()
    if isinstance(value,dict):
        if str(value.get('provenance','')).startswith('material:'):found.add(value['provenance'].split(':',1)[1])
        for v in value.values():found.update(refs(v))
    elif isinstance(value,list):
        for v in value:found.update(refs(v))
    return found
assert not O.exists()
rp=P/'RUN-RESULT.v1.json';run=read(rp);assert run['status']=='native_flag_geometry_extracted_review_pending' and not run['source_changes']
pp=P/'PREPARED.v1.json';prep=read(pp);irp=W/'gladiator-active-material-ir-v386/RESULT.v1.json';irproof=read(irp)
pins={str(p):sha(p) for p in [rp,pp,irp,Path(__file__)]};rows=[]
for targetname in run['private_importer_changes']:
    row=next(r for r in prep['private_copies'] if r['copy']==targetname);source=Path(row['source']);target=Path(targetname)
    assert source.suffix==target.suffix=='.mat' and sha(source)==row['sha256']
    original,old=material(source);current,new=material(target)
    assert original['m_Shader']==current['m_Shader'],'Native shader changed'
    guid=re.search(r'^guid: ([a-f0-9]{32})$',Path(str(source)+'.meta').read_text('utf-8-sig'),re.M)[1]
    irrow=next(r for r in irproof['rows'] if r['guid']==guid);ip=Path(irrow['path']);assert sha(ip)==irrow['sha256'];ir=read(ip)
    active=refs(ir['terminals'])|refs(ir['nodes'])|refs(ir['selected_branches'])
    state={'_DoubleSidedEnable','_DoubleSidedNormalMode','_AlphaCutoffEnable','_AlphaCutoff','_SurfaceType','_BlendMode','_AlphaToMask'}
    guards=[]
    for name in sorted(active|state):
        kinds=[kind for kind in old if name in old[kind]];assert len(kinds)==1,(name,kinds)
        kind=kinds[0];assert name in new[kind] and old[kind][name]==new[kind][name],(name,old[kind].get(name),new[kind].get(name))
        guards.append(dict(name=name,kind=kind,source=current_value=old[kind][name]))
    diffs={kind:dict(added={k:v for k,v in new[kind].items() if k not in old[kind]},removed={k:v for k,v in old[kind].items() if k not in new[kind]},changed={k:dict(source=v,imported=new[kind][k]) for k,v in old[kind].items() if k in new[kind] and v!=new[kind][k]}) for kind in old}
    pins[str(source)]=sha(source);pins[str(target)]=sha(target);pins[str(ip)]=sha(ip)
    rows.append(dict(material_guid=guid,name=irrow['name'],source=str(source),private_imported=str(target),active_and_render_properties_unchanged=guards,saved_property_differences=diffs,source_keywords=original.get('m_ValidKeywords',original.get('m_ShaderKeywords')),imported_keywords=current.get('m_ValidKeywords',current.get('m_ShaderKeywords')),source_gpu_equivalence_qualified=False))
assert len(rows)==2
for p,h in pins.items():assert sha(p)==h,p
result=dict(status='two_private_material_migrations_classified_active_source_properties_unchanged',rows=rows,input_pins=pins,private_sdk_migration_render_equivalence_qualified=False,converted=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(status=result['status'],materials=[dict(name=r['name'],guarded_properties=len(r['active_and_render_properties_unchanged']),added_floats=list(r['saved_property_differences']['floats']['added']),changed_floats=r['saved_property_differences']['floats']['changed']) for r in rows])))
