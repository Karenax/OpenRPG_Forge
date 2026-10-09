"""Preserve the v12 compile refusal and repair only the diagnostic namespace."""
from pathlib import Path
import json
import hashlib
import shutil

HERE=Path(__file__).resolve().parent
W=Path('F:/OpenRPG_ImportWork')
P=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
def main():
    run=json.loads((W/'detail-native-normal-loop-aov-v12/RUN-RESULT.v1.json').read_bytes())
    assert run['unity_exit_code']==1 and run['finished_utc']
    old=HERE/'candidates/SourceDetailAovV12Probe.cs';new=HERE/'candidates/SourceDetailAovV13Probe.cs'
    assert not new.exists()
    text=old.read_text('utf-8');assert text.count('using UnityEngine.Rendering.HighDefinition;')==1
    text=text.replace('using UnityEngine.Rendering.HighDefinition;',
                      'using UnityEngine.Rendering.HighDefinition;\nusing UnityEngine.Rendering.HighDefinition.Attributes;')
    text=text.replace('V12','V13');new.write_text(text,'utf-8')
    oldqueue=HERE/'queue_detail_native_aov_v12.py';newqueue=HERE/'queue_detail_native_aov_v13.py'
    assert not newqueue.exists()
    queue=oldqueue.read_text('utf-8').replace('V12','V13').replace('v12','v13')
    newqueue.write_text(queue,'utf-8')
    private=P/'Assets/Editor/SourceDetailAovV12Probe.cs'
    assert private.read_bytes()==old.read_bytes()
    retained=W/'detail-native-normal-loop-aov-v12/compilation-refusal-preserved-private'
    retained.mkdir();destination=retained/private.name
    assert not destination.exists();shutil.move(str(private),str(destination))
    assert destination.read_bytes()==old.read_bytes()
    receipt=dict(status='diagnostic_namespace_repaired_prior_refusal_preserved',
                 private_v12_preserved=str(destination),new_helper=str(new),
                 new_helper_sha256=hashlib.sha256(new.read_bytes()).hexdigest(),
                 new_controller=str(newqueue),full_import=False,native_default_rg_qualified=False)
    (retained/'REPAIR.v1.json').write_text(json.dumps(receipt,indent=2),'utf-8')
    print(json.dumps(receipt))
if __name__=='__main__':main()
