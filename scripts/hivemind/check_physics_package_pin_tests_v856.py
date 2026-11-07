"""Exercise substitution with the same carrier/manifest but a different collider."""
from pathlib import Path
recipe_path = Path(__file__).with_name('check_placement_physics_native_clippy_v784.py')
code = recipe_path.read_text('utf-8')
code = code.replace('placement-physics-native-clippy-v784', 'physics-package-pin-tests-v856')
before = "'clippy','--locked','--offline','-j','1','-p','forge_studio','--lib','--bin','forge-studio','--','-D','warnings'"
after = "'test','--locked','--offline','-j','1','-p','forge_assets','--lib','source_physics_package::tests::full_package_pin_distinguishes_candidates_with_same_carrier_and_manifest'"
assert before in code
code = code.replace(before, after).replace('physics_controls_commands_verified', 'full_physics_package_substitution_refused')
exec(compile(code, str(recipe_path)+'#full-package-pin-test', 'exec'), globals())
