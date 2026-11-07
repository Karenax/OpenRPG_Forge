"""Run GeneralORM albedo controls with the same meshes and original foliage."""
from pathlib import Path
recipe=Path(__file__).with_name('run_mountain_albedo_controls_v935.py')
code=recipe.read_text('utf-8').replace('mountain-albedo-controls-run-v935','mountain-generalorm-controls-run-v939').replace('mountain-albedo-controls-v934/PREPARED.v1.json','mountain-generalorm-controls-v938/PREPARED.v1.json')
code=code.replace("'MountainVertexColorControlsV934.cs'}", "'MountainVertexColorControlsV934.cs','MountainVertexColorControlsV938.cs'}")
code=code.replace("rglob('*.cs'))) == 11", "rglob('*.cs'))) == 12")
code=code.replace('native_diagnostic_albedo_controls_finished_no_image_admission','native_diagnostic_generalorm_controls_finished_no_image_admission')
exec(compile(code,str(recipe)+'#generalorm-source-and-clone-controls','exec'),globals())
