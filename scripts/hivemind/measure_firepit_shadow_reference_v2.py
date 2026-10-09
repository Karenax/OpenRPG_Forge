"""Reopen the explicitly static v2 capture; preserve the v1 refusal."""
from pathlib import Path
BASE=Path(__file__).with_name('measure_firepit_shadow_reference_v1.py')
source=BASE.read_text('utf-8').replace('firepit-shadow-source-reference-v1','firepit-shadow-source-reference-v2').replace('firepit-shadow-source-measurement-v1','firepit-shadow-source-measurement-v2')
exec(compile(source,str(BASE),'exec'),globals())
