"""Require direct unclamped native response before deriving an RG host packet."""
from pathlib import Path
import datetime
import hashlib
import json
import struct

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'detail-default-native-gain-v11'
OUT=W/'detail-default-native-rg-binding-v1'

def read(path):return json.loads(path.read_text('utf-8-sig'))
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

if __name__=='__main__':
    assert not OUT.exists()
    terminal=read(ROOT/'RUN-RESULT.v1.json')
    assert terminal.get('finished_utc') and terminal['unity_exit_code']==0
    prepared=read(ROOT/'PREPARED.v1.json')
    report=read(ROOT/'NATIVE.v1.json')
    assert report['source_material_unchanged'] and report['source_renderer_unchanged'] and report['debug_material_settings_restored']
    assert len(report['rows'])==36 and report['surface_data_only']
    OUT.mkdir()
    result=dict(status='checking_native_unclamped_rg_response',full_import=False,
        native_default_rg_binding_qualified=False,renderer_installed=False,whole_prefab_qualified=False,
        input_pins={},per_mode=[])
    try:
        groups={}
        for row in report['rows']:
            assert row['diagnostic_clone_roughness']==0.125 and row['diagnostic_clone_ao_intensity']==0.25
            assert row['effective_hd_camera_time']==0 and row['same_hd_request_camera'] and row['native_debug_mode_active']
            assert row['debug_surface_mode']==(1005 if row['camera']<3 else 1006)
            path=Path(row['path']);assert path.resolve().is_relative_to((ROOT/'native-pixels').resolve())
            raw=path.read_bytes();assert len(raw)==131072 and sha(path)==row['sha256']
            groups.setdefault(row['camera'],{})[row['variant']]=list(struct.iter_unpack('<4e',raw))
            result['input_pins'][str(path)]=sha(path)
        assert set(groups)==set(range(6)) and all(len(group)==6 for group in groups.values())
        for mode,label,white,black in [(1005,'g',0.875,1.0),(1006,'r',0.25,0.0)]:
            observed=0
            for camera in (range(3) if mode==1005 else range(3,6)):
                group=groups[camera];null=group['original_null']
                assert null==group['original_null_repeat']==group['explicit_white_control']
                for p,q in zip(null,group['explicit_black_control'],strict=True):
                    if p!=q:
                        assert p==(white,white,white,1.0) and q==(black,black,black,1.0), 'Native response differs from source graph transfer'
                        observed+=1
            assert observed>=32
            value=(1.0-white)/0.125 if mode==1005 else white/0.25
            assert value==1.0
            result['per_mode'].append(dict(mode=mode,channel=label,positive_pixels=observed,
                native_null_value=white,native_black_value=black,derived_value=value))
        for path,digest in prepared['input_pins'].items():
            assert sha(Path(path))==digest,'Pinned source changed: '+path
        for path in [ROOT/'PREPARED.v1.json',ROOT/'RUN-RESULT.v1.json',ROOT/'NATIVE.v1.json',Path(__file__).resolve()]:
            result['input_pins'][str(path)]=sha(path)
        packet=struct.pack('<2f',1.0,1.0)
        (OUT/'native_default_mask_rg.f32le').write_bytes(packet)
        result.update(status='native_default_rg_measured_through_unclamped_source_transfer',
            native_default_rg_binding_qualified=True,rgba_default_qualified=False,
            material_guid=report['material_guid'],property='_Mask',channels=['r','g'],values=[1.0,1.0],
            packet=str(OUT/'native_default_mask_rg.f32le'),packet_sha256=hashlib.sha256(packet).hexdigest(),
            original_material_unchanged=True,native_material_migration_qualified=False,
            gpu_render_parity_qualified=False)
    except Exception as error:
        result.update(status='refused_or_unverified',error=repr(error))
    result['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
    (OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({key:value for key,value in result.items() if key!='input_pins'}))
