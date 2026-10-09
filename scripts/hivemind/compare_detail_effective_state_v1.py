"""Compare Detail's authored and effective state without adopting migration implicitly."""
from pathlib import Path
import datetime,hashlib,json,math,struct

W=Path('F:/OpenRPG_ImportWork');ROOT=W/'detail-authored-effective-state-v1'
NATIVE=W/'detail-native-material-state-v1'

def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def f32(v):
    assert isinstance(v,(int,float)) and math.isfinite(v)
    return struct.unpack('<f',struct.pack('<f',v))[0]

def main():
    assert not ROOT.exists()
    state=read(NATIVE/'RUN-RESULT.v1.json');proof=read(NATIVE/'INDEPENDENT-REOPEN.v1.json')
    assert state['native_materials_verified']==1 and state['actual_unity_exit_code']==0
    assert proof['native_materials_verified']==1 and proof['active_texture_bindings_verified']==2
    assert proof['private_material_unchanged'] and len(proof['refused_mutations'])==4
    path=NATIVE/'NATIVE-RESULT.v1.json';assert sha(path)==proof['native_result_sha256']==state['native_result_sha256']
    native=read(path)['cases'][0]
    contract_path=W/'detail-original-render-contracts-v1/RESULT.v1.json'
    original=read(contract_path)['rows'][0];assert original['material_guid']==native['material_guid']
    pins=dict(read(contract_path)['input_pins'])
    pins.update(read(NATIVE/'PREPARED.v1.json')['input_pins'])
    for p in [path,contract_path,NATIVE/'RUN-RESULT.v1.json',NATIVE/'INDEPENDENT-REOPEN.v1.json',Path(__file__).resolve()]:pins[str(p)]=sha(p)
    compared=[]
    for prop in native['properties']:
        name=prop['name'];kind=prop['type']
        if kind in ['Float','Range','Int'] and name in original['source_floats']:
            before=f32(original['source_floats'][name]);after=f32(prop['native_float'])
        elif kind in ['Color','Vector'] and name in original['source_colors']:
            before=[f32(original['source_colors'][name][c]) for c in 'rgba']
            after=[f32(prop['native_vector'][c]) for c in 'xyzw']
        else:continue
        compared.append(dict(property=name,type=kind,original_float32=before,effective_float32=after,equal=before==after))
    properties={p['name']:p for p in native['properties']}
    assert f32(properties['_Roughness']['native_float'])==5.0
    assert f32(properties['_AOIntensity']['native_float'])==1.0
    mask=properties['_Mask']
    assert mask['type']=='Texture' and mask['texture_null'] is True
    assert not mask['texture_guid'] and not mask['texture_file_id']
    authored=set(original['source_valid_keywords']);disabled=set(original['source_disabled_shader_passes'])
    changes=[dict(pass_name=p['name'],authored_enabled=p['name'] not in disabled,native_enabled=p['enabled'])
        for p in native['passes'] if (p['name'] not in disabled)!=p['enabled']]
    for p,h in pins.items():assert sha(Path(p))==h,p
    result=dict(status='detail_authored_and_effective_state_compared',material_guid=native['material_guid'],
        compared_properties=compared,effective_property_differences=[r for r in compared if not r['equal']],
        added_native_keywords=sorted(set(native['keywords'])-authored),removed_author_keywords=sorted(authored-set(native['keywords'])),
        disabled_pass_differences=changes,source_custom_render_queue=original['source_custom_render_queue'],
        effective_render_queue=native['render_queue'],native_null_mask_observed=True,
        authored_roughness_and_ao_preserved=True,private_material_unchanged=True,
        native_default_rg_has_separate_linear_aov_proof=True,original_hdrp_version_qualified=False,
        render_state_migration_implicitly_adopted=False,gpu_render_parity_qualified=False,full_import=False,
        input_pins=pins,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps(dict(properties_compared=len(compared),differences=len(result['effective_property_differences']),
        native_null_mask_observed=True,render_queue=native['render_queue'],passes=len(native['passes']),full_import=False)))

if __name__=='__main__':main()
