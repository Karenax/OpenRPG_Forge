"""Run the existing WGSL emitter for the exact preserved palm topology."""
from pathlib import Path
import hashlib, json
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork')
I=W/'pirate-palm-wgsl-input-v532';O=W/'pirate-palm-wgsl-v532'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not I.exists() and not O.exists()
proofpath=W/'pirate-palm-active-material-ir-v531/RESULT.v1.json';proof=read(proofpath)
assert proof['status']=='palm_active_material_topology_compiled' and proof['actual_exit_code']==0
assert sha(proof['ir_path'])==proof['ir_sha256']
assert proof['opcode_counts']['TimeNode']==proof['opcode_counts']['GradientNoiseNode']==1
pins=dict(proof['input_pins'])
for p,h in pins.items():assert sha(p)==h,p
generator=S/'generate_nested_material_wgsl.py'
pins[str(generator)]=sha(generator);pins[str(Path(__file__))]=sha(Path(__file__))
pins[str(proofpath)]=sha(proofpath);pins[proof['ir_path']]=proof['ir_sha256']
code=generator.read_text('utf-8')
for old,new in [
    ('ROOT = W / "nested-prefab-material-wgsl-v1"','ROOT = W / "pirate-palm-wgsl-v532"'),
    ('batch_path = W / "nested-prefab-material-active-ir-v1/RESULT.v1.json"','batch_path = W / "pirate-palm-wgsl-input-v532/BATCH.v1.json"'),
    ('batch["compiled_materials"] != 16','batch["compiled_materials"] != 1'),
    ('Closed sixteen-material active topology required','Exact single original palm material topology required'),
    ('"total": 16','"total": 1'),
    ('"requested_materials": 16','"requested_materials": 1'),
]:
    assert old in code,old;code=code.replace(old,new)
I.mkdir()
helper=I/'configured_existing_generator.py';helper.write_text(code,'utf-8')
pins[str(helper)]=sha(helper)
batch=dict(status='material_active_topology_compiled',compiled_materials=1,source_pins=pins,rows=[dict(pack_id='hivemind.pirate-island',material_guid=proof['material_guid'],output=proof['ir_path'],output_sha256=proof['ir_sha256'])])
(I/'BATCH.v1.json').write_text(json.dumps(batch,indent=2),'utf-8')
exec(compile(code,str(generator)+'#exact-palm-topology','exec'),globals())
