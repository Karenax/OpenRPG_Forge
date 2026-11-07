"""Reconcile selected source graph samples with actual Unity texture bindings."""
from pathlib import Path
from datetime import datetime,timezone
import copy,json,hashlib
W=Path('F:/OpenRPG_ImportWork');O=W/'medieval-torch-active-texture-bindings-v156'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
 assert not O.exists()
 ip=W/'medieval-torch-active-material-ir-v154/RESULT.v1.json';np=W/'medieval-torch-native-prefab-import-v137/NATIVE-RESULT.v1.json';cp=W/'medieval-torch-private-prefab-clone-v134/RESULT.v1.json';dp=W/'medieval-torch-source-dependency-closure-v132/RESULT.v1.json'
 irproof=read(ip);native=read(np);clone=read(cp);deps=read(dp);mapping=clone['guid_mapping'];materials={}
 for node in native['nodes']:
  for m in node['materials']:
   guid=m['material']['guid']
   if guid in materials:assert materials[guid]==m
   materials[guid]=m
 pins={str(p):sha(p) for p in [ip,np,cp,dp,Path(__file__)]};rows=[];controls=[]
 def reconcile(original_guid,prop,n):
  assert not n['explicitly_null'] and n['texture']['guid']==mapping[original_guid]
  assert n['texture']['file_id']=='2800000' and n['texture']['type']=='UnityEngine.Texture2D'
  assert n['width']>0 and n['height']>0 and n['mips']>0
  dep=deps['nodes'][original_guid];source=Path(dep['source']);meta=Path(dep['meta']);assert sha(source)==dep['source_sha256'] and sha(meta)==dep['meta_sha256']
  pins[str(source)]=sha(source);pins[str(meta)]=sha(meta)
  return dict(property=prop,original_guid=original_guid,source=str(source),source_sha256=sha(source),meta=str(meta),meta_sha256=sha(meta),native_binding=n,source_import_format_preserved=n['format'],srgb_declared_by_native_format=n['format'].endswith('_SRGB'))
 for row in irproof['rows']:
  p=Path(row['path']);assert sha(p)==row['sha256'];pins[str(p)]=sha(p);ir=read(p);m=materials[mapping[row['guid']]];byprop={t['property']:t for t in m['texture_bindings']};samples=[];textures={}
  for node in ir['nodes']:
   if node['opcode']!='SampleTexture2DNode':continue
   texture=next(x['value'] for x in node['inputs'] if x['name']=='Texture');assert texture['provenance'].startswith('material:');prop=texture['provenance'].split(':',1)[1];guid=texture['literal']['guid'];assert prop in {'_BaseMap','_NormalMap','_ORMH'}
   binding=reconcile(guid,prop,byprop[prop]);textures[prop]=binding
   samples.append(dict(node=node['id'],source_node=node['node_id'],property=prop,output_slot=node['output_slot_id'],output_channel=node['output_slot']['m_DisplayName'],normal_decode_required=node['source_parameters']['m_TextureType']==1))
  assert len(samples)==5 and len(textures)==3
  assert {(b['predicate']['provenance'],b['predicate']['literal'],b['chosen_input']) for b in ir['selected_branches']}=={('material:_UseMask',0.0,'False'),('material:_UseOpacity',0.0,'False')}
  assert byprop['_MaskTexture']['explicitly_null'] and byprop['_OpacityMap']['explicitly_null']
  bad=copy.deepcopy(byprop['_BaseMap']);bad['texture']['guid']=byprop['_NormalMap']['texture']['guid']
  try:reconcile(textures['_BaseMap']['original_guid'],'_BaseMap',bad)
  except AssertionError:controls.append(dict(material=row['guid'],crossed_native_texture_guid_refused=True))
  else:raise AssertionError('Crossed texture admitted')
  rows.append(dict(material_guid=row['guid'],native_material_identity=m['material'],active_samples=samples,active_textures=list(textures.values()),inactive_null_properties=['_MaskTexture','_OpacityMap'],shader_execution_verified=False))
 O.mkdir();result=dict(status='three_materials_fifteen_samples_nine_source_textures_matched_to_native_import',finished_utc=datetime.now(timezone.utc).isoformat(),materials=rows,negative_controls=controls,input_pins=pins,shader_execution_verified=False,compressed_mip_payloads_exported=False,normal_swizzle_and_color_transfer_qualified=False,full_import=False)
 (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(dict(materials=len(rows),samples=sum(len(r['active_samples']) for r in rows),textures=sum(len(r['active_textures']) for r in rows),negative_controls=len(controls),shader_execution_verified=False)))
if __name__=='__main__':main()
