"""Retain every render/collider use of a shared mesh; never duplicate or discard roles."""
from pathlib import Path
_native_recipe=Path(__file__).with_name('convert_house_native_geometry_v652.py')
text=_native_recipe.read_text('utf-8-sig').replace('house-converted-native-geometry-v652','house-converted-shared-native-geometry-v653')
needle="for p in [Path(__file__),recipe,N/";assert text.count(needle)==1;text=text.replace(needle,"for p in [Path(__file__),_native_recipe,recipe,N/")
needle="for p in [Path(__file__),_native_recipe,recipe,N/'RUN-RESULT.v1.json',N/'NATIVE-RESULT.v1.json',N/'PREPARED.v1.json']:pins[str(p)]=sha(p)"
addition="""old='assert len(used) == 1';assert code.count(old)==1;code=code.replace(old,'assert len(used)>=1 and len({(b["path"],b["role"]) for b in used})==len(used)')
old='binding = used[0]';assert code.count(old)==1;code=code.replace(old,'binding = next((b for b in used if b["role"]=="mesh_filter"),used[0])')
old='binding=binding,';assert code.count(old)==1;code=code.replace(old,'binding=binding,source_mesh_usages=used,')
"""+needle
assert text.count(needle)==1;text=text.replace(needle,addition)
exec(compile(text,str(_native_recipe)+'#preserve-shared-mesh-roles','exec'),globals())
