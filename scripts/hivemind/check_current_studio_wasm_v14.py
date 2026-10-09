"""Compile the production preview shadow callsite; retain the prior receipts."""
from pathlib import Path
BASE=Path(__file__).with_name('check_current_studio_wasm_v13.py')
source=BASE.read_text('utf-8')
assert source.count('studio-current-source-wasm-check-v13')==1
source=source.replace('studio-current-source-wasm-check-v13','studio-current-source-wasm-check-v14')
needle='files = {Path(__file__).resolve(), REPO'
assert source.count(needle)==1
source=source.replace(needle,'files = {Path(__file__).resolve(), BASE, REPO')
exec(compile(source,str(BASE),'exec'),globals())
