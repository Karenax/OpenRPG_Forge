"""Run source/white-albedo material-clone experiments without changing meshes."""
from pathlib import Path
recipe=Path(__file__).with_name('run_mountain_vertex_color_controls_v929.py')
code=recipe.read_text('utf-8').replace('mountain-vertex-color-controls-run-v929','mountain-albedo-controls-run-v935').replace('mountain-vertex-color-controls-v928/PREPARED.v1.json','mountain-albedo-controls-v934/PREPARED.v1.json')
code=code.replace("'MountainVertexColorControlsV928.cs'}", "'MountainVertexColorControlsV928.cs','MountainVertexDiagnosticV932.cs','MountainVertexColorControlsV934.cs'}")
code=code.replace("rglob('*.cs'))) == 9", "rglob('*.cs'))) == 11")
code=code.replace('native_diagnostic_vertex_color_controls_finished_no_image_admission','native_diagnostic_albedo_controls_finished_no_image_admission')
exec(compile(code,str(recipe)+'#source-and-white-albedo-controls','exec'),globals())
