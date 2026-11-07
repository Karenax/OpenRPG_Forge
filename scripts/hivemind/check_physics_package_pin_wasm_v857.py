from pathlib import Path
recipe_path = Path(__file__).with_name('check_reviewed_physics_wasm_v814.py')
code = recipe_path.read_text('utf-8').replace('placement-reviewed-physics-wasm-v814', 'physics-package-pin-wasm-v857')
exec(compile(code, str(recipe_path)+'#complete-package-selection', 'exec'), globals())
