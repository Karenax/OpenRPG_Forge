"""Measure both native BakeMesh transform modes without altering existing captures."""
from pathlib import Path
import ast, copy, importlib.util, json, sys

HERE=Path(__file__).parent
BASE=HERE/'queue_firepit_particle_geometry_v1.py'
spec=importlib.util.spec_from_file_location('geometry_probe_v228',BASE)
probe=importlib.util.module_from_spec(spec);spec.loader.exec_module(probe)
OLD=probe.W/'unity-delta-firepit-particle-geometry-v1'
probe.ROOT=probe.W/'firepit-particle-native-mesh-raster-v246';probe.job.ROOT=probe.ROOT
original_helper=probe.helper_source
BENCH_DRIFT=[]

def refresh_private_bench(pins):
    # Old receipts pin the entire mutable bench, including unrelated static/Pirate
    # materials migrated since the particle acquisition. Keep those receipts intact
    # and pin the present bench for a new experiment; require identical old particle
    # states and geometry in verify(). Never rebase original source files or helpers.
    allowed={'M_FirePit_A_01.mat','M_FoodSeed_01a.mat','M_Cannon02.mat','M_FoodKit_01a.mat',
             'M_WoodTrim_01.mat','M_FishnMeat_01a.mat','M_Cannon.mat','M_WoodCarvingTrim_01b.mat',
             'M_PirateHat.mat','M_BottleKit_01a.mat','M_MetalKitPots_01a.mat','M_Wood_Planks_01.mat',
             'M_WoodBarrel_01a.mat','M_RocksTemperate_01.mat','M_EuropeanBeech_Detail_01.mat','VFXManager.asset'}
    refreshed=dict(pins)
    for name,expected in pins.items():
        p=Path(name)
        if p.is_relative_to(probe.PROJECT) and p.name in allowed:
            actual=probe.sha(p)
            if actual!=expected:
                assert p.suffix=='.mat' or p==probe.PROJECT/'ProjectSettings/VFXManager.asset'
                BENCH_DRIFT.append(dict(path=name,historical_sha256=expected,current_sha256=actual,
                    effect_material_or_prefab=False,old_receipt_preserved=True))
                refreshed[name]=actual
    assert len(BENCH_DRIFT)==17,'Unexpected private bench drift; inspect before proceeding'
    return refreshed

