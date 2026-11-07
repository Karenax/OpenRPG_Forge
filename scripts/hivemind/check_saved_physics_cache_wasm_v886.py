from pathlib import Path
recipe_path = Path(__file__).with_name('check_reviewed_physics_wasm_v814.py')
code = recipe_path.read_text('utf-8').replace('placement-reviewed-physics-wasm-v814','saved-physics-cache-wasm-v886')
exec(compile(code,str(recipe_path)+'#bounded-historical-cache-full-identity','exec'),globals())
