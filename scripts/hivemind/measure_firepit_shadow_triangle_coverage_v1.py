"""Analytical triangle coverage diagnostic; no fit or GPU qualification."""
from pathlib import Path
import json,struct,math,hashlib,datetime
W=Path('F:/OpenRPG_ImportWork');O=W/'firepit-source-shadow-triangle-coverage-v1'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert not O.exists()
geompath=W/'firepit-source-caster-geometry-v1/SOURCE-CASTER-GEOMETRY-0.json';splitpath=W/'firepit-source-shadow-raster-v6/SOURCE-CASTER-SPLITS-0.json';casepath=W/'firepit-source-shadow-raster-case-v1/CASE.v1.json'
geom=read(geompath)['renderers'][0];assert geom['lod_membership']==[0]
case=read(casepath);eye=case['source_shadow_context_replay']['source_culling_inputs']['origin'];rows=read(splitpath)['splits']
sourcepath=W/'firepit-source-shadow-raster-v4/SOURCE-SHADOW-DEPTH-0.float32';nativepath=W/'firepit-source-shadow-raster-native-v7/NATIVE-SHADOW-DEPTH-0.float32'
a=struct.unpack('<1048576f',sourcepath.read_bytes());b=struct.unpack('<1048576f',nativepath.read_bytes());indices=geom['submeshes'][0]['indices']
results=[]
for layer,split in enumerate(rows):
 m=split['clip_from_world'];points=[]
 for v in geom['positions']:
  rws=[v[k]-eye[k] for k in ['x','y','z']]+[1.];clip=[sum(m[c*4+r]*rws[c] for c in range(4)) for r in range(4)];assert clip[3]==1
  points.append(((clip[0]+1)*256,(1-clip[1])*256,clip[2]))
 masks={name:bytearray(262144) for name in ['positive_screen_area','negative_screen_area','both_faces']}
 triangle_counts={name:0 for name in masks};total_tests=0
 for offset in range(0,len(indices),3):
  p0,p1,p2=[points[indices[offset+j]] for j in range(3)]
  area=(p1[0]-p0[0])*(p2[1]-p0[1])-(p1[1]-p0[1])*(p2[0]-p0[0])
  if abs(area)<=1e-12:continue
  mode='positive_screen_area' if area>0 else 'negative_screen_area';triangle_counts[mode]+=1;triangle_counts['both_faces']+=1
  lo_x=max(0,math.ceil(min(p0[0],p1[0],p2[0])-.5));hi_x=min(511,math.floor(max(p0[0],p1[0],p2[0])-.5));lo_y=max(0,math.ceil(min(p0[1],p1[1],p2[1])-.5));hi_y=min(511,math.floor(max(p0[1],p1[1],p2[1])-.5))
  for y in range(lo_y,hi_y+1):
   for x in range(lo_x,hi_x+1):
    px=x+.5;py=y+.5
    w1=((px-p0[0])*(p2[1]-p0[1])-(py-p0[1])*(p2[0]-p0[0]))/area
    w2=((p1[0]-p0[0])*(py-p0[1])-(p1[1]-p0[1])*(px-p0[0]))/area;w0=1-w1-w2;total_tests+=1
    if min(w0,w1,w2)>=-1e-9 and w0*p0[2]+w1*p1[2]+w2*p2[2]>0:
     masks[mode][y*512+x]=1;masks['both_faces'][y*512+x]=1
 tx=layer%2;ty=layer//2;source=bytes(a[(ty*512+y)*1024+tx*512+x]!=0 for y in range(512) for x in range(512));native=bytes(b[layer*262144+i]!=0 for i in range(262144));checks=[]
 for mode,mask in masks.items():
  entry={'mode':mode,'analytical_nonzero_pixels':sum(mask),'triangle_count':triangle_counts[mode]}
  for name,actual in [('source',source),('native',native)]:
   intersection=sum(x and y for x,y in zip(mask,actual));union=sum(x or y for x,y in zip(mask,actual));entry[name]={'actual_nonzero':sum(actual),'support_iou':intersection/union if union else 1.,'mask_mismatch_pixels':sum(x!=y for x,y in zip(mask,actual))}
  checks.append(entry)
 results.append({'cascade':layer,'checks':checks,'pixel_triangle_tests':total_tests})
files=[Path(__file__),geompath,splitpath,casepath,sourcepath,nativepath]
result={'status':'declared_source_triangle_projection_coverage_measured_without_fitting','finished_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'raster_rule':'512x512 pixel centers, y-down viewport, float64 matrix products, inclusive epsilon 1e-9, positive interpolated depth; not a hardware top-left-rule implementation','source_lod0_vertices':len(points),'source_lod0_triangles':len(indices)//3,'results':results,'gpu_vertex_output_verified':False,'source_actual_raster_cull_mode_inferred':False,'pixel_alignment_fit_allowed':False,'full_shadow_fidelity_qualified':False,'full_import':False,'complete_assets_delivered_increment':0,'input_pins':{str(p):sha(p) for p in files}}
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
