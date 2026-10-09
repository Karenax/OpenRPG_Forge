"""Export the actual source ShaderGraph generated code alongside unchanged captures."""
from pathlib import Path
import runpy
import sys

BASE = Path(__file__).with_name('queue_firepit_source_camera_matrices_v1.py')
loaded = runpy.run_path(str(BASE),run_name='source_generated_shader_probe')
g = loaded['g']
original_helper = g['helper']
popen = loaded['popen']

def helper():
    text = original_helper().replace('SourceCameraMatricesV1','SourceGeneratedShaderV1')
    needle = '   if(sourceBefore!=EditorJsonUtility.ToJson(source,true))'
    assert text.count(needle)==1
    export = r'''
   var graphType=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.ShaderGraph.ShaderGraphImporter")).First(t=>t!=null);
   var graphMethod=graphType.GetMethods(System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Single(m=>m.Name=="GetShaderText"&&m.GetParameters().Length==2&&m.GetParameters()[0].ParameterType==typeof(string));
   object[] shaderArgs={"Assets/Hivemind/NativeAmericanVillage/HDRP(Default)/Art/Shaders/S_MasterProps.shadergraph",null};
   var shaderText=(string)graphMethod.Invoke(null,shaderArgs);
   if(string.IsNullOrEmpty(shaderText)||shaderText.Contains("Hidden/GraphErrorShader2"))throw new Exception("Actual generated source unavailable");
   File.WriteAllText(Path.Combine(Path.GetDirectoryName(request.output),"source-generated.shader"),shaderText,new System.Text.UTF8Encoding(false));
'''
    return text.replace(needle,export+'\n'+needle)

def corrected_popen(args,*a,**kw):
    if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
        args=list(args)
        i=args.index('-executeMethod')+1
        assert args[i]=='OpenRPGSourceShadowReferenceV8.Export'
        args[i]='OpenRPGSourceGeneratedShaderV1.Export'
    return popen(args,*a,**kw)

g['subprocess'].Popen=corrected_popen
g.update(ROOT=g['W']/'firepit-generated-shader-v1',HELPER=g['PROJECT']/'Assets/Editor/SourceGeneratedShaderV1.cs',helper=helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':
    if sys.argv[-1]=='prepare':
        g['prepare']()
    else:
        g['run']()
        d=g['read'](g['ROOT']/'RUN-RESULT.v1.json')
        p=g['ROOT']/'source-generated.shader'
        if d.get('actual_exit_code')==0 and p.exists():
            d.update(status='actual_original_masterprops_shadergraph_code_exported',generated_shader_sha256=g['sha'](p),generated_shader_bytes=p.stat().st_size)
        else:
            d.update(status='source_generated_shader_export_refused')
        g['save']('RUN-RESULT.v1.json',d)
