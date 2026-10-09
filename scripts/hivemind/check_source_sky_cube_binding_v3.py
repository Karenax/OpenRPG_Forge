"""Retain v1/v2 refusals and supply the production module's crate-root imports."""
from pathlib import Path
BASE=Path(__file__).with_name('check_source_sky_cube_binding_v1.py')
source=BASE.read_text('utf-8').replace('source-sky-cube-binding-check-v1','source-sky-cube-binding-check-v3')
source=source.replace('version="=1.0.228"','version="=1.0.229"')
needle=r'mod source_bundle;\n'
assert source.count(needle)==1
source=source.replace(needle,r'mod source_bundle;\nuse source_bundle as source_graph_bundle;\nuse forge_assets::source_material_parameters;\n')
exec(compile(source,str(BASE),'exec'),globals())
