"""Reopen corrected production host evidence without replacing the first receipt."""
from pathlib import Path
BASE=Path(__file__).with_name('reopen_source_host_shadows_v1.py')
source=BASE.read_text('utf-8').replace('studio-source-host-shadows-v1','studio-source-host-shadows-v2').replace('studio-source-host-shadows-reopen-v1','studio-source-host-shadows-reopen-v2')
exec(compile(source,str(BASE),'exec'),globals())
