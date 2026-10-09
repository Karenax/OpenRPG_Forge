"""The actual Naga compiler must refuse an omitted default-data input."""
from pathlib import Path
import datetime
import hashlib
import json
import subprocess

W = Path('F:/OpenRPG_ImportWork')
ROOT = W/'detail-native-default-stages-negative-controls-v1'

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

if __name__ == '__main__':
    assert not ROOT.exists()
    parent_path = W/'detail-native-default-stages-v1/RESULT.v1.json'
    parent = json.loads(parent_path.read_text('utf-8-sig'))
    assert parent['actual_naga_exit_code']==0 and parent['explicit_native_default_host_input_required']
    path = Path(parent['probe'])
    assert sha(path)==parent['probe_sha256']
    original = path.read_text('utf-8')
    marker = 'native_default_mask_rg:vec2<f32>,'
    assert original.count(marker)==1
    validator = W/'native-hdrp-nine-source-programs-v1/rock-surface-kernel-port-v1/target/debug/openrpg-rock-kernel-validator.exe'
    assert sha(validator)=='bb928cfed4c84b4918fc306a23fae8818e8d5a0fc85b0f9444edbc6be9c55e1b'
    ROOT.mkdir()
    rows=[]
    for name,source in [('missing_default_input',original.replace(marker,'')),
        ('renamed_default_input',original.replace(marker,'wrong_default_mask_rg:vec2<f32>,'))]:
        candidate=ROOT/(name+'.wgsl')
        candidate.write_text(source,'utf-8')
        with (ROOT/(name+'.stdout.log')).open('xb') as out,(ROOT/(name+'.stderr.log')).open('xb') as err:
            code=subprocess.run([str(validator),str(candidate)],stdout=out,stderr=err,
                creationflags=subprocess.CREATE_NO_WINDOW).returncode
        assert code!=0,'Required host field was silently accepted'
        rows.append(dict(control=name,actual_naga_exit_code=code,source_sha256=sha(candidate),
            diagnostic_sha256=sha(ROOT/(name+'.stderr.log'))))
    assert sha(path)==parent['probe_sha256']
    receipt=dict(status='actual_naga_refused_both_missing_default_input_controls',controls=rows,
        parent_receipt_sha256=sha(parent_path),full_import=False,host_binding_qualified=False,
        gpu_render_parity_qualified=False,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT/'RESULT.v1.json').write_text(json.dumps(receipt,indent=2),'utf-8')
    print(json.dumps(receipt))