def helper_source():
    text=original_helper().replace('OpenRPGFirePitParticleGeometry','OpenRPGFirePitParticleNativeMeshRasterV246')
    anchor='[Serializable] public class Geometry {';assert text.count(anchor)==1
    text=text.replace(anchor,anchor+'public Vector3[] with_transform_positions,with_transform_normals;public Vector4[] with_transform_tangents;public Color32[] with_transform_colors;public int[] with_transform_indices;public Matrix4x4 renderer_local_to_world;')
    anchor='renderer.BakeMesh(mesh,camera,false);';assert text.count(anchor)==1
    text=text.replace(anchor,anchor+'''
    Vector3[] withPositions,withNormals;Vector4[] withTangents;Color32[] withColors;int[] withIndices;
    var transformedMesh=new Mesh();transformedMesh.hideFlags=HideFlags.HideAndDontSave;
    try{renderer.BakeMesh(transformedMesh,camera,true);withPositions=transformedMesh.vertices;withNormals=transformedMesh.normals;withTangents=transformedMesh.tangents;withColors=transformedMesh.colors32;withIndices=transformedMesh.triangles;}
    finally{UnityEngine.Object.DestroyImmediate(transformedMesh);}
''')
    anchor='rows.Add(new Geometry{fixture=fixture,';assert text.count(anchor)==1
    text=text.replace(anchor,'rows.Add(new Geometry{with_transform_positions=withPositions,with_transform_normals=withNormals,with_transform_tangents=withTangents,with_transform_colors=withColors,with_transform_indices=withIndices,renderer_local_to_world=renderer.localToWorldMatrix,fixture=fixture,')
    anchor='[Serializable] public class Geometry {';assert text.count(anchor)==1
    text=text.replace(anchor,anchor+'public string native_pixels_path,native_pixels_sha256;')
    anchor='static Geometry[] BakeGeometry';assert text.count(anchor)==1
    render="""
 static int nativeRasterIndex=0;
 static string[] NativeRaster(Mesh mesh,Camera camera,ParticleSystemRenderer renderer){
  var args=Environment.GetCommandLineArgs();var outputRoot=Path.GetDirectoryName(args[Array.IndexOf(args,"--openrpg-manifest")+1]);var dir=Path.Combine(outputRoot,"native-pixels");Directory.CreateDirectory(dir);
  var originalGraphics=GraphicsSettings.defaultRenderPipeline;var originalQuality=QualitySettings.renderPipeline;var previous=RenderTexture.active;var originalFog=RenderSettings.fog;
  var keys=new[]{"FOG_LINEAR","FOG_EXP","FOG_EXP2","SOFTPARTICLES_ON","SOFTPARTICLES_OFF"};var oldKeys=keys.Select(Shader.IsKeywordEnabled).ToArray();
  var source=renderer.sharedMaterial;var before=EditorJsonUtility.ToJson(source,true);var material=new Material(source);var target=new RenderTexture(256,256,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);target.antiAliasing=1;target.useMipMap=false;target.autoGenerateMips=false;var readback=new Texture2D(256,256,TextureFormat.RGBAHalf,false,true);
  try{
   if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null||source.shader.name!="Legacy Shaders/Particles/Alpha Blended")throw new Exception("Original raster shader/GPU missing");
   GraphicsSettings.defaultRenderPipeline=null;QualitySettings.renderPipeline=null;RenderSettings.fog=false;foreach(var key in keys){Shader.DisableKeyword(key);material.DisableKeyword(key);}Shader.EnableKeyword("SOFTPARTICLES_OFF");material.EnableKeyword("SOFTPARTICLES_OFF");if(!target.Create())throw new Exception("Native half raster target missing");
   var commands=new CommandBuffer();try{commands.SetRenderTarget(target);commands.ClearRenderTarget(true,true,new Color(.02f,.025f,.03f,.25f));commands.SetViewProjectionMatrices(camera.worldToCameraMatrix,GL.GetGPUProjectionMatrix(camera.projectionMatrix,true));var m=renderer.localToWorldMatrix;commands.DrawMesh(mesh,Matrix4x4.Translate(new Vector3(m.m03,m.m13,m.m23)),material,0,0);Graphics.ExecuteCommandBuffer(commands);}finally{commands.Release();}
   RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,256,256),0,0,false);readback.Apply(false,false);var raw=readback.GetRawTextureData<byte>().ToArray();if(raw.Length!=524288)throw new Exception("Native half raster layout differs");var path=Path.Combine(dir,(nativeRasterIndex++).ToString("D4")+".rgba.f16");using(var stream=new FileStream(path,FileMode.CreateNew))stream.Write(raw,0,raw.Length);string digest;using(var hash=System.Security.Cryptography.SHA256.Create())digest=BitConverter.ToString(hash.ComputeHash(raw)).Replace("-","").ToLowerInvariant();return new[]{path,digest};
  }finally{GraphicsSettings.defaultRenderPipeline=originalGraphics;QualitySettings.renderPipeline=originalQuality;RenderTexture.active=previous;RenderSettings.fog=originalFog;for(int i=0;i<keys.Length;i++){if(oldKeys[i])Shader.EnableKeyword(keys[i]);else Shader.DisableKeyword(keys[i]);}target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(readback);UnityEngine.Object.DestroyImmediate(material);if(before!=EditorJsonUtility.ToJson(source,true))throw new Exception("Source material mutated during native raster");}
 }
 """
    text=text.replace(anchor,render+anchor)
    text=text.replace('using UnityEditor;','using UnityEditor;using UnityEngine.Rendering;')
    anchor='Vector3[] withPositions,withNormals;';assert text.count(anchor)==1;text=text.replace(anchor,'string[] nativeRaster=null;'+anchor)
    anchor='withIndices=transformedMesh.triangles;';assert text.count(anchor)==1;text=text.replace(anchor,anchor+'nativeRaster=NativeRaster(transformedMesh,camera,renderer);')
    anchor='rows.Add(new Geometry{with_transform_positions=';assert text.count(anchor)==1;text=text.replace(anchor,'rows.Add(new Geometry{native_pixels_path=nativeRaster[0],native_pixels_sha256=nativeRaster[1],with_transform_positions=')
    return text

