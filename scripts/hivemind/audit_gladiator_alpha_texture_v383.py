"""Trace the real flag's alpha terminal to its original image, without baking it."""
from pathlib import Path
from datetime import datetime,timezone
from collections import Counter
import hashlib,json,re
from PIL import Image
W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-alpha-texture-audit-v383'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists()
proofpath=W/'gladiator-active-material-ir-v382/RESULT.v1.json';proof=read(proofpath)
assert proof['status']=='two_gladiator_material_topologies_compiled'
auditpath=W/'gladiator-mirror-source-case-v379/RESULT.v1.json';audit=read(auditpath)
row=next(r for r in proof['rows'] if r['name']=='M_BigFlags_Red');irp=Path(row['path']);assert sha(irp)==row['sha256'];ir=read(irp)
nodes={n['id']:n for n in ir['nodes']};alpha=nodes[ir['terminals']['SurfaceDescription.Alpha']['ref']]
assert alpha['opcode']=='SampleTexture2DNode' and alpha['output_slot']['m_DisplayName']=='A'
texture=next(i['value'] for i in alpha['inputs'] if i['name']=='Texture')
assert texture['provenance']=='material:_OpacityMap'
uv=next(i['value'] for i in alpha['inputs'] if i['name']=='UV');assert uv['source_slot']['m_Channel']==0 and 'ref' not in uv
assert ir['terminals']['SurfaceDescription.AlphaClipThreshold']['literal']==0.5
assert any(b['predicate']['provenance']=='material:_UseOpacity' and b['predicate']['literal']==1.0 and b['chosen_input']=='True' for b in ir['selected_branches'])
files={}
for r in audit['resolved_files']:
    p=Path(r['path']);meta=Path(str(p)+'.meta')
    m=re.search(r'^guid: ([a-f0-9]{32})$',meta.read_text('utf-8-sig'),re.M)
    if m:files[m[1]]=p
base=next(i['value'] for n in ir['nodes'] if n['opcode']=='SampleTexture2DNode' for i in n['inputs'] if i['name']=='Texture' and i['value'].get('provenance')=='material:_BaseMap')
pins={str(p):sha(p) for p in [proofpath,auditpath,irp,Path(__file__)]};images=[]
for role,ref in [('alpha',texture),('base_color',base)]:
    path=files[ref['literal']['guid']];meta=Path(str(path)+'.meta')
    assert sha(path)==audit['input_pins'][str(path)] and sha(meta)==audit['input_pins'][str(meta)]
    pins[str(path)]=sha(path);pins[str(meta)]=sha(meta)
    with Image.open(path) as im:
        im.load();dimensions=list(im.size);mode=im.mode;counts=Counter(im.convert('RGBA').getchannel('A').tobytes())
    images.append(dict(role=role,path=str(path),sha256=sha(path),dimensions=dimensions,source_mode=mode,alpha_min=min(counts),alpha_max=max(counts),alpha_histogram=dict(sorted(counts.items())),pixels_below_half=sum(c for value,c in counts.items() if value<127.5),pixels_at_or_above_half=sum(c for value,c in counts.items() if value>=127.5),srgb_texture_meta=re.search(r'^\s*sRGBTexture: (\d+)',meta.read_text('utf-8-sig'),re.M)[1]))
for p,h in pins.items():assert sha(Path(p))==h,p
result=dict(status='source_alpha_channel_and_cutout_image_traced',asset_id=audit['asset_id'],alpha_property='_OpacityMap',alpha_channel='A',uv_channel=0,alpha_uv_is_unconnected_source_uv0=True,threshold=0.5,alpha_texture_identity=texture['literal'],images=images,base_color_alpha_substitution_qualified=False,input_pins=pins,imported_texture_sampling_qualified=False,source_gpu_execution_qualified=False,converted=False,full_import=False,finished_utc=datetime.now(timezone.utc).isoformat())
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps({k:result[k] for k in ['status','alpha_property','alpha_channel','threshold']}));print(json.dumps([{k:r[k] for k in ['role','dimensions','source_mode','alpha_min','alpha_max','pixels_below_half']} for r in images]))
