"""Select explicit LOD cards while retaining their separate generic prefab card."""
from pathlib import Path
parent=Path(__file__).with_name('run_pirate_wood_lod_migration_v590.py')
code=parent.read_text('utf-8').replace('v590','v591').replace('V590','V591')
old="and a.get('source_asset_path')==path]"
new="and a.get('source_asset_path')==path and a.get('original_lod_level') in {0,1,2}]"
assert code.count(old)==1;code=code.replace(old,new)
exec(compile(code,str(parent)+'#explicit-lod-card-selection','exec'),globals())
