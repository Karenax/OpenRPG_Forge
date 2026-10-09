"""Keep exact upstream Unity helper licenses alongside the new private derivatives."""
from pathlib import Path
import json
from verify_native_urp_material_snapshot import sha

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'delta-rocks-firepit-program-licenses-v1'

def main():
    assert not ROOT.exists()
    native=W/'native-hdrp-nine-source-programs-v1'
    kernel=native/'house-on-hill-source-shader-kernel-v1/LICENSE.Unity.md'
    pom=native/'house-on-hill-pom-kernel-port-v1/LICENSE.Unity.md'
    assert kernel.is_file() and pom.is_file()
    sources=[(kernel,'LICENSE.Unity.md')]
    if sha(kernel)!=sha(pom):sources.append((pom,'LICENSE.Unity.POM.md'))
    destinations=[W/name for name in ['delta-rocks-firepit-native-stages-v1','delta-rocks-firepit-bevy-vertex-v1',
        'delta-rocks-firepit-native-surface-v1','delta-rocks-firepit-native-lit-v1',
        'delta-rocks-firepit-normal-decoder-gpu-v1']]
    pins={str(p):sha(p) for p in [Path(__file__),kernel,pom]}
    copies=[]
    for folder in destinations:
        assert folder.is_dir()
        for original,name in sources:
            target=folder/name
            assert not target.exists()
            copies.append(dict(original=str(original),copy=str(target),sha256=sha(original)))
    ROOT.mkdir()
    for row in copies:
        with Path(row['copy']).open('xb') as stream:stream.write(Path(row['original']).read_bytes())
        assert sha(row['copy'])==row['sha256']
        pins[row['copy']]=row['sha256']
    assert all(sha(p)==h for p,h in pins.items())
    result=dict(status='exact_helper_licenses_preserved_with_private_derivatives',copied_files=len(copies),
        copies=copies,input_pins=pins,upstream_helper_licenses_only=True,
        purchased_asset_licenses_preserved_separately=True,redistribution_authorized=False,full_import=False)
    with (ROOT/'RESULT.v1.json').open('x',encoding='utf-8') as stream:json.dump(result,stream,indent=2,allow_nan=False)
    print(json.dumps(dict(helper_license_copies=len(copies),full_import=False)))

if __name__=='__main__':main()
