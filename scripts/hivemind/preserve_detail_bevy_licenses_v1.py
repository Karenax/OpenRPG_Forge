from pathlib import Path
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');OUT=W/'detail-bevy-derived-licenses-v1'
def main():
    assert not OUT.exists();OUT.mkdir()
    source=W/'detail-native-default-kernel-v1/LICENSE.Unity.md';raw=source.read_bytes();assert raw
    rows=[]
    for name in ['detail-native-default-bevy-vertex-v2','detail-native-default-bevy-compositions-v2']:
        target=W/name/'LICENSE.Unity.md';assert not target.exists();target.write_bytes(raw);assert target.read_bytes()==raw
        rows.append(dict(path=str(target),sha256=hashlib.sha256(raw).hexdigest()))
    (OUT/'RESULT.v1.json').write_text(json.dumps(dict(status='exact_unity_helper_license_preserved_with_bevy_derivatives',
        source=str(source),source_sha256=hashlib.sha256(raw).hexdigest(),copies=rows,full_import=False),indent=2),'utf-8')
    print(json.dumps(dict(license_copies_verified=len(rows))))
if __name__=='__main__':main()
