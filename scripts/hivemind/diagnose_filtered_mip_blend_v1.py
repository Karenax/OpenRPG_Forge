from pathlib import Path
from datetime import datetime,timezone
import json,struct,hashlib
W=Path('F:/OpenRPG_ImportWork');SRC=W/'townsmith-s-paint-native-filtered-reference-v1';GPU=W/'townsmith-s-paint-native-filtered-gpu-control-v1';ROOT=W/'townsmith-s-paint-filtered-mip-blend-diagnostic-v1'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def main():
 source=read(SRC/'NATIVE-RESULT.v1.json');gpu=read(GPU/'NATIVE-RESULT.v1.json');lookup={r['guid']:r for r in source['rows']};rows=[];pins={}
 for r in gpu['rows']:
  sp=Path(lookup[r['guid']]['samples_file']);gp=Path(r['file']);a=sp.read_bytes();b=gp.read_bytes();pins[str(sp)]=sha(sp);pins[str(gp)]=sha(gp)
  maximum=0;outside=0
  for i in range(108):
   expected=struct.unpack('<4f',a[i*48:i*48+16]);base=struct.unpack('<4f',b[i*32:i*32+16]);j=i+9 if i<99 else i;nextmip=struct.unpack('<4f',b[j*32:j*32+16])
   error=max(abs(x-(.75*y+.25*z)) for x,y,z in zip(expected,base,nextmip));maximum=max(maximum,error);outside+=error>1e-6
  rows.append({'guid':r['guid'],'cases':108,'max_adjacent_mip_75_25_expression_error':maximum,'outside_fixed_tolerance':outside})
 for p in [SRC/'NATIVE-RESULT.v1.json',GPU/'NATIVE-RESULT.v1.json',Path(__file__)]:pins[str(p)]=sha(p)
 result={'status':'adjacent_mip_blending_hypothesis_measured','finished_utc':datetime.now(timezone.utc).isoformat(),'rows':rows,'input_pins':pins,'sampler_policy_qualified':False,'actual_material_render_qualified':False,'full_import':False,'complete_assets_delivered_increment':0}
 ROOT.mkdir(exist_ok=False);(ROOT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(rows))
if __name__=='__main__':main()
