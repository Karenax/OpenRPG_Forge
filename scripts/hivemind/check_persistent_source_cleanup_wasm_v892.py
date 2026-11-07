from pathlib import Path
recipe = Path(__file__).with_name('check_reviewed_physics_wasm_v814.py')
code = recipe.read_text('utf-8').replace('placement-reviewed-physics-wasm-v814', 'persistent-source-cleanup-wasm-v892')
exec(compile(code, str(recipe)+'#owned-source-nodes-cleanup', 'exec'), globals())
