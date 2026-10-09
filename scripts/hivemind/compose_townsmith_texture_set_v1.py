from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,copy,re
S=Path(__file__).parent;R=S.parents[1];W=Path('F:/OpenRPG_ImportWork');ROOT=W/'townsmith-s-paint-texture-set-v1'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 packetpath=W/'townsmith-native-texture-contract-packets-v1/TEXTURE-CONTRACTS.v1.json';packets=read(packetpath)
 profilepath=W/'townsmith-s-paint-normal-abi-v1/PROFILES.v1.json';profiles=read(profilepath)
 generationpath=W/'townsmith-s-paint-wgsl-v1/typed-program-v1/GENERATION.v1.json';generation=read(generationpath)
 common=R/'crates/forge_assets/src/source_graph_bundle.rs';text=common.read_text(encoding='utf-8')
 table=text.split('pub const SOURCE_GRAPH_TEXTURE_BINDINGS:',1)[1].split('];',1)[0];bindings=[tuple(map(int,m)) for m in re.findall(r'\((\d+),\s*(\d+)\)',table)];assert len(bindings)==10
 program=Path(profiles['material_program']);assert sha(program)==profiles['material_program_sha256'];wgsl=program.read_text(encoding='utf-8')
 views=[];byhash={};textures=[];ids=[];pins={str(p):sha(p) for p in [packetpath,profilepath,generationpath,common,program,Path(__file__)]}
 for row in packets['rows']:
  index=row['authored_texture_index'];assert index==len(textures) and generation['textures'][row['guid']]==index
  local={}
  for i,view in enumerate(row['local_buffer_views']):
   path=Path(view['file']);assert sha(path)==view['sha256'] and path.stat().st_size==view['bytes'];pins[str(path)]=view['sha256']
   key=(view['sha256'],view['bytes'])
   if key not in byhash:byhash[key]=len(views);views.append(copy.deepcopy(view))
   else:assert Path(views[byhash[key]]['file']).read_bytes()==path.read_bytes()
   local[i]=byhash[key]
  texture=copy.deepcopy(row['texture'])
  for blob in [texture['png'],texture['source_meta'],texture['imported_storage']['storage'],texture['imported_storage']['capture']]:blob['buffer_view']=local[blob['buffer_view']]
  textures.append(texture);ids.append({'guid':row['guid'],'authored_texture_index':index,'normal_profile_index':row['normal_profile_index'],'texture_binding':bindings[index][0],'sampler_binding':bindings[index][1]})
  for name,kind,oldbinding,newbinding in [('t','texture_2d<f32>',2*index,bindings[index][0]),('s','sampler',2*index+1,bindings[index][1])]:
   old=f'@group(0) @binding({oldbinding}) var {name}{index}:{kind};';new=f'@group(#{{MATERIAL_BIND_GROUP}}) @binding({newbinding}) var {name}{index}:{kind};'
   assert wgsl.count(old)==1;wgsl=wgsl.replace(old,new)
 assert len(textures)==10 and len(views)==32
 ROOT.mkdir(exist_ok=False);target=ROOT/'material-arithmetic-canonical-bindings.wgsl';target.write_text(wgsl,encoding='utf-8');pins[str(target)]=sha(target)
 d={'status':'ten_source_texture_contracts_composed_into_shared_32_view_set','prepared_utc':datetime.now(timezone.utc).isoformat(),'textures':textures,'authored_texture_ids':ids,'buffer_views':views,
   'normal_decode_modes':profiles['normal_decode_modes'],'arithmetic_program':str(target),'arithmetic_program_sha256':sha(target),'translated_operations':134,'input_pins':pins,
   'buffer_views_scope':'Content sharing within one source material; all ten authored slots, source GUIDs and meta preserved',
   'complete_render_stages_created':False,'actual_material_render_qualified':False,'forge_editor_verified':False,'full_import':False,'complete_assets_delivered_increment':0}
 (ROOT/'TEXTURE-SET.v1.json').write_text(json.dumps(d,indent=2),encoding='utf-8')
 # Verify global view indexing with the actual common candidate types, not a Python approximation.
 t=(S/'check_texture_packet_reopen_v1.py').read_text(encoding='utf-8').replace('townsmith-native-texture-contract-packets-reopen-v1','townsmith-s-paint-texture-set-check-v1')
 anchor=' #[test] fn ten_actual_resource_chains_and_identity_sampler_refusals() {';assert t.count(anchor)==1
 test=''' #[test] fn composed_ten_texture_set_reopens_shared_views_and_canonical_bindings(){
  let set:Value=serde_json::from_slice(&fs::read("F:/OpenRPG_ImportWork/townsmith-s-paint-texture-set-v1/TEXTURE-SET.v1.json").unwrap()).unwrap();
  let buffers:Vec<Vec<u8>>=set["buffer_views"].as_array().unwrap().iter().map(|view|fs::read(view["file"].as_str().unwrap()).unwrap()).collect();
  let views:Vec<&[u8]>=buffers.iter().map(|v|v.as_slice()).collect();assert_eq!(views.len(),32);
  for row in set["textures"].as_array().unwrap(){let texture:SourceGraphTexture=serde_json::from_value(row.clone()).unwrap();assert_eq!(validate_texture_integrity(&texture,&views),Ok(()));}
  let stage=fs::read_to_string(set["arithmetic_program"].as_str().unwrap()).unwrap();assert_eq!(crate::source_graph_bundle::stage_texture_count(&stage),Ok(10));
 }
'''
 t=t.replace(anchor,test+anchor)
 t=t.replace("packet=W/'townsmith-native-texture-contract-packets-v1/TEXTURE-CONTRACTS.v1.json';pins[str(packet)]=sha(packet)","packet=W/'townsmith-native-texture-contract-packets-v1/TEXTURE-CONTRACTS.v1.json';pins[str(packet)]=sha(packet);setpath=W/'townsmith-s-paint-texture-set-v1/TEXTURE-SET.v1.json';pins[str(setpath)]=sha(setpath)")
 (S/'check_townsmith_texture_set_v1.py').write_text(t,encoding='utf-8');print(json.dumps({'status':d['status'],'textures':10,'shared_buffer_views':32}))
if __name__=='__main__':main()
