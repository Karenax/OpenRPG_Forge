"""Fixed pre-pixel source atlas/native layers comparison; no fitted alignment."""
from pathlib import Path
import array,datetime,hashlib,json,math,sys
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-source-shadow-depth-comparison-v4'
P=W/'firepit-source-shadow-depth-protocol-v1/PROTOCOL.v1.json';CAP=W/'firepit-source-shadow-raster-native-v5'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not ROOT.exists()
 protocol=read(P);run=read(CAP/'RUN-RESULT.v1.json')
 assert run.get('actual_exit_code')==0,run
 pins=read(CAP/'PREPARED.v1.json')['input_pins']
 for p,h in pins.items():assert sha(p)==h,p
 source=Path(protocol['source_file']);assert sha(source)==protocol['source_sha256']
 native=CAP/'NATIVE-SHADOW-DEPTH-0.float32';meta=read(CAP/'NATIVE-SHADOW-DEPTH-0.json')
 assert (meta['width'],meta['height'],meta['layers'])==(512,512,4)
 assert meta['actual_gpu_depth_readback'] and meta['format']=='Depth32Float'
 assert meta['raster_pipelines']
 for row in meta['raster_pipelines']:
  assert row['ready'] and row['format']=='Depth32Float' and (row['constant'],row['slope_scale'],row['clamp'])==(-1,-2.5,0)
 a=array.array('f');a.frombytes(source.read_bytes());b=array.array('f');b.frombytes(native.read_bytes())
 if sys.byteorder!='little':a.byteswap();b.byteswap()
 assert len(a)==len(b)==protocol['expected_samples']
 assert all(math.isfinite(v) and 0<=v<=1 for v in a) and all(math.isfinite(v) and 0<=v<=1 for v in b)
 rows=[]
 for tx,ty,layer in protocol['source_tiles_to_native_layers']:
  intersection=union=source_nonzero=native_nonzero=0;maximum=total=0.0
  for y in range(512):
   ai=(ty*512+y)*1024+tx*512;bi=layer*512*512+y*512
   for x in range(512):
    av,bv=a[ai+x],b[bi+x];error=abs(av-bv);maximum=max(maximum,error);total+=error
    sa,nb=av!=0,bv!=0;source_nonzero+=sa;native_nonzero+=nb;intersection+=sa and nb;union+=sa or nb
  iou=intersection/union if union else 1.0
  rows.append(dict(layer=layer,source_tile=[tx,ty],source_nonzero=source_nonzero,native_nonzero=native_nonzero,support_iou=iou,maximum_absolute_error=maximum,mean_absolute_error=total/(512*512),passed=iou>=protocol['support_iou_minimum'] and maximum<=protocol['maximum_absolute_depth_error']))
 passed=all(row['passed'] for row in rows)
 files=[Path(__file__).resolve(),P,source,native,CAP/'NATIVE-SHADOW-DEPTH-0.json',CAP/'RUN-RESULT.v1.json',CAP/'PREPARED.v1.json']
 result=dict(status='shadow_depth_fixed_comparison_passed' if passed else 'shadow_depth_fixed_comparison_refused',created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),depth_samples_verified=len(a),native_raster_descriptors_verified=len(meta['raster_pipelines']),layers=rows,input_pins={str(p):sha(p) for p in files},full_import=False,full_shadow_fidelity_qualified=False,complete_assets_delivered_increment=0)
 ROOT.mkdir();(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(result))
if __name__=='__main__':main()
