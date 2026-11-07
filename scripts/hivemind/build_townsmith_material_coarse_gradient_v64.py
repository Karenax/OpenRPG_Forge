"""Private full-material fine-derivative candidate, preserving all original resources."""
from pathlib import Path
import hashlib,json,struct,subprocess,sys,os,re
s=Path(__file__).parent;w=Path('F:/OpenRPG_ImportWork');root=w/'townsmith-material-coarse-grad-carrier-v64';assert not root.exists()
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
parent=w/'townsmith-lighting-components-carrier-v52/SM_Door-Single-B.lighting-components.candidate.glb';assert sha(parent)=='801f9af042aecdeba0a8774f67d85bd8acdd20b6af3954f0877630fb8a4d8e19'
raw=parent.read_bytes();n=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+n]);prefix=raw[28+n:];binary=bytearray(prefix);c=doc['materials'][0]['extensions']['OPENRPG_source_graph'];v=doc['bufferViews'][c['forward']['buffer_view']];stage=prefix[v.get('byteOffset',0):v.get('byteOffset',0)+v['byteLength']];assert hashlib.sha256(stage).hexdigest()==c['forward']['sha256'];t=stage.decode();offset=0;changed=[]
while True:
 m=re.search(r'textureSample\(t[0-9]+,s[0-9]+,',t[offset:])
 if m is None:break
 start=offset+m.start();openpos=t.index('(',start);depth=1;end=openpos+1
 while depth:
  if t[end]=='(':depth+=1
  elif t[end]==')':depth-=1
  end+=1
 call=t[openpos+1:end-1];parts=[];level=0;last=0
 for i,ch in enumerate(call):
  if ch=='(':level+=1
  elif ch==')':level-=1
  elif ch==',' and level==0:parts.append(call[last:i]);last=i+1
 parts.append(call[last:]);assert len(parts)==3,(call,parts)
 uv=parts[2];replacement='textureSampleGrad('+','.join(parts)+',dpdxCoarse('+uv+'),dpdyCoarse('+uv+'))';changed.append(dict(before=t[start:end],after=replacement));t=t[:start]+replacement+t[end:];offset=start+len(replacement)
assert len(changed)>=19,len(changed)
stage=t.encode();i=len(doc['bufferViews']);doc['bufferViews'].append(dict(buffer=0,byteOffset=len(binary),byteLength=len(stage)));binary.extend(stage);c['forward']=dict(buffer_view=i,sha256=hashlib.sha256(stage).hexdigest());binary.extend(b'\0'*(-len(binary)%4));doc['buffers'][0]['byteLength']=len(binary);doc.setdefault('extras',{})['private_material_coarse_gradient_control']=dict(mode='explicit_coarse_gradients',installed_in_product=False,full_import=False)
j=json.dumps(doc,separators=(',',':')).encode();j+=b' '*(-len(j)%4);out=struct.pack('<III',0x46546c67,2,28+len(j)+len(binary))+struct.pack('<II',len(j),0x4e4f534a)+j+struct.pack('<II',len(binary),0x004e4942)+binary;assert binary[:len(prefix)]==prefix
root.mkdir();glb=root/'SM_Door-Single-B.material-coarse-grad.candidate.glb';glb.write_bytes(out);(root/'material-coarse-grad.forward.wgsl').write_bytes(stage)
q=s/'queue_source_glb_material_coarse_grad_v64.py';assert not q.exists();qt=(s/'queue_source_glb_hdr_buffer_v56.py').read_text(encoding='utf-8');qt=qt.replace('townsmith-native-hdr-buffer-layer-{LAYER}-v56','townsmith-native-material-coarse-grad-layer-{LAYER}-v64').replace('townsmith-lighting-components-carrier-v52/SM_Door-Single-B.lighting-components.candidate.glb','townsmith-material-coarse-grad-carrier-v64/SM_Door-Single-B.material-coarse-grad.candidate.glb').replace(sha(parent),sha(glb)).replace('townsmith-lighting-components-carrier-v52/RESULT.v1.json','townsmith-material-coarse-grad-carrier-v64/RESULT.v1.json');compile(qt,str(q),'exec');q.write_text(qt,encoding='utf-8')
(root/'RESULT.v1.json').write_text(json.dumps(dict(status='private_full_material_coarse_gradient_control_prepared',input_pins={str(p):sha(p) for p in [Path(__file__),parent,glb,q,w/'townsmith-normal-coarse-grad-comparison-v63/RESULT.v1.json']},sample_call_changes=changed,original_binary_prefix_byte_equal=True,resources_unchanged=True,source_pixels_applied_to_native=False,installed_in_product=False,full_import=False),indent=2),encoding='utf-8')
env=os.environ.copy();env['OPENRPG_PRIVATE_LIGHT_LAYER']='0';subprocess.run([sys.executable,'-B',str(q)],env=env,check=True);print(json.dumps(dict(sample_calls=len(changed),root=str(root))))
