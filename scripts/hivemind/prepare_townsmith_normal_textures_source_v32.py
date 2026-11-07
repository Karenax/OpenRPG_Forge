"""Source raw normal texture samples with original texture resources and depth."""
from pathlib import Path
import subprocess,sys
s=Path(__file__).parent
target=s/'queue_townsmith_normal_textures_source_v32.py'
assert not target.exists()
q=(s/'queue_townsmith_depth_bound_uv_v27.py').read_text(encoding='utf-8')
q=q.replace('v27','v32').replace('V27','V32')
q=q.replace('townsmith-door-source-depth-bound-uv-v32','townsmith-door-source-normal-textures-v32')
q=q.replace('.uv-control-','.normal-texture-')
before='float _UvProbeMode;'
assert q.count(before)==1
q=q.replace(before,'float _UvProbeMode;Texture2D _ProbeNormal6;SamplerState sampler_ProbeNormal6;Texture2D _ProbeNormal7;SamplerState sampler_ProbeNormal7;')
before='if(_UvProbeMode<0.5)return float4(input.uv.xy,0,1);return float4(ddx(input.uv.xy),ddy(input.uv.xy));'
assert q.count(before)==1
q=q.replace(before,'if(_UvProbeMode<0.5)return _ProbeNormal6.Sample(sampler_ProbeNormal6,input.uv.xy);return _ProbeNormal7.Sample(sampler_ProbeNormal7,input.uv.xy*30.0);')
before='SubShader { Tags'
assert q.count(before)==1
q=q.replace(before,'Properties { _ProbeNormal6 ("Original normal 6",2D)="bump"{} _ProbeNormal7 ("Original normal 7",2D)="bump"{} }\\nSubShader { Tags')
before='CustomPassVolume.RegisterGlobalCustomPass(CustomPassInjectionPoint.AfterOpaqueDepthAndNormal,sourceUvPass);'
assert q.count(before)==1
setup='''
    var normalGuids=new string[]{"abc00000000004864883221994049297","abc00000000005543024739457366141"};
    for(int normalIndex=0;normalIndex<2;normalIndex++){
     string path=AssetDatabase.GUIDToAssetPath(normalGuids[normalIndex]);var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
     if(texture==null||AssetDatabase.AssetPathToGUID(path)!=normalGuids[normalIndex])throw new Exception("Original normal texture identity missing");
     sourceUvPass.probeMaterial.SetTexture(normalIndex==0?"_ProbeNormal6":"_ProbeNormal7",texture);
     File.WriteAllText(Path.Combine(root,item.id+".normal-resource-"+normalIndex+".json"),JsonUtility.ToJson(new SourceNormalResourceV32{guid=normalGuids[normalIndex],path=path,width=texture.width,height=texture.height,mips=texture.mipmapCount,aniso=texture.anisoLevel,format=texture.graphicsFormat.ToString(),filter=texture.filterMode.ToString(),wrapU=texture.wrapModeU.ToString(),wrapV=texture.wrapModeV.ToString(),mipBias=texture.mipMapBias},true));
    }
'''
# The setup occurs in the generator's escaped C# replacement string.
q=q.replace(before,setup.replace('\n','\\n')+before)
before="    CS.write_text(text,encoding='utf-8');HELPER.write_bytes(CS.read_bytes())"
assert q.count(before)==1
record='\n[Serializable] public class SourceNormalResourceV32 {public string guid,path,format,filter,wrapU,wrapV;public int width,height,mips,aniso;public float mipBias;public bool original_resource=true,full_import=false;}\n'
q=q.replace(before,'    text+='+repr(record)+'\n'+before)
compile(q,str(target),'exec');target.write_text(q,encoding='utf-8')
subprocess.run([sys.executable,'-B',str(target)],check=True)
