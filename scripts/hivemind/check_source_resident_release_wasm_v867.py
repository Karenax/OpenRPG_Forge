from pathlib import Path
recipe_path = Path(__file__).with_name('check_reviewed_physics_wasm_v814.py')
code = recipe_path.read_text('utf-8').replace('placement-reviewed-physics-wasm-v814','source-resident-release-wasm-v867')
exec(compile(code,str(recipe_path)+'#source-host-owned-cleanup','exec'),globals())
