"""Measure native convex surface planes; reconstruction and qualification are separate steps."""
from pathlib import Path
S=Path(__file__).resolve().parent
source_helper=S/'candidates/HouseComponentStateV684.cs'
CODE=source_helper.read_text('utf-8-sig').replace('V684','V701')
CODE=CODE.replace('public bool convex;','public FaceProbe[] native_face_probes;public bool convex;')
helper=r'''
[Serializable]public class FaceProbe{public string family;public int sample;public float[] origin;public float[] direction;public float max_distance;public bool hit;public float[] point;public float[] normal;public float distance;}
static FaceProbe[] ProbePlanes(MeshCollider collider){
 if(!collider.convex)return new FaceProbe[0];
 var mesh=collider.sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;
 var center=mesh.bounds.center;float radius=mesh.bounds.extents.magnitude;if(!float.IsFinite(radius)||radius<=0)throw new Exception("Invalid convex bounds");
 var rows=new List<FaceProbe>();
 Action<string,int,Vector3,Vector3> probe=(family,sample,target,normal)=>{
  var origin=collider.transform.TransformPoint(target+normal*radius*4);var direction=collider.transform.TransformDirection(-normal).normalized;
  float maximum=collider.transform.TransformVector(normal*radius*8).magnitude;
  RaycastHit hit;bool found=collider.Raycast(new Ray(origin,direction),out hit,maximum);
  rows.Add(new FaceProbe{family=family,sample=sample,origin=Vec(origin),direction=Vec(direction),max_distance=maximum,hit=found,point=found?Vec(hit.point):new float[0],normal=found?Vec(hit.normal):new float[0],distance=found?hit.distance:0});
 };
 for(int t=0;t<triangles.Length;t+=3){
  var a=vertices[triangles[t]];var b=vertices[triangles[t+1]];var c=vertices[triangles[t+2]];var normal=Vector3.Cross(b-a,c-a);
  if(normal.sqrMagnitude<=1e-20f)continue;normal.Normalize();var target=(a+b+c)/3f;if(Vector3.Dot(normal,target-center)<0)normal=-normal;
  probe("source_triangle_face",t/3,target,normal);
 }
 for(int s=0;s<1024;s++){
  double y=1.0-2.0*(s+0.5)/1024.0;double r=Math.Sqrt(Math.Max(0,1-y*y));double angle=s*Math.PI*(3-Math.Sqrt(5));
  var normal=new Vector3((float)(r*Math.Cos(angle)),(float)y,(float)(r*Math.Sin(angle))).normalized;
  probe("fixed_fibonacci_direction",s,center,normal);
 }
 return rows.ToArray();
}
'''
needle='static float[] Vec(Vector3 v)'
assert CODE.count(needle)==1
CODE=CODE.replace(needle,helper+needle)
needle='new Box{convex=source.convex,'
assert CODE.count(needle)==1
CODE=CODE.replace(needle,'new Box{native_face_probes=ProbePlanes(box),convex=source.convex,')
parent=S/'run_house_component_state_v672.py'
recipe=parent.read_text('utf-8-sig').replace('v672','v701').replace('V672','V701')
recipe=recipe.replace('house-wire-native-storage-v668/HELPER-OWNERSHIP.v1.json',
                      'house-native-component-state-v700/HELPER-OWNERSHIP.v1.json').replace("len(audit['helpers'])==37","len(audit['helpers'])==49")
anchor="exec(compile(code,str(BASE)+'#house-owned-component-capture','exec'),globals())"
addition='''
needle="helper=PROJECT/'Assets/Editor/HouseComponentStateV701.cs'"
assert code.count(needle)==1
code=code.replace(needle,"code=OWNED_PLANE_CODE\\n"+needle)
code=code.replace('OpenRPGHouseComponentStateV701.Capture.Export','OpenRPGHouseNativeMeshMetricV701.Capture.Export')
code=code.replace('OPENRPG_COMPONENT_STATE_OUTPUT','OPENRPG_BOX_METRIC_OUTPUT')
old="assert len(native['prefabs'])==24 and sum(len(n['renderers']) for p in native['prefabs'] for n in p['nodes'])==24\\n    assert sum(len(n['colliders']) for p in native['prefabs'] for n in p['nodes'])==24"
assert code.count(old)==1
code=code.replace(old,"assert len(native['boxes'])==19 and native['original_components_unchanged'] and not native['purchased_scripts_executed'] and not native['physical_simulation_stepped']\\n    assert all(len(b['native_face_probes'])>=1024 if b['convex'] else not b['native_face_probes'] for b in native['boxes'])")
code=code.replace('prefabs_instantiated=False','prefabs_instantiated=True')
code=code.replace('twentyfour_house_prefab_component_states_captured_review_pending','native_convex_surface_plane_observations_captured')
code=code.replace('pins={str(p):sha(p) for p in [Path(__file__),parent,bridge_path,closure_path]}','pins={str(p):sha(p) for p in [Path(__file__),parent,bridge_path,closure_path,source_helper]}')
'''
assert recipe.count(anchor)==1
recipe=recipe.replace(anchor,addition+anchor)
OWNED_PLANE_CODE=CODE
exec(compile(recipe,str(parent)+'#native-surface-planes-v701','exec'),globals())
