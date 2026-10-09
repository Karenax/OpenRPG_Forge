"""Verify the private witness changes only its forward-stage diagnostic."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,struct

W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-sky-cube-gpu-case-v1'
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def glb(p):
    raw=p.read_bytes();assert struct.unpack_from('<III',raw)==(0x46546c67,2,len(raw))
    n,kind=struct.unpack_from('<II',raw,12);assert kind==0x4e4f534a
    binary_size,binary_type=struct.unpack_from('<II',raw,20+n);assert binary_type==0x004e4942
    binary=raw[28+n:];assert len(binary)==binary_size
    return json.loads(raw[20:20+n]),binary

def main():
    proof=json.loads((ROOT/'RESULT.v1.json').read_text('utf-8'))
    for p,h in proof['input_pins'].items():assert sha(p)==h,p
    original_case=W/'firepit-shadow-comparison-case-v1/CASE.v1.json'
    old=json.loads(original_case.read_text('utf-8'));a=Path(old['fixture'])
    assert sha(a)==old['fixture_sha256']
    b=Path(proof['private_fixture']);assert sha(b)==proof['private_fixture_sha256']
    source,original_bytes=glb(a);target,derived_bytes=glb(b)
    assert derived_bytes[:len(original_bytes)]==original_bytes
    keys=['meshes','nodes','accessors','scenes','scene','images','textures','samplers']
    for key in keys:assert source.get(key)==target.get(key),key
    assert target['bufferViews'][:len(source['bufferViews'])]==source['bufferViews']
    left=json.loads(json.dumps(source['materials']));right=json.loads(json.dumps(target['materials']))
    for i,(old_material,new_material) in enumerate(zip(left,right,strict=True)):
        old_contract=old_material['extensions']['OPENRPG_source_graph']
        new_contract=new_material['extensions']['OPENRPG_source_graph']
        delta=next(d for d in proof['forward_stage_deltas'] if d['original']==old_contract['forward'])
        assert new_contract['forward']==delta['diagnostic']
        new_contract['forward']=old_contract['forward']
        assert new_material==old_material,i
    receipt=dict(status='private_sky_witness_original_geometry_and_authored_bindings_preserved',
        verified_utc=datetime.now(timezone.utc).isoformat(),original_binary_bytes_preserved=len(original_bytes),
        original_buffer_views_preserved=len(source['bufferViews']),unchanged_sections=keys,
        expected_gpu_cases=182,target_gpu_sampling_verified=False,full_import=False,editor_visual_verified=False,
        input_pins={str(p):sha(p) for p in [Path(__file__).resolve(),ROOT/'RESULT.v1.json',a,b]})
    path=ROOT/'STRUCTURE.v1.json';assert not path.exists();path.write_text(json.dumps(receipt,indent=2),'utf-8')
    print(json.dumps({k:v for k,v in receipt.items() if k!='input_pins'}))

if __name__=='__main__':main()
