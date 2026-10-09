"""Retain the v2 Rust borrow refusal and rerun the corrected residency tests."""
from pathlib import Path
BASE_V2=Path(__file__).with_name('check_source_sky_cube_storage_v2.py')
source_v3=BASE_V2.read_text('utf-8').replace(
    "source_v2=source_v2.replace('source-sky-cube-storage-check-v1','source-sky-cube-storage-check-v2')",
    "source_v2=source_v2.replace('source-sky-cube-storage-check-v1','source-sky-cube-storage-check-v3')")
exec(compile(source_v3,str(BASE_V2),'exec'),globals())
