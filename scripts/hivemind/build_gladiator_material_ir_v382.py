"""Compile exact preserved Gladiator materials using the existing source IR compiler."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib,json,re,subprocess,sys
W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-active-material-ir-v382'
P=Path('F:/OpenRPG_AssetVault/originals/unity-purchased/hivemind/source-support-v1/modular-gladiator-arena-base170-v1')
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
auditpath=W/'gladiator-mirror-source-case-v379/RESULT.v1.json';audit=read(auditpath)
queuepath=W/'queue/modular-gladiator-arena/prefabs.json';queue=read(queuepath)
graph=Path(audit['shader_graphs'][0]['path']);assert sha(graph)==audit['input_pins'][str(graph)]
assert len(audit['shader_graphs'])==1
guid=re.search(r'^guid: ([a-f0-9]{32})$',Path(str(graph)+'.meta').read_text('utf-8-sig'),re.M)[1]
decoder=json.JSONDecoder();text=graph.read_text('utf-8-sig');objects=[];offset=0
while offset<len(text):
    while offset<len(text) and text[offset].isspace():offset+=1
    if offset==len(text):break
    obj,offset=decoder.raw_decode(text,offset);objects.append(obj)
ids=[o['m_ObjectId'] for o in objects if 'm_ObjectId' in o];assert len(ids)==len(set(ids))
assert not any(o.get('m_Type','').endswith('.SubGraphNode') for o in objects)
selected={};pins={str(p):sha(p) for p in [auditpath,queuepath,graph,Path(str(graph)+'.meta'),Path(__file__)]}
for row in audit['materials']:
    mat=Path(row['path']);mguid=re.search(r'^guid: ([a-f0-9]{32})$',Path(str(mat)+'.meta').read_text('utf-8-sig'),re.M)[1]
    entry=queue['materials'][mguid];assert entry['source']==mat.relative_to(P/'source').as_posix()
    assert sha(mat)==entry['sha256']==audit['input_pins'][str(mat)] and entry['shader_guid']==guid
    pins[str(mat)]=sha(mat);pins[str(Path(str(mat)+'.meta'))]=sha(Path(str(mat)+'.meta'));selected[mguid]=entry
assert len(selected)==2
compiler=W/'villa-original-basictextured-all-material-ir-v2/compile_villa_material_ir.py';pins[str(compiler)]=sha(compiler)
source=compiler.read_text('utf-8-sig');needle="Q=Path('F:\\\\OpenRPG_ImportWork\\\\queue\\\\villa-forge\\\\prefabs.json')"
assert source.count(needle)==1 and source.count("'e6f99812ca2d22a4c98d5e88e4d78f2a'")==1
source=source.replace(needle,"Q=Path(__file__).parent/'SOURCE-QUEUE.v1.json'")
source=source.replace("'e6f99812ca2d22a4c98d5e88e4d78f2a'",repr(guid))
O.mkdir();(O/'material-family-active-ir-v1').mkdir()
(O/'SOURCE-QUEUE.v1.json').write_text(json.dumps(dict(source_root=str(P/'source'),materials=selected),indent=2),'utf-8')
(O/'SHADER-SUBGRAPH-CLOSURE.v1.json').write_text(json.dumps(dict(graphs=[dict(guid=guid,path=str(graph),sha256=sha(graph),meta_sha256=sha(Path(str(graph)+'.meta')),objects=objects)]),indent=2),'utf-8')
helper=O/'compile_gladiator_material_ir.py';helper.write_text(source,'utf-8');rows=[]
for mguid in selected:
    with (O/(mguid+'.stdout.log')).open('xb') as out,(O/(mguid+'.stderr.log')).open('xb') as err:
        code=subprocess.run([sys.executable,'-B',str(helper),mguid],stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW).returncode
    row=dict(guid=mguid,name=selected[mguid]['name'],exit_code=code)
    if code==0:
        p=O/'material-family-active-ir-v1'/(mguid+'.json');ir=read(p)
        assert ir['source_material_sha256']==selected[mguid]['sha256']
        row.update(path=str(p),sha256=sha(p),nodes=len(ir['nodes']),selected_branches=ir['selected_branches'],opcode_counts=ir['opcode_counts'],terminals=ir['terminals'])
    rows.append(row)
for p,h in pins.items():assert sha(Path(p))==h,p
result=dict(status='two_gladiator_material_topologies_compiled' if all(r['exit_code']==0 for r in rows) else 'gladiator_material_ir_refusal_retained',asset_id=audit['asset_id'],rows=rows,input_pins=pins,compiler_logic_changes=['Exact Gladiator material subset source paths','Exact Gladiator shader GUID'],shader_execution_verified=False,material_conversion_qualified=False,converted=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps(dict(status=result['status'],materials=[dict(name=r['name'],exit_code=r['exit_code'],opcode_counts=r.get('opcode_counts'),selected_branches=r.get('selected_branches')) for r in rows])))
assert all(r['exit_code']==0 for r in rows)
