"""Reopen FirePit capture, preserving and measuring Unity's private serialization."""
from pathlib import Path
import copy
import json
from reopen_delta_remaining_success_v1 import f32
from verify_native_urp_material_snapshot import reconcile, sha

W = Path('F:/OpenRPG_ImportWork')
PRIOR = W/'unity-delta-firepit-closure-retry-v3'
ROOT = W/'delta-firepit-native-serialization-reopen-v1'
GUID = 'abc00000000009294800760372203426'

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

def main():
    assert not ROOT.exists()
    run, prepared, manifest, native = [read(PRIOR/name) for name in
        ['RUN-RESULT.v1.json','PREPARED.v1.json','manifest.v1.json','NATIVE-RESULT.v1.json']]
    assert run.get('finished_utc') and run['actual_unity_exit_code']==0
    assert run['status']=='refused_or_unverified'
    assert native['requested']==native['captured']==1 and native['failed']==0
    assert len(reconcile(manifest,native))==1
    assert len(manifest['cases'][0]['required_textures'])==3
    row = next(r for r in read(W/'nested-prefab-delta-material-source-ir-v1/RESULT.v1.json')['rows']
               if r['material_identity']['guid']==GUID)
    original = Path(row['source_material']['path'])
    assert sha(original)==row['source_material']['sha256']
    private = Path(prepared['project'])/original.relative_to(W/'hivemind-run/native-american-village/source')
    assert str(private) in run['error']
    pins = dict(prepared['input_pins'])
    assert pins.pop(str(private))==sha(original)
    assert sha(private)!=sha(original)
    for path,digest in pins.items():
        assert sha(path)==digest, 'Unexpected changed source/capture: '+path
    properties = json.loads(native['cases'][0]['native_material_json'])['Material']['m_SavedProperties']
    differences=[]
    for key,kind in [('m_Floats','float'),('m_Colors','color')]:
        expected={k:v for entry in row['source_material_properties']['m_SavedProperties'][key] for k,v in entry.items()}
        captured={entry['first']:entry['second'] for entry in properties[key]}
        for name,value in expected.items():
            other=captured.get(name)
            same=other is not None and (f32(value)==f32(other) if kind=='float' else
                                       all(f32(value[k])==f32(other[k]) for k in 'rgba'))
            if not same: differences.append(dict(kind=kind,property=name,original=value,native=other))
    controls=[]
    for name in ['wrong_shader','uncompiled_pass','missing_material','wrong_texture']:
        bad=copy.deepcopy(native)
        if name=='wrong_shader': bad['cases'][0]['shader_guid']='0'*32
        elif name=='uncompiled_pass': bad['cases'][0]['passes'][0]['compiled']=False
        elif name=='missing_material': bad['cases'].pop()
        else:
            prop=manifest['cases'][0]['required_textures'][0]['property']
            next(r for r in bad['cases'][0]['properties'] if r['name']==prop)['texture_guid']='0'*32
        try: reconcile(manifest,bad)
        except ValueError as error: controls.append(dict(test=name,reason=str(error)))
        else: raise AssertionError('Altered native capture accepted')
    ROOT.mkdir()
    snapshots=[]
    for source,name in [(original,'original-source.mat'),(private,'unity-private-after.mat')]:
        target=ROOT/name
        with target.open('xb') as stream: stream.write(source.read_bytes())
        assert sha(target)==sha(source)
        pins[str(target)]=sha(target)
        snapshots.append(dict(source=str(source),snapshot=str(target),sha256=sha(target)))
    for path in [Path(__file__),PRIOR/'manifest.v1.json',PRIOR/'NATIVE-RESULT.v1.json',
                 PRIOR/'RUN-RESULT.v1.json',PRIOR/'PREPARED.v1.json']:
        pins[str(path)]=sha(path)
    receipt=dict(status='firepit_capture_and_three_bindings_reopened_with_serialization_difference',
        native_materials_reopened=1,active_texture_bindings_verified=3,
        original_source_sha256=sha(original),private_after_sha256=sha(private),
        native_result_sha256=sha(PRIOR/'NATIVE-RESULT.v1.json'),
        private_serialization_snapshots=snapshots,source_and_native_saved_float_color_differences=differences,
        refused_mutations=controls,input_pins=pins,original_sources_unchanged=True,
        original_effective_render_state_comparison_pending=True,
        private_serialization_unchanged=False,gpu_render_parity_qualified=False,
        studio_verified=False,full_import=False)
    with (ROOT/'RESULT.v1.json').open('x',encoding='utf-8') as stream:
        json.dump(receipt,stream,indent=2,allow_nan=False)
    print(json.dumps(dict(native_materials_reopened=1,active_bindings=3,
        saved_property_differences=len(differences),full_import=False)))

if __name__=='__main__': main()
