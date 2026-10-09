"""Acquire original caster mesh and face-policy evidence in the existing private source scene."""
from pathlib import Path
import runpy,sys
BASE=Path(__file__).with_name('queue_firepit_source_shadow_raster_v6.py')
loaded=runpy.run_path(str(BASE),run_name='caster_geometry');g=loaded['g'];popen=loaded['popen']
SOURCE_HELPER=g['PROJECT']/'Assets/Editor/SourceShadowRasterV6.cs'
def helper():
 text=SOURCE_HELPER.read_text('utf-8').replace('OpenRPGSourceShadowRasterV6','OpenRPGSourceCasterGeometryV1')
 utilities=r"""
 [Serializable] class CasterMaterialEvidence {public string identity,shader,keywords;public bool shadow_pass_enabled;public string[] property_names;public float[] property_values;}
 [Serializable] class CasterSubmeshEvidence {public int index;public string topology;public int[] indices;}
 [Serializable] class CasterGeometryEvidence {public string name,mesh_identity;public bool enabled,force_rendering_off,active_in_hierarchy,is_visible;public string shadow_casting_mode;public float[] local_to_world;public Vector3[] positions,normals;public CasterSubmeshEvidence[] submeshes;public CasterMaterialEvidence[] materials;public int[] lod_membership;}
 [Serializable] class CasterGeometryCapture {public int view;public bool lod0_forced_diagnostic_only;public CasterGeometryEvidence[] renderers;public bool source_gpu_vertex_buffer_readback=false;}
 static void CaptureCasterGeometry(GameObject fixture,int view,string output){
  var rows=new System.Collections.Generic.List<CasterGeometryEvidence>();var groups=fixture.GetComponentsInChildren<LODGroup>(true);
  foreach(var renderer in fixture.GetComponentsInChildren<MeshRenderer>(true)){
   var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;if(mesh==null)throw new Exception("Caster mesh missing");
   var levels=new System.Collections.Generic.List<int>();foreach(var group in groups){var lods=group.GetLODs();for(int i=0;i<lods.Length;i++)if(lods[i].renderers.Contains(renderer))levels.Add(i);}
   var sub=new System.Collections.Generic.List<CasterSubmeshEvidence>();for(int i=0;i<mesh.subMeshCount;i++)sub.Add(new CasterSubmeshEvidence{index=i,topology=mesh.GetTopology(i).ToString(),indices=mesh.GetIndices(i,true)});
   var mats=new System.Collections.Generic.List<CasterMaterialEvidence>();foreach(var material in renderer.sharedMaterials){
    var names=new System.Collections.Generic.List<string>();var values=new System.Collections.Generic.List<float>();
    foreach(var key in new[]{"_Cull","_CullMode","_CullModeForward","_OpaqueCullMode","_DoubleSidedEnable","_DepthOffsetEnable","_DepthOffset","_AlphaCutoffEnable","_AlphaCutoff","_ZWrite"})if(material.HasProperty(key)){var value=material.GetFloat(key);if(!float.IsFinite(value))throw new Exception("Nonfinite caster material property");names.Add(key);values.Add(value);}
    mats.Add(new CasterMaterialEvidence{identity=Identity(material),shader=material.shader.name,keywords=string.Join(",",material.shaderKeywords.OrderBy(k=>k)),shadow_pass_enabled=material.GetShaderPassEnabled("ShadowCaster"),property_names=names.ToArray(),property_values=values.ToArray()});
   }
   rows.Add(new CasterGeometryEvidence{name=renderer.name,mesh_identity=Identity(mesh),enabled=renderer.enabled,force_rendering_off=renderer.forceRenderingOff,active_in_hierarchy=renderer.gameObject.activeInHierarchy,is_visible=renderer.isVisible,shadow_casting_mode=renderer.shadowCastingMode.ToString(),local_to_world=RasterMatrix(renderer.localToWorldMatrix),positions=mesh.vertices,normals=mesh.normals,submeshes=sub.ToArray(),materials=mats.ToArray(),lod_membership=levels.ToArray()});
  }
  if(rows.Count!=4)throw new Exception("Expected four original static mesh levels");var path=Path.Combine(Path.GetDirectoryName(output),"SOURCE-CASTER-GEOMETRY-"+view+".json");if(File.Exists(path))throw new Exception("Preserve caster evidence");File.WriteAllText(path,JsonUtility.ToJson(new CasterGeometryCapture{view=view,lod0_forced_diagnostic_only=true,renderers=rows.ToArray()},true));
 }
"""
 marker=' static string Hash(';assert text.count(marker)==1;text=text.replace(marker,utilities+marker)
 marker='File.WriteAllText(cullingPath,JsonUtility.ToJson(cullingState,true));';assert text.count(marker)==1
 return text.replace(marker,marker+'CaptureCasterGeometry(fixture,view,request.output);')
def corrected_popen(args,*a,**kw):
 if isinstance(args,list) and args and str(args[0])==str(g['UNITY']):
  args=list(args);i=args.index('-executeMethod')+1;assert args[i]=='OpenRPGSourceShadowReferenceV8.Export';args[i]='OpenRPGSourceCasterGeometryV1.Export'
 return popen(args,*a,**kw)
g['subprocess'].Popen=corrected_popen
g.update(ROOT=g['W']/'firepit-source-caster-geometry-v1',HELPER=g['PROJECT']/'Assets/Editor/SourceCasterGeometryV1.cs',helper=helper,__file__=str(Path(__file__).resolve()))
if __name__=='__main__':
 if sys.argv[-1]=='prepare':
  g['prepare']()
 else:
  g['run']();state=g['read'](g['ROOT']/'RUN-RESULT.v1.json');paths=[g['ROOT']/f'SOURCE-CASTER-GEOMETRY-{i}.json' for i in range(2)]
  if state.get('actual_exit_code')==0 and all(p.exists() for p in paths):state.update(status='source_caster_meshes_face_policy_and_lod_membership_captured',geometry_files={str(p):g['sha'](p) for p in paths})
  else:state.update(status='source_caster_geometry_capture_refused')
  g['save']('RUN-RESULT.v1.json',state)
