"""Measure implicit mip selection on original geometry using a private palette."""
from pathlib import Path
import runpy
import sys
import json
import hashlib
import re

BASE = Path(__file__).with_name('queue_firepit_source_camera_matrices_v1.py')
loaded = runpy.run_path(str(BASE),run_name='source_mip_palette_probe')
g = loaded['g']
original_helper = g['helper']
popen = loaded['popen']
SHADER = g['PROJECT']/'Assets/Editor/OpenRPGPrivateMipPaletteV1.shader'
EXPORT = g['W']/'firepit-generated-shader-v2'
ROOT = g['W']/'firepit-source-mip-palette-v1'

def build_shader():
    run = g['read'](EXPORT/'RUN-RESULT.v1.json')
    path = EXPORT/'source-generated.shader'
    assert run['actual_exit_code']==0 and g['sha'](path)==run['generated_shader_sha256']
    source = path.read_text('utf-8')
    assert source.startswith('Shader "Shader Graphs/S_MasterProps"')
    source = source.replace('Shader "Shader Graphs/S_MasterProps"','Shader "OpenRPG/PrivateMipPaletteV1"',1)
    source = source.replace('Properties\n{','Properties\n{\n_OpenRPGProbeMode("Private diagnostic mode", Float) = 0',1)
    source = source.replace('CBUFFER_START(UnityPerMaterial)','CBUFFER_START(UnityPerMaterial)\nfloat _OpenRPGProbeMode;')
    pattern = r'surface\.BaseColor = [^;]+;'
    replacements = len(re.findall(pattern,source))
    assert replacements>4
    replacement = 'surface.BaseColor = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,IN.uv0.xy*_Tiling.xy).rgb; if(_OpenRPGProbeMode>0.5)surface.BaseColor=(_BaseMap.CalculateLevelOfDetail(sampler_BaseMap,IN.uv0.xy*_Tiling.xy)/16.0).xxx;'
    return re.sub(pattern,replacement,source),replacements

def helper():
    text = original_helper().replace('SourceCameraMatricesV1','SourceMipPaletteV1')
    needle = '   if(sourceBefore!=EditorJsonUtility.ToJson(source,true))'
    assert text.count(needle)==1
    probe = r'''
   var probeShader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/OpenRPGPrivateMipPaletteV1.shader");
   if(probeShader==null||ShaderUtil.ShaderHasError(probeShader))throw new Exception("Private mip shader unavailable");
   var palette=new Texture2D(2048,2048,TextureFormat.RGBA32,true,true){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Repeat,anisoLevel=1,mipMapBias=0};
   for(int mip=0;mip<palette.mipmapCount;mip++){int edge=Math.Max(1,2048>>mip);byte value=(byte)Math.Round(mip*255.0/16.0);var pixels=new Color32[edge*edge];for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(value,value,value,255);palette.SetPixels32(pixels,mip);}palette.Apply(false,false);
   var originals=sourceDraws.Select(r=>r.sharedMaterials).ToArray();
   var privateMaterials=new System.Collections.Generic.List<Material>();
   var debug=((HDRenderPipeline)RenderPipelineManager.currentPipeline).debugDisplaySettings;
   if(debug.IsDebugDisplayEnabled())throw new Exception("Refuse preexisting debug state");
   try {
    for(int index=0;index<sourceDraws.Length;index++)sourceDraws[index].sharedMaterials=originals[index].Select(m=>{var copy=new Material(probeShader);copy.CopyPropertiesFromMaterial(m);if(copy.GetVector("_Tiling").x!=1||copy.GetVector("_Tiling").y!=1)throw new Exception("Private probe requires verified unit tiling");copy.SetTexture("_BaseMap",palette);privateMaterials.Add(copy);return copy;}).ToArray();
    debug.SetDebugViewCommonMaterialProperty(UnityEngine.Rendering.HighDefinition.Attributes.MaterialSharedProperty.Albedo);
    for(int mode=0;mode<2;mode++){
     foreach(var material in privateMaterials)material.SetFloat("_OpenRPGProbeMode",mode);
     var probeRequest=new RenderPipeline.StandardRequest{destination=target};for(int warm=0;warm<3;warm++)RenderPipeline.SubmitRenderRequest(camera,probeRequest);
     var active=RenderTexture.active;Texture2D captured=null;
     try {RenderTexture.active=target;captured=new Texture2D(512,512,TextureFormat.RGBAHalf,false,true);captured.ReadPixels(new Rect(0,0,512,512),0,0,false);captured.Apply(false,false);File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(request.output),"MIP-PROBE-"+mode+".rgba16f"),captured.GetRawTextureData<byte>().ToArray());}
     finally {RenderTexture.active=active;if(captured!=null)UnityEngine.Object.DestroyImmediate(captured);}
    }
   }finally{debug.SetDebugViewCommonMaterialProperty(UnityEngine.Rendering.HighDefinition.Attributes.MaterialSharedProperty.None);for(int i=0;i<sourceDraws.Length;i++)sourceDraws[i].sharedMaterials=originals[i];foreach(var m in privateMaterials)UnityEngine.Object.DestroyImmediate(m);UnityEngine.Object.DestroyImmediate(palette);}
'''
    return text.replace(needle,probe+'\n'+needle)

def corrected_popen(args,*a,**kw):
    if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
        args=list(args);i=args.index('-executeMethod')+1
        assert args[i]=='OpenRPGSourceShadowReferenceV8.Export'
        args[i]='OpenRPGSourceMipPaletteV1.Export'
    return popen(args,*a,**kw)

g['subprocess'].Popen=corrected_popen
g.update(ROOT=ROOT,HELPER=g['PROJECT']/'Assets/Editor/SourceMipPaletteV1.cs',helper=helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':
    if sys.argv[-1]=='prepare':
        assert not ROOT.exists() and not SHADER.exists()
        shader,count=build_shader()
        SHADER.write_text(shader,'utf-8')
        g['prepare']()
        g['save']('DERIVATION.v1.json',dict(original_shader=str(EXPORT/'source-generated.shader'),original_sha256=g['sha'](EXPORT/'source-generated.shader'),private_shader=str(SHADER),private_sha256=g['sha'](SHADER),base_color_assignments=count,original_shader_unchanged=True,source_geometry_unchanged=True,private_palette=True,full_import=False))
    else:
        g['run']();d=g['read'](ROOT/'RUN-RESULT.v1.json');paths=[ROOT/f'MIP-PROBE-{mode}.rgba16f' for mode in range(2)]
        if d.get('actual_exit_code')==0 and all(p.exists() for p in paths):
            d.update(status='source_implicit_mip_palette_and_hardware_lod_captured',probe_files={str(p):g['sha'](p) for p in paths})
        else:
            d.update(status='source_mip_palette_capture_refused')
        g['save']('RUN-RESULT.v1.json',d)
