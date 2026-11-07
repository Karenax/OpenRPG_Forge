"""Reopen all actual GPU images; preserve raw HDR pixels and exact repeat evidence."""
from pathlib import Path
import json,hashlib,datetime
import numpy as np
from PIL import Image,ImageDraw
W=Path('F:/OpenRPG_ImportWork');I=W/'firepit-particle-mesh-gpu-v244';O=W/'firepit-particle-mesh-gpu-reopen-v250'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not O.exists();run=read(I/'RUN-RESULT.v1.json');assert run['build_exit_code']==0 and run['gpu_exit_code']==0
 inp=read(I/'INPUT.v1.json');compact=read(W/'firepit-particle-mesh-gpu-receipt-v249/RESULT.v1.json');assert sha(Path(compact['metadata']))==compact['metadata_sha256'];gpu=read(Path(compact['metadata']));assert len(gpu['rows'])==len(inp['fixtures'])==336
 assert gpu['backend']=='Dx12';rows=[];pairs={};repeats=0;previews=[];pins={str(p):sha(p) for p in [Path(__file__),I/'INPUT.v1.json',I/'RUN-RESULT.v1.json',I/'gpu/RESULT.v1.json']}
 O.mkdir()
 for fixture,r in zip(inp['fixtures'],gpu['rows']):
  frame=read(Path(fixture['frame_path']));rawpath=Path(r['gpu_rgba_f16']);f32path=Path(r['rgba_f32']);raw=rawpath.read_bytes();assert len(raw)==256*256*8 and sha(rawpath)==r['gpu_rgba_f16_sha256'];assert sha(f32path)==r['sha256']
  pixels=np.frombuffer(raw,dtype='<f2').astype(np.float32).reshape(256,256,4);assert np.isfinite(pixels).all();assert np.array_equal(pixels.ravel(),np.frombuffer(f32path.read_bytes(),dtype='<f4'));assert np.all(pixels[:,:,3]==.25)
  background=np.array(fixture['background'][:3],dtype='<f2').astype(np.float32);changed=np.any(pixels[:,:,:3]!=background,axis=2);count=int(changed.sum());assert count>0,'Empty actual particle render'
  key=(frame['node_path'],frame['seed'],frame['requested_time'],frame['camera_fixture'])
  digest=sha(rawpath)
  if key in pairs:assert pairs[key]==digest,'Repeated actual GPU particle render changed';repeats+=1
  else:pairs[key]=digest
  for p in [rawpath,f32path]:pins[str(p)]=sha(p)
  rows.append(dict(frame_path=fixture['frame_path'],pixels_sha256=digest,changed_pixels=count,minimum_rgb=float(pixels[:,:,:3].min()),maximum_rgb=float(pixels[:,:,:3].max()),alpha_preserved=True))
  if frame['seed']==1729 and frame['repetition']==0 and frame['requested_time'] in [.5,10.] and frame['camera_fixture'] in [0,1,2]:
   rgb=np.clip(pixels[:,:,:3],0,1);display=np.where(rgb<=.0031308,rgb*12.92,1.055*rgb**(1/2.4)-.055);im=Image.fromarray(np.rint(display*255).astype('uint8'),'RGB');preview=O/f'{rawpath.stem}.png';im.save(preview);previews.append((im,f"{frame['node_path']} t={frame['requested_time']} camera={frame['camera_fixture']}"));pins[str(preview)]=sha(preview)
 assert repeats==168 and len(previews)==12
 sheet=Image.new('RGB',(3*256,4*282),(18,20,23));draw=ImageDraw.Draw(sheet)
 for i,(im,label) in enumerate(previews):x=i%3*256;y=i//3*282;sheet.paste(im,(x,y));draw.text((x+6,y+260),label,fill='white')
 sheet.save(O/'CONTACT.png');pins[str(O/'CONTACT.png')]=sha(O/'CONTACT.png')
 result=dict(status='336_actual_gpu_particle_mesh_images_reopened',input_output_pins=pins,frames=336,stable_repeat_pairs=repeats,rows=rows,actual_bc3_mips_and_sampler_used=True,native_color_bytes_and_uvs_used=True,product_renderer_integrated=False,source_unity_mesh_parity=False,agent_visual_review=False,autonomous_simulation=False,full_import=False,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
 (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ['input_output_pins','rows']}))
if __name__=='__main__':main()
