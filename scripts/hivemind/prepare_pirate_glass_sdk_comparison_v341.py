"""Prepare a one-prefab check of the SDK-derived transparent blend factor."""
from pathlib import Path
import hashlib,json
S=Path(__file__).resolve().parent;W=Path('F:/OpenRPG_ImportWork')
HDRP=W/'unity-library-source-thumbnail-pirate-bench-v2/project/Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2'
O=W/'pirate-glass-sdk-policy-audit-v341';dest=S/'run_pirate_glass_sdk_comparison_v342.py'
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
assert not O.exists() and not dest.exists()
api=HDRP/'Runtime/Material/Unlit/BaseUnlitAPI.cs';graph=HDRP/'Editor/Material/ShaderGraph/HDSubShaderUtilities.cs'
assert 'material.SetInt(HDShaderIDs._DstBlend2, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);' in api.read_text('utf-8-sig')
assert 'collector.AddFloatProperty("_DstBlend2", 0.0f);' in graph.read_text('utf-8-sig')
pins={str(p):sha(p) for p in [api,graph,Path(__file__)]}
text=(S/'run_pirate_p13_migration_render_v318.py').read_text('utf-8-sig')
text=text.replace('pirate-p13-migration-render-v318','pirate-glass-sdk-comparison-v342').replace('PirateP13MigrationRenderV318','PirateGlassSdkComparisonV342')
text=text.replace("guard=read(guard_path);assert len(guard['cases_refused_source_parity'])==10", "guard=read(guard_path);guard['cases_refused_source_parity']=[c for c in guard['cases_refused_source_parity'] if 'windowglass-01' in c['id']];assert len(guard['cases_refused_source_parity'])==1")
text=text.replace('len(selected)==10','len(selected)==1').replace('prefabs=10,captures=40,views=80','prefabs=1,captures=4,views=8').replace("len(batch['cases'])==40","len(batch['cases'])==4").replace('range(10)','range(1)').replace('expected_prefabs=10','expected_prefabs=1')
needle='if(comparisonSource.HasProperty(property)&&comparisonSource.GetFloat(property)!=defaults.GetFloat(property))throw new Exception("Added property differs from current shader default: "+property);'
assert text.count(needle)==1
policy='''if(property=="_DstBlend2") {
      if(comparisonSource.GetFloat("_SurfaceType")!=1||comparisonSource.GetFloat("_BlendMode")!=0||comparisonSource.GetFloat("_DstBlend")!=(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha)throw new Exception("Transparent alpha source policy prerequisites absent");
      var policyType=typeof(HDRenderPipelineAsset).Assembly.GetType("UnityEngine.Rendering.HighDefinition.BaseUnlitAPI",true);
      var method=policyType.GetMethod("SetupBaseUnlitKeywords",BindingFlags.Public|BindingFlags.Static);if(method==null)throw new Exception("SDK material blend policy absent");
      var policyMaterial=new Material(comparisonSource);policyMaterial.hideFlags=HideFlags.HideAndDontSave;
      try {
       policyMaterial.SetFloat("_DstBlend2",defaults.GetFloat("_DstBlend2"));method.Invoke(null,new object[]{policyMaterial});
       float derived=policyMaterial.GetFloat("_DstBlend2");if(derived!=(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha||derived!=comparisonSource.GetFloat("_DstBlend2"))throw new Exception("Derived SDK blend factor differs from imported material");
       comparisonMaterial.SetFloat("_DstBlend2",derived);
       policyMaterial.SetFloat("_SurfaceType",0);method.Invoke(null,new object[]{policyMaterial});if(policyMaterial.GetFloat("_DstBlend2")!=0)throw new Exception("Opaque blend negative control failed");
       if(comparisonSource.HasProperty("_TransmissionMaskMap")&&comparisonSource.GetTexture("_TransmissionMaskMap")!=null)throw new Exception("Added transmission texture requires separate source control");
      }finally{UnityEngine.Object.DestroyImmediate(policyMaterial);}
     }else if(comparisonSource.HasProperty(property)&&comparisonSource.GetFloat(property)!=defaults.GetFloat(property))throw new Exception("Added property differs from current shader default: "+property);'''
text=text.replace(needle,policy)
text=text.replace('pins.update(source_pins)',"pins.update(source_pins)\n    pins.update("+repr(pins)+")")
text=text.replace("qualified_scope='Observed wood stencil changes and added defaults in current HDRP thumbnail fixture only; original SDK pipeline equivalence unqualified'", "qualified_scope='Current HDRP thumbnail: source stencil masks, shader defaults and SDK-derived transparent alpha blend policy with opaque negative control; original SDK and OpenRPG renderer equivalence unqualified'")
compile(text,str(dest),'exec')
dest.write_text(text,'utf-8');pins[str(dest)]=sha(dest)
O.mkdir();(O/'RESULT.v1.json').write_text(json.dumps(dict(status='sdk_policy_identified_native_control_pending',input_pins=pins,default_property=0,transparent_policy='OneMinusSrcAlpha',native_enum_value_unverified=True,original_pipeline_equivalence=False,full_import=False),indent=2),'utf-8')
print(str(dest))
