"""Derive WebGPU-compatible normal guards while preserving all accepted arithmetic.

This is a packaging correction, not a source-render or full-import qualification.
Only the two explicit normal decoder ABI functions are supported. Unknown guards
refuse instead of being silently replaced. Original GLBs are never overwritten.
"""
from pathlib import Path
import argparse,copy,hashlib,json,re,struct

OLD='bitcast<f32>(0x7fc00000u)'
DECODERS={'source_unpack_normal_sample_native':'mode','source_unpack_normal_sample':'common_mode'}
def digest(data):return hashlib.sha256(data).hexdigest()
def lower_normal_guards(source):
    result=source;edits=[]
    for name,mode in DECODERS.items():
        matches=list(re.finditer(r'\bfn\s+'+name+r'\s*\(',result))
        if not matches:continue
        if len(matches)!=1:raise ValueError('Duplicate normal ABI function: '+name)
        start=matches[0].start();opening=result.find('{',start)
        if opening<0:raise ValueError('Missing function body: '+name)
        signature=result[start:opening]
        if not re.search(r'\b'+mode+r'\s*:\s*u32\b',signature):raise ValueError('Unknown selector ABI: '+name)
        depth=1;end=opening+1
        while depth and end<len(result):
            depth+=(result[end]=='{')-(result[end]=='}');end+=1
        if depth:raise ValueError('Unclosed normal decoder: '+name)
        original=result[start:end]
        new=f'bitcast<f32>(0x7fc00000u | ({mode} & 0x003fffffu))'
        if original.count(new)==1 and OLD not in original:continue
        if original.count(OLD)!=1 or new in original:raise ValueError('Unknown normal refusal guard: '+name)
        changed=original.replace(OLD,new)
        if changed.replace(new,OLD)!=original:raise ValueError('Accepted arithmetic changed')
        result=result[:start]+changed+result[end:]
        edits.append(dict(function=name,selector=mode,accepted_arithmetic_byte_unchanged=True))
    if OLD in result:raise ValueError('Constant NaN guard outside the supported normal ABI')
    reverse=result
    for edit in edits:
        mode=edit['selector'];new=f'bitcast<f32>(0x7fc00000u | ({mode} & 0x003fffffu))'
        if reverse.count(new)!=1:raise ValueError('Ambiguous reverse edit')
        reverse=reverse.replace(new,OLD)
    if reverse!=source:raise ValueError('Unrelated shader code changed')
    return result,edits

def unpack_glb(raw):
    if len(raw)<28 or struct.unpack_from('<III',raw,0)!=(0x46546c67,2,len(raw)):raise ValueError('Invalid GLB header')
    size,kind=struct.unpack_from('<II',raw,12)
    if kind!=0x4e4f534a or 20+size+8>len(raw):raise ValueError('Invalid JSON chunk')
    document=json.loads(raw[20:20+size]);offset=20+size
    length,kind=struct.unpack_from('<II',raw,offset)
    if kind!=0x004e4942 or offset+8+length!=len(raw):raise ValueError('One complete BIN chunk required')
    binary=raw[offset+8:]
    if len(document.get('buffers',[]))!=1 or document['buffers'][0]['byteLength']>len(binary):raise ValueError('One embedded buffer required')
    return document,binary

def normalize_glb(raw):
    before,binary=unpack_glb(raw);document=copy.deepcopy(before);data=bytearray(binary);changes=[]
    for index,material in enumerate(document.get('materials',[])):
        contract=material.get('extensions',{}).get('OPENRPG_source_graph')
        if contract is None:continue
        for stage in ['forward','prepass']:
            if stage not in contract:continue
            blob=contract[stage];view=document['bufferViews'][blob['buffer_view']]
            if view.get('buffer')!=0:raise ValueError('Shader must use embedded buffer')
            start=view.get('byteOffset',0);size=view['byteLength']
            if start<0 or size<0 or start+size>len(binary):raise ValueError('Shader outside buffer')
            original=binary[start:start+size]
            if digest(original)!=blob['sha256']:raise ValueError('Source shader SHA mismatch')
            lowered,edits=lower_normal_guards(original.decode('utf-8'))
            if not edits:continue
            payload=lowered.encode('utf-8');data.extend(b'\0'*(-len(data)%4));offset=len(data);data.extend(payload)
            new_index=len(document['bufferViews']);document['bufferViews'].append(dict(buffer=0,byteOffset=offset,byteLength=len(payload)))
            contract[stage]=dict(blob,buffer_view=new_index,sha256=digest(payload))
            changes.append(dict(material_index=index,stage=stage,prior_blob=copy.deepcopy(blob),edits=edits,shader_sha256=digest(payload)))
    if not changes:return raw,dict(changes=[],unchanged=True,original_binary_prefix_byte_equal=True,all_other_document_fields_unchanged=True)
    document['buffers'][0]['byteLength']=len(data)
    encoded=json.dumps(document,separators=(',',':'),ensure_ascii=False).encode('utf-8');encoded+=b' '*(-len(encoded)%4);data.extend(b'\0'*(-len(data)%4))
    output=struct.pack('<III',0x46546c67,2,28+len(encoded)+len(data))+struct.pack('<II',len(encoded),0x4e4f534a)+encoded+struct.pack('<II',len(data),0x004e4942)+data
    reopened,actual=unpack_glb(output)
    if actual[:len(binary)]!=binary:raise ValueError('Original geometry or texture bytes changed')
    restored=copy.deepcopy(reopened);restored['buffers']=before['buffers'];restored['bufferViews']=before['bufferViews']
    for change in changes:
        restored['materials'][change['material_index']]['extensions']['OPENRPG_source_graph'][change['stage']]=change['prior_blob']
        blob=reopened['materials'][change['material_index']]['extensions']['OPENRPG_source_graph'][change['stage']]
        view=reopened['bufferViews'][blob['buffer_view']];payload=actual[view['byteOffset']:view['byteOffset']+view['byteLength']]
        if digest(payload)!=blob['sha256']:raise ValueError('Derived shader SHA mismatch')
    if restored!=before:raise ValueError('Unrelated GLB metadata changed')
    return output,dict(changes=changes,unchanged=False,original_binary_prefix_byte_equal=True,all_other_document_fields_unchanged=True)

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--input',type=Path,required=True);parser.add_argument('--output',type=Path,required=True);parser.add_argument('--receipt',type=Path,required=True);args=parser.parse_args()
    if args.output.resolve()==args.input.resolve() or args.output.exists() or args.receipt.exists():raise ValueError('Preserve original and previous outputs')
    raw=args.input.read_bytes();converted,proof=normalize_glb(raw)
    proof.update(status='source_normal_guard_packaging_verified_gpu_qualification_separate',input=str(args.input),input_sha256=digest(raw),output=str(args.output),output_sha256=digest(converted),converter_sha256=digest(Path(__file__).read_bytes()),full_import=False,gpu_executed=False)
    args.output.parent.mkdir(parents=True,exist_ok=True);args.receipt.parent.mkdir(parents=True,exist_ok=True)
    with args.output.open('xb') as f:f.write(converted)
    if args.output.read_bytes()!=converted or digest(args.input.read_bytes())!=digest(raw):raise ValueError('Disk reopen mismatch')
    with args.receipt.open('x',encoding='utf-8') as f:json.dump(proof,f,indent=2)
    print(json.dumps(dict(output=str(args.output),changed_stages=len(proof['changes']),unchanged=proof['unchanged'])))
if __name__=='__main__':main()
