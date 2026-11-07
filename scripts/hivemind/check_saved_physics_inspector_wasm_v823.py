"""Compile the real inspector and its historical physics IPC for WebGPU."""
from pathlib import Path
parent = Path(__file__).with_name('check_reviewed_physics_wasm_v814.py')
code = parent.read_text('utf-8').replace('placement-reviewed-physics-wasm-v814', 'saved-physics-inspector-wasm-v823')
code = code.replace('files=[Path(__file__),', 'files=[parent,Path(__file__),')
exec(compile(code, str(parent)+'#saved-reference-inspector', 'exec'), globals())
