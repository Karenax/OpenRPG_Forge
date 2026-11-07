"""Reuse six native controls with official SDK derivation of original inputs."""
from pathlib import Path
recipe=Path(__file__).with_name('run_mountain_clone_control_v905.py')
code=recipe.read_text('utf-8')
code=code.replace('mountain-native-clone-control-run-v905','mountain-native-sdk-derivation-run-v909')
code=code.replace('mountain-native-parameter-comparison-v904','mountain-native-parameter-comparison-v908')
code=code.replace("'MountainParameterComparisonV904.cs'}", "'MountainParameterComparisonV904.cs','MountainParameterComparisonV908.cs'}")
code=code.replace('== 5")','== 6")')
code=code.replace('native_clone_control_finished_requires_scope_review','native_sdk_derivation_finished_requires_scope_review')
exec(compile(code,str(recipe)+'#original-authored-inputs-sdk-state', 'exec'),globals())
