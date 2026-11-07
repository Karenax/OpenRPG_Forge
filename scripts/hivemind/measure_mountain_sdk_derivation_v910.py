"""Measure official SDK reconstruction only when author-input guards all pass."""
from pathlib import Path

wrapper_path=Path(__file__).with_name('measure_mountain_clone_control_v906.py')
wrapper=wrapper_path.read_text('utf-8')
anchor="exec(compile(code,str(recipe)+'#cloned-versus-cloned-controls', 'exec'),globals())"
assert wrapper.count(anchor)==1
adapt=r'''
code=code.replace('mountain-native-clone-control-measurements-v906','mountain-native-sdk-derivation-measurements-v910')
code=code.replace('mountain-native-clone-control-run-v905','mountain-native-sdk-derivation-run-v909')
code=code.replace('mountain-native-parameter-comparison-v904','mountain-native-parameter-comparison-v908')
code=code.replace('native_clone_control_finished_requires_scope_review','native_sdk_derivation_finished_requires_scope_review')
anchor="        original_repeat=read(prep['cases'][6*index+4]['output'])"
assert anchor in code
guard="""
        expected_guids={entry['guid'] for entry in prep['cases'][6*index+3]['comparisons']}
        for report in [original,original_repeat]:
            observations=report['material_comparisons']
            assert {entry['guid'] for entry in observations}==expected_guids
            assert all(entry['sdk_derived_state_applied'] and entry['authored_inputs_preserved']
                       and entry['active_author_float_count']>0 and entry['sdk_material_state'] for entry in observations)
"""
code=code.replace(anchor,anchor+guard)
code=code.replace('native_clone_control_parameter_deltas_and_repeatability_measured','official_sdk_derivation_author_inputs_guarded_native_deltas_measured')
code=code.replace('original_sdk_pipeline_equivalence=False,native_parameter_equivalence_verified=False,', "original_sdk_pipeline_equivalence=False,active_original_author_inputs_preserved=True,native_parameter_equivalence_verified=False,")
'''
wrapper=wrapper.replace(anchor,adapt+'\n'+anchor)
exec(compile(wrapper,str(wrapper_path)+'#official-derived-state-and-author-guards', 'exec'),globals())
