"""Read native prefab mesh identities/counters without changing import settings."""
from pathlib import Path
import datetime
import json
import os
import subprocess
import sys
import queue_rocks_firepit_native_component_state_v1 as base

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'rocks-native-mesh-counter-v1'
PROJECT = base.PROJECT
HELPER = PROJECT / 'Assets/Editor/RocksNativeMeshCounterV1.cs'
DEPENDENCY = Path(base.__file__).resolve()
CODE = r'''
using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEditor;using UnityEngine;
namespace OpenRPGRocksNativeMeshCounterV1 {
public static class Capture {
[Serializable]public class Row {public string node_path;public string renderer_guid;public string renderer_file_id;public string mesh_guid;public string mesh_file_id;public string mesh_path;public string mesh_name;public int vertices;public int submeshes;public long[] index_counts;public string[] topologies;public bool readable;public string importer_json;public string[] vertex_formats;}
[Serializable]public class Result {public string unity_version;public Row[] rows;public bool import_settings_changed;public bool prefab_instantiated;public bool geometry_streams_captured;}
static string PathOf(Transform t,Transform root){if(t==root)return "root";return PathOf(t.parent,root)+"/"+t.GetSiblingIndex().ToString("D4");}
public static void Export(){
 string output=Environment.GetEnvironmentVariable("OPENRPG_MESH_COUNTER_OUTPUT");if(string.IsNullOrEmpty(output))throw new Exception("Explicit output required");
 string path="Assets/Hivemind/NativeAmericanVillage/HDRP(Default)/Art/Prefabs/SM_rocks_002.prefab";
 var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab==null)throw new Exception("Original prefab absent");var rows=new List<Row>();
 foreach(var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true)){
  var filter=renderer.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)throw new Exception("Mesh binding absent");var mesh=filter.sharedMesh;
  string rg,mg;long rf,mf;if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer,out rg,out rf)||!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out mg,out mf))throw new Exception("Exact identity unavailable");
  var counts=new long[mesh.subMeshCount];var topology=new string[mesh.subMeshCount];for(int i=0;i<mesh.subMeshCount;i++){counts[i]=(long)mesh.GetIndexCount(i);topology[i]=mesh.GetTopology(i).ToString();}
  string mp=AssetDatabase.GetAssetPath(mesh);var importer=AssetImporter.GetAtPath(mp);if(!(importer is ModelImporter))throw new Exception("Model importer expected");
  rows.Add(new Row{node_path=PathOf(renderer.transform,prefab.transform),renderer_guid=rg,renderer_file_id=rf.ToString(),mesh_guid=mg,mesh_file_id=mf.ToString(),mesh_path=mp,mesh_name=mesh.name,vertices=mesh.vertexCount,submeshes=mesh.subMeshCount,index_counts=counts,topologies=topology,readable=mesh.isReadable,importer_json=EditorJsonUtility.ToJson(importer),vertex_formats=mesh.GetVertexAttributes().Select(v=>v.attribute+":"+v.format+":"+v.dimension+":"+v.stream).ToArray()});
 }
 File.WriteAllText(Path.Combine(output,"NATIVE-RESULT.v1.json"),JsonUtility.ToJson(new Result{unity_version=Application.unityVersion,rows=rows.ToArray(),import_settings_changed=false,prefab_instantiated=false,geometry_streams_captured=false},true));
}
}}
'''


def save(name, value):
    temp = ROOT / (name + '.tmp')
    temp.write_text(json.dumps(value, indent=2), 'utf-8')
    temp.replace(ROOT / name)


def prepare():
    source = W / 'hivemind-run/native-american-village/source'
    audit = base.read(W / 'rocks-lod-geometry-discrepancy-source-audit-v1/RESULT.v1.json')
    additional = {}
    for pairs in audit['meshes'].values():
        for pair in pairs:
            for key in ('original', 'private'):
                path = pair[key]
                assert base.sha(path) == pair['sha256'], path
                additional[path] = pair['sha256']
    additional[str(DEPENDENCY)] = base.sha(DEPENDENCY)
    base.ROOT = ROOT
    base.HELPER = HELPER
    base.CODE = CODE
    base.__file__ = str(Path(__file__).resolve())
    original_save = base.save
    def prepared_save(name, value):
        if name == 'PREPARED.v1.json':
            value['input_pins'].update(additional)
            value.update(import_settings_changed=False, geometry_streams_captured=False)
        original_save(name, value)
    base.save = prepared_save
    base.prepare()


def run():
    state = dict(status='capturing_native_mesh_counters_and_importer_settings', full_import=False)
    try:
        pins = base.read(ROOT / 'PREPARED.v1.json')['input_pins']
        for path, digest in pins.items():
            assert base.sha(path) == digest, path
        env = os.environ.copy()
        env.update(TEMP=str(ROOT), TMP=str(ROOT), OPENRPG_MESH_COUNTER_OUTPUT=str(ROOT),
                   UPM_CACHE_ROOT=str(W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4/upm-cache'))
        with (ROOT / 'unity.stdout.log').open('xb') as out, (ROOT / 'unity.stderr.log').open('xb') as err:
            child = subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe', '-batchmode', '-nographics', '-quit',
                                      '-projectPath', str(PROJECT), '-executeMethod', 'OpenRPGRocksNativeMeshCounterV1.Capture.Export',
                                      '-logFile', str(ROOT / 'unity.log')], cwd=ROOT, env=env, stdout=out, stderr=err,
                                     creationflags=subprocess.CREATE_NO_WINDOW)
            state['unity_pid'] = child.pid
            save('RUN-RESULT.v1.json', state)
            state['actual_unity_exit_code'] = child.wait()
        assert state['actual_unity_exit_code'] == 0, 'Native mesh counter capture refused'
        native = base.read(ROOT / 'NATIVE-RESULT.v1.json')
        assert len(native['rows']) == 57 and not native['import_settings_changed'] and not native['prefab_instantiated']
        for path, digest in pins.items():
            assert base.sha(path) == digest, path
        state.update(status='native_mesh_counters_captured_reopen_pending', renderers=57,
                     native_result_sha256=base.sha(ROOT / 'NATIVE-RESULT.v1.json'), input_pins_unchanged=True)
    except Exception as error:
        state.update(status='refused_or_unverified', error=repr(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)


if __name__ == '__main__':
    run() if len(sys.argv) > 1 and sys.argv[1] == 'run' else prepare()
