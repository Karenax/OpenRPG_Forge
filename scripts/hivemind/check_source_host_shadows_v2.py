"""Retest current host after the WASM Component derive import repair."""
from pathlib import Path
BASE=Path(__file__).with_name('check_source_host_shadows_v1.py')
source=BASE.read_text('utf-8').replace('studio-source-host-shadows-v1','studio-source-host-shadows-v2').replace('openrpg-source-host-shadows-v1','openrpg-source-host-shadows-v2')
source=source.replace("'Path(__file__).resolve(),Path('","'Path(__file__).resolve(),Path('+repr(str(Path(__file__).with_name('check_source_host_shadows_v1.py')))+'),Path('")
exec(compile(source,str(BASE),'exec'),globals())
