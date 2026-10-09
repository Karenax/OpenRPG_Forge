from pathlib import Path
BASE_SCENE=Path(__file__).with_name('reopen_firepit_sky_cube_gpu_case_v1.py')
source=BASE_SCENE.read_text('utf-8').replace('firepit-sky-cube-gpu-case-v1','firepit-specular-scene-case-v1')
source=source.replace("proof['forward_stage_deltas']","proof['forward_deltas']").replace("delta['diagnostic']","delta['derived']")
source=source.replace('private_sky_witness_original_geometry_and_authored_bindings_preserved','private_specular_scene_original_geometry_and_authored_bindings_preserved')
source=source.replace('expected_gpu_cases=182,target_gpu_sampling_verified=False','source_specular_gpu_cases_verified=756,scene_render_qualified=False')
exec(compile(source,str(BASE_SCENE),'exec'),globals())
