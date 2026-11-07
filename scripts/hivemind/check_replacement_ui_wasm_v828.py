from pathlib import Path
parent = Path(__file__).with_name('check_reviewed_physics_wasm_v814.py')
code = parent.read_text('utf-8').replace('placement-reviewed-physics-wasm-v814', 'reviewed-replacement-ui-wasm-v828').replace('files=[Path(__file__),', 'files=[parent,Path(__file__),')
exec(compile(code, str(parent)+'#physical-replacement-ui', 'exec'), globals())
