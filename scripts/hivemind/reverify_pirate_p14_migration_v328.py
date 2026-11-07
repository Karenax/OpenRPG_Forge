"""Reverify existing terminal P14 captures; preserve the v327 count refusal."""
from pathlib import Path
from datetime import datetime, timezone
import ast, hashlib, json, textwrap

S=Path(__file__).resolve().parent
W=Path('F:/OpenRPG_ImportWork')
O=W/'pirate-p14-migration-render-v327'
ROOT=W/'pirate-p14-migration-verification-v328'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()

assert not ROOT.exists()
state=read(O/'RUN-RESULT.v1.json')
assert state['finished_utc'] and state['unity_exit_code']==0
assert state['status']=='pirate_migration_comparison_refusal_retained'
assert state['error']=='AssertionError()' and not state['input_changes']
prep=read(O/'PREPARED.v1.json')
assert len(prep['cases'])==28
pins=dict(prep['input_pins'])
for p,h in pins.items():assert sha(p)==h,p
helper=O/'PirateP14MigrationRenderV327.cs'
installed=W/'unity-library-source-thumbnail-pirate-bench-v2/project/Assets/Editor'/helper.name
assert sha(helper)==sha(installed)
recipe=S/'run_pirate_p14_migration_render_v327.py'
text=recipe.read_text('utf-8-sig')
start=text.index("        batch=read(O/'NATIVE-RESULT.v1.json');assert len(batch['cases'])==40")
end=text.index('    except Exception as exc:',start)
verification=textwrap.dedent(text[start:end]).replace("len(batch['cases'])==40", "len(batch['cases'])==28").replace('range(10)','range(7)').replace('expected_prefabs=10','expected_prefabs=7')
assert "range(10)" not in verification
exec(compile(verification,str(recipe)+'#count-corrected-verification','exec'))
assert state['expected_prefabs']==7
state.pop('error',None);state.pop('traceback',None)
for p in [recipe,Path(__file__),O/'RUN-RESULT.v1.json',O/'PREPARED.v1.json',O/'NATIVE-RESULT.v1.json',installed]:pins[str(p)]=sha(p)
state.update(input_pins=pins,original_count_refusal_preserved=True,captures_repeated=False,verification_count_correction='28 actual captures for 7 prefabs, 4 states each',finished_utc=datetime.now(timezone.utc).isoformat(),full_import=False)
ROOT.mkdir()
(ROOT/'RESULT.v1.json').write_text(json.dumps(state,indent=2),'utf-8')
print(json.dumps({k:state[k] for k in ['successful_prefabs','expected_prefabs','all_views_repeat_exact','all_views_migration_exact','comparison_refusals']}))
