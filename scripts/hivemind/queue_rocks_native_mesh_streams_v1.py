"""Capture original editor mesh streams without a readability reimport."""
from pathlib import Path
import datetime
import os
import subprocess
import sys
import queue_rocks_native_mesh_counter_v1 as preparation

base = preparation.base
W = preparation.W
PROJECT = preparation.PROJECT
ROOT = W / 'rocks-native-mesh-streams-v1'
HELPER = PROJECT / 'Assets/Editor/RocksNativeMeshStreamsV1.cs'
PREPARATION_DEPENDENCY = Path(preparation.__file__).resolve()
CODE = r'''
using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEditor;using UnityEngine;using UnityEngine.Rendering;using Unity.Collections;
namespace OpenRPGRocksNativeMeshStreamsV1 {
public static class Capture {
[Serializable]public class Row{public string mesh_guid;public string mesh_file_id;public string mesh_path;public string directory;public int vertices;public int submeshes;public string[] streams;}
[Serializable]public class Result{public string unity_version;public string actual_editor_api;public Row[] meshes;public bool import_settings_changed;public bool prefab_instantiated;}
static void Write3(string path,NativeArray<Vector3> values){using(var w=new BinaryWriter(File.Create(path))){foreach(var v in values){w.Write(v.x);w.Write(v.y);w.Write(v.z);}}}
static void Write4(string path,NativeArray<Vector4> values){using(var w=new BinaryWriter(File.Create(path))){foreach(var v in values){w.Write(v.x);w.Write(v.y);w.Write(v.z);w.Write(v.w);}}}
public static void Export(){
 string output=Environment.GetEnvironmentVariable("OPENRPG_MESH_STREAMS_OUTPUT");if(string.IsNullOrEmpty(output))throw new Exception("Explicit output required");
 var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hivemind/NativeAmericanVillage/HDRP(Default)/Art/Prefabs/SM_rocks_002.prefab");if(prefab==null)throw new Exception("Original prefab absent");var rows=new List<Row>();var seen=new HashSet<string>();
 foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true)){var mesh=filter.sharedMesh;if(mesh==null)throw new Exception("Mesh absent");string guid;long file;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out guid,out file))throw new Exception("Exact mesh identity absent");string key=guid+"-"+file.ToString();if(!seen.Add(key))continue;
  string directory=Path.Combine(output,key);Directory.CreateDirectory(directory);var names=new List<string>();
  using(var snapshot=MeshUtility.AcquireReadOnlyMeshData(mesh)){var data=snapshot[0];int n=data.vertexCount;if(n!=mesh.vertexCount)throw new Exception("Native snapshot count mismatch");
   using(var v=new NativeArray<Vector3>(n,Allocator.Temp)){data.GetVertices(v);Write3(Path.Combine(directory,"positions.f32"),v);names.Add("positions.f32");}
   foreach(var attribute in new[]{VertexAttribute.Normal,VertexAttribute.Tangent,VertexAttribute.Color}){
    string name=attribute==VertexAttribute.Normal?"normals.f32":attribute==VertexAttribute.Tangent?"tangents.f32":"colors.f32";string path=Path.Combine(directory,name);names.Add(name);
    if(!mesh.HasVertexAttribute(attribute)){File.WriteAllBytes(path,new byte[0]);continue;}
    if(attribute==VertexAttribute.Normal){using(var v=new NativeArray<Vector3>(n,Allocator.Temp)){data.GetNormals(v);Write3(path,v);}}
    else if(attribute==VertexAttribute.Tangent){using(var v=new NativeArray<Vector4>(n,Allocator.Temp)){data.GetTangents(v);Write4(path,v);}}
    else{using(var v=new NativeArray<Color>(n,Allocator.Temp)){data.GetColors(v);using(var writer=new BinaryWriter(File.Create(path))){foreach(var color in v){writer.Write(color.r);writer.Write(color.g);writer.Write(color.b);writer.Write(color.a);}}}}
   }
   for(int channel=0;channel<8;channel++){string name="uv"+channel+".f32";string path=Path.Combine(directory,name);names.Add(name);var attribute=(VertexAttribute)((int)VertexAttribute.TexCoord0+channel);
    if(!mesh.HasVertexAttribute(attribute)){File.WriteAllBytes(path,new byte[0]);continue;}using(var v=new NativeArray<Vector4>(n,Allocator.Temp)){data.GetUVs(channel,v);Write4(path,v);}
   }
   for(int i=0;i<data.subMeshCount;i++){var descriptor=data.GetSubMesh(i);if(descriptor.topology!=MeshTopology.Triangles)throw new Exception("Unqualified topology");string name="submesh-"+i+".i32";names.Add(name);using(var indices=new NativeArray<int>(descriptor.indexCount,Allocator.Temp)){data.GetIndices(indices,i,true);using(var writer=new BinaryWriter(File.Create(Path.Combine(directory,name)))){foreach(int index in indices)writer.Write(index);}}}
  }
  rows.Add(new Row{mesh_guid=guid,mesh_file_id=file.ToString(),mesh_path=AssetDatabase.GetAssetPath(mesh),directory=directory,vertices=mesh.vertexCount,submeshes=mesh.subMeshCount,streams=names.ToArray()});
 }
 File.WriteAllText(Path.Combine(output,"NATIVE-RESULT.v1.json"),JsonUtility.ToJson(new Result{unity_version=Application.unityVersion,actual_editor_api="UnityEditor.MeshUtility.AcquireReadOnlyMeshData(Mesh)",meshes=rows.ToArray(),import_settings_changed=false,prefab_instantiated=false},true));
}
}}
'''


