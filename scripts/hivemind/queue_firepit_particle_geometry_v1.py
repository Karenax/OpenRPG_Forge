"""Measure native billboards/atlas UVs for all verified times and three cameras."""
from pathlib import Path
import ast
import copy
import datetime
import hashlib
import importlib.util
import json
import os
import shutil
import subprocess
import sys

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'unity-delta-firepit-particle-geometry-v1'
PROJECT=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
HERE=Path(__file__).resolve().parent
BASE=HERE/'queue_delta_firepit_effect_materials_v1.py'
spec=importlib.util.spec_from_file_location('material_job',BASE)
job=importlib.util.module_from_spec(spec);spec.loader.exec_module(job)
job.ROOT=ROOT
def sha(p):return job.sha(p)
def read(p):return job.read(p)
def save(name,value):return job.save(name,value)

def helper_source():
    original=HERE/'candidates/FirePitParticleBehavior.cs'
    assert sha(original)=='58c02f9c6a9f6ee2e02e135a5bd7b18a705954e8035dc00fd966c69ac3a93f16'
    text=original.read_text('utf-8-sig').replace('OpenRPGFirePitParticleBehavior','OpenRPGFirePitParticleGeometry')
    needle='[Serializable] public class Frame {';assert text.count(needle)==1
    text=text.replace(needle,'[Serializable] public class Frame {public Geometry[] geometry;')
    needle='rows.Add(new Frame{seed=seed,';assert text.count(needle)==1
    text=text.replace(needle,'rows.Add(new Frame{geometry=BakeGeometry(ps),seed=seed,')
    needle=' public static void Export(){';assert text.count(needle)==1
    addition='''
 [Serializable] public class Geometry {public int fixture,vertices,submeshes;public string render_mode,alignment;public bool use_transform=false;public Vector3[] positions,normals;public Vector4[] tangents,uv0,uv1;public Color32[] colors;public int[] indices;public Matrix4x4 camera_local_to_world,world_to_camera,projection,particle_local_to_world;public float fov,aspect,near,far,orthographic_size;public bool orthographic;}
 static Geometry[] BakeGeometry(ParticleSystem ps){
  var renderer=ps.GetComponent<ParticleSystemRenderer>();var rows=new List<Geometry>();
  var points=new[]{new Vector3(0,2,-5),new Vector3(-5,2,0),new Vector3(0,5,-2)};
  string before=EditorJsonUtility.ToJson(ps,true),beforeRenderer=EditorJsonUtility.ToJson(renderer,true);int count=ps.particleCount;float time=ps.time;
  for(int fixture=0;fixture<points.Length;fixture++){
   var cameraObject=new GameObject("OpenRPG native measurement camera");cameraObject.hideFlags=HideFlags.HideAndDontSave;
   var mesh=new Mesh();mesh.hideFlags=HideFlags.HideAndDontSave;
   try{
    var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.transform.position=points[fixture];camera.transform.LookAt(new Vector3(0,0.5f,0));
    camera.fieldOfView=60;camera.aspect=1;camera.nearClipPlane=0.1f;camera.farClipPlane=100;camera.orthographic=fixture==2;camera.orthographicSize=3;
    renderer.BakeMesh(mesh,camera,false);
    var uv0=new List<Vector4>();var uv1=new List<Vector4>();mesh.GetUVs(0,uv0);mesh.GetUVs(1,uv1);
    rows.Add(new Geometry{fixture=fixture,vertices=mesh.vertexCount,submeshes=mesh.subMeshCount,render_mode=renderer.renderMode.ToString(),alignment=renderer.alignment.ToString(),positions=mesh.vertices,normals=mesh.normals,tangents=mesh.tangents,colors=mesh.colors32,uv0=uv0.ToArray(),uv1=uv1.ToArray(),indices=mesh.triangles,camera_local_to_world=camera.transform.localToWorldMatrix,world_to_camera=camera.worldToCameraMatrix,projection=camera.projectionMatrix,particle_local_to_world=ps.transform.localToWorldMatrix,fov=camera.fieldOfView,aspect=camera.aspect,near=camera.nearClipPlane,far=camera.farClipPlane,orthographic_size=camera.orthographicSize,orthographic=camera.orthographic});
   }finally{UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(cameraObject);}
  }
  if(before!=EditorJsonUtility.ToJson(ps,true)||beforeRenderer!=EditorJsonUtility.ToJson(renderer,true)||count!=ps.particleCount||time!=ps.time)throw new Exception("Baking changed native particle state");
  return rows.ToArray();
 }
'''
    return text.replace(needle,addition+needle)

