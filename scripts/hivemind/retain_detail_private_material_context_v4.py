from pathlib import Path
import hashlib,json,difflib,re
W=Path('F:/OpenRPG_ImportWork');P=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
SOURCE=W/'hivemind-run/native-american-village/source';OUT=W/'detail-aov-private-material-context-v4'
def sha(raw):return hashlib.sha256(raw).hexdigest()
def main():
    assert not OUT.exists();OUT.mkdir()
    prior=W/'detail-default-native-gain-v11';pins=json.loads((prior/'PREPARED.v1.json').read_bytes())['input_pins']
    retained={sha(p.read_bytes()):p for p in (prior/'preserved-private-context').rglob('*.mat')}
    rows=[];target_count=0
    pattern=r'm_Shader:\s*\{\s*fileID:\s*(-?\d+),\s*guid:\s*([a-f0-9]{32}),\s*type:\s*(\d+)\s*\}'
    for name,expected in pins.items():
        path=Path(name);raw=path.read_bytes()
        if sha(raw)==expected or path==P/'ProjectSettings/ProjectSettings.asset':continue
        assert path.resolve().is_relative_to(P.resolve()) and path.suffix=='.mat',name
        origin=SOURCE/path.relative_to(P)
        if expected in retained:before=retained[expected].read_bytes()
        else:
            before=origin.read_bytes();assert sha(before)==expected,'No exact before snapshot: '+name
        a,b=re.search(pattern,before.decode()),re.search(pattern,raw.decode())
        assert a and b and a.groups()==b.groups(),'Private shader identity changed'
        target='abc00000000012210656512993269337' in Path(str(path)+'.meta').read_text('utf-8-sig')
        if target:
            assert before.count(b'  version: 12\n')==1 and raw==before.replace(b'  version: 12\n',b'  version: 13\n')
            target_count+=1
        folder=OUT/f'{len(rows):03}';folder.mkdir();(folder/'before.mat').write_bytes(before);(folder/'after.mat').write_bytes(raw)
        diff=''.join(difflib.unified_diff(before.decode().splitlines(True),raw.decode().splitlines(True)));(folder/'difference.patch').write_text(diff,'utf-8')
        rows.append(dict(path=name,before=str(folder/'before.mat'),after=str(folder/'after.mat'),before_sha256=expected,
                         after_sha256=sha(raw),target_material=target,target_authored_payload_byte_unchanged=target,
                         shader_identity=list(a.groups()),other_material_context_qualified_as_original=False,diff=str(folder/'difference.patch')))
    assert target_count==1
    result=dict(status='target_metadata_byte_proven_other_private_material_context_retained',rows=rows,
                original_full_scene_context_qualified=False,whole_migration_qualified=False,full_import=False)
    (OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(dict(status=result['status'],changed_private_materials=len(rows),target_metadata_only=target_count)))
if __name__=='__main__':main()
