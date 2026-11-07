from pathlib import Path
recipe_path = Path(__file__).with_name('check_physics_package_pin_tests_v856.py')
recipe = recipe_path.read_text('utf-8').replace('physics-package-pin-tests-v856', 'physics-profile-pin-tests-v859')
exec(compile(recipe, str(recipe_path)+'#declared-sha256-profile', 'exec'), globals())