def prepare():
    assert not ROOT.exists();job.idle()
    prior=W/'unity-delta-firepit-particle-behavior-v7'
    result=read(prior/'RUN-RESULT.v1.json');assert result['frames_verified']==112 and result['native_behavior_verified']
    proof=read(prior/'INDEPENDENT-REOPEN.v1.json');job.check(proof['input_pins'])
    assert sha(prior/'INDEPENDENT-REOPEN.v1.json')==result['independent_receipt_sha256']
    destination=PROJECT/'Assets/Editor/OpenRPGFirePitParticleGeometry.cs'
    assert not destination.exists() and not Path(str(destination)+'.meta').exists()
    ROOT.mkdir();(ROOT/'temp').mkdir()
    code=helper_source();(ROOT/'OpenRPGFirePitParticleGeometry.cs').write_text(code,'utf-8')
    shutil.copyfile(ROOT/'OpenRPGFirePitParticleGeometry.cs',destination)
    manifest=read(W/'unity-delta-firepit-particle-behavior-v4/manifest.json')
    manifest['output']=str(ROOT/'GEOMETRY.v1.json');save('manifest.json',manifest)
    pins={str(p):sha(p) for p in (PROJECT/'Assets').rglob('*') if p.is_file()}
    pins.update(proof['input_pins'])
    for p in [BASE,HERE/'candidates/FirePitParticleBehavior.cs',Path(__file__).resolve(),
              ROOT/'OpenRPGFirePitParticleGeometry.cs',ROOT/'manifest.json',
              prior/'BEHAVIOR.v1.json',prior/'INDEPENDENT-REOPEN.v1.json',
              PROJECT/'Packages/manifest.json',PROJECT/'Packages/packages-lock.json']:
        pins[str(p)]=sha(p)
    job.check(pins)
    save('PREPARED.v1.json',dict(input_pins=pins,expected_effects=2,expected_frames=112,
        expected_geometry_observations=336,source_protocol_retained=True,
        fixture_cameras_only=True,new_authored_3d_models_created=False,full_import=False))
    with (ROOT/'controller.log').open('xb') as log:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,
            stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',dict(controller_pid=child.pid));print(json.dumps(dict(controller_pid=child.pid,native_frames=112,geometry_observations=336,full_import=False)))

def verify(report,prepared):
    baseline=read(W/'unity-delta-firepit-particle-behavior-v7/BEHAVIOR.v1.json')
    assert len(report['cases'])==2 and not report['full_import']
    observations=0;vertices=0
    for case in report['cases']:
        original=next(c for c in baseline['cases'] if c['node_path']==case['node_path'])
        assert len(case['rows'])==56
        stripped=copy.deepcopy(case)
        pairs={}
        for row in stripped['rows']:
            geometry=row.pop('geometry');assert len(geometry)==3
            assert job.read(W/'unity-delta-firepit-particle-behavior-v4/manifest.json')['repetitions']==2
            for fixture,g in enumerate(geometry):
                assert g['fixture']==fixture and not g['use_transform']
                assert g['orthographic']==(fixture==2) and g['aspect']==1 and g['fov']==60
                assert len(g['positions'])==g['vertices'] and len(g['indices'])%3==0
                assert all(type(i) is int and 0<=i<g['vertices'] for i in g['indices'])
                assert len(g['colors']) in (0,g['vertices']) and len(g['uv0']) in (0,g['vertices'])
                if row['count']>0:assert g['vertices']>0 and len(g['colors'])==len(g['uv0'])==g['vertices']
                for group in ['positions','normals','tangents','uv0','uv1']:
                    assert all(all(__import__('math').isfinite(v) for v in item.values()) for item in g[group])
                key=(row['seed'],row['requested_time'],fixture)
                if key in pairs:assert pairs[key]==g,'Native geometry repeat differs'
                else:pairs[key]=g
                observations+=1;vertices+=g['vertices']
        assert stripped==original,'Particle behavior differs from the prior independent native reference'
    assert observations==336
    job.check(prepared['input_pins'])
    return dict(geometry_observations_verified=observations,vertex_observations=vertices,
        native_geometry_repeated_exactly=True,prior_112_particle_frames_unchanged=True)

