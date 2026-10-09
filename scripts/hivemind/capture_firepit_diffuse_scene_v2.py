"""Retain v1 compile refusal; match the generator before float normalization."""
from pathlib import Path
BASE=Path(__file__).with_name('capture_firepit_diffuse_scene_v1.py')
source=BASE.read_text('utf-8')
source=source.replace('source_size.y*0.5-0.01','source_size.y*.5-.01',1)
needle='source=source.replace(needle,insertion+needle)'
assert source.count(needle)==1
source=source.replace(needle,"insertion+=\"source=source.replace('firepit-diffuse-scene-v1','firepit-diffuse-scene-v2')\\n\"\n"+needle)
exec(compile(source,str(BASE),'exec'),globals())
