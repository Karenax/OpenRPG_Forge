#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace OpenRPGRigCapture {
 [Serializable] public class Request {public string asset_id,asset_path,expected_guid,expected_file_id,expected_name,output_root;}
 [Serializable] public class Node {public int index,parent;public string name;public Vector3 position,scale;public Quaternion rotation;public Matrix4x4 world;}
 [Serializable] public class Format {public string attribute,format;public int dimension,stream;}
 [Serializable] public class Clip {public string name,guid,file_id;public float length,frame_rate;public bool legacy;public string wrap_mode;public Curve[] curves;public ObjectCurve[] object_curves;public Event[] events;}
 [Serializable] public class Curve {public string path,type,property,pre_wrap,post_wrap;public Key[] keys;}
 [Serializable] public class Key {public float time,value,in_weight,out_weight;public uint in_tangent_bits,out_tangent_bits;public string weighted_mode,left_tangent_mode,right_tangent_mode;public bool broken;}
 [Serializable] public class Event {public float time,float_parameter;public int int_parameter;public string function_name,string_parameter,message_options;public ObjectKey object_parameter;}
 [Serializable] public class ObjectCurve {public string path,type,property;public ObjectKey[] keys;}
 [Serializable] public class ObjectKey {public float time;public string guid,file_id,type,name;public bool is_null;}
 [Serializable] public class Pose {public string name;public Matrix4x4 mesh_world;public Matrix4x4[] joint_world;public int vertices;}
 [Serializable] public class Result {public string unity_version,asset_id,guid,file_id,mesh_name,importer_json;public int vertices,submeshes,normals,tangents,colors,bind_poses,blend_shapes,renderer_node,root_bone_node;public int[] uv_counts,bone_nodes;public Node[] hierarchy;public Format[] formats;public Clip[] clips;public Pose[] probe_poses;public bool rig_flattened=false,animation_events_executed=false,source_animation_render_parity_qualified=false,full_import=false;}
 public static class Snapshot {
  static string Arg(string name){var a=Environment.GetCommandLineArgs();for(int i=0;i+1<a.Length;i++)if(a[i]==name)return a[i+1];throw new Exception("Missing "+name);}
  static void Write(string path,Action<BinaryWriter> body){using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write))using(var writer=new BinaryWriter(stream)){body(writer);writer.Flush();stream.Flush(true);}}
  static void V3(BinaryWriter w,Vector3 v){w.Write(v.x);w.Write(v.y);w.Write(v.z);}
  static void V4(BinaryWriter w,Vector4 v){w.Write(v.x);w.Write(v.y);w.Write(v.z);w.Write(v.w);}
  static void Mat(BinaryWriter w,Matrix4x4 m){for(int column=0;column<4;column++)for(int row=0;row<4;row++)w.Write(m[row,column]);}
  static Key[] Keys(AnimationCurve curve){var source=curve.keys;var result=new Key[source.Length];for(int i=0;i<source.Length;i++){var key=source[i];result[i]=new Key{time=key.time,value=key.value,in_weight=key.inWeight,out_weight=key.outWeight,in_tangent_bits=BitConverter.ToUInt32(BitConverter.GetBytes(key.inTangent),0),out_tangent_bits=BitConverter.ToUInt32(BitConverter.GetBytes(key.outTangent),0),weighted_mode=key.weightedMode.ToString(),left_tangent_mode=AnimationUtility.GetKeyLeftTangentMode(curve,i).ToString(),right_tangent_mode=AnimationUtility.GetKeyRightTangentMode(curve,i).ToString(),broken=AnimationUtility.GetKeyBroken(curve,i)};}return result;}
  static Event[] Events(AnimationClip clip){var rows=new List<Event>();foreach(var source in AnimationUtility.GetAnimationEvents(clip)){var obj=new ObjectKey{is_null=source.objectReferenceParameter==null};if(!obj.is_null){string guid;long id;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source.objectReferenceParameter,out guid,out id))throw new Exception("Unresolved event object identity");obj.guid=guid;obj.file_id=id.ToString(System.Globalization.CultureInfo.InvariantCulture);obj.name=source.objectReferenceParameter.name;obj.type=source.objectReferenceParameter.GetType().FullName;}rows.Add(new Event{time=source.time,float_parameter=source.floatParameter,int_parameter=source.intParameter,function_name=source.functionName,string_parameter=source.stringParameter,message_options=source.messageOptions.ToString(),object_parameter=obj});}return rows.ToArray();}
  public static void Export(){
   var request=JsonUtility.FromJson<Request>(File.ReadAllText(Arg("--openrpg-manifest")));
   if(Application.unityVersion!="6000.5.8f1"||!request.asset_path.StartsWith("Assets/SourceModels/",StringComparison.Ordinal)||request.asset_path.Contains(".."))throw new Exception("Exact isolated source context required");
   if(Directory.Exists(request.output_root))throw new Exception("Preserve existing rig capture");Directory.CreateDirectory(request.output_root);
   AssetDatabase.ImportAsset(request.asset_path,ImportAssetOptions.ForceSynchronousImport);
   var importer=AssetImporter.GetAtPath(request.asset_path) as ModelImporter;if(importer==null||!importer.isReadable)throw new Exception("Exact readable original importer required; no silent override");
   Mesh mesh=null;var assets=AssetDatabase.LoadAllAssetsAtPath(request.asset_path);
   foreach(var asset in assets){var item=asset as Mesh;if(item==null)continue;string guid;long id;if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(item,out guid,out id)&&guid==request.expected_guid&&id.ToString(System.Globalization.CultureInfo.InvariantCulture)==request.expected_file_id){if(mesh!=null)throw new Exception("Duplicate mesh identity");mesh=item;}}
   if(mesh==null||mesh.name!=request.expected_name||mesh.bindposes.Length==0)throw new Exception("Exact original rigged mesh required");
   // Morphs require their own complete frame adapter; preserve and refuse them here.
   if(mesh.blendShapeCount!=0)throw new Exception("Morph frames need complete capture; no discard");
   var model=AssetDatabase.LoadAssetAtPath<GameObject>(request.asset_path);if(model==null)throw new Exception("Original model hierarchy absent");
   GameObject instance=null;
   try{
    instance=UnityEngine.Object.Instantiate(model);instance.name=model.name;
    var transforms=instance.GetComponentsInChildren<Transform>(true);var ids=new Dictionary<Transform,int>();for(int i=0;i<transforms.Length;i++)ids.Add(transforms[i],i);
    var nodes=new List<Node>();for(int i=0;i<transforms.Length;i++){var t=transforms[i];nodes.Add(new Node{index=i,parent=t.parent!=null&&ids.ContainsKey(t.parent)?ids[t.parent]:-1,name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale,world=t.localToWorldMatrix});}
    SkinnedMeshRenderer renderer=null;
    foreach(var candidate in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)){if(candidate.sharedMesh==mesh){if(renderer!=null)throw new Exception("Multiple skin bindings require distinct captured instances");renderer=candidate;}}
    if(renderer==null||renderer.bones.Length!=mesh.bindposes.Length)throw new Exception("Complete original skin bone mapping required");
    var boneNodes=new int[renderer.bones.Length];for(int i=0;i<boneNodes.Length;i++){if(renderer.bones[i]==null||!ids.ContainsKey(renderer.bones[i]))throw new Exception("Bone outside original hierarchy");boneNodes[i]=ids[renderer.bones[i]];}
    var formats=new List<Format>();foreach(var d in mesh.GetVertexAttributes())formats.Add(new Format{attribute=d.attribute.ToString(),format=d.format.ToString(),dimension=d.dimension,stream=d.stream});
    var result=new Result{unity_version=Application.unityVersion,asset_id=request.asset_id,guid=request.expected_guid,file_id=request.expected_file_id,mesh_name=mesh.name,importer_json=EditorJsonUtility.ToJson(importer),vertices=mesh.vertexCount,submeshes=mesh.subMeshCount,bind_poses=mesh.bindposes.Length,blend_shapes=mesh.blendShapeCount,hierarchy=nodes.ToArray(),bone_nodes=boneNodes,renderer_node=ids[renderer.transform],root_bone_node=renderer.rootBone!=null&&ids.ContainsKey(renderer.rootBone)?ids[renderer.rootBone]:-1,formats=formats.ToArray(),uv_counts=new int[8]};
    var p=mesh.vertices;var n=mesh.normals;var tng=mesh.tangents;var c=mesh.colors;result.normals=n.Length;result.tangents=tng.Length;result.colors=c.Length;
    Write(Path.Combine(request.output_root,"positions.f32"),w=>{foreach(var v in p)V3(w,v);});Write(Path.Combine(request.output_root,"normals.f32"),w=>{foreach(var v in n)V3(w,v);});Write(Path.Combine(request.output_root,"tangents.f32"),w=>{foreach(var v in tng)V4(w,v);});Write(Path.Combine(request.output_root,"colors.f32"),w=>{foreach(var v in c){w.Write(v.r);w.Write(v.g);w.Write(v.b);w.Write(v.a);}});
    for(int uv=0;uv<8;uv++){var list=new List<Vector4>();mesh.GetUVs(uv,list);result.uv_counts[uv]=list.Count;Write(Path.Combine(request.output_root,"uv"+uv+".f32"),w=>{foreach(var v in list)V4(w,v);});}
    for(int sub=0;sub<mesh.subMeshCount;sub++){if(mesh.GetTopology(sub)!=MeshTopology.Triangles)throw new Exception("Original topology unsupported");var indices=mesh.GetIndices(sub);Write(Path.Combine(request.output_root,"submesh-"+sub+".i32"),w=>{foreach(var value in indices)w.Write(value);});}
    Write(Path.Combine(request.output_root,"bindposes.f32"),w=>{foreach(var value in mesh.bindposes)Mat(w,value);});
    using(var counts=mesh.GetBonesPerVertex())using(var weights=mesh.GetAllBoneWeights()){
     if(counts.Length!=mesh.vertexCount)throw new Exception("Incomplete skin weights");
     Write(Path.Combine(request.output_root,"bones-per-vertex.u8"),w=>{foreach(var value in counts)w.Write(value);});
     Write(Path.Combine(request.output_root,"boneweights.i32-f32"),w=>{foreach(var value in weights){w.Write(value.boneIndex);w.Write(value.weight);}});
    }
    var clips=new List<Clip>();foreach(var asset in assets){var clip=asset as AnimationClip;if(clip==null)continue;string guid;long id;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip,out guid,out id))throw new Exception("Animation identity unavailable");var curves=new List<Curve>();foreach(var binding in AnimationUtility.GetCurveBindings(clip)){var curve=AnimationUtility.GetEditorCurve(clip,binding);curves.Add(new Curve{path=binding.path,type=binding.type.FullName,property=binding.propertyName,pre_wrap=curve.preWrapMode.ToString(),post_wrap=curve.postWrapMode.ToString(),keys=Keys(curve)});}var objectCurves=new List<ObjectCurve>();foreach(var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)){var keys=new List<ObjectKey>();foreach(var key in AnimationUtility.GetObjectReferenceCurve(clip,binding)){var entry=new ObjectKey{time=key.time,is_null=key.value==null};if(key.value!=null){long objectId;string objectGuid;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(key.value,out objectGuid,out objectId))throw new Exception("Unresolved animated object identity");entry.guid=objectGuid;entry.file_id=objectId.ToString(System.Globalization.CultureInfo.InvariantCulture);entry.type=key.value.GetType().FullName;entry.name=key.value.name;}keys.Add(entry);}objectCurves.Add(new ObjectCurve{path=binding.path,type=binding.type.FullName,property=binding.propertyName,keys=keys.ToArray()});}clips.Add(new Clip{name=clip.name,guid=guid,file_id=id.ToString(System.Globalization.CultureInfo.InvariantCulture),length=clip.length,frame_rate=clip.frameRate,legacy=clip.legacy,wrap_mode=clip.wrapMode.ToString(),curves=curves.ToArray(),object_curves=objectCurves.ToArray(),events=Events(clip)});}result.clips=clips.ToArray();
    var poses=new List<Pose>();var original=renderer.bones[0].localRotation;
    for(int pose=0;pose<2;pose++){
     renderer.bones[0].localRotation=pose==0?original:original*Quaternion.Euler(0,0,30);
     var baked=new Mesh();try{renderer.BakeMesh(baked,false);var positions=baked.vertices;var normals=baked.normals;var tangents=baked.tangents;if(positions.Length!=mesh.vertexCount)throw new Exception("Incomplete native skin deformation");Write(Path.Combine(request.output_root,"pose-"+pose+"-positions.f32"),w=>{foreach(var v in positions)V3(w,v);});Write(Path.Combine(request.output_root,"pose-"+pose+"-normals.f32"),w=>{foreach(var v in normals)V3(w,v);});Write(Path.Combine(request.output_root,"pose-"+pose+"-tangents.f32"),w=>{foreach(var v in tangents)V4(w,v);});var world=new Matrix4x4[renderer.bones.Length];for(int i=0;i<world.Length;i++)world[i]=renderer.bones[i].localToWorldMatrix;poses.Add(new Pose{name=pose==0?"original-bind-context":"diagnostic-bone-rotation-30deg",mesh_world=renderer.localToWorldMatrix,joint_world=world,vertices=positions.Length});}finally{UnityEngine.Object.DestroyImmediate(baked);}
    }
    renderer.bones[0].localRotation=original;result.probe_poses=poses.ToArray();File.WriteAllText(Path.Combine(request.output_root,"RESULT.json"),JsonUtility.ToJson(result,true));
   }finally{if(instance!=null)UnityEngine.Object.DestroyImmediate(instance);}
  }
 }
}
#endif
