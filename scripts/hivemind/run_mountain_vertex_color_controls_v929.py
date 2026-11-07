"""Run paired source/diagnostic-only clone controls, with explicit clone IDs."""
from pathlib import Path
recipe=Path(__file__).with_name('run_mountain_clone_control_v905.py');wrapper=recipe.read_text('utf-8')
anchor="exec(compile(code,str(recipe)+'#six-native-clone-controls', 'exec'),globals())";assert wrapper.count(anchor)==1
adapt="""
code=code.replace('mountain-native-clone-control-run-v905','mountain-vertex-color-controls-run-v929').replace('mountain-native-parameter-comparison-v904/PREPARED.v1.json','mountain-vertex-color-controls-v928/PREPARED.v1.json')
code=code.replace("prepared['source_cards'] == 47", "prepared['source_cards'] == 3").replace("prepared['capture_cases'] == 282", "prepared['capture_cases'] == 18")
code=code.replace("'MountainParameterComparisonV904.cs'}", "'MountainParameterComparisonV904.cs','MountainParameterComparisonV908.cs','MountainParameterComparisonV912.cs','MountainVertexDiagnosticV924.cs','MountainVertexColorControlsV928.cs'}")
code=code.replace("rglob('*.cs'))) == 5", "rglob('*.cs'))) == 9").replace("len(batch['cases']) == 282", "len(batch['cases']) == 18").replace('range(47)','range(3)').replace('len(pairs)==94','len(pairs)==6')
code=code.replace('native_clone_control_finished_requires_scope_review','native_diagnostic_vertex_color_controls_finished_no_image_admission')
"""
wrapper=wrapper.replace(anchor,adapt+'\n'+anchor);exec(compile(wrapper,str(recipe)+'#diagnostic-only-color-clones','exec'),globals())
