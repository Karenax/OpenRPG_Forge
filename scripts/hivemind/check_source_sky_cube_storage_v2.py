"""Retain v1 evidence; exercise the new residency guard and captured storage."""
from pathlib import Path
BASE_V1=Path(__file__).with_name('check_source_sky_cube_storage_v1.py')
source_v2=BASE_V1.read_text('utf-8')
source_v2=source_v2.replace('source-sky-cube-storage-check-v1','source-sky-cube-storage-check-v2')
source_v2=source_v2.replace('expected_tests=4','expected_tests=5')
source_v2=source_v2.replace('tests_verified=4 if passed','tests_verified=5 if passed')
source_v2=source_v2.replace('4 passed; 0 failed','5 passed; 0 failed')
source_v2=source_v2.replace("image.data_order,bevy::render::render_resource::TextureDataOrder::MipMajor); }",
    "image.data_order,bevy::render::render_resource::TextureDataOrder::MipMajor); assert!(_binding.validate_residency(&images).is_ok()); }")
source_v2=source_v2.replace('actual_sky_cube_storage_admission_and_refusals_verified',
    'actual_sky_cube_storage_and_residency_refusals_verified')
exec(compile(source_v2,str(BASE_V1),'exec'),globals())
