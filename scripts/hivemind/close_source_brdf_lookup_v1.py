"""Close only shared BRDF storage/intake/sampling/compile, never scene fidelity."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'source-brdf-lookup-install-v1'


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()


def main():
    paths=[W/'source-brdf-storage-check-v2/RUN-RESULT.v1.json',
        W/'source-brdf-layout-check-v1/RESULT.v1.json',
        W/'firepit-brdf-lookup-gpu-v4/RUN-RESULT.v1.json',
        W/'firepit-brdf-lookup-gpu-v4/GPU-PAIR.v1.json',
        W/'firepit-brdf-lookup-gpu-reopen-v4/RESULT.v1.json',
        W/'studio-current-source-wasm-check-v18/RUN-RESULT.v1.json']
    storage,layout,gpu,pair,sampling,web=map(read,paths)
    assert storage['actual_exit_code']==0 and storage['tests_verified']==4
    assert layout['tests_verified']==20 and layout['new_brdf_tests_verified']==3
    assert gpu['actual_exit_code']==0 and pair['missing_brdf_frame_refused_atomically']
    assert sampling['status']=='actual_gpu_brdf_35_case_sampling_verified'
    assert len(sampling['rows'])==2 and all(len(r['cases'])==35 for r in sampling['rows'])
    assert all(r['channels_outside_predeclared_limit']==0 for r in sampling['rows'])
    assert web['actual_exit_code']==0 and web['status']=='actual_studio_wasm_frontend_compile_check_passed'
    assert not web['changed_source_files'] and not storage['changed_source_files']
    for receipt in [storage,layout,gpu,sampling,web]:
        for path,digest in receipt['input_pins'].items():
            assert sha(path)==digest,path
    source_pins={str(p):sha(p) for p in [Path(__file__).resolve(),*paths,
        W/'source-brdf-lookup-case-v1/CASE.v1.json',
        W/'firepit-brdf-lookup-gpu-case-v1/CASE.v1.json']}
    result=dict(status='shared_source_brdf_storage_intake_gpu_and_web_technical_batch_verified',
        finished_utc=datetime.now(timezone.utc).isoformat(),sub_lot='L01.ECLAIRAGE.BRDF',
        storage_tests_verified=4,source_contract_tests_verified=20,
        gpu_unique_cases_verified=35,gpu_cases_across_two_images_verified=70,
        gpu_witness_pixels_verified=sum(g['samples'] for r in sampling['rows'] for g in r['cases']),
        gpu_source_files_verified=sampling['source_pins_verified'],
        web_source_files_verified=len(web['input_pins']),shared_brdf_bound_materials=4,
        missing_required_brdf_frame_refused_atomically=True,
        source_lut_sampling_qualified=True,web_frontend_compiled=True,
        web_gpu_runtime_qualified=False,combined_diffuse_lighting_qualified=False,
        specular_environment_qualified=False,exposure_qualified=False,
        render_fidelity_qualified=False,editor_visual_verified=False,full_import=False,
        input_pins=source_pins,
        next_action='L01.ECLAIRAGE.APPLICATION: apply measured SH and source FGD/occlusion/exposure to the original source scene and receiver; capture HDRI/specular context and resolve shadow parity')
    output=ROOT/'RESULT.v1.json'
    assert not output.exists()
    output.write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))


if __name__=='__main__':
    main()
