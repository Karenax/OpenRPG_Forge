"""Capture actual generated ShaderLab and native pass-label queries in the isolated project."""
from pathlib import Path
import datetime,hashlib,json,os,subprocess,sys
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'detail-native-generated-passes-v1'
PROJECT=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
HELPER=PROJECT/'Assets/Editor/DetailGeneratedPassesV1.cs'
CODE=r"""
using System;using System.IO;using System.Linq;using System.Reflection;using UnityEditor;using UnityEngine;
namespace OpenRPGDetailGeneratedPassesV1 {
public static class Capture {
[Serializable]public class Query {public string label;public bool enabled;}
[Serializable]public class Result {public string graph_path;public string material_path;public string shader_name;public Query[] queries;public bool source_scripts_executed=false;}
public static void Export(){
try {
string root=Environment.GetEnvironmentVariable("OPENRPG_DETAIL_PASS_OUTPUT");if(string.IsNullOrEmpty(root))throw new Exception("Explicit output required");
string materialPath="Assets/Hivemind/NativeAmericanVillage/HDRP(Default)/Art/Foliage/Objects/Foliage/EuropeanBeech/M_EuropeanBeech_Detail_01.mat";
string graphPath="Assets/Hivemind/NativeAmericanVillage/HDRP(Default)/Art/Shaders/S_Foliage.shadergraph";
var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(material==null||material.shader==null)throw new Exception("Source material required");
var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.ShaderGraph.ShaderGraphImporter")).First(t=>t!=null);
var method=type.GetMethods(BindingFlags.Static|BindingFlags.NonPublic).Single(m=>m.Name=="GetShaderText"&&m.GetParameters().Length==2&&m.GetParameters()[0].ParameterType==typeof(string));
object[] args={graphPath,null};var source=(string)method.Invoke(null,args);
if(string.IsNullOrEmpty(source)||source.Contains("Hidden/GraphErrorShader2"))throw new Exception("Generated source unavailable");
File.WriteAllText(Path.Combine(root,"source-generated.shader"),source,new System.Text.UTF8Encoding(false));
string[] labels={"MOTIONVECTORS","MotionVectors","Forward","DepthOnly","ShadowCaster","TransparentDepthPrepass","TransparentDepthPostpass","TransparentBackface","RayTracingPrepass"};
var result=new Result{graph_path=graphPath,material_path=materialPath,shader_name=material.shader.name,queries=labels.Select(l=>new Query{label=l,enabled=material.GetShaderPassEnabled(l)}).ToArray()};
File.WriteAllText(Path.Combine(root,"NATIVE-RESULT.v1.json"),JsonUtility.ToJson(result,true));
}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);throw;}
}
}}
"""
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text('utf-8'))
def save(name,v):
 p=ROOT/(name+'.tmp');p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(ROOT/name)
def check(pins):
 for p,h in pins.items():assert sha(Path(p))==h,p
def prepare():
 assert not ROOT.exists() and not HELPER.exists()
 count=subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True,creationflags=subprocess.CREATE_NO_WINDOW).strip();assert count=='0'
 ROOT.mkdir();HELPER.write_text(CODE,'utf-8')
 original=read(W/'detail-original-render-contracts-v1/RESULT.v1.json')['rows'][0]
 files=[HELPER,Path(__file__),W/'detail-autonomous-material-payload-v1/INDEPENDENT-REOPEN.v1.json',
  PROJECT/'Library/PackageCache/com.unity.shadergraph@8d7376ec43ee/Editor/Importers/ShaderGraphImporter.cs',
  PROJECT/'Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2/Editor/Material/ShaderGraph/HDTarget.cs']
 files += [Path(original['original_material']),Path(original['original_graph'])]
 files += [PROJECT/x for x in ['ProjectSettings/GraphicsSettings.asset','ProjectSettings/QualitySettings.asset','ProjectSettings/ProjectSettings.asset','Packages/manifest.json']]
 for role in ['original_material','original_graph']:
  rel=str(Path(original[role]).relative_to(W/'hivemind-run/native-american-village/source'))
  files.append(PROJECT/rel)
 pins={str(p):sha(p) for p in files};save('PREPARED.v1.json',dict(input_pins=pins,helper=str(HELPER),source_scripts_executed=False,full_import=False))
 with (ROOT/'controller.log').open('xb') as log:
  child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdin=subprocess.DEVNULL,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
 save('JOB.v1.json',dict(controller_pid=child.pid));print(json.dumps(dict(controller_pid=child.pid,output=str(ROOT))))
def run():
 state=dict(status='preparing_actual_generated_pass_capture',full_import=False);save('RUN-RESULT.v1.json',state)
 try:
  prepared=read(ROOT/'PREPARED.v1.json');check(prepared['input_pins'])
  env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT),OPENRPG_DETAIL_PASS_OUTPUT=str(ROOT),UPM_CACHE_ROOT=str(W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/upm-cache'))
  with (ROOT/'unity.stdout.log').open('xb') as out,(ROOT/'unity.stderr.log').open('xb') as err:
   child=subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe','-batchmode','-force-d3d11','-quit','-projectPath',str(PROJECT),'-executeMethod','OpenRPGDetailGeneratedPassesV1.Capture.Export','-logFile',str(ROOT/'unity.log')],cwd=ROOT,env=env,stdin=subprocess.DEVNULL,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
   state.update(status='capturing_actual_generated_shaderlab',unity_pid=child.pid);save('RUN-RESULT.v1.json',state);state['actual_unity_exit_code']=child.wait()
  assert state['actual_unity_exit_code']==0
  check(prepared['input_pins']);assert (ROOT/'source-generated.shader').stat().st_size>10000
  state.update(status='actual_shaderlab_and_native_pass_queries_captured',generated_shader_sha256=sha(ROOT/'source-generated.shader'),native_result_sha256=sha(ROOT/'NATIVE-RESULT.v1.json'),sources_and_configuration_unchanged=True)
 except Exception as e:state.update(status='refused_or_unverified',error=str(e))
 state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
