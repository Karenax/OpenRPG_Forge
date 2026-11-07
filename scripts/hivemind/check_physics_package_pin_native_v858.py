from pathlib import Path
recipe_path = Path(__file__).with_name('check_placement_physics_native_clippy_v784.py')
code = recipe_path.read_text('utf-8').replace('placement-physics-native-clippy-v784', 'physics-package-pin-native-v858')
code = code.replace('physics_controls_commands_verified', 'studio_full_physics_package_pin_clippy_verified')
exec(compile(code, str(recipe_path)+'#complete-package-selection', 'exec'), globals())
