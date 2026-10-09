"""Accept only a byte-proven HDRP metadata version 12 to 13 transition."""
from pathlib import Path
import hashlib
import json
W=Path('F:/OpenRPG_ImportWork');P=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
OUT=W/'detail-aov-private-hdrp-version-metadata-v1'
def digest(raw):return hashlib.sha256(raw).hexdigest()
def main():
    assert not OUT.exists();OUT.mkdir()
    pins=json.loads((W/'detail-default-native-gain-v11/PREPARED.v1.json').read_bytes())['input_pins']
    rows=[]
    for name,expected in pins.items():
        path=Path(name);raw=path.read_bytes()
        if digest(raw)==expected:continue
        if path==P/'ProjectSettings/ProjectSettings.asset':continue
        assert path.resolve().is_relative_to(P.resolve()) and path.suffix=='.mat',name
        assert raw.count(b'  version: 13\n')==1 and b'guid: da692e001514ec24dbc4cca1949ff7e8' in raw
        before=raw.replace(b'  version: 13\n',b'  version: 12\n')
        assert digest(before)==expected,'Additional material differences: '+name
        prefix=OUT/f'{len(rows):03}';prefix.mkdir();(prefix/'before.mat').write_bytes(before);(prefix/'after.mat').write_bytes(raw)
        rows.append(dict(path=name,before=str(prefix/'before.mat'),after=str(prefix/'after.mat'),
                         before_sha256=expected,after_sha256=digest(raw),changed_field='HDRP internal material-version metadata only',
                         authored_material_payload_byte_unchanged=True))
    assert rows
    result=dict(status='only_private_hdrp_version_metadata_changed_byte_proven',rows=rows,
                source_originals_unchanged=True,full_import=False,whole_migration_qualified=False)
    (OUT/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8');print(json.dumps(dict(materials=len(rows),status=result['status'])))
if __name__=='__main__':main()
