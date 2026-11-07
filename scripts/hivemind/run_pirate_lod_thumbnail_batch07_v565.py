"""Capture the next untouched image lot; retain prior refusals in their own repair lot."""
from pathlib import Path
recipe=Path(__file__).with_name('run_pirate_lod_thumbnail_batch05_v502.py')
code=recipe.read_text('utf-8').replace('pirate-lod-thumbnail-batch05-v502','pirate-lod-thumbnail-batch07-v565').replace('L08.IMAGE.PIRATE.LOD.05','L08.IMAGE.PIRATE.LOD.07')
code=code.replace('pirate-lod-thumbnail-batch04-v491/HELPER-OWNERSHIP.v1.json','pirate-palm-wind-native-reference-v558/HELPER-OWNERSHIP.v1.json')
marker="exec(compile(code, str(parent) + '#next-reviewed-lod-image-lot', 'exec'), globals())"
injection='''
old="groups=[rows for path,rows in sorted(pool.items()) if all(level in rows for level in [0,1,2]) and not rows[1].get('preview') and not rows[2].get('preview')][:8]"
new="""previous_capture=W/'pirate-lod-thumbnail-batch06-v513/PREPARED.v1.json'
    previous=read(previous_capture)
    previous_paths={c['prefab_path'] for c in previous['cases']}
    groups=[rows for path,rows in sorted(pool.items()) if path not in previous_paths and all(level in rows for level in [0,1,2]) and not rows[1].get('preview') and not rows[2].get('preview')][:8]"""
assert code.count(old)==1;code=code.replace(old,new)
code=code.replace("for p in [parent,audit_path,Path(__file__),base", "for p in [parent,recipe,audit_path,previous_capture,Path(__file__),base")
'''+marker
assert code.count(marker)==1
exec(compile(code.replace(marker,injection),str(recipe)+'#next-untouched-lod-images','exec'),globals())
