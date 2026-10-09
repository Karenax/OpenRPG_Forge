"""Private visibility control only: disable culling, preserve all binary data."""
from pathlib import Path
from datetime import datetime,timezone
import json,struct,hashlib
W=Path('F:/OpenRPG_ImportWork');OLD=W/'firepit-surface-native-case-v1';ROOT=W/'firepit-surface-cull-control-case-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not ROOT.exists()
    prior=read(OLD/'RESULT.v1.json');case=read(OLD/'CASE.v1.json');fixture=Path(case['fixture']);assert sha(fixture)==case['fixture_sha256']
    for p,h in prior['input_pins'].items():assert sha(p)==h,p
    b=fixture.read_bytes();n=struct.unpack_from('<I',b,12)[0];d=json.loads(b[20:20+n]);binary=b[28+n:]
    states=[]
    for m in d['materials']:
        state=m['extensions']['OPENRPG_source_graph']['state'];assert state['forward_cull']==2 and state['prepass_cull']==2
        states.append(state.copy());state['forward_cull']=0;state['prepass_cull']=0
    d['extras']['private_cull_control']={'authored_cull_preserved_in_source':True,'derived_cull':0,'not_delivered':True}
    j=json.dumps(d,separators=(',',':')).encode();j+=b' '*((-len(j))%4)
    out=struct.pack('<III',0x46546c67,2,28+len(j)+len(binary))+struct.pack('<II',len(j),0x4e4f534a)+j+struct.pack('<II',len(binary),0x004e4942)+binary
    ROOT.mkdir();p=ROOT/'private-cull-control.glb';p.write_bytes(out)
    verify=p.read_bytes();size=struct.unpack_from('<I',verify,12)[0];assert verify[28+size:]==binary
    case.update(fixture=str(p),fixture_sha256=sha(p),private_cull_control=True,authored_cull_state_qualified=False)
    (ROOT/'CASE.v1.json').write_text(json.dumps(case,indent=2),'utf-8')
    result={**prior,'status':'private_no_cull_visibility_control_prepared','created_utc':datetime.now(timezone.utc).isoformat(),'private_fixture':str(p),'private_fixture_sha256':sha(p),'original_material_states':states,'binary_entirely_unchanged':True,'binary_bytes':len(binary),'binary_sha256':hashlib.sha256(binary).hexdigest(),'source_programs_and_geometry_unchanged':True,'authored_cull_state_qualified':False,'private_control_not_delivered':True,'input_pins':{str(p):sha(p) for p in [Path(__file__).resolve(),OLD/'RESULT.v1.json',OLD/'CASE.v1.json',fixture,ROOT/'CASE.v1.json',*[Path(p) for p in prior['input_pins']]]}}
    (ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:result[k] for k in ['status','binary_bytes','binary_entirely_unchanged','private_fixture']}))
if __name__=='__main__':main()
