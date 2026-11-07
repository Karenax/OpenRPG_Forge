"""Adapt the proven EV8 comparison to four wood-material source prefabs."""
from pathlib import Path
import subprocess,sys
S=Path(__file__).resolve().parent
base=S/'run_pirate_migration_render_v196.py';target=S/'run_pirate_wood_render_v209.py'
assert not target.exists()
text=base.read_text('utf-8-sig')
text=text.replace('v196','v209').replace('V196','V209')
text=text.replace('pirate-material-native-defaults-reopen-v187','pirate-wood-defaults-reopen-v208')
text=text.replace('a441b81a4efaff04a85744086c47c402','abc00000000004592200770135964921')
start=text.index('    var mask=AssetDatabase.LoadAssetAtPath<Texture2D>')
end=text.index('    int replaced=0;',start)
text=text[:start]+'    // The complete reverse diff has no texture or authored selector changes.\n'+text[end:]
before="original=read(W/'pirate-source-thumbnail-batch-0113-0120-v163/PREPARED.v1.json')\n    selected=original['cases'][1:3];cases=[]"
after="original=read(W/'pirate-source-thumbnail-batch-0121-0128-v201/PREPARED.v1.json')\n    refused={r['id'] for r in read(W/'pirate-source-thumbnail-batch-0121-0128-guard-v202/RESULT.v1.json')['cases_refused_source_parity']}\n    selected=[c for c in original['cases'] if c['id'] in refused];assert len(selected)==4;cases=[]"
assert text.count(before)==1;text=text.replace(before,after)
for before,after in [('prefabs=2,captures=8,views=16','prefabs=4,captures=16,views=32'),
    ("len(batch['cases'])==8","len(batch['cases'])==16"),('for i in range(2):','for i in range(4):'),
    ('Two prefabs, two views each;','Four prefabs, two views each;'),
    ('restored pre-migration masks/default texture','restored pre-migration stencil write masks')]:
    assert text.count(before)==1,before;text=text.replace(before,after)
target.write_text(text,'utf-8')
subprocess.run([sys.executable,'-B',str(target)],check=True)
