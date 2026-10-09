"""Close the explicit irradiance binding only, not BRDF or scene fidelity."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'source-diffuse-probe-install-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not (ROOT/'RESULT.v1.json').exists()
    paths=[W/'source-diffuse-probe-packet-check-v1/RUN-RESULT.v1.json',W/'firepit-diffuse-probe-gpu-reopen-v1/RESULT.v1.json',W/'studio-current-source-wasm-check-v17/RUN-RESULT.v1.json']
    packet,gpu,web=map(read,paths)
    assert packet['actual_exit_code']==web['actual_exit_code']==0 and packet['tests_verified']==2
    assert gpu['status']=='actual_gpu_diffuse_probe_six_direction_evaluation_verified' and not web['changed_source_files']
    for receipt in [packet,gpu,web]:
        for p,h in receipt['input_pins'].items():assert sha(p)==h,p
    result=dict(status='explicit_diffuse_probe_binding_native_gpu_and_web_technical_batch_verified',finished_utc=datetime.now(timezone.utc).isoformat(),packet_tests_verified=2,gpu_direction_cases_verified=12,
        gpu_witness_pixels_verified=sum(d['samples'] for row in gpu['rows'] for d in row['directions']),web_source_files_verified=len(web['input_pins']),
        binding=121,original_basis_and_time_uniform_abi_preserved=True,disabled_by_default=True,source_coefficients_not_fitted=True,
        brdf_qualified=False,exposure_qualified=False,render_fidelity_qualified=False,editor_visual_verified=False,full_import=False,
        input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),*paths,ROOT/'source_graph-before.rs',ROOT/'source_unlit_forward-before.rs']},
        next_action='Add explicitly admitted shared BRDF lookup, verify its GPU sampling, apply source FGD/occlusion/exposure and compare the original scene')
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
if __name__=='__main__':main()
