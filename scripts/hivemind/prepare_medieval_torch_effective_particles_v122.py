"""Retain full particle documents and apply every authored instance override strictly."""
from pathlib import Path
from datetime import datetime,timezone
import copy,hashlib,importlib.util,json
W=Path('F:/OpenRPG_ImportWork');O=W/'medieval-torch-effective-particles-v122'
HELPER=W/'hivemind-effect-source-ir-v1/capture_source_ir.py'
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def write(p,v):
 with p.open('x',encoding='utf-8') as f:json.dump(v,f,ensure_ascii=False,allow_nan=False,indent=2)
def apply(documents,edits,guid):
 out=copy.deepcopy(documents);by_id={d['file_id']:d for d in out};applied=[]
 for edit in edits:
  target=edit['target'];assert target['guid']==guid,'Foreign target GUID'
  assert target['fileID'] in by_id,'Unknown target file ID'
  assert edit['objectReference']['fileID']=='0','Object override requires explicit reference resolution'
  keys=edit['propertyPath'].split('.');obj=by_id[target['fileID']]['data']
  for key in keys[:-1]:assert isinstance(obj,dict) and key in obj,'Unknown field path';obj=obj[key]
  assert isinstance(obj,dict) and keys[-1] in obj,'Unknown target field'
  old=obj[keys[-1]];value=edit['value'];assert type(old) in [str,int,float,bool] and type(value) in [str,int,float,bool],'Unsupported override type'
  obj[keys[-1]]=value;applied.append(dict(file_id=target['fileID'],component_type=by_id[target['fileID']]['type'],property_path=edit['propertyPath'],before=old,after=value))
 return out,applied
assert not O.exists()
proof_path=W/'medieval-torch-source-effect-contract-v121/RESULT.v1.json';proof=read(proof_path);pins=dict(proof['input_pins'])
for name,digest in pins.items():assert sha(name)==digest,name
original=read(W/'hivemind-effect-source-ir-v1/RESULT.v1.json');assert sha(HELPER)==original['source_pins'][str(HELPER)]
pins[str(HELPER)]=sha(HELPER);pins[str(proof_path)]=sha(proof_path);pins[str(Path(__file__).resolve())]=sha(__file__)
spec=importlib.util.spec_from_file_location('trusted_effect_source_parser',HELPER);parser=importlib.util.module_from_spec(spec);spec.loader.exec_module(parser)
controls=parser.controls();parent=parser.parse(Path(proof['source']['stored_path']).read_bytes());instances=[d for d in parent if d['type']=='PrefabInstance'];assert len(instances)==2
manifest_path=W/'hivemind-run/medieval-kingdom/source.v1.json';manifest=read(manifest_path);pins[str(manifest_path)]=sha(manifest_path);assert manifest['private_only']
cases=[];controls_out=[]
for instance in instances:
 data=instance['data'];guid=data['m_SourcePrefab']['guid'];dependency=next(d for d in proof['nested_particle_prefabs'] if d['guid']==guid)
 source=W/'hivemind-run/medieval-kingdom/source'/dependency['source_path'];assert sha(source)==dependency['sha256'];docs=parser.parse(source.read_bytes())
 edits=data['m_Modification'];assert not edits['m_RemovedComponents'];assert not edits.get('m_AddedComponents');assert not edits.get('m_RemovedGameObjects');assert not edits.get('m_AddedGameObjects')
 effective,applied=apply(docs,edits['m_Modifications'],guid)
 for bad_kind in ['foreign_guid','unknown_field','unknown_target']:
  bad=copy.deepcopy(edits['m_Modifications'])
  if bad_kind=='foreign_guid':bad[0]['target']['guid']='f'*32
  elif bad_kind=='unknown_field':bad[0]['propertyPath']='missing.field'
  else:bad[0]['target']['fileID']='-999'
  try:apply(docs,bad,guid)
  except AssertionError:controls_out.append(dict(source_guid=guid,control=bad_kind,refused=True))
  else:raise AssertionError('Corrupted instance override admitted')
 systems=[d for d in effective if d['type']=='ParticleSystem'];renderers=[d for d in effective if d['type']=='ParticleSystemRenderer'];assert len(systems)==len(renderers)==1
 flags={k:v['enabled'] for k,v in systems[0]['data'].items() if isinstance(v,dict) and 'enabled' in v};assert all(v in [0,1,False,True] for v in flags.values())
 references=list(parser.refs(effective));material_guids=sorted({ref['reference']['guid'] for ref in references if ref['field_path'].find('m_Materials')>=0 and 'guid' in ref['reference']})
 material_sources=[]
 for mg in material_guids:
  rel=manifest['guid_paths'].get(mg);assert rel,'Material not present in same source manifest'
  material=source.parents[len(Path(dependency['source_path']).parts)-1]/rel
  # Explicit pack source root avoids relying on parent-count arithmetic.
  material=W/'hivemind-run/medieval-kingdom/source'/rel
  assert material.is_file();pins[str(material)]=sha(material);meta=Path(str(material)+'.meta');assert meta.is_file();pins[str(meta)]=sha(meta)
  material_sources.append(dict(guid=mg,path=str(material),sha256=pins[str(material)]))
 cases.append(dict(source_guid=guid,source_path=str(source),source_sha256=sha(source),original_documents=docs,effective_documents=effective,parent_instance=instance,applied_overrides=applied,particle_module_flags=flags,particle_renderer_file_id=renderers[0]['file_id'],material_sources=material_sources,global_transform_composition_verified=False,native_behavior_verified=False,runtime_converted=False))
for name,digest in pins.items():assert sha(name)==digest,name
O.mkdir();payload=O/'EFFECTIVE-SOURCE-IR.v1.json';write(payload,dict(source_group_id=proof['source_group_id'],original_batch_id=proof['original_batch_id'],license=manifest['license'],archive_sha256=manifest['archive_sha256'],private_only=True,parent_documents=parent,cases=cases,all_component_fields_retained=True,source_semantics_normalized=False,runtime_converted=False))
write(O/'RESULT.v1.json',dict(status='two_nested_particle_sources_reopened_with_all_authored_scalar_overrides_applied',finished_utc=datetime.now(timezone.utc).isoformat(),lot='L06.MEDIEVAL.TORCH.001',source_group_id=proof['source_group_id'],particle_instances=len(cases),applied_overrides=sum(len(c['applied_overrides']) for c in cases),parser_controls=controls,override_corruption_controls=controls_out,input_pins=pins,output_pins={str(payload):sha(payload)},source_inputs_unchanged=True,native_behavior_verified=False,global_transform_composition_verified=False,full_import=False))
print(json.dumps(dict(particle_instances=len(cases),applied_overrides=sum(len(c['applied_overrides']) for c in cases),enabled_modules=[c['particle_module_flags'] for c in cases],proof=str(O/'RESULT.v1.json'))))
