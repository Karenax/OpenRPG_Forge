"""Check the finite-ray fix and native/wasm derive import boundary in Studio."""
from pathlib import Path
parent = Path(__file__).with_name('check_source_collider_host_web_v485.py')
code = parent.read_text('utf-8').replace('source-collider-host-studio-web-check-v485', 'source-ray-and-projection-studio-web-check-v505')
exec(compile(code, str(parent) + '#finite-ray-wasm-check', 'exec'), globals())
