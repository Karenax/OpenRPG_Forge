"""Measure native Color property transfer on disposable clones with exact declarations."""
from pathlib import Path
from datetime import datetime,timezone
import ast,hashlib,json,subprocess,sys
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork');O=W/'gladiator-color-property-gpu-v428';PROJECT=W/'gladiator-native-geometry-v380/project';PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(v):
    v.update(updated_utc=datetime.now(timezone.utc).isoformat(),converted=False,full_import=False)
    p=O/'RUN-RESULT.tmp';p.write_text(json.dumps(v,indent=2),'utf-8');p.replace(O/'RUN-RESULT.v1.json')
def prepare():
    assert not O.exists()
    assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
    proof=W/'gladiator-all-texture-gpu-delivery-v427/RESULT.v1.json'
    for p,h in read(proof)['input_pins'].items():assert sha(p)==h
    native=W/'gladiator-native-shader-defaults-v410/NATIVE-RESULT.v1.json';materials=read(native)['materials'];assert len(materials)==6
    generated=W/'gladiator-native-shader-defaults-v410/source-generated.shader';text=generated.read_text('utf-8')
    assert '[HDR]_Tint("Tint", Color)' in text and 'IsGammaSpace() ? LinearToSRGB(_Tint) : _Tint' in text
    start=text.index('Properties\n{');opening=text.index('{',start);depth=1;end=None
    for i in range(opening+1,len(text)):
        depth+=(text[i]=='{')-(text[i]=='}')
        if depth==0:end=i+1;break
    assert end;properties=text[start:end]
    O.mkdir();(O/'temp').mkdir()
    shader=PROJECT/'Assets/Editor/GladiatorColorTransferV428.shader';assert not shader.exists()
    shader.write_text('Shader "OpenRPGImportProbe/GladiatorColorTransferV428" {\n'+properties+r'''
SubShader {Pass {Cull Off ZWrite Off ZTest Always Blend Off
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 4.5
#include "UnityCG.cginc"
float4 _Tint;
struct Varying {float4 position:SV_POSITION;};
Varying vert(appdata_img input){Varying o;o.position=UnityObjectToClipPos(input.vertex);return o;}
float4 frag(Varying input):SV_Target {return _Tint;}
ENDCG
}}}
''','utf-8')
    helper=PROJECT/'Assets/Editor/GladiatorColorTransferV428.cs';assert not helper.exists()
    helper.write_text(r'''using System;using System.IO;using System.Collections.Generic;using UnityEditor;using UnityEngine;
public static class OpenRPGGladiatorColorTransferV428 {
 [Serializable] public class Request {public string output;public string[] material_guids;}
 [Serializable] public class Row {public string guid,name,source_shader_guid,source_shader_file_id,source_serialized_before,source_serialized_after;public Color source_get_color,clone_get_color,native_gpu_color,negative_gpu_color;public bool source_material_unchanged,negative_control_observed;}
 [Serializable] public class Report {public string unity_version,graphics_api,color_space;public Row[] materials;public bool source_materials_unchanged,original_shader_render_parity_qualified=false,full_import=false;}
 static Color Pixel(Material m){var rt=new RenderTexture(1,1,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);rt.Create();var prior=RenderTexture.active;Texture2D read=null;try {Graphics.Blit(null,rt,m,0);RenderTexture.active=rt;read=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);read.ReadPixels(new Rect(0,0,1,1),0,0,false);read.Apply(false,false);return read.GetPixel(0,0);}finally {RenderTexture.active=prior;if(read!=null)UnityEngine.Object.DestroyImmediate(read);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}}
 public static void Export(){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--openrpg-manifest");if(i<0)throw new Exception("Request missing");var r=JsonUtility.FromJson<Request>(File.ReadAllText(args[i+1]));if(File.Exists(r.output)||r.material_guids.Length!=6)throw new Exception("Preserve receipts/six exact cases required");if(PlayerSettings.colorSpace!=ColorSpace.Linear)throw new Exception("Linear source project required");var shader=Shader.Find("OpenRPGImportProbe/GladiatorColorTransferV428");if(shader==null||!shader.isSupported)throw new Exception("Owned probe shader unavailable");var rows=new List<Row>();foreach(var guid in r.material_guids){string path=AssetDatabase.GUIDToAssetPath(guid);if(!path.StartsWith("Assets/Hivemind/GladitorArena/HDRP(Default)/",StringComparison.Ordinal))throw new Exception("Source path differs");var source=AssetDatabase.LoadAssetAtPath<Material>(path);if(source==null)throw new Exception("Source material missing");string g;long id;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out g,out id)||g!=guid||id!=2100000)throw new Exception("Source identity differs");if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source.shader,out g,out id)||g!="5a14110ac97e4694887098700a513fbe"||id!=-6465566751694194690L)throw new Exception("Source graph identity differs");var before=EditorJsonUtility.ToJson(source,true);var row=new Row{guid=guid,name=source.name,source_shader_guid=g,source_shader_file_id=id.ToString(),source_serialized_before=before,source_get_color=source.GetColor("_Tint")};var clone=new Material(source);try {clone.shader=shader;row.clone_get_color=clone.GetColor("_Tint");row.native_gpu_color=Pixel(clone);var changed=row.clone_get_color;changed.r+=0.125f;clone.SetColor("_Tint",changed);row.negative_gpu_color=Pixel(clone);row.negative_control_observed=row.negative_gpu_color.r!=row.native_gpu_color.r;if(!row.negative_control_observed)throw new Exception("Negative property control not observed");}finally{UnityEngine.Object.DestroyImmediate(clone);}row.source_serialized_after=EditorJsonUtility.ToJson(source,true);row.source_material_unchanged=before==row.source_serialized_after;if(!row.source_material_unchanged)throw new Exception("Original material changed");rows.Add(row);}File.WriteAllText(r.output,JsonUtility.ToJson(new Report{unity_version=Application.unityVersion,graphics_api=SystemInfo.graphicsDeviceType.ToString(),color_space=PlayerSettings.colorSpace.ToString(),materials=rows.ToArray(),source_materials_unchanged=true},true));}
}
''','utf-8')
    request=O/'REQUEST.v1.json';request.write_text(json.dumps(dict(output=str(O/'NATIVE-RESULT.v1.json'),material_guids=[m['guid'] for m in materials]),indent=2),'utf-8')
    pins={str(p):sha(p) for p in [Path(__file__),proof,native,generated,shader,helper,request,S/'run_gladiator_remaining_geometry_v400.py',S/'queue_delta_firepit_particle_behavior_v4.py']};copies=[]
    for row in read(W/'gladiator-lot-dependency-closure-v394/RESULT.v1.json')['files']:
        source=Path(row['source']);private=PROJECT/row['relative_project_path'];assert sha(source)==row['sha256'];pins[str(source)]=row['sha256'];copies.append(dict(copy=str(private),private_before_sha256=sha(private)))
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,private_copies=copies,property_declarations_copied_verbatim=True,measurement_only=True,purchased_scripts_copied_or_executed=False),indent=2),'utf-8');save(dict(status='six_native_color_transfer_cases_prepared'))
    with (O/'controller.log').open('xb') as log:p=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (O/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=p.pid,one_shot=True)),'utf-8');print(json.dumps(dict(controller_pid=p.pid)))
def run():
    recipe=S/'run_gladiator_remaining_geometry_v400.py';tree=ast.parse(recipe.read_text('utf-8'));nodes=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name=='run'];code=ast.unparse(ast.Module(body=nodes,type_ignores=[]))
    code=code.replace('OpenRPGGladiatorLotGeometryV400.Export','OpenRPGGladiatorColorTransferV428.Export')
    needle="assert len(native['cases']) == 5 and sum((len(c['meshes']) for c in native['cases'])) == 13 and all((c['source_components_unchanged'] for c in native['cases']))";assert code.count(needle)==1
    code=code.replace(needle,"assert len(native['materials']) == 6 and native['source_materials_unchanged'] and all(m['negative_control_observed'] for m in native['materials'])")
    code=code.replace('remaining_thirteen_meshes_native_capture_running','six_color_property_transfers_running').replace('remaining_thirteen_meshes_and_five_root_material_snapshots_captured_review_pending','six_color_property_transfers_captured_review_pending').replace('new_meshes=13','materials=6')
    ns=dict(S=S,W=W,O=O,PROJECT=PROJECT,PS=PS,read=read,sha=sha,save=save);exec(compile(code,str(recipe)+'#native-color-transfer','exec'),ns);ns['run']()
if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