def run():
    state=dict(status='preparing_native_billboard_and_atlas_geometry_capture',native_geometry_verified=False,
        gpu_rendered=False,studio_verified=False,full_import=False)
    save('RUN-RESULT.v1.json',state);broker=None;globals_=None
    try:
        prepared=read(ROOT/'PREPARED.v1.json');job.check(prepared['input_pins']);job.idle()
        broker_helper=HERE/'queue_delta_firepit_particle_behavior_v4.py'
        tree=ast.parse(broker_helper.read_text('utf-8-sig'))
        selected=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name in {'sha','read','write_state','stop_broker','start_broker'}]
        globals_=dict(ROOT=ROOT,W=W,UPM=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/Server/UnityPackageManager.exe'),LOG_HANDLES=[])
        exec(compile(ast.Module(body=selected,type_ignores=[]),str(broker_helper),'exec'),globals_)
        broker,label=globals_['start_broker'](state)
        env=os.environ.copy();env.update(TEMP=str(ROOT/'temp'),TMP=str(ROOT/'temp'),UPM_CACHE_ROOT=str(W/'unity-package-cache'))
        with (ROOT/'unity.stdout.log').open('xb') as out,(ROOT/'unity.stderr.log').open('xb') as err:
            child=subprocess.Popen([str(job.UNITY),'-batchmode','-nographics','-quit','-upmIpcPath',label,
                '-projectPath',str(PROJECT),'-executeMethod','OpenRPGFirePitParticleGeometry.Export',
                '--openrpg-manifest',str(ROOT/'manifest.json'),'-logFile',str(ROOT/'unity.log')],cwd=ROOT,
                env=env,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='capturing_336_native_billboard_and_atlas_observations',unity_pid=child.pid)
            save('RUN-RESULT.v1.json',state);state['unity_exit_code']=child.wait()
        assert state['unity_exit_code']==0,'Native geometry capture refused'
        job.check(prepared['input_pins']);summary=verify(read(ROOT/'GEOMETRY.v1.json'),prepared)
        save('INDEPENDENT-REOPEN.v1.json',dict(input_pins=prepared['input_pins'],
            capture_sha256=sha(ROOT/'GEOMETRY.v1.json'),**summary,
            reference_cameras=3,bake_use_transform=False,fixture_camera_api_reference='https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/particlesystemrenderer/bakemesh',
            coordinate_mapping_to_forge_qualified=False,gpu_rendered=False,runtime_converted=False,full_import=False))
        state.update(status='336_native_billboard_and_atlas_observations_reopened',native_geometry_verified=True,
            **summary,independent_receipt_sha256=sha(ROOT/'INDEPENDENT-REOPEN.v1.json'))
    except Exception as error:
        import traceback
        state.update(status='refused_or_unverified',error=type(error).__name__+': '+str(error),diagnostic_traceback=traceback.format_exc())
    finally:
        if broker is not None:globals_['stop_broker'](broker,state)
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)

if __name__=='__main__':run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
