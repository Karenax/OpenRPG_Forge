"""Resolve the complete source GUID graph conservatively inside one authored variant."""
from pathlib import Path
from collections import defaultdict
from datetime import datetime,timezone
import hashlib,json,re,subprocess
W=Path('F:/OpenRPG_ImportWork');O=W/'medieval-torch-source-dependency-closure-v132';MP=W/'hivemind-run/medieval-kingdom/source.v1.json';SDK=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Library/PackageCache'
TEXT={'.prefab','.mat','.asset','.anim','.controller','.terrainlayer','.meta','.shadergraph','.shadersubgraph'}
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(p.read_text('utf-8-sig'))
def refs(text,suffix):
 found=set(re.findall(r'\bguid:\s*([0-9a-fA-F]{32})\b',text))
 if suffix in {'.shadergraph','.shadersubgraph'}:
  decoder=json.JSONDecoder();index=0;objects=[]
  while index<len(text):
   while index<len(text) and text[index].isspace():index+=1
   if index==len(text):break
   obj,end=decoder.raw_decode(text,index);objects.append(obj);index=end
  def visit(v):
   if isinstance(v,dict):
    for k,x in v.items():
     if k.lower() in {'guid','m_guid','m_assetguid'} and isinstance(x,str) and re.fullmatch('[0-9a-fA-F]{32}',x):found.add(x)
     visit(x)
   elif isinstance(v,list):
    for x in v:visit(x)
   elif isinstance(v,str) and v.strip().startswith(('{','[')):
    try:visit(json.loads(v))
    except json.JSONDecodeError:pass
  visit(objects)
 return {x.lower() for x in found}
assert not O.exists();manifest=read(MP);assert manifest['private_only'];index=defaultdict(list)
for f in manifest['files']:
 if f['kind']=='asset':index[f['guid'].lower()].append(f)
pins={str(MP):sha(MP),str(Path(__file__).resolve()):sha(__file__)};pending={'abc00000000004388197602292238737'};seen={};edges=[];refusals=[];sdk_index={};unresolved=set()
while pending:
 guid=pending.pop()
 if guid in seen:continue
 candidates=index.get(guid,[])
 if len(candidates)>1:refusals.append(dict(guid=guid,reason='Ambiguous same-variant source GUID'));seen[guid]=dict(status='ambiguous');continue
 if candidates:
  f=candidates[0];asset=MP.parent/f['stored_path'];assert sha(asset)==f['sha256'];meta=Path(str(asset)+'.meta');meta_record=next(r for r in manifest['files'] if r['stored_path']==meta.relative_to(MP.parent).as_posix());assert sha(meta)==meta_record['sha256'];kind='same_pack_authored_variant'
 else:
  if guid not in sdk_index:
   match=subprocess.run(['rg','-l','--glob','*.meta','^guid: '+guid+'\r?$',str(SDK)],capture_output=True,text=True);assert match.returncode in [0,1];sdk_index[guid]=match.stdout.splitlines()
  matches=sdk_index[guid]
  if len(matches)!=1:unresolved.add(guid);seen[guid]=dict(status='external_unresolved',sdk_matches=matches);continue
  meta=Path(matches[0]);asset=meta.with_suffix('');assert asset.is_file();kind='current_sdk_dependency'
 pins[str(asset)]=sha(asset);pins[str(meta)]=sha(meta);referenced=set()
 for p in [asset,meta]:
  if p.suffix.lower() in TEXT:
   try:referenced.update(refs(p.read_text('utf-8-sig'),p.suffix.lower()))
   except (UnicodeError,json.JSONDecodeError) as e:refusals.append(dict(guid=guid,path=str(p),reason=str(e)))
 referenced.discard(guid);referenced.discard('0'*32)
 seen[guid]=dict(status='source_payload_and_meta_rehashed',kind=kind,source=str(asset),source_sha256=pins[str(asset)],meta=str(meta),meta_sha256=pins[str(meta)],references=sorted(referenced))
 for dependency in referenced:edges.append(dict(source_guid=guid,dependency_guid=dependency))
 pending.update(referenced-set(seen))
for p,h in pins.items():assert sha(Path(p))==h,p
O.mkdir();r=dict(status='torch_source_dependency_graph_reopened_with_external_refs_retained',finished_utc=datetime.now(timezone.utc).isoformat(),lot='L06.MEDIEVAL.TORCH.001',root_guid='abc00000000004388197602292238737',nodes=seen,edges=edges,source_payloads=sum(n.get('kind')=='same_pack_authored_variant' for n in seen.values()),sdk_payloads=sum(n.get('kind')=='current_sdk_dependency' for n in seen.values()),unresolved_external_guids=sorted(unresolved),refusals=refusals,input_pins=pins,license=manifest['license'],archive_sha256=manifest['archive_sha256'],other_variants_borrowed=False,source_scripts_executed=False,unity_asset_database_resolution_executed=False,binary_model_import_dependencies_and_subasset_ids_verified=False,full_import=False);(O/'RESULT.v1.json').write_text(json.dumps(r,indent=2,allow_nan=False),'utf-8');print(json.dumps(dict(source_payloads=r['source_payloads'],sdk_payloads=r['sdk_payloads'],unresolved_external_guids=r['unresolved_external_guids'],refusals=r['refusals'],root=str(O))))
