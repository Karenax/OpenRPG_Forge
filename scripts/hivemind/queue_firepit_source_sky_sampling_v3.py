"""Retain prior refusals; run with the corrected private legacy helper."""
from pathlib import Path
BASE=Path(__file__).with_name('queue_firepit_source_sky_sampling_v2.py')
source=BASE.read_text('utf-8').replace('firepit-source-sky-sampling-v2','firepit-source-sky-sampling-v3').replace("+'V2'","+'V3'")
exec(compile(source,str(BASE),'exec'),globals())
