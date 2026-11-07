"""Compile all three torch materials from their own immutable ShaderGraph."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,subprocess,sys
W=Path('F:/OpenRPG_ImportWork');O=W/'medieval-torch-active-material-ir-v154'
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(p.read_text('utf-8-sig'))
def main():
 assert not O.exists()
 proofpath=W/'medieval-torch-shader-material-applicability-v133/RESULT.v1.json';proof=read(proofpath);queuepath=W/'queue/medieval-kingdom/prefabs.json';queue=read(queuepath)
 graph=Path(next(p for p in proof['input_pins'] if p.endswith('.shadergraph')));assert sha(graph)==proof['input_pins'][str(graph)]
 decoder=json.JSONDecoder();text=graph.read_text('utf-8-sig');objects=[];offset=0
 while offset<len(text):
  while offset<len(text) and text[offset].isspace():offset+=1
  if offset==len(text):break
  obj,offset=decoder.raw_decode(text,offset);objects.append(obj)
 ids=[obj['m_ObjectId'] for obj in objects if 'm_ObjectId' in obj];assert len(ids)==len(set(ids))
 assert not any(obj.get('m_Type','').endswith('.SubGraphNode') for obj in objects),'Explicit subgraph closure needed'
 graphguid='7db8146d89c83334f9b1a46e070dd7dc';materials=[r['guid'] for r in proof['materials']];assert len(materials)==len(set(materials))==3
 pins={str(p):sha(p) for p in [proofpath,queuepath,graph,graph.with_suffix('.shadergraph.meta'),Path(__file__)]}
 for guid in materials:
  m=queue['materials'][guid];assert m['shader_guid']==graphguid;p=Path(queue['source_root'])/m['source'];assert sha(p)==m['sha256'];pins[str(p)]=sha(p)
 compiler=W/'villa-original-basictextured-all-material-ir-v2/compile_villa_material_ir.py';pins[str(compiler)]=sha(compiler);source=compiler.read_text('utf-8-sig');needle="Q=Path('F:\\\\OpenRPG_ImportWork\\\\queue\\\\villa-forge\\\\prefabs.json')";assert source.count(needle)==1
 source=source.replace(needle,"Q=Path('F:/OpenRPG_ImportWork/queue/medieval-kingdom/prefabs.json')");assert source.count("'e6f99812ca2d22a4c98d5e88e4d78f2a'")==1;source=source.replace("'e6f99812ca2d22a4c98d5e88e4d78f2a'",repr(graphguid))
 O.mkdir();(O/'material-family-active-ir-v1').mkdir();helper=O/'compile_torch_material_ir.py';helper.write_text(source,'utf-8')
 closure=dict(graphs=[dict(guid=graphguid,path=str(graph),sha256=sha(graph),meta_sha256=sha(graph.with_suffix('.shadergraph.meta')),objects=objects)])
 (O/'SHADER-SUBGRAPH-CLOSURE.v1.json').write_text(json.dumps(closure,indent=2),'utf-8')
 rows=[]
 for guid in materials:
  with (O/(guid+'.stdout.log')).open('xb') as out,(O/(guid+'.stderr.log')).open('xb') as err:code=subprocess.run([sys.executable,'-B',str(helper),guid],stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW).returncode
  row=dict(guid=guid,exit_code=code)
  if code==0:
   p=O/'material-family-active-ir-v1'/(guid+'.json');ir=read(p);assert ir['source_material_sha256']==queue['materials'][guid]['sha256'];row.update(path=str(p),sha256=sha(p),nodes=len(ir['nodes']),branches=len(ir['selected_branches']),opcode_counts=ir['opcode_counts'])
  rows.append(row)
 for p,h in pins.items():assert sha(Path(p))==h,p
 result=dict(status='three_torch_material_graphs_and_selected_branches_compiled' if all(r['exit_code']==0 for r in rows) else 'torch_material_ir_refusal_retained',finished_utc=datetime.now(timezone.utc).isoformat(),rows=rows,input_pins=pins,other_pack_material_results_reused=False,compiler_logic_changes=['Exact Medieval queue path','Exact Medieval shader GUID'],shader_execution_verified=False,material_conversion_qualified=False,renderer_admitted=False,full_import=False)
 (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(result));assert all(r['exit_code']==0 for r in rows)
if __name__=='__main__':main()
