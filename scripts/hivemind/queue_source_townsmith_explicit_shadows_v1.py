"""Admit the exact dependency closure of eight original mesh/slot cases to HDRP."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,re,shutil,os,sys,subprocess
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'townsmith-explicit-shadow-controls-v1'
PROJECT=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
SOURCE=W/'hivemind-run/townsmith'
SCOPE=W/'townsmith-current-draw-binding-scope-v1/RESULT.v1.json'
CS=Path(__file__).parent/'candidates/SourceTownsmithExplicitShadowsV1.cs'
HELPER=PROJECT/'Assets/Editor/SourceTownsmithExplicitShadowsV1.cs'
UNITY=Path('F:/Unity/6000.5.8f1/Editor/Unity.exe')
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def save(n,v):
 p=ROOT/n;t=p.with_suffix('.tmp');t.write_text(json.dumps(v,indent=2,ensure_ascii=False),encoding='utf-8');t.replace(p)
def prepare():
 assert not ROOT.exists() and not HELPER.exists() and UNITY.is_file()
 assert subprocess.check_output(['powershell','-NoProfile','-Command','@(Get-Process Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
 context_path=W/'townsmith-hdrp-slot-contexts-reopen-v1/RESULT.v1.json'
 context=read(context_path)
 assert context['cases_reopened']==8 and context['source_files_unchanged']
 derived={r['private']:r['private_serialized_sha256'] for r in context['private_material_serializations']}
 scope=read(SCOPE);m=read(SOURCE/'source.v1.json');files={r['stored_path']:r for r in m['files']};guids=m['guid_paths']
 vault=Path('F:/OpenRPG_AssetVault')
 intake_path=vault/'metadata/hivemind/townsmith.intake.v1.json'
 intake=read(intake_path);assert intake['archive_sha256']==m['archive_sha256']
 archive=vault/'originals/unity-purchased/hivemind/townsmith'/Path(m['archive_path']).name
 assert archive.is_file() and archive.stat().st_size==intake['archive_bytes']
 assert sha(archive)==m['archive_sha256']
 cases=[];seen=set();pending=[]
 for row in scope['rows']:
  mesh=row['source_mesh_identity'];key=(mesh['guid'],mesh['file_id'])
  if key in seen:continue
  seen.add(key);group=next(g for g in scope['mesh_groups'] if (g['source_mesh_identity']['guid'],g['source_mesh_identity']['file_id'])==key)
  ref=lambda r:{k:r[k] for k in ['guid','file_id','asset_path']}
  cases.append({'id':'case-'+str(len(cases)).zfill(2),'mesh':ref(mesh),'materials':[ref(r) for r in row['material_slots']],'expected_submeshes':row['source_submesh_count'],'affected_bindings':group['bindings']})
  pending.extend([mesh['asset_path'],*[r['asset_path'] for r in row['material_slots']]])
 assert len(cases)==8
 pins={str(p):sha(p) for p in [Path(__file__),CS,SCOPE,SOURCE/'source.v1.json']}
 pins[str(archive)]=m['archive_sha256'];pins[str(intake_path)]=sha(intake_path);private={};copies=[];unresolved=set();visited=set()
 # Decide and validate the complete file set before modifying the private project.
 while pending:
  rel=pending.pop()
  if rel in visited:continue
  visited.add(rel)
  for suffix in ['','.meta']:
   key='source/'+rel+suffix
   if key not in files:continue
   info=files[key];source=SOURCE/key
   assert source.is_file() and sha(source)==info['sha256'],source
   assert source.suffix.lower() not in ['.cs','.dll','.exe'], 'Purchased executable dependency not admitted'
   pins[str(source)]=info['sha256']
   target=PROJECT/(rel+suffix)
   if target.exists():assert sha(target)==derived.get(str(target),info['sha256']),'Private target conflict: '+str(target)
   copies.append((source,target,info['sha256']))
   if source.suffix.lower() in ['.meta','.mat','.shadergraph','.shadersubgraph','.shader']:
    for guid in re.findall(r'[0-9a-fA-F]{32}',source.read_text(encoding='utf-8',errors='strict')):
     dep=guids.get(guid.lower())
     if dep and ('source/'+dep) in files:pending.append(dep)
     elif not dep:unresolved.add(guid.lower())
 ROOT.mkdir()
 for source,target,h in copies:
  target.parent.mkdir(parents=True,exist_ok=True)
  if not target.exists():shutil.copy2(source,target)
  assert sha(target)==derived.get(str(target),h);private[str(target)]=sha(target)
 pins[str(context_path)]=sha(context_path)
 cases=[dict(c,id=c['id']+'-'+mode,control_mode=mode) for c in cases[:3] for mode in ['explicit_full_shadow','explicit_visible_shadow']]
 assert len(cases)==6
 HELPER.write_bytes(CS.read_bytes());pins[str(HELPER)]=sha(HELPER)
 for rel in ['ProjectSettings/GraphicsSettings.asset','ProjectSettings/QualitySettings.asset','ProjectSettings/ProjectSettings.asset','Packages/manifest.json','Packages/packages-lock.json']:
  p=PROJECT/rel;pins[str(p)]=sha(p)
 save('REQUEST.v1.json',{'output':str(ROOT/'NATIVE-RESULT.v1.json'),'cases':cases})
 pins[str(ROOT/'REQUEST.v1.json')]=sha(ROOT/'REQUEST.v1.json')
 save('PREPARED.v1.json',{'sub_lot':'L03.TOWNSMITH.DESSINS','input_pins':pins,'private_before_pins':private,'resolved_archive_path':str(archive),'historical_archive_path_preserved_in_source_manifest':m['archive_path'],'license':intake['license'],'cases':6,'affected_bindings':15,'source_and_meta_files_admitted':len(copies),'source_bytes_admitted':sum(p.stat().st_size for p,_,_ in copies),'unresolved_guid_references_to_check_in_unity':sorted(unresolved),'purchased_scripts_copied_or_executed':False,'source_material_assets_replaced':False,'scoped_private_serialization_receipt':str(context_path),'triangle_geometry_modified':False,'isolated_triangle_grouping_controls':True,'original_index_descriptors_preserved':True,'private_material_instances_used':True,'full_import':False})
 with (ROOT/'controller.log').open('xb') as log:
  child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 save('JOB.v1.json',{'pid':child.pid});print(json.dumps({'pid':child.pid,'root':str(ROOT),'cases':6,'files_admitted':len(copies)}))
def run():
 state={'status':'source_townsmith_draw_slots_running','full_import':False}
 try:
  prepared=read(ROOT/'PREPARED.v1.json')
  for p,h in prepared['input_pins'].items():assert sha(p)==h,p
  env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT),UPM_CACHE_ROOT=str(W/'unity-upm-cache'),OPENRPG_TOWNSMITH_DRAW_OUT=str(ROOT))
  with (ROOT/'stdout.log').open('xb') as out,(ROOT/'stderr.log').open('xb') as err:
   child=subprocess.Popen([str(UNITY),'-batchmode','-force-d3d11','-quit','-job-worker-count','2','-projectPath',str(PROJECT),'-executeMethod','OpenRPGSourceTownsmithExplicitShadowsV1.Export','-logFile',str(ROOT/'Unity.log')],cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
   state['unity_pid']=child.pid;save('RUN-RESULT.v1.json',state);state['actual_exit_code']=child.wait();assert child.returncode==0
  report=read(ROOT/'NATIVE-RESULT.v1.json');assert report['backend']=='Direct3D11' and report['source_objects_unchanged'] and len(report['rows'])==6 and all(r['original_material_runtime_states_unchanged'] and r['private_material_instances_used'] for r in report['rows'])
  for p,h in prepared['input_pins'].items():assert sha(p)==h,p
  changed=[p for p,h in prepared['private_before_pins'].items() if sha(p)!=h];assert not changed,changed
  state.update(status='source_townsmith_explicit_shadow_controls_captured_comparison_pending',source_inputs_unchanged=True,private_changed=[],cases_captured=6,frame_debugger_locally_supported=report['frame_debugger_locally_supported'],native_draw_cases_observed=sum(r['native_draw_mapping_observed'] for r in report['rows']),editor_verified=False,complete_assets_delivered_increment=0)
 except Exception as e:state.update(status='source_townsmith_draw_slot_refusal_retained',error=repr(e))
 state['finished_utc']=datetime.now(timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()
