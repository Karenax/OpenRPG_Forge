"""Retain v13 and handle only a verified empty private startup scene."""
from pathlib import Path
import json
import shutil
import hashlib
HERE=Path(__file__).resolve().parent
W=Path('F:/OpenRPG_ImportWork')
P=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
def main():
    previous=W/'detail-native-normal-loop-aov-v13'
    run=json.loads((previous/'RUN-RESULT.v1.json').read_bytes())
    assert run['unity_exit_code']==2 and run['finished_utc']
    failure=(previous/'NATIVE.v1.json.failure.txt').read_text('utf-8-sig')
    assert 'Cannot create a new scene additively with an untitled scene unsaved' in failure
    old=HERE/'candidates/SourceDetailAovV13Probe.cs';new=HERE/'candidates/SourceDetailAovV14Probe.cs'
    assert not new.exists()
    text=old.read_text('utf-8').replace('V13','V14')
    needle='activeBefore=SceneManager.GetActiveScene();scene=EditorSceneManager.NewScene'
    assert text.count(needle)==1
    text=text.replace(needle,'''activeBefore=SceneManager.GetActiveScene();
   if(string.IsNullOrEmpty(activeBefore.path)) {
    if(activeBefore.rootCount!=0)throw new Exception("Private startup scene is not empty; preserve it");
    const string startup="Assets/OpenRPGDiagnosticEmptyStartupV14.unity";
    if(File.Exists(startup)||!EditorSceneManager.SaveScene(activeBefore,startup))throw new Exception("Private empty startup preservation refused");
   }
   scene=EditorSceneManager.NewScene''')
    new.write_text(text,'utf-8')
    controller=HERE/'queue_detail_native_aov_v14.py';assert not controller.exists()
    controller.write_text((HERE/'queue_detail_native_aov_v13.py').read_text('utf-8').replace('V13','V14').replace('v13','v14'),'utf-8')
    private=P/'Assets/Editor/SourceDetailAovV13Probe.cs';assert private.read_bytes()==old.read_bytes()
    retained=previous/'startup-scene-refusal-preserved-private';retained.mkdir()
    dest=retained/private.name;assert not dest.exists();shutil.move(str(private),str(dest));assert dest.read_bytes()==old.read_bytes()
    receipt=dict(status='private_empty_scene_guard_added_prior_refusal_preserved',
                 new_helper_sha256=hashlib.sha256(new.read_bytes()).hexdigest(),new_helper=str(new),
                 nonempty_startup_scene_refused=True,full_import=False)
    (retained/'REPAIR.v1.json').write_text(json.dumps(receipt,indent=2),'utf-8');print(json.dumps(receipt))
if __name__=='__main__':main()
