from pathlib import Path
import hashlib,json,difflib
W=Path('F:/OpenRPG_ImportWork');P=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
OUT=W/'detail-aov-private-material-change-inspection-v2'
def sha(raw):return hashlib.sha256(raw).hexdigest()
def main():
    assert not OUT.exists();OUT.mkdir()
    prior=W/'detail-default-native-gain-v11'
    pins=json.loads((prior/'PREPARED.v1.json').read_bytes())['input_pins']
    retained={sha(p.read_bytes()):p for p in (prior/'preserved-private-context').rglob('private.mat')}
    rows=[]
    for name,expected in pins.items():
        path=Path(name);raw=path.read_bytes()
        if sha(raw)==expected or path==P/'ProjectSettings/ProjectSettings.asset':continue
        assert path.resolve().is_relative_to(P.resolve()) and path.suffix=='.mat',name
        before=retained[expected].read_bytes();folder=OUT/f'{len(rows):03}';folder.mkdir()
        (folder/'before.mat').write_bytes(before);(folder/'after.mat').write_bytes(raw)
        diff=''.join(difflib.unified_diff(before.decode('utf-8').splitlines(True),raw.decode('utf-8').splitlines(True)))
        (folder/'difference.patch').write_text(diff,'utf-8')
        row=dict(path=name,before=str(folder/'before.mat'),after=str(folder/'after.mat'),before_sha256=expected,after_sha256=sha(raw),diff=str(folder/'difference.patch'))
        rows.append(row);print(path.name);print(diff[:5000])
    (OUT/'RESULT.v1.json').write_text(json.dumps(dict(status='private_material_differences_preserved_not_admitted',rows=rows,full_import=False),indent=2),'utf-8')
if __name__=='__main__':main()
