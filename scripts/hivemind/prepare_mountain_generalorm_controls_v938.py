"""White BaseMap diagnostic for GeneralORM, leaving SpeedTree foliage untouched."""
from pathlib import Path
recipe=Path(__file__).with_name('prepare_mountain_albedo_controls_v934.py')
code=recipe.read_text('utf-8').replace('mountain-albedo-controls-v934','mountain-generalorm-controls-v938').replace('V934','V938')
code=code.replace('original.shader.name.IndexOf("SpeedTree",StringComparison.OrdinalIgnoreCase)<0','original.shader.name!="Shader Graphs/S_GeneralORM"')
code=code.replace('_MainTex','_BaseMap').replace('diagnostic_white_main_texture','diagnostic_white_base_map')
code=code.replace('only SpeedTree MainTex replaced','only GeneralORM BaseMap replaced')
exec(compile(code,str(recipe)+'#generalorm-source-albedo-only','exec'),globals())
