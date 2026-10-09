"""Retain v1/v2 refusals; clone the receiver handle needed for pipeline checks."""
from pathlib import Path
BASE=Path(__file__).with_name('capture_firepit_diffuse_scene_v1.py')
source=BASE.read_text('utf-8').replace('source_size.y*0.5-0.01','source_size.y*.5-.01',1)
needle='source=source.replace(needle,insertion+needle)'
assert source.count(needle)==1
extra="source=source.replace('firepit-diffuse-scene-v1','firepit-diffuse-scene-v3')\n"
extra+="source=source.replace('MeshMaterial3d(ground_material)','MeshMaterial3d(ground_material.clone())')\n"
source=source.replace(needle,'insertion+='+repr(extra)+'\n'+needle)
exec(compile(source,str(BASE),'exec'),globals())
