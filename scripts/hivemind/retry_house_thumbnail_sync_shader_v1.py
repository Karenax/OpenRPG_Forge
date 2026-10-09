"""Capture original compiled shader passes and texture identities in the warm project."""
from pathlib import Path
from datetime import datetime,timezone
import json,shutil,subprocess,sys,types
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'unity-library-source-thumbnail-house-bench-v6';OLD=W/'unity-library-source-thumbnail-house-bench-v5';PROJECT=W/'unity-library-source-thumbnail-house-bench-v2/project'
LIB=Path(__file__).with_name('queue_source_library_thumbnail_v1.py');capture=types.ModuleType('synchronous_original_shader_capture');capture.__file__=str(LIB)
code=LIB.read_text('utf-8');assert code.count("'OpenRPGSourceLibraryThumbnailV1.Export'")==1
exec(compile(code.replace("'OpenRPGSourceLibraryThumbnailV1.Export'","'OpenRPGSourceLibraryThumbnailV4.Export'"),str(LIB),'exec'),capture.__dict__)
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
COMPILE=r'''foreach(var material in r.sharedMaterials.Distinct()) {
    foreach(var property in material.GetTexturePropertyNames()) {
     var texture=material.GetTexture(property);if(texture==null)continue;
     textureInfos.Add(new TextureBinding{material=Identity(material),property=property,identity=Identity(texture),width=texture.width,height=texture.height});
    }
    for(int pass=0;pass<material.passCount;pass++) {
     string passName=material.GetPassName(pass);
     if(!material.GetShaderPassEnabled(passName)||passName.IndexOf("RayTracing",StringComparison.OrdinalIgnoreCase)>=0)continue;
     ShaderUtil.CompilePass(material,pass,true);
     if(!ShaderUtil.IsPassCompiled(material,pass)||ShaderUtil.ShaderHasError(material.shader))throw new Exception("Original source pass did not compile: "+passName);
     passInfos.Add(new CompiledPass{material=Identity(material),index=pass,name=passName,compiled=true});
    }
   }
   '''
def no_unity():assert subprocess.check_output([PS,'-NoProfile','-Command',"@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count"],text=True).strip()=='0','Another Unity is live'
def prepare():
    assert not ROOT.exists();no_unity();assert capture.read(OLD/'RUN-RESULT.v1.json')['source_inputs_unchanged'];assert capture.read(OLD/'VISUAL-REVIEW.v1.json')['status']=='source_thumbnail_candidates_rejected_by_agent_visual_review';ROOT.mkdir()
    source=Path(__file__).parent/'candidates/SourceLibraryThumbnailV3.cs';helper=source.with_name('SourceLibraryThumbnailV4.cs');assert not helper.exists();text=source.read_text('utf-8')
    assert text.count('var infos=source.GetComponentsInChildren<Renderer>(true).Select(r=>{')==1 and text.count('return new RendererInfo{')==1
    text=text.replace('OpenRPGSourceLibraryThumbnailV3','OpenRPGSourceLibraryThumbnailV4')
    text=text.replace(' [Serializable] public class RendererInfo',' [Serializable] public class TextureBinding { public string material,property,identity; public int width,height; }\n [Serializable] public class CompiledPass { public string material,name; public int index; public bool compiled; }\n [Serializable] public class RendererInfo')
    text=text.replace('public RendererInfo[] renderers;','public bool synchronous_source_passes=true; public TextureBinding[] textures; public CompiledPass[] compiled_passes; public RendererInfo[] renderers;')
    text=text.replace('var infos=source.GetComponentsInChildren<Renderer>(true).Select(r=>{','bool priorAsync=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;var textureInfos=new System.Collections.Generic.List<TextureBinding>();var passInfos=new System.Collections.Generic.List<CompiledPass>();\n  var infos=source.GetComponentsInChildren<Renderer>(true).Select(r=>{')
    text=text.replace('return new RendererInfo{',COMPILE+'return new RendererInfo{')
    text=text.replace('renderers=infos,images=rows.ToArray()','textures=textureInfos.ToArray(),compiled_passes=passInfos.ToArray(),renderers=infos,images=rows.ToArray()')
    assert text.count('}finally {if(target!=null)')==1
    text=text.replace('}finally {if(target!=null)','}finally {ShaderUtil.allowAsyncCompilation=priorAsync;if(target!=null)');helper.write_text(text,'utf-8')
    dst=PROJECT/'Assets/Editor/SourceLibraryThumbnailV4.cs';assert not dst.exists();shutil.copyfile(helper,dst)
    prep=capture.read(OLD/'PREPARED.v1.json');prep['input_pins'].update({str(p):capture.sha(p) for p in [Path(__file__).resolve(),LIB,source,helper,OLD/'VISUAL-REVIEW.v1.json',OLD/'NATIVE-RESULT.v1.json']})
    prep.update(reused_project=str(PROJECT),source_shader_substituted=False,synchronous_original_pass_compilation=True,texture_identities_captured=True,original_project_version_qualification=False)
    (ROOT/'PREPARED.v1.json').write_text(json.dumps(prep,indent=2),'utf-8');request=capture.read(OLD/'REQUEST.v1.json');request['output']=str(ROOT/'NATIVE-RESULT.v1.json');(ROOT/'REQUEST.v1.json').write_text(json.dumps(request,indent=2),'utf-8')
    with (ROOT/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (ROOT/'JOB.v1.json').write_text(json.dumps(dict(pid=proc.pid,created_utc=datetime.now(timezone.utc).isoformat()),indent=2),'utf-8');print(json.dumps(dict(pid=proc.pid,synchronous_original_passes=True)))
def run():
    capture.ROOT=ROOT;capture.PROJECT=PROJECT
    try:no_unity()
    except Exception as exc:
        capture.save(dict(status='source_thumbnail_capture_refusal_retained',error=repr(exc),unity_launched=False,conversion_qualified=False,images_installed_in_vault=False));return
    capture.run()
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else run()
