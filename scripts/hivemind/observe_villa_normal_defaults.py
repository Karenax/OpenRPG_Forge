"""Observe material/default APIs in an isolated project; this is not render proof."""
import datetime
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

from assemble_native_villa_delivery import read, require, sha
from recover_native_scene_check_queue import wait_exact

WORK = Path('F:/OpenRPG_ImportWork')
ROOT = WORK/'unity-villa-normal-default-api-observation-v2'
SOURCE = WORK/'hivemind-run/villa-forge/source'
ART = SOURCE/'Assets/Hivemind/VillaForge/HDRP(Default)/Art'
TEMPLATE = WORK/'unity-dungeon-original-shadergraph-compiler-v1/project'
UNITY = Path('F:/Unity/6000.5.8f1/Editor/Unity.exe')
PS = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'

CS = r'''
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
public static class VillaNormalDefaultApiObservation {
 [Serializable] public class TextureRow {
  public string property; public string owner; public bool is_null;
  public string asset_path; public string guid; public string file_id;
  public string object_type; public int width; public int height;
 }
 [Serializable] public class Result {
  public string unity_version; public string material_guid; public string shader_guid;
  public string shader_file_id; public string normal_default_name;
  public TextureRow[] rows; public bool render_binding_proven=false; public bool full_import=false;
 }
 static TextureRow Observe(Material material,string property,string owner) {
  if(!material.HasProperty(property)) throw new Exception("Expected property absent: "+property);
  var texture=material.GetTexture(property);
  var row=new TextureRow{property=property,owner=owner,is_null=texture==null};
  if(texture!=null) {
   row.asset_path=AssetDatabase.GetAssetPath(texture);row.object_type=texture.GetType().FullName;
   row.width=texture.width;row.height=texture.height;
   if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(texture,out string guid,out long fileId)) {
    row.guid=guid;row.file_id=fileId.ToString(System.Globalization.CultureInfo.InvariantCulture);
   }
  }
  return row;
 }
 public static void Run() {
  try {
   var args=Environment.GetCommandLineArgs();string output=null;
   for(int i=0;i<args.Length-1;i++) if(args[i]=="-normalDefaultOutput") output=args[i+1];
   if(output==null) throw new Exception("Output required");
   var path="Assets/Hivemind/VillaForge/HDRP(Default)/Art/Materials/M_parallax_master2.mat";
   var material=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(material==null || material.shader==null) throw new Exception("Source material/shader absent");
   var guid=AssetDatabase.AssetPathToGUID(path);
   if(guid!="abc00000000015195609946836415000") throw new Exception("Material GUID differs");
   if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material.shader,out string shaderGuid,out long shaderId)) throw new Exception("Shader identity absent");
   if(shaderGuid!="e6f99812ca2d22a4c98d5e88e4d78f2a" || shaderId.ToString()!="-6465566751694194690") throw new Exception("Source shader identity differs");
   var countMethod=typeof(Shader).GetMethod("GetPropertyCount",Type.EmptyTypes);
   var nameMethod=typeof(Shader).GetMethod("GetPropertyName",new Type[]{typeof(int)});
   var defaultMethod=typeof(Shader).GetMethod("GetPropertyTextureDefaultName",new Type[]{typeof(int)});
   if(countMethod==null || nameMethod==null || defaultMethod==null) throw new Exception("Required shader default API absent");
   string normalDefault=null;int count=(int)countMethod.Invoke(material.shader,null);
   for(int i=0;i<count;i++) if((string)nameMethod.Invoke(material.shader,new object[]{i})=="_NormalMap") normalDefault=(string)defaultMethod.Invoke(material.shader,new object[]{i});
   if(normalDefault==null) throw new Exception("Normal default property absent");
   var defaults=new Material(material.shader);
   try {
    var result=new Result{unity_version=Application.unityVersion,material_guid=guid,shader_guid=shaderGuid,
     shader_file_id=shaderId.ToString(System.Globalization.CultureInfo.InvariantCulture),normal_default_name=normalDefault,
     rows=new TextureRow[]{Observe(material,"_NormalMap","source_material"),Observe(defaults,"_NormalMap","new_material_shader_defaults"),
      Observe(material,"_BaseMap","source_material_control"),Observe(defaults,"_BaseMap","shader_defaults_control")}};
    File.WriteAllText(output,JsonUtility.ToJson(result,true));
   } finally {UnityEngine.Object.DestroyImmediate(defaults);}
   EditorApplication.Exit(0);
  } catch(Exception error) {Debug.LogException(error);EditorApplication.Exit(1);}
 }
}
'''


def save(name,data):
    path=ROOT/name; temporary=path.with_suffix('.json.tmp')
    temporary.write_text(json.dumps(data,indent=2),'utf-8');temporary.replace(path)


