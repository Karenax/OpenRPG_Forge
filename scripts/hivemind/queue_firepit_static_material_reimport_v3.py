"""Retry the refused static FirePit material after explicit dependency-first imports."""
from pathlib import Path
import datetime,hashlib,json,os,subprocess,sys
from verify_native_material_snapshot import reconcile
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'firepit-static-material-native-reimport-v3'
PROJECT=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
HELPER=PROJECT/'Assets/Editor/FirePitDependencyReimportV3.cs'
CODE=r"""
using UnityEditor;using UnityEngine;using System;using System.IO;using System.Linq;using System.Collections.Generic;
namespace OpenRPGFirePitDependencyReimportV3 {
public static class Capture {
[Serializable]public class Asset {public string requested_path;public string guid;public string[] object_types;public string[] file_ids;}
[Serializable]public class Report {public Asset[] assets;}
public static void Export(){
 string[] paths={"Assets/Hivemind/NativeAmericanVillage/HDRP(Default)/Art/Shaders/SubG_WorldAlignedTexture.shadersubgraph","Assets/Hivemind/NativeAmericanVillage/HDRP(Default)/Art/Shaders/SubG_WorldAlignedTextureNormal.shadersubgraph","Assets/Hivemind/NativeAmericanVillage/HDRP(Default)/Art/Shaders/S_MasterProps.shadergraph","Assets/Hivemind/NativeAmericanVillage/HDRP(Default)/Art/Materials/Instances/M_FirePit_A_01.mat"};
 var rows=new List<Asset>();
 foreach(var path in paths){
  AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
  var objects=AssetDatabase.LoadAllAssetsAtPath(path);var ids=new List<string>();
  foreach(var obj in objects){string guid;long id;if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out guid,out id))ids.Add(id.ToString(System.Globalization.CultureInfo.InvariantCulture));else ids.Add("unavailable");}
  rows.Add(new Asset{requested_path=path,guid=AssetDatabase.AssetPathToGUID(path),object_types=objects.Select(o=>o.GetType().FullName).ToArray(),file_ids=ids.ToArray()});
 }
 string root=Environment.GetEnvironmentVariable("OPENRPG_FIREPIT_REIMPORT_OUTPUT");if(string.IsNullOrEmpty(root))throw new Exception("Explicit output required");
 File.WriteAllText(Path.Combine(root,"DEPENDENCY-RESULT.v1.json"),JsonUtility.ToJson(new Report{assets=rows.ToArray()},true));
 OpenRPGPreparedHDRPV6.OriginalGraphMaterialSnapshotPreparedHDRPV6.Export();
}
}}
"""
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def save(name,v):
 p=ROOT/(name+'.tmp');p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(ROOT/name)
