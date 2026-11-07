from pathlib import Path
recipe_path = Path(__file__).with_name('check_physics_package_pin_wasm_v857.py')
recipe = recipe_path.read_text('utf-8').replace('physics-package-pin-wasm-v857', 'physics-profile-pin-wasm-v860')
exec(compile(recipe, str(recipe_path)+'#declared-sha256-profile', 'exec'), globals())
