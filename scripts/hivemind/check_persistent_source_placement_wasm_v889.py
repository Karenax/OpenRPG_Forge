from pathlib import Path
recipe_path = Path(__file__).with_name('check_reviewed_physics_wasm_v814.py')
code = recipe_path.read_text('utf-8').replace('placement-reviewed-physics-wasm-v814','persistent-source-placement-wasm-v889')
exec(compile(code,str(recipe_path)+'#common-root-owned-source-data-mount','exec'),globals())
