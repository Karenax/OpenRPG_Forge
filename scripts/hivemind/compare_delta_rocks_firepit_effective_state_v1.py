"""Measure authored versus effective properties; never silently adopt HDRP migration."""
from pathlib import Path
import datetime
import json
from reopen_delta_remaining_success_v1 import f32
from verify_native_urp_material_snapshot import sha

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'delta-rocks-firepit-effective-state-v1'

def read(path): return json.loads(Path(path).read_text('utf-8-sig'))

def main():
    assert not ROOT.exists()
    source_path=W/'nested-prefab-delta-material-source-ir-v1/RESULT.v1.json'
    sources={r['material_identity']['guid']:r for r in read(source_path)['rows']}
    rocks_root=W/'delta-rockstemperate-native-independent-reopen-v1'
    firepit_root=W/'delta-firepit-native-serialization-reopen-v1'
    firepit_native=W/'unity-delta-firepit-closure-retry-v3/NATIVE-RESULT.v1.json'
    native_paths=[rocks_root/'NATIVE.success-projection.v1.json',firepit_native]
    receipts=[rocks_root/'RESULT.v1.json',firepit_root/'RESULT.v1.json']
    assert sha(firepit_native)==read(receipts[1])['native_result_sha256']
    pins={str(p):sha(p) for p in [Path(__file__),source_path,*native_paths,*receipts]}
    rows=[]
    for path in native_paths:
        native=read(path)['cases'][0]
        source=sources[native['material_guid']]
        original=source['source_material_properties']
        assert sha(source['source_material']['path'])==source['source_material']['sha256']
        pins[source['source_material']['path']]=source['source_material']['sha256']
        floats={k:v for entry in original['m_SavedProperties']['m_Floats'] for k,v in entry.items()}
        colors={k:v for entry in original['m_SavedProperties']['m_Colors'] for k,v in entry.items()}
        compared=[]
        for prop in native['properties']:
            name=prop['name']
            if prop['type'] in ['Float','Range','Int'] and name in floats:
                before,after=f32(floats[name]),f32(prop['native_float'])
            elif prop['type'] in ['Vector','Color'] and name in colors:
                before=[f32(colors[name][axis]) for axis in 'rgba']
                after=[f32(prop['native_vector'][axis]) for axis in 'xyzw']
            else: continue
            compared.append(dict(property=name,type=prop['type'],original_float32=before,
                effective_float32=after,equal=before==after))
        saved=json.loads(native['native_material_json'])['Material']
        original_keywords=set(original.get('m_ValidKeywords',[]))
        source_disabled=set(original.get('disabledShaderPasses',[]))
        pass_changes=[dict(name=p['name'],source_enabled=p['name'] not in source_disabled,native_enabled=p['enabled'])
            for p in native['passes'] if (p['name'] not in source_disabled)!=p['enabled']]
        rows.append(dict(material_guid=native['material_guid'],compared_properties=compared,
            effective_property_differences=[r for r in compared if not r['equal']],
            added_native_keywords=sorted(set(native['keywords'])-original_keywords),
            removed_original_keywords=sorted(original_keywords-set(native['keywords'])),
            disabled_pass_differences=pass_changes,
            source_custom_render_queue=original['m_CustomRenderQueue'],
            native_saved_custom_render_queue=saved['m_CustomRenderQueue'],
            native_effective_render_queue=native['render_queue'],
            source_default_queue_requires_original_shader_default=original['m_CustomRenderQueue']==-1,
            original_authoring_version_qualified=False,original_render_state_parity_qualified=False))
    assert len(rows)==2 and all(sha(p)==h for p,h in pins.items())
    result=dict(status='two_original_and_effective_material_states_measured',materials_compared=2,
        observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),rows=rows,input_pins=pins,
        effective_property_differences=sum(len(r['effective_property_differences']) for r in rows),
        original_render_state_parity_qualified=False,gpu_qualified=False,studio_verified=False,full_import=False)
    ROOT.mkdir()
    with (ROOT/'RESULT.v1.json').open('x',encoding='utf-8') as stream:
        json.dump(result,stream,indent=2,allow_nan=False)
    print(json.dumps(dict(materials_compared=2,effective_property_differences=result['effective_property_differences'],full_import=False)))

if __name__=='__main__': main()
