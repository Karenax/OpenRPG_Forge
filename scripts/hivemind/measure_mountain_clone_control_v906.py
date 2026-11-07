"""Separate material cloning from parameter restoration using native witnesses."""
from pathlib import Path

recipe=Path(__file__).with_name('measure_mountain_parameter_comparison_v903.py')
code=recipe.read_text('utf-8')
code=code.replace('mountain-native-parameter-measurements-v903','mountain-native-clone-control-measurements-v906')
code=code.replace('mountain-native-parameter-comparison-run-v902','mountain-native-clone-control-run-v905')
code=code.replace('native_parameter_comparison_finished_requires_scope_review','native_clone_control_finished_requires_scope_review')
code=code.replace('mountain-native-parameter-comparison-v901','mountain-native-parameter-comparison-v904')
code=code.replace("prep['cases'][4*index+1]", "prep['cases'][6*index+1]")
code=code.replace("prep['cases'][4*index+2]", "prep['cases'][6*index+3]")
code=code.replace("prep['cases'][4*index+3]", "prep['cases'][6*index+5]")
anchor="        repeated=read(prep['cases'][6*index+5]['output'])"
assert anchor in code
code=code.replace(anchor,anchor+"\n        cloned=read(prep['cases'][6*index+2]['output'])\n        original_repeat=read(prep['cases'][6*index+4]['output'])")
anchor="            assert a['raw_sha256']==c['raw_sha256']"
assert anchor in code
code=code.replace(anchor,anchor+"\n            clone=cloned['images'][view]\n            source_repeat=original_repeat['images'][view]")
code=code.replace("first=np.fromfile(a['raw_path']", "first=np.fromfile(clone['raw_path']")
code=code.replace("raw_exact=a['raw_sha256']==b['raw_sha256'],", "raw_exact=clone['raw_sha256']==b['raw_sha256'],\n                     raw_clone_current_equal=a['raw_sha256']==clone['raw_sha256'],\n                     raw_original_repeat_exact=b['raw_sha256']==source_repeat['raw_sha256'],")
code=code.replace("current_images=current['images']", "current_images=cloned['images']")
code=code.replace("'native_parameter_raw_differences_and_shader_defaults_measured'", "'native_clone_control_parameter_deltas_and_repeatability_measured'")
code=code.replace("cards=47,views=94,different_raw_views=", "cards=47,views=94,clone_current_different_views=sum(not row['raw_clone_current_equal'] for row in comparisons),original_repeat_different_views=sum(not row['raw_original_repeat_exact'] for row in comparisons),different_raw_views=")
code=code.replace("['status','different_raw_views','differing_cards','mismatch_counts_by_property']", "['status','clone_current_different_views','original_repeat_different_views','different_raw_views','differing_cards','mismatch_counts_by_property']")
exec(compile(code,str(recipe)+'#cloned-versus-cloned-controls', 'exec'),globals())