def verify(report,prepared):
    original=probe.read(OLD/'GEOMETRY.v1.json');stripped=copy.deepcopy(report);observations=0;rows=[]
    for case in stripped['cases']:
        for row in case['rows']:
            for g in row['geometry']:
                pixelpath=Path(g.pop('native_pixels_path'));pixeldigest=g.pop('native_pixels_sha256');assert pixelpath.resolve().is_relative_to(probe.ROOT.resolve()) and len(pixelpath.read_bytes())==524288 and probe.sha(pixelpath)==pixeldigest
                fields={k:g.pop(k) for k in ['with_transform_positions','with_transform_normals','with_transform_tangents','with_transform_colors','with_transform_indices','renderer_local_to_world']}
                assert len(fields['with_transform_positions'])==g['vertices']
                assert fields['with_transform_indices']==g['indices']
                assert fields['with_transform_colors']==g['colors']
                assert fields['renderer_local_to_world']==g['particle_local_to_world']
                for name in ['with_transform_positions','with_transform_normals','with_transform_tangents']:
                    assert all(all(__import__('math').isfinite(v) for v in item.values()) for item in fields[name])
                rows.append(dict(node_path=case['node_path'],seed=row['seed'],requested_time=row['requested_time'],repetition=row['repetition'],fixture=g['fixture'],vertices=g['vertices'],positions_differ=fields['with_transform_positions']!=g['positions'],normals_differ=fields['with_transform_normals']!=g['normals'],tangents_differ=fields['with_transform_tangents']!=g['tangents'],colors_and_indices_byte_values_equal=True))
                observations+=1
    assert stripped==original,'Original 336 geometries or 112 particle states changed'
    assert observations==336;probe.job.check(prepared['input_pins'])
    probe.save('TRANSFORM-MEASUREMENTS.v1.json',dict(status='native_bake_transform_modes_captured',rows=rows,observations=observations,coordinate_mapping_to_forge_qualified=False,autonomous_runtime_qualified=False,full_import=False))
    return dict(geometry_observations_verified=336,vertex_observations=sum(r['vertices'] for r in rows),prior_336_geometry_observations_unchanged=True,prior_112_particle_frames_unchanged=True,both_native_transform_modes_captured=True)

def main():
    meshstate=probe.read(probe.W/'firepit-particle-mesh-gpu-v244/RUN-RESULT.v1.json');assert meshstate.get('gpu_exit_code')==0,'GPU mesh batch must finish before Unity raster'
    prior=probe.read(OLD/'RUN-RESULT.v1.json');assert prior['native_geometry_verified'] and prior['unity_exit_code']==0
    assert probe.sha(OLD/'INDEPENDENT-REOPEN.v1.json')==prior['independent_receipt_sha256']
    source=BASE.read_text('utf-8-sig');tree=ast.parse(source)
    functions=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in {'prepare','run'}]
    code=ast.unparse(ast.Module(body=functions,type_ignores=[])).replace('OpenRPGFirePitParticleGeometry','OpenRPGFirePitParticleNativeMeshRasterV246')
    assert code.count("job.check(proof['input_pins'])")==1
    code=code.replace("job.check(proof['input_pins'])","refreshed_prior_pins=refresh_private_bench(proof['input_pins'])\n    job.check(refreshed_prior_pins)")
    assert code.count("pins.update(proof['input_pins'])")==1
    code=code.replace("pins.update(proof['input_pins'])","pins.update(refreshed_prior_pins)")
    anchor='job.check(pins)';assert code.count(anchor)==1
    code=code.replace(anchor,"save('PRIVATE-BENCH-DRIFT.v1.json',dict(status='new_experiment_pins_current_private_bench',rows=BENCH_DRIFT,source_files_rebased=False,previous_particle_reference_unchanged=True,full_import=False))\n    for extra in [BASE, HERE/'queue_firepit_native_mesh_raster_v246.py', OLD/'GEOMETRY.v1.json', OLD/'INDEPENDENT-REOPEN.v1.json', OLD/'RUN-RESULT.v1.json', ROOT/'PRIVATE-BENCH-DRIFT.v1.json']:\n        pins[str(extra)]=sha(extra)\n    job.check(pins)")
    code=code.replace("'-nographics', ","")
    scope=probe.__dict__;scope.update(__file__=str(Path(__file__).resolve()),helper_source=helper_source,verify=verify,BASE=BASE,OLD=OLD,refresh_private_bench=refresh_private_bench,BENCH_DRIFT=BENCH_DRIFT)
    exec(compile(code,str(BASE),'exec'),scope)
    if len(sys.argv)>1:
        assert sys.argv[1:]==['run'];scope['run']()
    else:scope['prepare']()

if __name__=='__main__':main()
