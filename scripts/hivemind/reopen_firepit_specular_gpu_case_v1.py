from pathlib import Path
BASE=Path(__file__).with_name('reopen_firepit_sky_cube_gpu_case_v1.py')
source=BASE.read_text('utf-8').replace('firepit-sky-cube-gpu-case-v1','firepit-specular-gpu-case-v1').replace('private_sky_witness_original_geometry_and_authored_bindings_preserved','private_specular_witness_original_geometry_and_authored_bindings_preserved').replace('expected_gpu_cases=182','expected_gpu_cases=756')
exec(compile(source,str(BASE),'exec'),globals())
