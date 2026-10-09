"""Apply handle ownership correction at the innermost Rust host generator."""
from pathlib import Path
BASE=Path(__file__).with_name('capture_firepit_diffuse_scene_v1.py')
source=BASE.read_text('utf-8').replace('source_size.y*0.5-0.01','source_size.y*.5-.01',1)
marker='# The wrapper\'s source variable contains the BRDF host generator at this point.'
assert source.count(marker)==1
command="assert source.count('MeshMaterial3d(ground_material)')==1\nsource=source.replace('MeshMaterial3d(ground_material)','MeshMaterial3d(ground_material.clone())')\n"
source=source.replace(marker,'commands+='+repr(command)+'\n'+marker)
needle='source=source.replace(needle,insertion+needle)'
assert source.count(needle)==1
source=source.replace(needle,"insertion+=\"source=source.replace('firepit-diffuse-scene-v1','firepit-diffuse-scene-v4')\\n\"\n"+needle)
exec(compile(source,str(BASE),'exec'),globals())
