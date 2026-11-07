"""Retain exact SDK material diffs against the immutable private source clone."""
from pathlib import Path
from datetime import datetime,timezone
import json,hashlib,difflib
W=Path('F:/OpenRPG_ImportWork'); O=W/'medieval-torch-private-material-audit-v148'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    assert not O.exists()
    runpath=W/'medieval-torch-native-prefab-import-v137/RUN-RESULT.v1.json'
    preppath=W/'medieval-torch-native-prefab-import-v137/PREPARED.v1.json'
    run=read(runpath); prep=read(preppath)
    assert run['unity_exit_code']==0 and run['native_import_verified']
    clone=W/'medieval-torch-private-prefab-clone-v134/private-source-fragment'
    project=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project/Assets/OpenRPGTorchOriginalVariantV134'
    pins={str(runpath):sha(runpath),str(preppath):sha(preppath),str(Path(__file__)):sha(Path(__file__))}
    rows=[]
    for change in run['private_sdk_import_changed_files']:
        after=Path(change['path']); relative=after.relative_to(project); before=clone/relative
        assert before.suffix==after.suffix=='.mat'
        assert sha(before)==change['before'] and sha(after)==change['after']
        a=before.read_text('utf-8-sig').replace('\r\n','\n'); b=after.read_text('utf-8-sig').replace('\r\n','\n')
        rows.append(dict(path=str(after),before_path=str(before),before_sha256=change['before'],after_sha256=change['after'],exact_diff=''.join(difflib.unified_diff(a.splitlines(True),b.splitlines(True),fromfile='immutable-private-before',tofile='sdk-private-after')),classified=False))
        pins[str(before)]=sha(before);pins[str(after)]=sha(after)
    changed={r['path'] for r in rows}
    for name,h in prep['input_pins'].items():
        p=Path(name)
        if name not in changed:assert sha(p)==h, 'Unexpected input mutation: '+name
        pins[name]=sha(p)
    assert len(rows)==3
    O.mkdir(); result=dict(status='three_private_material_diffs_retained_original_input_pins_rehashed',finished_utc=datetime.now(timezone.utc).isoformat(),material_changes=rows,original_sources_unchanged=True,private_changes_classified=False,material_rendering_verified=False,full_import=False,input_pins=pins)
    (O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps(rows,indent=2))
if __name__=='__main__':main()
