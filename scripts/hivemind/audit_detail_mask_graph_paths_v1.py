"""Trace the original mask property to source graph outputs; no GPU claim."""
from pathlib import Path
import hashlib
import json

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'detail-mask-original-graph-paths-v1'
SOURCE=W/'hivemind-run/native-american-village/source'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    assert not ROOT.exists()
    inventory_path=SOURCE.parent/'source.v1.json'
    inventory=json.loads(inventory_path.read_text('utf-8-sig'))
    graph=SOURCE/inventory['guid_paths']['53284c37c978c3442b89935f545c724f']
    material=SOURCE/inventory['guid_paths']['abc00000000012210656512993269337']
    records={str((SOURCE.parent/r['stored_path']).resolve()):r for r in inventory['files']}
    pins={str(p):sha(p) for p in [graph,material,inventory_path,Path(__file__).resolve()]}
    for path in [graph,material]:assert sha(path)==records[str(path.resolve())]['sha256']
    text=graph.read_text('utf-8-sig');decoder=json.JSONDecoder();position=0;objects=[]
    while position<len(text):
        while position<len(text) and text[position].isspace():position+=1
        if position==len(text):break
        value,position=decoder.raw_decode(text,position);objects.append(value)
    by_id={obj['m_ObjectId']:obj for obj in objects if 'm_ObjectId' in obj}
    assert len(by_id)==len(objects)
    data=next(obj for obj in objects if obj['m_Type'].endswith('.GraphData'))
    property_=next(obj for obj in objects if obj.get('m_DefaultReferenceName')=='_Mask')
    starts=[obj['m_ObjectId'] for obj in objects if obj.get('m_Property',{}).get('m_Id')==property_['m_ObjectId']]
    assert starts
    adjacency={}
    for edge in data['m_Edges']:
        output=edge['m_OutputSlot'];input_=edge['m_InputSlot']
        a=output['m_Node']['m_Id'];b=input_['m_Node']['m_Id'];assert a in by_id and b in by_id
        adjacency.setdefault(a,[]).append(dict(node=b,output_slot=output['m_SlotId'],input_slot=input_['m_SlotId']))
    todo=[(node,[node]) for node in starts];visited=set();sinks=[];traversed=[]
    while todo:
        node,path=todo.pop(0)
        if node in visited:continue
        visited.add(node);obj=by_id[node]
        if obj['m_Type'].endswith('.BlockNode'):
            sinks.append(dict(node=node,descriptor=obj.get('m_Descriptor'),name=obj.get('m_Name'),
                path=[dict(id=id_,type=by_id[id_]['m_Type'],name=by_id[id_].get('m_Name')) for id_ in path]))
        for edge in adjacency.get(node,[]):
            traversed.append(dict(from_node=node,**edge));todo.append((edge['node'],path+[edge['node']]))
    result=dict(input_pins=pins,graph_objects=len(objects),property=property_,property_nodes=starts,
        downstream_node_count=len(visited),source_graph_edges_traced=traversed,output_blocks_reachable=sinks,
        static_reachability_only=True,compiled_branch_activity_qualified=False,
        shader_default_pixels_verified=False,native_refused_captures_preserved=True,full_import=False)
    for path,digest in pins.items():assert sha(Path(path))==digest
    ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps(dict(receipt=str(ROOT/'RESULT.v1.json'),downstream_nodes=len(visited),
        output_blocks=[row['descriptor'] for row in sinks],full_import=False)))


if __name__=='__main__':main()