def prepare():
    preparation.ROOT = ROOT
    preparation.HELPER = HELPER
    preparation.CODE = CODE
    preparation.__file__ = str(Path(__file__).resolve())
    prior_save = base.save
    def save_with_dependency(name, value):
        if name == 'PREPARED.v1.json':
            value['input_pins'][str(PREPARATION_DEPENDENCY)] = base.sha(PREPARATION_DEPENDENCY)
            value['geometry_capture_requested'] = True
        prior_save(name, value)
    base.save = save_with_dependency
    preparation.prepare()


def run():
    state = dict(status='capturing_native_editor_mesh_streams_without_reimport', full_import=False)
    try:
        pins = base.read(ROOT / 'PREPARED.v1.json')['input_pins']
        for path, digest in pins.items():
            assert base.sha(path) == digest, path
        env = os.environ.copy()
        env.update(TEMP=str(ROOT), TMP=str(ROOT), OPENRPG_MESH_STREAMS_OUTPUT=str(ROOT),
                   UPM_CACHE_ROOT=str(W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4/upm-cache'))
        with (ROOT / 'unity.stdout.log').open('xb') as out, (ROOT / 'unity.stderr.log').open('xb') as err:
            child = subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe', '-batchmode', '-nographics', '-quit',
                                      '-projectPath', str(PROJECT), '-executeMethod', 'OpenRPGRocksNativeMeshStreamsV1.Capture.Export',
                                      '-logFile', str(ROOT / 'unity.log')], cwd=ROOT, env=env, stdout=out, stderr=err,
                                     creationflags=subprocess.CREATE_NO_WINDOW)
            state['unity_pid'] = child.pid
            preparation.save('RUN-RESULT.v1.json', state)
            state['actual_unity_exit_code'] = child.wait()
        assert state['actual_unity_exit_code'] == 0, 'Native mesh stream capture refused'
        native = base.read(ROOT / 'NATIVE-RESULT.v1.json')
        assert not native['import_settings_changed'] and not native['prefab_instantiated']
        for path, digest in pins.items():
            assert base.sha(path) == digest, path
        state.update(status='native_mesh_streams_captured_reopen_pending', meshes=len(native['meshes']),
                     native_result_sha256=base.sha(ROOT / 'NATIVE-RESULT.v1.json'), input_pins_unchanged=True)
    except Exception as error:
        state.update(status='refused_or_unverified', error=repr(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    preparation.save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    preparation.ROOT = ROOT
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
