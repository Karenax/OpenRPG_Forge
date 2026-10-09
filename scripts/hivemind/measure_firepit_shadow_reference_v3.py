"""Reopen current static reference, retaining both failed attempts."""
from pathlib import Path
BASE=Path(__file__).with_name('measure_firepit_shadow_reference_v1.py')
source=BASE.read_text('utf-8').replace('firepit-shadow-source-reference-v1','firepit-shadow-source-reference-v3').replace('firepit-shadow-source-measurement-v1','firepit-shadow-source-measurement-v3')
exec(compile(source,str(BASE),'exec'),globals())
