"""Check current common scene sampling in the actual Studio Web frontend."""
from pathlib import Path
BASE=Path(__file__).with_name('check_current_studio_wasm_v13.py')
source=BASE.read_text('utf-8').replace('studio-current-source-wasm-check-v13','studio-current-source-wasm-check-v20')
source=source.replace('files = {Path(__file__).resolve(), REPO','files = {Path(__file__).resolve(), BASE, REPO')
exec(compile(source,str(BASE),'exec'),globals())
