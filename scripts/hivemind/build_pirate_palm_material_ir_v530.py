"""Compile the preserved palm-frond graph with the existing topology compiler."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, importlib.util, json, subprocess, sys
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'pirate-palm-active-material-ir-v530'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
proofpath=W/'pirate-palm-render-contract-v529/RESULT.v1.json';proof=read(proofpath)
queuepath=W/'queue/pirate-island/prefabs.json';queue=read(queuepath)
guid='abc00000000004284671107665666980';entry=queue['materials'][guid]
material=Path(queue['source_root'])/entry['source'];assert sha(material)==entry['sha256']
assert entry['shader_guid']=='8a71d1ff6799ee7429ba5af1b299c193'
spec=importlib.util.spec_from_file_location('source_contract_reader',S/'read_delta_foliage_render_contracts_v1.py')
reader=importlib.util.module_from_spec(spec);spec.loader.exec_module(reader)
graph=next(Path(p) for p in proof['input_pins'] if p.endswith('S_Vegetation.shadergraph') and 'hivemind-run' in p)
assert sha(graph)==proof['input_pins'][str(graph)]
objects,byid=reader.objects(graph)
assert not any(o.get('m_Type','').endswith('.SubGraphNode') for o in objects),'Close referenced subgraphs before compilation'
compiler=W/'villa-original-basictextured-all-material-ir-v2/compile_villa_material_ir.py'
pins={str(p):sha(p) for p in [Path(__file__),proofpath,queuepath,material,Path(str(material)+'.meta'),graph,Path(str(graph)+'.meta'),compiler,S/'read_delta_foliage_render_contracts_v1.py']}
source=compiler.read_text('utf-8-sig')
needle="Q=Path('F:\\\\OpenRPG_ImportWork\\\\queue\\\\villa-forge\\\\prefabs.json')"
assert source.count(needle)==1 and source.count("'e6f99812ca2d22a4c98d5e88e4d78f2a'")==1
source=source.replace(needle,"Q=Path(__file__).parent/'SOURCE-QUEUE.v1.json'").replace("'e6f99812ca2d22a4c98d5e88e4d78f2a'",repr(entry['shader_guid']))
O.mkdir();(O/'material-family-active-ir-v1').mkdir()
(O/'SOURCE-QUEUE.v1.json').write_text(json.dumps(dict(source_root=queue['source_root'],materials={guid:entry}),indent=2),'utf-8')
(O/'SHADER-SUBGRAPH-CLOSURE.v1.json').write_text(json.dumps(dict(graphs=[dict(guid=entry['shader_guid'],path=str(graph),sha256=sha(graph),meta_sha256=sha(Path(str(graph)+'.meta')),objects=objects)]),indent=2),'utf-8')
helper=O/'compile_palm_material_ir.py';helper.write_text(source,'utf-8')
with (O/'stdout.log').open('xb') as out,(O/'stderr.log').open('xb') as err:
    exit_code=subprocess.run([sys.executable,'-B',str(helper),guid],stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW).returncode
result=dict(status='palm_active_material_ir_refusal_retained',actual_exit_code=exit_code,material_guid=guid,input_pins=pins,compiler_logic_changes=['Exact original palm material subset and source root','Exact original vegetation shader GUID'],shader_execution_verified=False,material_conversion_qualified=False,full_import=False)
if exit_code==0:
    p=O/'material-family-active-ir-v1'/(guid+'.json');ir=read(p)
    assert ir['source_material_sha256']==entry['sha256']
    result.update(status='palm_active_material_topology_compiled',ir_path=str(p),ir_sha256=sha(p),nodes=len(ir['nodes']),opcode_counts=ir['opcode_counts'],terminals=ir['terminals'],selected_branches=ir['selected_branches'])
for p,h in pins.items():assert sha(p)==h,p
for p in [helper,O/'SOURCE-QUEUE.v1.json',O/'SHADER-SUBGRAPH-CLOSURE.v1.json']:pins[str(p)]=sha(p)
result['finished_utc']=datetime.now(timezone.utc).isoformat()
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ['input_pins','terminals','selected_branches']}))
assert exit_code==0,'Preserve original compiler refusal'
