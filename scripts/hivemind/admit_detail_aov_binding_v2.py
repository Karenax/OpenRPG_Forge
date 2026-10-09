"""Admit only the measured two-channel default packet, not the full material."""
from pathlib import Path
import datetime,hashlib,json,struct
W=Path('F:/OpenRPG_ImportWork');N=W/'detail-native-normal-loop-aov-v16';OUT=W/'detail-default-native-rg-binding-v2'
def read(p):return json.loads(Path(p).read_bytes())
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not OUT.exists();OUT.mkdir()
    paths=[N/'RUN-RESULT.v1.json',N/'NATIVE.v1.json',N/'INDEPENDENT-REOPEN.v1.json',N/'manifest.json']
    run,native,proof,manifest=map(read,paths)
    assert run['unity_exit_code']==0 and run['native_default_rg_qualified'] and run['private_input_configuration_restored']
    assert proof['native_default_rg_qualified'] and proof['status']=='native_linear_aov_default_rg_measured_and_reopened'
    assert native['source_material_unchanged'] and native['source_renderer_unchanged'] and native['editor_play_options_restored']
    assert len(native['rows'])==36 and native['material_guid']==manifest['material_guid']
    repair=read(W/'detail-aov-private-input-compatibility-v1/REPAIR.v1.json')
    assert sha(repair['project_config'])==repair['before_sha256']
    for name,digest in proof['input_pins'].items():
        if name==repair['project_config']:assert digest==repair['after_sha256']
        else:assert sha(name)==digest,name
    for name,digest in proof['output_pins'].items():assert sha(name)==digest,name
    packet=Path(proof['packet']);assert sha(packet)==proof['packet_sha256']
    measured={}
    for row in proof['measurements']:
        assert row['channel'] in ('r','g')
        assert row['channel'] not in measured or measured[row['channel']]==row['value']
        measured[row['channel']]=row['value']
    assert set(measured)=={'r','g'} and packet.read_bytes()==struct.pack('<2f',measured['r'],measured['g'])
    assert min(proof['positive_pixels_by_view'].values())>=32 and len(proof['positive_pixels_by_view'])==6
    result=dict(status='native_linear_aov_rg_packet_admitted_for_detail_only',
                native_default_rg_binding_qualified=True,material_guid=native['material_guid'],channels=['r','g'],
                packet=str(packet),packet_sha256=sha(packet),measured_rg=[measured['r'],measured['g']],
                positive_pixels_by_view=proof['positive_pixels_by_view'],native_images_verified=36,
                input_pins={str(p):sha(p) for p in paths},private_input_configuration_restored=True,
                normal_decode_qualified=False,whole_prefab_qualified=False,gpu_forge_parity_qualified=False,
                full_import=False,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
