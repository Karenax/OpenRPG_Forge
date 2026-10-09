"""Use the repository's exact Serde pin; retain the v1 resolver refusal."""
from pathlib import Path
BASE=Path(__file__).with_name('check_source_sky_cube_binding_v1.py')
source=BASE.read_text('utf-8').replace('source-sky-cube-binding-check-v1','source-sky-cube-binding-check-v2')
source=source.replace('version="=1.0.228"','version="=1.0.229"')
exec(compile(source,str(BASE),'exec'),globals())