def prepare():
    require(not ROOT.exists(),'Existing native observation preserved')
    receipt=WORK/'villa-builtin-normal-source-reconciliation-v1/RESULT.v1.json'
    pins={**read(receipt)['source_pins'],str(receipt):sha(receipt),str(Path(__file__).resolve()):sha(__file__),str(UNITY):sha(UNITY)}
    for path,expected in pins.items():require(sha(path)==expected,'Source changed before preparation')
    ROOT.mkdir();project=ROOT/'project';(project/'Assets/Editor').mkdir(parents=True)
    editor=project/'Assets/Editor/VillaNormalDefaultApiObservation.cs';editor.write_text(CS,'utf-8');pins[str(editor)]=sha(editor)
    for folder in ['ProjectSettings','Packages']:
        for source in (TEMPLATE/folder).glob('*'):
            if source.is_file():
                target=project/folder/source.name;target.parent.mkdir(parents=True,exist_ok=True)
                shutil.copyfile(source,target);pins[str(source)]=sha(source);pins[str(target)]=sha(target)
    require('m_ActiveColorSpace: 1' in (project/'ProjectSettings/ProjectSettings.asset').read_text('utf-8-sig'),'Linear template required')
    files=list((ART/'Shaders').rglob('*'))+list((ART/'Textures').rglob('*'))
    material=ART/'Materials/M_parallax_master2.mat';files.extend([material,Path(str(material)+'.meta')])
    copies=[]
    for source in files:
        if not source.is_file():continue
        require(source.suffix.lower() not in {'.cs','.dll','.exe','.bat','.cmd','.ps1','.js'},'Purchased executable source excluded')
        expected=sha(source);target=project/source.relative_to(SOURCE);target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(source,target);require(sha(target)==expected,'Private copy differs');pins[str(source)]=expected
        copies.append({'source':str(source),'copy':str(target),'sha256':expected})
    meta_texts=[Path(c['source']).read_text('utf-8-sig') for c in copies if c['source'].endswith('.meta')]
    require(any('guid: abc00000000006596162916920159822' in text for text in meta_texts),
            'Source base texture control must be physically present')
    default_guid_present=any('guid: abc00000000011349718092515854789' in text for text in meta_texts)
    save('PREPARED.v1.json',{'project':str(project),'source_pins':pins,'private_copies':copies,
        'wait_jobs':[[45700,639269316542014449],[17752,639269292678355524],[34288,639269228636277089],
                     [43320,639269220609065830],[45688,639269203327956258],[43256,639269118471258673]],
        'declared_default_normal_guid_present_in_copied_source_meta':default_guid_present,
        'absent_authored_reference_must_not_be_replaced_or_promoted':True,
        'preserved_preparation_refusal':str(WORK/'unity-villa-normal-default-api-observation-v1/RUN-RESULT.v1.json'),
        'observation_scope':'material_api_and_new_material_shader_defaults_only','render_binding_proven':False,'full_import':False})
    with (ROOT/'controller.log').open('xb') as log:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdin=subprocess.DEVNULL,
             stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',{'pid':child.pid,'started_utc':datetime.datetime.now(datetime.timezone.utc).isoformat()})
    print(json.dumps({'pid':child.pid,'private_files_copied':len(copies),'native_observations':0}))


def run():
    state={'status':'waiting_for_existing_unity_queue','native_observations':0,'render_binding_proven':False,'full_import':False}
    save('RUN-RESULT.v1.json',state)
    try:
        prep=read(ROOT/'PREPARED.v1.json')
        for pid,birth in prep['wait_jobs']:wait_exact(pid,birth)
        while True:
            query=subprocess.run([PS,'-NoProfile','-Command','Get-Process -Name Unity -ErrorAction SilentlyContinue | ForEach-Object {$_.Id}; exit 0'],capture_output=True,text=True,creationflags=subprocess.CREATE_NO_WINDOW)
            require(query.returncode==0,'Unity process observation failed')
            if not query.stdout.strip():break
            time.sleep(30)
        for path,expected in prep['source_pins'].items():require(sha(path)==expected,'Pinned source changed')
        state['status']='observing_native_material_default_apis';save('RUN-RESULT.v1.json',state)
        output=ROOT/'NATIVE-RESULT.v1.json'
        with (ROOT/'unity.stdout.log').open('xb') as out,(ROOT/'unity.stderr.log').open('xb') as err:
            child=subprocess.Popen([str(UNITY),'-batchmode','-nographics','-quit','-projectPath',prep['project'],
                '-executeMethod','VillaNormalDefaultApiObservation.Run','-normalDefaultOutput',str(output),
                '-logFile',str(ROOT/'unity.log')],stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
            save('UNITY-JOB.v1.json',{'pid':child.pid});require(child.wait()==0,'Native API observation failed; retained logs')
        native=read(output);require(len(native['rows'])==4 and native['render_binding_proven']==False,'Expected API-only observations required')
        require(native['rows'][2]['is_null']==False and native['rows'][2]['guid']=='abc00000000006596162916920159822',
                'Native source base texture control differs')
        for path,expected in prep['source_pins'].items():require(sha(path)==expected,'Source changed during native observation')
        state.update(status='native_material_and_shader_default_api_observations_captured_render_binding_unproven',
                     native_observations=4,native_result_sha256=sha(output),original_source_pins_unchanged=True)
    except Exception as error:state.update(status='refused_or_unverified',error=str(error))
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
    require(state['status']!='refused_or_unverified',state.get('error','Observation refused'))


if __name__=='__main__':{'prepare':prepare,'run':run}[sys.argv[1]]()
