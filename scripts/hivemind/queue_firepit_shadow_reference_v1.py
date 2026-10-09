"""Capture unchanged FirePit LOD0 with source shadows on/off in a private HDRP scene."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,re,subprocess,os,sys
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-shadow-source-reference-v1'
PROJECT=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
SOURCE=W/'hivemind-run/native-american-village'
BASE=Path(__file__).parent/'candidates/SourceLibraryThumbnailV6.cs'
HELPER=PROJECT/'Assets/Editor/SourceShadowReferenceV1.cs'
UNITY=Path('F:/Unity/6000.5.8f1/Editor/Unity.exe')
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
PREFAB='Assets/Hivemind/NativeAmericanVillage/HDRP(Default)/Art/Prefabs/SM_FirePit_01.prefab'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(name,v):
 t=ROOT/(name+'.tmp');t.write_text(json.dumps(v,indent=2,ensure_ascii=False),'utf-8');t.replace(ROOT/name)
def replace(text,before,after):
 assert text.count(before)==1,before
 return text.replace(before,after)
def helper():
 text=BASE.read_text('utf-8').replace('OpenRPGSourceLibraryThumbnailV6','OpenRPGSourceShadowReferenceV1')
 text=replace(text,'public string path,sha256; public int view','public string path,sha256,raw_path,raw_sha256; public bool source_shadows;public Vector3 light_euler,source_bounds_center,source_bounds_size; public int view')
 text=replace(text,'lightObject=null,volumeObject=null;VolumeProfile','lightObject=null,volumeObject=null,ground=null;Material groundMaterial=null;VolumeProfile')
 text=replace(text,'var visible=fixture.GetComponentsInChildren<Renderer>()','foreach(var fx in fixture.GetComponentsInChildren<ParticleSystem>(true))fx.gameObject.SetActive(false);\n   var sourceDraws=fixture.GetComponentsInChildren<MeshRenderer>(true);if(sourceDraws.Length!=4||sourceDraws.Any(r=>r.shadowCastingMode!=ShadowCastingMode.On))throw new Exception("Expected four original static shadow casters");\n   var visible=fixture.GetComponentsInChildren<Renderer>()')
 needle='cameraObject=new GameObject("Source thumbnail camera");'
 text=replace(text,needle,'ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.hideFlags=HideFlags.HideAndDontSave;UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ground,preview);ground.transform.position=new Vector3(bounds.center.x,bounds.min.y-.01f,bounds.center.z);ground.transform.localScale=Vector3.one*radius;groundMaterial=new Material(Shader.Find("HDRP/Lit"));groundMaterial.hideFlags=HideFlags.HideAndDontSave;groundMaterial.SetColor("_BaseColor",Color.white);groundMaterial.SetFloat("_Smoothness",0);ground.GetComponent<Renderer>().sharedMaterial=groundMaterial;ground.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;\n   '+needle)
 text=replace(text,'FrameSettingsField.Postprocess,FrameSettingsField.ExposureControl','FrameSettingsField.Postprocess,FrameSettingsField.ExposureControl,FrameSettingsField.ShadowMaps')
 text=replace(text,'light.intensity=1;\n   UnityEngine.SceneManagement','light.intensity=1;light.shadows=LightShadows.Hard;\n   UnityEngine.SceneManagement')
 text=replace(text,'new Vector3(-1,.5f,1).normalized','new Vector3(1,.65f,-1).normalized')
 text=replace(text,'light.transform.rotation=camera.transform.rotation;','light.transform.rotation=Quaternion.LookRotation(new Vector3(.5f,-1,.35f).normalized);foreach(var draw in sourceDraws)draw.shadowCastingMode=view==0?ShadowCastingMode.On:ShadowCastingMode.Off;')
 needle='var colors=raw.GetPixels();'
 text=replace(text,needle,'byte[] rawBytes=raw.GetRawTextureData<byte>().ToArray();string rawPath=Path.Combine(Path.GetDirectoryName(request.output),"linear-"+view+".rgba16f");if(File.Exists(rawPath))throw new Exception("Preserve raw reference");File.WriteAllBytes(rawPath,rawBytes);'+needle)
 text=replace(text,'new Row{path=path,sha256=Hash(bytes),view=view,','new Row{path=path,sha256=Hash(bytes),raw_path=rawPath,raw_sha256=Hash(rawBytes),source_shadows=view==0,light_euler=light.transform.eulerAngles,source_bounds_center=bounds.center,source_bounds_size=bounds.size,view=view,')
 text=replace(text,'if(fixture!=null)UnityEngine.Object.DestroyImmediate(fixture);','if(ground!=null)UnityEngine.Object.DestroyImmediate(ground);if(groundMaterial!=null)UnityEngine.Object.DestroyImmediate(groundMaterial);if(fixture!=null)UnityEngine.Object.DestroyImmediate(fixture);')
 return text
def prepare():
 assert not ROOT.exists() and not HELPER.exists() and UNITY.is_file()
 assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
 assert not list((PROJECT/'Assets/Hivemind').rglob('*.cs')),'Purchased scripts must not execute'
 m=read(SOURCE/'source.v1.json');by_guid=m['guid_paths'];guid=next(g for g,p in by_guid.items() if p==PREFAB)
 files={r['stored_path']:r for r in m['files']};pending=[PREFAB];seen=set();pins={str(Path(__file__).resolve()):sha(Path(__file__).resolve()),str(BASE):sha(BASE),str(SOURCE/'source.v1.json'):sha(SOURCE/'source.v1.json')};private={}
 while pending:
  rel=pending.pop()
  if rel in seen:continue
  seen.add(rel)
  for suffix in ['', '.meta']:
   key='source/'+rel+suffix
   if key not in files:continue
   p=SOURCE/key;assert sha(p)==files[key]['sha256'],p;pins[str(p)]=files[key]['sha256']
   q=PROJECT/(rel+suffix);assert q.is_file(),q;private[str(q)]=sha(q)
   if p.suffix in ['.prefab','.mat','.shadergraph','.shadersubgraph','.meta','.shader']:
    for g in re.findall(r'[0-9a-fA-F]{32}',p.read_text('utf-8',errors='strict')):
     path=by_guid.get(g.lower())
     if path and (SOURCE/('source/'+path)).is_file():pending.append(path)
 ROOT.mkdir();HELPER.write_text(helper(),'utf-8');pins[str(HELPER)]=sha(HELPER)
 for rel in ['ProjectSettings/GraphicsSettings.asset','ProjectSettings/QualitySettings.asset','ProjectSettings/ProjectSettings.asset','Packages/manifest.json','Packages/packages-lock.json']:
  p=PROJECT/rel;pins[str(p)]=sha(p)
 request=dict(id='hivemind.native-american-village.prefab-sm-firepit-01-'+guid,prefab_path=PREFAB,prefab_guid=guid,output=str(ROOT/'NATIVE-RESULT.v1.json'))
 save('REQUEST.v1.json',request);pins[str(ROOT/'REQUEST.v1.json')]=sha(ROOT/'REQUEST.v1.json')
 save('PREPARED.v1.json',dict(sub_lot='L01.OMBRES.02.SOURCE',input_pins=pins,private_before_pins=private,dependency_paths=sorted(seen),expected_images=2,expected_raw_images=2,lod0_diagnostic_only=True,test_receiver_not_delivered=True,original_project_version_qualified=False,source_scripts_executed=False,full_import=False))
 with (ROOT/'controller.log').open('xb') as f:child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=f,stderr=f,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
 save('JOB.v1.json',dict(pid=child.pid,created_utc=datetime.now(timezone.utc).isoformat()));print(json.dumps(dict(pid=child.pid,root=str(ROOT),source_dependencies=len(seen))))
def run():
 state=dict(status='source_shadow_pair_capture_running',full_import=False)
 try:
  p=read(ROOT/'PREPARED.v1.json')
  for file,h in p['input_pins'].items():assert sha(file)==h,file
  env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT),UPM_CACHE_ROOT=str(W/'unity-upm-cache'))
  with (ROOT/'stdout.log').open('xb') as out,(ROOT/'stderr.log').open('xb') as err:
   child=subprocess.Popen([str(UNITY),'-batchmode','-force-d3d11','-quit','-job-worker-count','2','-projectPath',str(PROJECT),'-executeMethod','OpenRPGSourceShadowReferenceV1.Export','--openrpg-manifest',str(ROOT/'REQUEST.v1.json'),'-logFile',str(ROOT/'Unity.log')],cwd=ROOT,env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW);state['unity_pid']=child.pid;save('RUN-RESULT.v1.json',state);state['actual_exit_code']=child.wait();assert child.returncode==0
  n=read(ROOT/'NATIVE-RESULT.v1.json');assert n['source_object_unchanged'] and n['gpu']!='Null' and len(n['images'])==2
  assert [r['source_shadows'] for r in n['images']]==[True,False]
  for r in n['images']:
   assert sha(r['path'])==r['sha256'] and sha(r['raw_path'])==r['raw_sha256'];assert Path(r['raw_path']).stat().st_size==512*512*8
  for file,h in p['input_pins'].items():assert sha(file)==h,file
  state.update(status='source_shadow_pair_captured_measurement_pending',images_captured=2,raw_images_captured=2,source_inputs_unchanged=True,private_changed=[f for f,h in p['private_before_pins'].items() if sha(f)!=h],native_receipt_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'),forge_comparison_qualified=False,editor_visual_verified=False)
 except Exception as e:state.update(status='source_shadow_reference_refusal_retained',error=repr(e))
 state['finished_utc']=datetime.now(timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()
