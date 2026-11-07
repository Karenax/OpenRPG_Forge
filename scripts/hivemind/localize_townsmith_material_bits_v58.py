from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,numpy as np
from PIL import Image,ImageDraw
w=Path('F:/OpenRPG_ImportWork');root=w/'townsmith-material-bits-localization-v58';assert not root.exists()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
proof=w/'townsmith-material-bits-comparison-v58/RESULT.v1.json';d=read(proof);assert d['status']=='source_float32_and_native_lossless_graph_values_compared'
pins={str(proof):sha(proof),str(Path(__file__)):sha(Path(__file__))}
def pin(p,expected):
 assert sha(p)==expected,str(p)
 pins[str(p)]=expected
source=w/'townsmith-door-source-material-values-v41';depth=source/'door-nopost-linear.depth-3.r32f';pin(depth,d['input_pins'][str(depth)]);mask=(np.fromfile(depth,dtype='<f4').reshape(512,512)>0)[::-1]
hdrproof=w/'townsmith-hdr-residual-localization-v1/RESULT.v1.json';hp=read(hdrproof);pins[str(hdrproof)]=sha(hdrproof)
npth=w/'townsmith-hdr-buffer-comparison-v56/FORGE-DECODED.rgb32f';spth=w/'townsmith-door-source-microshadow-inputs-v55/door-nopost-linear.rgba16f'
for p in [npth,spth]:pin(p,hp['input_pins'][str(p)])
native=np.fromfile(npth,dtype='<f4').reshape(512,512,3);original=np.fromfile(spth,dtype='<f2').reshape(512,512,4)[::-1,:,:3].astype(np.float32);hdrerr=np.abs(native-original)[mask].max(1)
sheet=Image.new('RGB',(1536,1632),(28,28,28));draw=ImageDraw.Draw(sheet);rows=[];decoded_all=[]
for ch,row in enumerate(d['rows']):
 group=ch//3;lane=ch%3;raw=source/f'door-nopost-linear.graph-probe-{9+group}.rgba32f';meta=source/f'door-nopost-linear.graph-probe-{9+group}.json'
 for p in [raw,meta]:pin(p,d['input_pins'][str(p)])
 m=read(meta);a=np.fromfile(raw,dtype='<f4').reshape(m['height'],m['width'],4)[:512,:512][::-1,:,lane][mask]
 image=w/f'townsmith-native-graph-bits-channel-{ch}-v58/FORGE-LINEAR.rgba16f';pin(image,d['input_pins'][str(image)]);codes=np.rint(np.fromfile(image,dtype='<f2').astype(np.float32).reshape(512,512,4)[mask]*255).astype(np.uint32);bits=codes[:,0]|(codes[:,1]<<8)|(codes[:,2]<<16)|(codes[:,3]<<24);b=bits.astype('<u4').view('<f4');decoded_all.append(b);err=np.abs(a.astype(np.float64)-b.astype(np.float64))
 assert abs(float(err.mean())-row['mean_absolute_error'])<1e-12
 r=dict(name=row['name'],pixels_over_0001=int((err>.0001).sum()),pixels_over_001=int((err>.001).sum()),rgb_residual_over_001_overlap=int(((err>.0001)&(hdrerr>.001)).sum()),error_percentiles=np.percentile(err,[50,90,95,99,99.9,100]).tolist(),correlation_with_hdr_residual=float(np.corrcoef(err,hdrerr)[0,1]),diagnostic_only=True);rows.append(r)
 heat=np.zeros((512,512,3),dtype=np.uint8);heat[mask,0]=np.round(np.clip(err/.002,0,1)*255).astype(np.uint8);heat[mask,1]=np.where(err<=.0001,30,0);x=(ch%3)*512;y=(ch//3)*544;sheet.paste(Image.fromarray(heat),(x,y+28));draw.text((x+8,y+7),row['name']+' (display scale .002)',fill='white')
root.mkdir();pic=root/'graph-residuals.png';sheet.save(pic);pins[str(pic)]=sha(pic)
values=root/'native-covered-pixels.9xf32';np.stack(decoded_all,axis=1).astype('<f4').tofile(values);pins[str(values)]=sha(values)
# Preserve the exact current common-source epoch only after verifying every pinned live path.
r=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust');epoch=root/'source-input-epoch';snapshots={}
for key,h in d['input_pins'].items():
 p=Path(key)
 if p.is_relative_to(r):
  pin(p,h);target=epoch/p.relative_to(r);target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(p.read_bytes());assert sha(target)==h;snapshots[str(p)]=dict(snapshot=str(target),sha256=h)
epochmeta=root/'SOURCE-EPOCH.v1.json';epochmeta.write_text(json.dumps(dict(source_snapshots=snapshots),indent=2),encoding='utf-8');pins[str(epochmeta)]=sha(epochmeta)
result=dict(status='lossless_graph_residuals_localized',rows=rows,input_pins=pins,finished_utc=datetime.now(timezone.utc).isoformat(),source_snapshot_files=len(snapshots),required_roi_unchanged=True,diagnostic_thresholds_not_qualification=True,correlation_is_not_causation=True,source_pixels_applied_to_native=False,full_import=False,material_fidelity_qualified=False)
(root/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
