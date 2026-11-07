"""Guard all six source materials and their actual native active property bindings."""
from pathlib import Path
from datetime import datetime,timezone
import ast,hashlib,json,math,re,yaml
W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-native-material-bindings-v404';PROJECT=W/'gladiator-native-geometry-v380/project'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
helper=Path(__file__).with_name('audit_gladiator_material_migration_v398.py');tree=ast.parse(helper.read_text('utf-8'))
namespace=dict(Path=Path,yaml=yaml,re=re);exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'material','refs'}],type_ignores=[]),str(helper)+'#unchanged-readers','exec'),namespace)
mp=W/'gladiator-mirror-lot-audit-v385/RESULT.v1.json';lot=read(mp)
np=W/'gladiator-remaining-geometry-reopen-v402/RESULT.v1.json';native=read(np)
ip=W/'gladiator-active-material-ir-v386/RESULT.v1.json';irproof=read(ip)
cp=W/'gladiator-lot-dependency-closure-v394/RESULT.v1.json';closure=read(cp)
allowed={'m_Parent':{'fileID':0},'m_ModifiedSerializedProperties':0,'m_LockedProperties':None,'m_AllowLocking':1}
pins={str(p):sha(p) for p in [mp,np,ip,cp,helper,Path(__file__)]};rows=[]
for snapshot in native['native_material_snapshots']:
    guid=snapshot['identity']['guid'];source_info=lot['materials'][guid];source=Path(source_info['path']);assert sha(source)==source_info['sha256']
    relative=next(r['relative_project_path'] for r in closure['files'] if r['source']==str(source));private=PROJECT/relative
    original,old=namespace['material'](source);current,new=namespace['material'](private)
    assert all(k in current and current[k]==v for k,v in original.items()),source
    additions={k:v for k,v in current.items() if k not in original}
    assert all(k in allowed and v==allowed[k] for k,v in additions.items()),additions
    assert (snapshot['shader']['guid'],snapshot['shader']['file_id'])==(original['m_Shader']['guid'],str(original['m_Shader']['fileID']))
    irrow=next(r for r in irproof['rows'] if r['guid']==guid);irpath=Path(irrow['path']);assert sha(irpath)==irrow['sha256'];ir=read(irpath)
    active=namespace['refs'](ir['terminals'])|namespace['refs'](ir['nodes'])|namespace['refs'](ir['selected_branches'])
    floats={p['property']:p for p in snapshot['floats']};vectors={p['property']:p for p in snapshot['vectors']};textures={p['property']:p for p in snapshot['textures']};bindings=[]
    for name in sorted(active):
        kinds=[kind for kind in old if name in old[kind]];assert len(kinds)==1,(name,kinds);kind=kinds[0];value=old[kind][name]
        if kind=='floats':
            actual=floats[name]['value'];assert math.isfinite(actual) and abs(actual-value)<=1e-6+1e-6*abs(value)
            binding=dict(property=name,kind=kind,source=value,native=actual,native_f32_absolute_difference=abs(actual-value))
        elif kind=='colors':
            actual=vectors[name]['value'];expected=dict(zip('xyzw',[value[c] for c in 'rgba']))
            assert all(math.isfinite(actual[k]) and abs(actual[k]-expected[k])<=1e-6+1e-6*abs(expected[k]) for k in expected)
            binding=dict(property=name,kind=kind,source=value,native=vectors[name],gpu_color_transfer_qualified=False)
        else:
            actual=textures[name];expected=value['m_Texture'];assert not actual['explicitly_null'] and actual['identity']
            assert (actual['identity']['guid'],actual['identity']['file_id'])==(expected['guid'],str(expected['fileID']))
            assert actual['identity']['type']=='UnityEngine.Texture2D' and actual['width']>0 and actual['height']>0 and actual['mips']>0
            binding=dict(property=name,kind=kind,source=value,native=actual,imported_sampler_and_mip_parity_qualified=False)
        bindings.append(binding)
    pins[str(source)]=sha(source);pins[str(private)]=sha(private);pins[str(irpath)]=sha(irpath)
    rows.append(dict(material_guid=guid,name=irrow['name'],source=str(source),private_imported=str(private),all_source_serialized_fields_preserved=True,added_editor_fields=additions,native_shader=snapshot['shader'],active_property_bindings=bindings,source_alpha_test=old['floats'].get('_AlphaCutoffEnable'),source_normal_mode=old['floats'].get('_DoubleSidedNormalMode'),source_double_sided=old['floats'].get('_DoubleSidedEnable')))
assert len(rows)==6
for p,h in pins.items():assert sha(p)==h,p
result=dict(status='six_materials_source_fields_and_native_active_bindings_verified',rows=rows,input_pins=pins,material_property_data_qualified=True,material_gpu_qualified=False,imported_texture_sampling_qualified=False,converted=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(status=result['status'],materials=len(rows),active_property_bindings=sum(len(r['active_property_bindings']) for r in rows),texture_bindings=sum(b['kind']=='textures' for r in rows for b in r['active_property_bindings']))))
