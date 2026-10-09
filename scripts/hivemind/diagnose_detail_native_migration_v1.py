"""Preserve authored and native-migrated target bytes, without admission."""
from pathlib import Path
import difflib
import hashlib
import json
import re

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'unity-source-null-mask-hdrp-v3'


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    prepared=json.loads((W/'unity-source-null-mask-hdrp-v2/PREPARED.v1.json').read_text('utf-8-sig'))
    manifest=json.loads((W/'unity-source-null-mask-hdrp-v2/manifest.json').read_text('utf-8-sig'))
    relative=manifest['material_path']
    source=W/'hivemind-run/native-american-village/source'/relative
    native=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'/relative
    assert sha(source)==prepared['input_pins'][str(source)]
    original=source.read_text('utf-8-sig');effective=native.read_text('utf-8-sig')
    shader=r'm_Shader:\s*\{\s*fileID:\s*(-?\d+),\s*guid:\s*([a-f0-9]{32}),\s*type:\s*(\d+)\s*\}'
    assert re.search(shader,original).groups()==re.search(shader,effective).groups()
    directory=ROOT/'preserved-target-migration';assert not directory.exists();directory.mkdir()
    (directory/'authored.mat').write_bytes(source.read_bytes())
    (directory/'native-effective.mat').write_bytes(native.read_bytes())
    (directory/'difference.patch').write_text(''.join(difflib.unified_diff(original.splitlines(True),effective.splitlines(True),fromfile='authored-source',tofile='native-effective-private')),'utf-8')
    def floats(text):
        block=text.split('    m_Floats:',1)[1].split('    m_Colors:',1)[0]
        return dict(re.findall(r'^\s*-\s+([^:]+):\s*([^\r\n]+)',block,re.M))
    a,b=floats(original),floats(effective)
    changed={key:dict(authored=a.get(key),native=b.get(key)) for key in sorted(set(a)|set(b)) if a.get(key)!=b.get(key)}
    pins={str(p):sha(p) for p in [source,native,Path(__file__).resolve(),directory/'authored.mat',directory/'native-effective.mat',directory/'difference.patch']}
    result=dict(status='preparation_refused_target_private_material_migrated',
        input_pins=pins,original_sources_modified=False,private_material_overwritten=False,
        shader_identity_preserved=True,float_property_differences=changed,
        migration_behavior_qualified=False,default_pixels_verified=False,full_import=False)
    target=ROOT/'TARGET-MIGRATION-DIAGNOSIS.v1.json';assert not target.exists()
    target.write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps(dict(receipt=str(target),float_property_differences=changed)))


if __name__=='__main__':main()
