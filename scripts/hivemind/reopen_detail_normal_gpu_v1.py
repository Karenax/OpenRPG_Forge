"""Rehash the completed Detail normal experiment without expanding its scope."""
from pathlib import Path
import datetime
import hashlib
import json
import subprocess
import sys

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'detail-normal-decoder-independent-reopen-v1'
GPU = W / 'detail-normal-decoder-gpu-v1'
CANDIDATE = W / 'detail-normal-mode2-bevy-candidate-v1'

def read(p): return json.loads(p.read_text('utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def function(t,n):
    start=t.index('fn '+n+'('); opening=t.index('{',start); depth=1
    for i in range(opening+1,len(t)):
        depth += (t[i]=='{')-(t[i]=='}')
        if depth==0:return t[start:i+1]
    raise ValueError('Incomplete helper')

def main():
    assert not ROOT.exists()
    state=read(GPU/'RUN-RESULT.v1.json'); parity=read(GPU/'DECODER-PARITY.v1.json')
    candidate=read(CANDIDATE/'RESULT.v1.json')
    assert state['native_normal_textures_verified']==1 and state['normal_mips_verified']==12
    assert state['build_exit_code']==state['execution_exit_code']==state['comparison_exit_code']==0
    assert sha(GPU/'DECODER-PARITY.v1.json')==state['parity_receipt_sha256']
    assert parity['exact_normal_textures_compared']==1 and parity['mip_levels_compared']==12
    assert parity['sampled_texel_lod_coordinates']==49152
    assert parity['candidate_mode2_matches_float_rgorag_gpu_reference'] and parity['all_match_builtin_unpack_reference']
    assert parity['tolerances']==dict(xy=1e-6,sqrt_conditioning_z=5e-4,alpha=1e-6)
    pins={}
    for packet in [candidate,read(GPU/'PREPARED.v1.json')]:
        for p,h in packet['input_pins'].items():
            assert sha(Path(p))==h,p
            pins[p]=h
    for p,h in parity['proof_pins'].items():
        assert sha(Path(p))==h,p
        pins[p]=h
    exe=GPU/'normal-decoder-gpu-probe.immutable.v1.exe'
    assert sha(exe)==state['executable_sha256']
    shader=Path(candidate['shader']); sample=GPU/'src/sample.wgsl'
    assert sha(shader)==candidate['shader_sha256']
    for n in candidate['helper_names']:
        assert function(shader.read_text('utf-8-sig'),n)==function(sample.read_text('utf-8-sig'),n)
    ROOT.mkdir()
    with (ROOT/'negative-controls.log').open('xb') as log:
        code=subprocess.run([sys.executable,'-B',str(GPU/'compare.py'),'--self-test'],stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW).returncode
    assert code==0
    controls=read(ROOT/'negative-controls.log');assert controls['negative_controls_passed']==5
    licenses=[]
    for p in [W/'detail-native-default-stages-v1/LICENSE.Unity.md',CANDIDATE/'LICENSE.Bevy.MIT.txt']:
        out=ROOT/p.name;out.write_bytes(p.read_bytes());assert sha(out)==sha(p)
        licenses.append(dict(source=str(p),copy=str(out),sha256=sha(out)))
    for p in [GPU/'RUN-RESULT.v1.json',GPU/'DECODER-PARITY.v1.json',exe,shader,sample,Path(__file__).resolve()]:pins[str(p)]=sha(p)
    result=dict(status='detail_exact_candidate_normal_gpu_parity_reopened',normal_textures_verified=1,
        normal_mips_verified=12,coordinates_verified=49152,actual_bevy_compositions=4,
        exact_helpers_match_staged_detail_candidate=True,negative_controls_passed=5,
        matches_float_rgorag_reference=True,matches_unity_builtin_reference=True,
        original_shadergraph_render_parity_qualified=False,renderer_installed=False,
        whole_prefab_qualified=False,studio_verified=False,full_import=False,
        source_proof_pins=pins,derived_licenses=licenses,
        finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k not in ['source_proof_pins','derived_licenses']}))

if __name__=='__main__':main()
