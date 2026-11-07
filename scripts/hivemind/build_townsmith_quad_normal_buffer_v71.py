"""Private source normal-buffer control from the measured quad-gradient material."""
from pathlib import Path
import hashlib,json,struct,subprocess,sys
S=Path(__file__).parent;W=Path('F:/OpenRPG_ImportWork');ROOT=W/'townsmith-quad-normal-buffer-carrier-v71'
assert not ROOT.exists()
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
parent=W/'townsmith-material-quad-difference-carrier-v68/SM_Door-Single-B.material-quad-difference.candidate.glb'
proof=json.loads((parent.parent/'RESULT.v1.json').read_text());assert sha(parent)==proof['input_pins'][str(parent)]
raw=parent.read_bytes();n=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+n]);prefix=raw[28+n:];binary=bytearray(prefix)
c=doc['materials'][0]['extensions']['OPENRPG_source_graph'];v=doc['bufferViews'][c['forward']['buffer_view']];stage=prefix[v.get('byteOffset',0):v.get('byteOffset',0)+v['byteLength']];assert hashlib.sha256(stage).hexdigest()==c['forward']['sha256']
t=stage.decode();anchor='out.color=source_graph_native_light_transport(p,surface);';assert t.count(anchor)==1
t=t.replace(anchor,'out.color=source_candidate_normal_roughness_buffer(surface);');stage=t.encode();i=len(doc['bufferViews']);doc['bufferViews'].append(dict(buffer=0,byteOffset=len(binary),byteLength=len(stage)));binary.extend(stage);c['forward']=dict(buffer_view=i,sha256=hashlib.sha256(stage).hexdigest());binary.extend(b'\0'*(-len(binary)%4));doc['buffers'][0]['byteLength']=len(binary)
doc.setdefault('extras',{})['private_normal_buffer_control']=dict(installed_in_product=False,full_import=False)
j=json.dumps(doc,separators=(',',':')).encode();j+=b' '*(-len(j)%4);out=struct.pack('<III',0x46546c67,2,28+len(j)+len(binary))+struct.pack('<II',len(j),0x4e4f534a)+j+struct.pack('<II',len(binary),0x004e4942)+binary;assert binary[:len(prefix)]==prefix
ROOT.mkdir();glb=ROOT/'SM_Door-Single-B.quad-normal-buffer.candidate.glb';glb.write_bytes(out);(ROOT/'quad-normal-buffer.forward.wgsl').write_bytes(stage)
q=S/'queue_source_glb_quad_normal_buffer_v71.py';assert not q.exists();qt=(S/'queue_source_glb_normal_roughness_draw_v4.py').read_text(encoding='utf-8')
qt=qt.replace('townsmith-paint-door-native-normal-roughness-draw-v4','townsmith-quad-normal-buffer-draw-v71').replace('townsmith-paint-door-normal-roughness-carrier-v4/SM_Door-Single-B.normal-roughness-control.candidate.glb','townsmith-quad-normal-buffer-carrier-v71/SM_Door-Single-B.quad-normal-buffer.candidate.glb').replace('5ca721b3e485f74114031452e3cbfa7ecb6b1c58047a2d7259c6f01bca800c26',sha(glb)).replace('townsmith-paint-door-normal-roughness-carrier-v4/RESULT.v1.json','townsmith-quad-normal-buffer-carrier-v71/RESULT.v1.json')
qt=qt.replace("*(R/'crates'/crate/'src').rglob('*.rs')","*(R/'crates'/crate/'src').rglob('*.rs'),*(R/'crates'/crate/'src').rglob('*.wgsl')")
epoch="""    epoch=ROOT/'source-input-epoch';epoch.mkdir();snapshots=[]
    for path in paths:
        if path.is_relative_to(R):
            target=epoch/path.relative_to(R);target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(path.read_bytes());assert sha(target)==pins[str(path)];snapshots.append(dict(live_path=str(path),snapshot=str(target),sha256=pins[str(path)]));pins[str(target)]=sha(target)
    save('SOURCE-EPOCH.v1.json',dict(snapshots=snapshots));pins[str(ROOT/'SOURCE-EPOCH.v1.json')]=sha(ROOT/'SOURCE-EPOCH.v1.json')
"""
qt=qt.replace('    ROOT.mkdir()\n','    ROOT.mkdir()\n'+epoch);compile(qt,str(q),'exec');q.write_text(qt,encoding='utf-8')
(ROOT/'RESULT.v1.json').write_text(json.dumps(dict(status='private_normal_buffer_control_prepared',input_pins={str(p):sha(p) for p in [Path(__file__),parent,glb,q,W/'townsmith-door-source-inpass-normal-reopen-v31/RESULT.v1.json']},original_binary_prefix_byte_equal=True,resources_unchanged=True,source_pixels_applied_to_native=False,installed_in_product=False,full_import=False),indent=2),encoding='utf-8')
subprocess.run([sys.executable,'-B',str(q)],check=True)
