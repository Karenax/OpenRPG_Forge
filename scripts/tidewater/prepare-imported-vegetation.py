"""Prepare content-addressed placements for the canonical Forge vegetation v2 document."""
from pathlib import Path
import hashlib,json,math,struct,sys,re
assert len(sys.argv)==4
source,terrain_path,target=map(lambda v:Path(v).resolve(),sys.argv[1:])
assert not target.exists() and not target.is_relative_to(source)
meta=json.loads((source/'manifest.json').read_text());terrain=json.loads(terrain_path.read_text())
assert meta['source']['commit']=='1438b1abfcaee3267092b75573014f4d9b4a983c'
files={x['path']:x for x in meta['files']}
assert terrain['schema_version']==4 and terrain['sampled_source']['samples_sha256']==files['terrain/heights.bin']['sha256']
template=json.loads((Path(__file__).resolve().parents[2]/'examples/visual_valley/vegetation.v1.json').read_text())
document={**template,'schema_version':2,'project_id':terrain['project_id'],'area_id':terrain['area_id'],'name':'Tidewater imported vegetation','seed':0,'extent_m':terrain['extent_m'],'cells_per_chunk_axis':1,'layers':[],'exclusions':[]}
blocks=[];payloads={};provenance=[]
parameters=['lean_azimuth_radians','lean_amount','stem_height_m','variation_seed','vertical_shape_scale','source_order']
offsets={}
for group in [['palms'],['bananas'],['youngPalms','ferns'],['monsteras'],['elephantEars','heliconias','strelitzias'],['trees','shrubs']]:
 offset=0
 for species in group:
  offsets[species]=offset;offset+=meta['vegetation_counts'][species]
for species,count in sorted(meta['vegetation_counts'].items()):
 if not isinstance(count,int) or count==0:continue
 name=f'vegetation/{species}.json';raw=(source/name).read_bytes();assert hashlib.sha256(raw).hexdigest()==files[name]['sha256']
 records=json.loads(raw);assert len(records)==count
 orders={r['source_id']:i+offsets[species] for i,r in enumerate(records)}
 layer='tidewater_'+re.sub(r'([A-Z])',lambda m:'_'+m[1].lower(),species)
 kind='fern' if species=='ferns' else 'shrub' if species in ['shrubs','monsteras','elephantEars','heliconias','strelitzias'] else 'tree'
 record_keys=set();data=bytearray()
 for row in sorted(records,key=lambda r:r['source_id']):
  assert row['source_id'] not in record_keys;record_keys.add(row['source_id'])
  assert set(row)<=set(['source_id','x','y','z','s','sy','yaw','la','l','H','seed'])
  yaw=row['yaw'];scale=row['s'];values=[row['x'],row['y'],row['z'],0,math.sin(yaw/2),0,math.cos(yaw/2),scale,scale,scale,row['la'],row['l'],row['H'],row['seed'],row.get('sy',1)]
  values.append(orders[row['source_id']])
  assert all(math.isfinite(v) for v in values)
  data+=bytes.fromhex(row['source_id'])+struct.pack('<16d',*values)
 digest=hashlib.sha256(data).hexdigest();payloads[digest+'.bin']=data
 blocks.append({'layer_id':layer,'prototype_key':layer,'count':count,'encoding':'transform_f64_le_v1','instances_sha256':digest,'parameter_names':parameters})
 base={**template['layers'][0],'id':layer,'display_name':species,'kind':kind,'acceptance_probability':1,'attempts_per_cell':1,'minimum_spacing_m':.001,'minimum_scale':.01,'maximum_scale':8,'maximum_slope_gradient':10,'minimum_altitude_m':-1000,'maximum_altitude_m':10000,'material_filter':None,'collision':None}
 document['layers'].append(base)
 provenance.append({'layer_id':layer,'source_file':name,'source_sha256':files[name]['sha256'],'count':count,'blob_sha256':digest})
document['imported_source']={'schema':'forge.imported-vegetation','version':1,'blocks':blocks}
target.mkdir(parents=True)
for name,data in payloads.items():(target/name).write_bytes(data)
(target/'vegetation.v2.json').write_text(json.dumps(document,indent=2))
(target/'import.json').write_text(json.dumps({'source':meta['source'],'source_manifest_sha256':hashlib.sha256((source/'manifest.json').read_bytes()).hexdigest(),'terrain_document_sha256':hashlib.sha256(terrain_path.read_bytes()).hexdigest(),'blocks':provenance,'total':sum(x['count'] for x in blocks),'limits':['canonical data preparation only','prototype asset catalog and renderer bindings pending','no Studio or runtime qualification','grass mask and village not included']},indent=2))
for name in ['LICENSE-Tidewater.txt','CREDITS-Tidewater.md']:(target/name).write_bytes((source/name).read_bytes())
print(json.dumps({'output':str(target),'blocks':len(blocks),'instances':sum(x['count'] for x in blocks),'bytes':sum(map(len,payloads.values()))}))
