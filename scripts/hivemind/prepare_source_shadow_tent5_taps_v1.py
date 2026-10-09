"""Capture each native PCF5 tap without changing the caster raster or inputs."""
from pathlib import Path
import hashlib,json,shutil
W=Path('F:/OpenRPG_ImportWork')
SEED=W/'firepit-punctual-d16-tent5-receiver-v1'
ROOT=W/'firepit-punctual-d16-tent5-taps-v1'
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not ROOT.exists()
 pins=json.loads((SEED/'CASE.v1.json').read_text(encoding='utf-8'))['input_pins'].copy()
 for p,h in pins.items():assert sha(p)==h,p
 ROOT.mkdir();(ROOT/'src').mkdir()
 for name in ['Cargo.toml','Cargo.lock','vertices.float32','indices.uint32','receiver-cases.float32']+[f'face-{i}.uniform' for i in range(6)]:
  shutil.copy2(SEED/name,ROOT/name)
 shader=(SEED/'receiver.wgsl').read_text(encoding='utf-8')
 start=shader.index(' let visibility=source_tent5_compare')
 end=shader.index('\n}',start)
 shader=shader[:start]+'''
 let taps=source_tent5_samples(vec4<f32>(1./4096.,1./4096.,4096.,4096.),item.xy);
 for(var i=0u;i<9u;i+=1u) {
  let comparison=textureSampleCompareLevel(atlas_depth,atlas_compare,taps.uv[i],item.z+1./65536.);
  receiver_output[id.x*9u+i]=vec4<f32>(taps.weights[i],taps.uv[i],comparison);
 }
'''+shader[end:]
 (ROOT/'receiver.wgsl').write_text(shader,encoding='utf-8')
 text=(SEED/'src/main.rs').read_text(encoding='utf-8')
 assert text.count('let receiver_size=1458u64*16;')==1
 text=text.replace('let receiver_size=1458u64*16;','let receiver_size=1458u64*9*16;')
 old='if !active{assert!((v-1.).abs()<2e-6);}'
 assert text.count(old)==1
 text=text.replace(old,'let comparison=f32::from_le_bytes(row[12..16].try_into().unwrap());assert!(comparison.is_finite()&&comparison>=0.&&comparison<=1.);if !active{assert!((comparison-1.).abs()<2e-6);}')
 text=text.replace('"receiver_cases_per_state":1458','"receiver_cases_per_state":1458,"taps_per_case":9,"tap_components":["weight","u","v","comparison"]')
 text=text.replace('native_d16_tent5_receiver_dispatched_comparison_pending','native_d16_tent5_nine_taps_dispatched_comparison_pending')
 (ROOT/'src/main.rs').write_text(text,encoding='utf-8')
 for p in [Path(__file__),SEED/'CASE.v1.json',*[p for p in ROOT.rglob('*') if p.is_file()]]:
  pins[str(p)]=sha(p)
 (ROOT/'CASE.v1.json').write_text(json.dumps({'sub_lot':'L01.PCF5.3.LIMITES','cases_per_state':1458,'taps_per_case':9,'components':['weight','u','v','comparison'],'absolute_weight_uv_threshold':2e-7,'absolute_comparison_threshold':2e-7,'source_depth_pixels_used_as_render_input':False,'full_import':False,'complete_assets_delivered_increment':0,'input_pins':pins},indent=2,ensure_ascii=False),encoding='utf-8')
 print(str(ROOT))
if __name__=='__main__':main()
