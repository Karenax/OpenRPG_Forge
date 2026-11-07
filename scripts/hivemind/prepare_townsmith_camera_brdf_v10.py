"""Capture actual HDRP frame mode, GPU projection and native BRDF texture loads."""
from pathlib import Path
import subprocess
import sys

S = Path(__file__).parent
target = S / 'queue_townsmith_camera_brdf_v10.py'
assert not target.exists()
text = (S / 'queue_townsmith_sky_capture_v9.py').read_text(encoding='utf-8')
text = text.replace('townsmith-door-source-sky-capture-v9', 'townsmith-door-source-camera-brdf-v10')
text = text.replace('SourceTownsmithSkyCaptureV9', 'SourceTownsmithCameraBrdfV10')
compute = '''#pragma kernel ReadFgd
Texture2D<float4> SourceFgd;
RWStructuredBuffer<float4> Output;
uint Width,Height;
[numthreads(8,8,1)]
void ReadFgd(uint3 id:SV_DispatchThreadID){if(id.x<Width&&id.y<Height)Output[id.y*Width+id.x]=SourceFgd.Load(int3(id.xy,0));}
'''
anchor = "    text=(S/'candidates/SourceTownsmithShadowModesV1.cs').read_text(encoding='utf-8')"
assert text.count(anchor) == 1
text = text.replace(anchor, "    compute_path=PROJECT/'Assets/Editor/SourceTownsmithFgdReadbackV10.compute'\n    assert not compute_path.exists()\n    compute_path.write_text(" + repr(compute) + ",encoding='utf-8')\n" + anchor)
text = text.replace('Path(__file__),CS,HELPER,', 'Path(__file__),CS,HELPER,compute_path,')
anchor = 'public string actual_camera_history;'
assert text.count(anchor) == 1
text = text.replace(anchor, anchor + ' public string actual_lit_shader_mode,actual_fgd_format,actual_hdri_source; public bool actual_ssao_enabled,actual_ssr_enabled; public Matrix4x4 actual_hdrp_gpu_projection;')
anchor = '    var reflectionMethod=manager.GetType().GetMethod("GetSkyReflection",'
assert text.count(anchor) == 1
addition = '''    var sourceFgd=Shader.GetGlobalTexture("_PreIntegratedFGD_GGXDisneyDiffuse");
    if(sourceFgd==null||sourceFgd.dimension!=TextureDimension.Tex2D||sourceFgd.width!=64||sourceFgd.height!=64)throw new Exception("Actual HDRP FGD missing or unexpected dimensions");
    var sourceCompute=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/SourceTownsmithFgdReadbackV10.compute");if(sourceCompute==null)throw new Exception("Owned source BRDF readback compute absent");
    var sourceKernel=sourceCompute.FindKernel("ReadFgd");var fgdBuffer=new ComputeBuffer(64*64,16);
    try{
      sourceCompute.SetTexture(sourceKernel,"SourceFgd",sourceFgd);sourceCompute.SetBuffer(sourceKernel,"Output",fgdBuffer);
      sourceCompute.SetInt("Width",64);sourceCompute.SetInt("Height",64);sourceCompute.Dispatch(sourceKernel,8,8,1);
      var fgdValues=new Vector4[64*64];fgdBuffer.GetData(fgdValues);
      if(!BitConverter.IsLittleEndian)throw new Exception("FGD capture requires declared little endian");
      using(var writer=new BinaryWriter(File.Open(Path.Combine(root,item.id+".fgd.rgba32f"),FileMode.CreateNew))){
        for(int i=0;i<fgdValues.Length;i++)for(int ch=0;ch<4;ch++){var v=fgdValues[i][ch];if(float.IsNaN(v)||float.IsInfinity(v))throw new Exception("Nonfinite FGD GPU load");writer.Write(v);}
      }
    }finally{fgdBuffer.Release();}
'''
text = text.replace(anchor, addition + anchor)
anchor = 'id=item.id,sky_cubemap_count='
assert text.count(anchor) == 1
text = text.replace(anchor, 'id=item.id,actual_lit_shader_mode=sourceHdCamera.frameSettings.litShaderMode.ToString(),actual_ssao_enabled=sourceHdCamera.frameSettings.IsEnabled(FrameSettingsField.SSAO),actual_ssr_enabled=sourceHdCamera.frameSettings.IsEnabled(FrameSettingsField.SSR),actual_hdrp_gpu_projection=sourceHdCamera.mainViewConstants.projMatrix,actual_fgd_format=sourceFgd.graphicsFormat.ToString(),actual_hdri_source=activeHdri.hdriSky.value==null?"null":AssetDatabase.GetAssetPath(activeHdri.hdriSky.value),sky_cubemap_count=')
compile(text, str(target), 'exec')
target.write_text(text, encoding='utf-8')
subprocess.run([sys.executable, '-B', str(target)], check=True)
