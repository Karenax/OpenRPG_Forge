"""Close the bounded HDRI technical batch; scene specular/visual acceptance stay open."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,re

R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'source-sky-cube-install-v1'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(p.read_text('utf-8'))

def main():
    assert not (ROOT/'RESULT.v1.json').exists(), 'Previous closure evidence preserved'
    paths=dict(storage=W/'source-sky-cube-storage-check-v3/RUN-RESULT.v1.json',
        binding=W/'source-sky-cube-binding-check-v3/RUN-RESULT.v1.json',
        gpu=W/'firepit-sky-cube-gpu-reopen-v1/RESULT.v1.json',
        gpu_run=W/'firepit-sky-cube-gpu-v1/RUN-RESULT.v1.json',
        pair=W/'firepit-sky-cube-gpu-v1/GPU-PAIR.v1.json',
        web=W/'studio-current-source-wasm-check-v19/RUN-RESULT.v1.json',
        structure=W/'firepit-sky-cube-gpu-case-v1/STRUCTURE.v1.json',
        protocol=W/'firepit-sky-cube-gpu-case-v1/RESULT.v1.json',
        source_samples=W/'firepit-source-sky-sampling-reopen-v3/RESULT.v1.json',
        packet=W/'source-sky-cube-packet-v1/PACKET.v1.json')
    proof={k:read(p) for k,p in paths.items()}
    assert proof['storage']['status']=='actual_sky_cube_storage_and_residency_refusals_verified'
    assert proof['storage']['tests_verified']==5
    assert proof['binding']['status']=='canonical_sky_binding_and_atomic_scene_preflight_verified'
    assert proof['binding']['contract_tests_verified']==23 and proof['binding']['frame_tests_verified']==7
    assert proof['gpu']['status']=='actual_gpu_source_sky_182_case_sampling_verified'
    assert proof['gpu']['sky_cube_cases_verified']==182 and proof['gpu']['images_reopened']==2
    assert proof['gpu_run']['actual_exit_code']==0
    # This runner supplies input_pins rather than a changed_source_files field;
    # the unified hash pass below verifies every pinned native input again.
    assert proof['web']['status']=='actual_studio_wasm_frontend_compile_check_passed'
    assert proof['web']['actual_exit_code']==0 and not proof['web']['changed_source_files']
    assert proof['pair']['shared_sky_cube_bound_materials']==4 and proof['pair']['missing_sky_frame_refused_atomically']
    assert all(p['state']=='ready' and p['error'] is None for p in proof['pair']['source_pipelines'])
    assert proof['structure']['status']=='private_sky_witness_original_geometry_and_authored_bindings_preserved'
    assert proof['packet']['bytes']==4194288
    pins={str(Path(__file__).resolve()):sha(Path(__file__).resolve())}
    for label,value in proof.items():
        pins[str(paths[label])]=sha(paths[label])
        for p,h in value.get('input_pins',{}).items():
            assert p not in pins or pins[p]==h, f'Conflicting snapshots: {p}'
            pins[p]=h
    for p,h in pins.items():assert sha(p)==h,p
    before=W/'source-sky-cube-binding-check-v1-preimages/source_graph.rs.before'
    current=R/'crates/forge_engine/src/source_graph.rs'
    pattern=r'pub struct SourceGraphUniforms \{.*?\n\}'
    assert re.search(pattern,before.read_text('utf-8'),re.S).group(0)==re.search(pattern,current.read_text('utf-8'),re.S).group(0)
    pins[str(before)]=sha(before)
    rows=proof['gpu']['rows'];pixels=sum(g['samples'] for r in rows for g in r['cases'])
    assert pixels==34526 and all(r['channels_outside_predeclared_limit']==0 for r in rows)
    receipt=dict(status='explicit_source_sky_storage_binding_native_gpu_and_web_compile_technical_batch_verified',
        verified_utc=datetime.now(timezone.utc).isoformat(),sub_lot='L01.ECLAIRAGE.HDRI.TECHNIQUE',
        storage_tests_verified=5,contract_tests_verified=23,frame_tests_verified=7,
        shared_scene_image_materials=4,image_binding=123,sampler_binding=124,
        cube_count=1,faces=6,mips=9,native_rgba16f_bytes=4194288,
        original_basis_and_time_uniform_abi_preserved=True,authored_texture_slots_not_substituted=True,
        required_missing_resource_frame_refused_atomically=True,native_gpu_sampling_qualified=True,
        gpu_cases_verified=182,gpu_observations_verified=364,gpu_witness_pixels=pixels,
        minimum_pixels_per_case_and_capture=min(g['samples'] for r in rows for g in r['cases']),
        thresholds_defined_before_pixels=True,channels_outside_predeclared_limit=0,
        source_pins_native=proof['gpu']['source_pins_verified'],source_pins_web=len(proof['web']['input_pins']),
        web_frontend_compile_verified=True,web_gpu_render_verified=False,
        source_direction_witness_explicit=True,target_world_to_source_reflection_qualified=False,
        sky_specular_applied_to_scene=False,render_fidelity_qualified=False,full_import=False,editor_visual_verified=False,
        fully_delivered_assets_increment=0,receipt_paths={k:str(p) for k,p in paths.items()},input_pins=pins,
        next_action='L01.ECLAIRAGE.HDRI.APPLICATION: measured source specular direction, roughness, FGD, occlusion and one exposure; compare the original scene again.')
    ROOT.mkdir(exist_ok=True);(ROOT/'RESULT.v1.json').write_text(json.dumps(receipt,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in receipt.items() if k not in ['input_pins','receipt_paths']}))

if __name__=='__main__':main()
