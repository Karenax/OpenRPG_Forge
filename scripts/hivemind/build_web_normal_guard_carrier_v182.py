"""Derive valid WebGPU WGSL without changing accepted normal decoder branches."""
from pathlib import Path
from datetime import datetime, timezone
import copy
import hashlib
import json
import struct

W = Path('F:/OpenRPG_ImportWork')
O = W / 'townsmith-web-normal-guard-carrier-v182'

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()

def chunks(raw):
    assert struct.unpack_from('<III',raw,0) == (0x46546c67,2,len(raw))
    size,kind = struct.unpack_from('<II',raw,12); assert kind == 0x4e4f534a
    doc = json.loads(raw[20:20+size]); offset = 20+size
    length,kind = struct.unpack_from('<II',raw,offset); assert kind == 0x004e4942
    assert offset+8+length == len(raw)
    return doc, raw[offset+8:]

def main():
    assert not O.exists()
    parent = W / 'townsmith-deferred-indirect-carrier-v94/SM_Door-Single-B.deferred-indirect.candidate.glb'
    assert sha(parent) == '6e4d6ec5e8a796e1d94e16b72466978f3d59081fcd19299704a7d84f8dbe5ddc'
    before,binary = chunks(parent.read_bytes()); doc = copy.deepcopy(before); data = bytearray(binary)
    contract = doc['materials'][0]['extensions']['OPENRPG_source_graph']; stages = []
    O.mkdir()
    for stage in ['forward','prepass']:
        blob = contract[stage]; view = doc['bufferViews'][blob['buffer_view']]
        original = binary[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']]
        assert hashlib.sha256(original).hexdigest() == blob['sha256']
        text = original.decode('utf-8'); reverse = []
        for name,mode in [('source_unpack_normal_sample_native','mode'),('source_unpack_normal_sample','common_mode')]:
            start = text.index('fn '+name+'('); opening = text.index('{',start); depth=1; end=opening+1
            while depth:
                depth += (text[end]=='{') - (text[end]=='}'); end += 1
            part = text[start:end]
            old = 'bitcast<f32>(0x7fc00000u)'
            new = f'bitcast<f32>(0x7fc00000u | ({mode} & 0x003fffffu))'
            assert part.count(old) == 1 and f'{mode}:u32' in part.replace(' ','')
            text = text[:start]+part.replace(old,new)+text[end:]
            reverse.append((new,old))
        restored = text
        for new,old in reverse:
            assert restored.count(new) == 1
            restored = restored.replace(new,old)
        assert restored.encode('utf-8') == original, 'Accepted branch or source arithmetic changed'
        payload = text.encode('utf-8')
        while len(data)%4: data.append(0)
        offset = len(data); data.extend(payload)
        index = len(doc['bufferViews']); doc['bufferViews'].append(dict(buffer=0,byteOffset=offset,byteLength=len(payload)))
        contract[stage] = dict(buffer_view=index,sha256=hashlib.sha256(payload).hexdigest())
        (O/(stage+'.wgsl')).write_bytes(payload)
        stages.append(dict(stage=stage,prior_buffer_view=blob['buffer_view'],new_buffer_view=index,original_sha256=blob['sha256'],new_sha256=contract[stage]['sha256'],accepted_decode_branches_byte_unchanged=True,unsupported_mode_guard_dynamic_nan=True))
    doc['buffers'][0]['byteLength'] = len(data)
    encoded = json.dumps(doc,separators=(',',':'),ensure_ascii=False).encode('utf-8')
    while len(encoded)%4: encoded += b' '
    while len(data)%4: data.append(0)
    raw = struct.pack('<III',0x46546c67,2,28+len(encoded)+len(data))+struct.pack('<II',len(encoded),0x4e4f534a)+encoded+struct.pack('<II',len(data),0x004e4942)+data
    target = O / 'SM_Door-Single-B.web-normal-guard.candidate.glb'; target.write_bytes(raw)
    reopened,rebinary = chunks(target.read_bytes()); assert rebinary[:len(binary)] == binary
    check = copy.deepcopy(reopened);check['buffers']=before['buffers'];check['bufferViews']=before['bufferViews']
    for stage in ['forward','prepass']:check['materials'][0]['extensions']['OPENRPG_source_graph'][stage]=before['materials'][0]['extensions']['OPENRPG_source_graph'][stage]
    assert check == before, 'Unrelated GLB metadata changed'
    for stage in ['forward','prepass']:
        blob = reopened['materials'][0]['extensions']['OPENRPG_source_graph'][stage];view=reopened['bufferViews'][blob['buffer_view']]
        payload=rebinary[view['byteOffset']:view['byteOffset']+view['byteLength']]
        assert hashlib.sha256(payload).hexdigest()==blob['sha256']
    proof = dict(status='dynamic_unsupported_normal_guard_carrier_reopened_gpu_pending',finished_utc=datetime.now(timezone.utc).isoformat(),candidate=str(target),candidate_sha256=sha(target),parent=str(parent),parent_sha256=sha(parent),stages=stages,original_binary_prefix_byte_equal=True,all_other_document_fields_unchanged=True,unknown_normal_modes_still_refused_by_common_contract=True,gpu_shader_compilation_verified=False,source_pixel_equivalence_verified=False,full_import=False,input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),parent,W/'browser-source-glb-v180/shader-diagnostics/shader-error-00.json',W/'browser-source-glb-v180/shader-diagnostics/shader-error-01.json']})
    (O/'RESULT.v1.json').write_text(json.dumps(proof,indent=2),'utf-8');print(json.dumps(dict(candidate=str(target),original_binary_prefix_byte_equal=True,accepted_normal_branches_unchanged=True)))

if __name__ == '__main__':main()
