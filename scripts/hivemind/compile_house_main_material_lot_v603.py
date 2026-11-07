"""Compile the exact six source materials used by 24 House static prefabs."""
from pathlib import Path
from datetime import datetime,timezone
from collections import Counter
import hashlib,json,importlib.util,subprocess,sys,re
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'house-main-material-lot-v603'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
routepath=W/'pending-prefab-conversion-routing-v602/ROUTED-CARDS.v1.json'
routeproof=routepath.with_name('RESULT.v1.json');routing=read(routeproof)
for p,h in routing['input_pins'].items():assert sha(p)==h,p
graphguid='7dcd0e3bb8cbf2345a1e124f5a095198'
cards=[r for r in read(routepath) if r['pack_id']=='hivemind.house-on-a-hill' and r['material_bindings'] and all(m['shader_guid']==graphguid for m in r['material_bindings'])]
assert len(cards)==24 and all(r['route']=='static_prefab_shader_frame_and_collider_qualification' for r in cards)
queuepath=W/'queue/house-on-a-hill/prefabs.json';queue=read(queuepath)
materialguids=sorted({m['guid'] for r in cards for m in r['material_bindings']});assert len(materialguids)==6
readerpath=S/'read_delta_foliage_render_contracts_v1.py';spec=importlib.util.spec_from_file_location('source_graph_reader',readerpath);reader=importlib.util.module_from_spec(spec);spec.loader.exec_module(reader)
compiler=W/'villa-original-basictextured-all-material-ir-v2/compile_villa_material_ir.py'
pins={str(p):sha(p) for p in [Path(__file__),routepath,routeproof,queuepath,readerpath,compiler]}
graphs=[];todo=[graphguid];seen=set()
while todo:
    guid=todo.pop(0)
    if guid in seen:continue
    seen.add(guid);assert guid in queue['guid_paths'],guid
    path=Path(queue['source_root'])/queue['guid_paths'][guid];meta=Path(str(path)+'.meta')
    assert re.search(r'^guid: ([a-f0-9]{32})$',meta.read_text('utf-8-sig'),re.M)[1]==guid
    objects,_=reader.objects(path);pins[str(path)]=sha(path);pins[str(meta)]=sha(meta)
    graphs.append(dict(guid=guid,path=str(path),sha256=sha(path),meta_sha256=sha(meta),objects=objects))
    todo.extend(json.loads(o['m_SerializedSubGraph'])['subGraph']['guid'] for o in objects if o.get('m_Type','').endswith('.SubGraphNode'))
assert len(graphs)==2
terminals={o['m_Name'] for o in graphs[0]['objects'] if o.get('m_Name','').startswith(('SurfaceDescription.','VertexDescription.'))}
assert len(terminals)==12 and {'VertexDescription.Position','VertexDescription.Normal','VertexDescription.Tangent'}<=terminals
for guid in materialguids:
    material=queue['materials'][guid];assert material['shader_guid']==graphguid
    path=Path(queue['source_root'])/material['source'];assert sha(path)==material['sha256'];pins[str(path)]=sha(path);pins[str(Path(str(path)+'.meta'))]=sha(Path(str(path)+'.meta'))
for card in cards:
    path=Path(queue['source_root'])/card['source_path'];assert sha(path)==card['source_sha256'];pins[str(path)]=sha(path)
O.mkdir();(O/'material-family-active-ir-v1').mkdir()
sourcequeue=O/'SOURCE-QUEUE.v1.json';sourcequeue.write_text(json.dumps(dict(source_root=queue['source_root'],materials={g:queue['materials'][g] for g in materialguids}),indent=2),'utf-8')
closure=O/'SHADER-SUBGRAPH-CLOSURE.v1.json';closure.write_text(json.dumps(dict(graphs=graphs),indent=2),'utf-8')
source=compiler.read_text('utf-8-sig')
needle="Q=Path('F:\\\\OpenRPG_ImportWork\\\\queue\\\\villa-forge\\\\prefabs.json')"
assert source.count(needle)==1
source=source.replace(needle,"Q=Path(__file__).parent/'SOURCE-QUEUE.v1.json'")
assert source.count("'e6f99812ca2d22a4c98d5e88e4d78f2a'")==1
source=source.replace("'e6f99812ca2d22a4c98d5e88e4d78f2a'",repr(graphguid))
assert source.count('assert len(terminals)==12')==1
source=source.replace('assert len(terminals)==12','assert set(terminals)=='+repr(terminals)+' and len(terminals)==12')
helper=O/'compile_house_main_material_ir.py';helper.write_text(source,'utf-8')
for p in [helper,sourcequeue,closure]:pins[str(p)]=sha(p)
rows=[];errors=[];outputs={}
for guid in materialguids:
    with (O/(guid+'.stdout.log')).open('xb') as out,(O/(guid+'.stderr.log')).open('xb') as err:
        code=subprocess.run([sys.executable,'-B',str(helper),guid],stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW).returncode
    if code:
        errors.append(dict(material_guid=guid,actual_exit_code=code,stderr=str(O/(guid+'.stderr.log'))));continue
    p=O/'material-family-active-ir-v1'/(guid+'.json');ir=read(p)
    assert ir['source_material_sha256']==queue['materials'][guid]['sha256'] and set(ir['terminals'])==terminals
    assert ir['source_program_semantics_gpu_qualified'] is False and ir['material_conversion_qualified'] is False
    outputs[str(p)]=sha(p);rows.append(dict(material_guid=guid,name=queue['materials'][guid]['name'],ir_path=str(p),ir_sha256=sha(p),nodes=len(ir['nodes']),opcode_counts=ir['opcode_counts'],selected_branch_count=len(ir['selected_branches']),subgraph_bindings=len(ir['subgraph_bindings'])))
for p,h in pins.items():assert sha(p)==h,p
result=dict(status='house_main_material_topology_compiled' if not errors else 'house_main_material_compiler_refusals_retained',prefab_cards_in_lot=len(cards),materials_expected=6,materials_compiled=len(rows),graphs_closed=2,rows=rows,errors=errors,card_ids=[r['id'] for r in cards],input_pins=pins,output_pins=outputs,compiler_changes=['Exact House source queue and graph GUID','Exact original twelve-terminal interface'],source_inputs_unchanged=True,gpu_execution_verified=False,materials_converted=False,forge_render_verified=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps({k:result[k] for k in ['status','prefab_cards_in_lot','materials_expected','materials_compiled','graphs_closed','errors','rows']}))
