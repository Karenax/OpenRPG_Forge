"""Check the explicit diffuse-probe binding in the actual Studio frontend."""
from pathlib import Path
BASE=Path(__file__).with_name('check_current_studio_wasm_v13.py')
source=BASE.read_text('utf-8').replace('studio-current-source-wasm-check-v13','studio-current-source-wasm-check-v17')
source=source.replace('files = {Path(__file__).resolve(), REPO','files = {Path(__file__).resolve(), BASE, REPO')
exec(compile(source,str(BASE),'exec'),globals())
