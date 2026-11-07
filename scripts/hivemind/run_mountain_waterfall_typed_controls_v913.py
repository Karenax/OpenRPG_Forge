"""Run six type-aware waterfall controls without repeating validated cards."""
from pathlib import Path
recipe=Path(__file__).with_name('run_mountain_clone_control_v905.py')
wrapper=recipe.read_text('utf-8')
anchor="exec(compile(code,str(recipe)+'#six-native-clone-controls', 'exec'),globals())"
assert wrapper.count(anchor)==1
adapt="""
code=code.replace('mountain-native-clone-control-run-v905','mountain-waterfall-typed-controls-run-v913')
code=code.replace('mountain-native-parameter-comparison-v904/PREPARED.v1.json','mountain-waterfall-typed-controls-v912/PREPARED.v1.json')
code=code.replace("prepared['source_cards'] == 47", "prepared['source_cards'] == 1").replace("prepared['capture_cases'] == 282", "prepared['capture_cases'] == 6")
code=code.replace("'MountainParameterComparisonV904.cs'}", "'MountainParameterComparisonV904.cs','MountainParameterComparisonV908.cs','MountainParameterComparisonV912.cs'}")
code=code.replace("rglob('*.cs'))) == 5", "rglob('*.cs'))) == 7").replace("len(batch['cases']) == 282", "len(batch['cases']) == 6").replace('range(47)','range(1)').replace('len(pairs)==94','len(pairs)==2')
code=code.replace('native_clone_control_finished_requires_scope_review','native_waterfall_typed_controls_finished_requires_review')
"""
wrapper=wrapper.replace(anchor,adapt+'\n'+anchor)
exec(compile(wrapper,str(recipe)+'#waterfall-typed-author-guards', 'exec'),globals())