def prepare():
 assert not ROOT.exists() and not HELPER.exists()
 assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True,creationflags=subprocess.CREATE_NO_WINDOW).strip()=='0'
 old=W/'unity-delta-remaining-native-materials-v2';previous=read(old/'RUN-RESULT.v1.json');assert previous['status']=='refused_or_unverified'
 manifest=read(old/'manifest.v1.json');case=next(c for c in manifest['cases'] if c['material_guid']=='abc00000000009294800760372203426')
 contract=next(c for c in read(W/'delta-rocks-firepit-render-contracts-v1/RESULT.v1.json')['rows'] if c['material_guid']==case['material_guid'])
 inventory=W/'hivemind-run/native-american-village/source.v1.json';source=read(inventory);sourcebase=W/'hivemind-run/native-american-village/source'
 rels=[source['guid_paths'][g] for g in ['c73979feb143b394ab7802f32a955d51','467167fecd430ac4c982f83c94f86f75','3dc72df83e8fccf408898970630314e5']]+[case['asset_path']]
 original_pins={};private_pins={}
 for rel in rels:
  for suffix in ['', '.meta']:
   original=sourcebase/(rel+suffix);private=PROJECT/(rel+suffix)
   assert original.exists() and private.exists();original_pins[str(original)]=sha(original);private_pins[str(private)]=sha(private)
 ROOT.mkdir();HELPER.write_text(CODE,'utf-8')
 helper=PROJECT/'Assets/Editor/OriginalGraphMaterialSnapshotPreparedHDRPV6.cs'
 files=[Path(__file__),HELPER,helper,inventory,old/'RUN-RESULT.v1.json',old/'manifest.v1.json',Path(sys.modules[reconcile.__module__].__file__)]
 files += [PROJECT/r for r in ['ProjectSettings/GraphicsSettings.asset','ProjectSettings/QualitySettings.asset','ProjectSettings/ProjectSettings.asset','Packages/manifest.json']]
 pins={str(p):sha(p) for p in files};pins.update(original_pins)
 save('manifest.v1.json',dict(output_path=str(ROOT/'NATIVE-RESULT.v1.json'),cases=[case]));pins[str(ROOT/'manifest.v1.json')]=sha(ROOT/'manifest.v1.json')
 for i,(path,h) in enumerate(private_pins.items()):(ROOT/('private-before-'+str(i)+'.bin')).write_bytes(Path(path).read_bytes())
 save('PREPARED.v1.json',dict(input_pins=pins,private_before_pins=private_pins,source_scripts_executed=False,original_files_not_modified=True,full_import=False))
 with (ROOT/'controller.log').open('xb') as log:
  child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdin=subprocess.DEVNULL,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
 save('JOB.v1.json',dict(controller_pid=child.pid));print(json.dumps(dict(controller_pid=child.pid,materials=1)))
def run():
 state=dict(status='dependency_first_native_import_and_material_capture',full_import=False)
 try:
  p=read(ROOT/'PREPARED.v1.json')
  for path,h in p['input_pins'].items():assert sha(Path(path))==h,path
  env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT),OPENRPG_FIREPIT_REIMPORT_OUTPUT=str(ROOT),UPM_CACHE_ROOT=str(W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/upm-cache'))
  with (ROOT/'unity.stdout.log').open('xb') as out,(ROOT/'unity.stderr.log').open('xb') as err:
   child=subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe','-batchmode','-force-d3d11','-quit','-projectPath',str(PROJECT),'-executeMethod','OpenRPGFirePitDependencyReimportV3.Capture.Export','--openrpg-manifest',str(ROOT/'manifest.v1.json'),'-logFile',str(ROOT/'unity.log')],cwd=ROOT,env=env,stdin=subprocess.DEVNULL,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
   state['unity_pid']=child.pid;save('RUN-RESULT.v1.json',state);state['actual_unity_exit_code']=child.wait()
  assert state['actual_unity_exit_code']==0,'Native capture still refused; inspect dependency observations'
  manifest=read(ROOT/'manifest.v1.json');native=read(ROOT/'NATIVE-RESULT.v1.json');verified=reconcile(manifest,native);assert len(verified)==1
  for path,h in p['input_pins'].items():assert sha(Path(path))==h,path
  changes=[]
  for i,(path,h) in enumerate(p['private_before_pins'].items()):
   after=sha(Path(path));(ROOT/('private-after-'+str(i)+'.bin')).write_bytes(Path(path).read_bytes())
   if after!=h:changes.append(dict(path=path,before_sha256=h,after_sha256=after))
  save('REOPEN.v1.json',dict(verified_materials=verified,dependency_result_sha256=sha(ROOT/'DEPENDENCY-RESULT.v1.json'),native_result_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'),original_sources_and_configuration_unchanged=True,private_changes=changes,full_import=False))
  state.update(status='static_firepit_original_shader_and_native_passes_reopened',native_materials_verified=1,native_result_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'),private_changes=changes)
 except Exception as e:state.update(status='refused_or_unverified',error=repr(e))
 state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
