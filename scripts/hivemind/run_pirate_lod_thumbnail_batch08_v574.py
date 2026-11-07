"""Route the next eight uncaptured source groups using the pinned material frontier."""
from pathlib import Path
recipe=Path(__file__).with_name('run_pirate_lod_thumbnail_batch05_v502.py')
code=recipe.read_text('utf-8').replace('pirate-lod-thumbnail-batch05-v502','pirate-lod-thumbnail-batch08-v574').replace('L08.IMAGE.PIRATE.LOD.05','L08.IMAGE.PIRATE.LOD.08')
code=code.replace('pirate-lod-thumbnail-batch04-v491/HELPER-OWNERSHIP.v1.json','pirate-palm-wind-native-reference-v558/HELPER-OWNERSHIP.v1.json')
marker="exec(compile(code, str(parent) + '#next-reviewed-lod-image-lot', 'exec'), globals())"
injection='''
old="groups=[rows for path,rows in sorted(pool.items()) if all(level in rows for level in [0,1,2]) and not rows[1].get('preview') and not rows[2].get('preview')][:8]"
new="""frontierpath=W/'pirate-image-frontier-v573/RESULT.v1.json'
    frontier=read(frontierpath)
    assert registry['revision']==frontier['catalog_revision']
    for p,h in frontier['input_pins'].items():assert sha(p)==h,p
    allowed={r['source_path'] for r in frontier['eligible_groups']}
    groups=[rows for path,rows in sorted(pool.items()) if path in allowed and all(level in rows for level in [0,1,2]) and not rows[1].get('preview') and not rows[2].get('preview')][:8]"""
assert code.count(old)==1;code=code.replace(old,new)
code=code.replace("for p in [parent,audit_path,Path(__file__),base", "for p in [parent,recipe,audit_path,frontierpath,Path(__file__),base")
'''+marker
assert code.count(marker)==1
exec(compile(code.replace(marker,injection),str(recipe)+'#pinned-material-frontier-routing','exec'),globals())
