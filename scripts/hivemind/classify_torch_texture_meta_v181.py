"""Explain each retained private SDK migration by exact inverse text comparison."""
from pathlib import Path
from datetime import datetime, timezone
import difflib
import hashlib
import json

W = Path('F:/OpenRPG_ImportWork')
O = W / 'medieval-torch-texture-meta-classification-v181'
pins = {}

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def read(path):
    pins[str(path)] = sha(path)
    return json.loads(Path(path).read_text('utf-8-sig'))

def main():
    assert not O.exists()
    run = read(W / 'medieval-torch-native-materials-v171/RUN-RESULT.v1.json')
    reopened = read(W / 'medieval-torch-native-materials-reopen-v174/RUN-RESULT.v1.json')
    assert run['native_materials_verified'] and run['compressed_mips_verified'] and reopened['native_export_verified']
    rows = []
    for change in run['private_meta_changes']:
        prior = Path(change['before_snapshot']); live = Path(change['path'])
        assert sha(prior) == change['before_sha256'] and sha(live) == change['after_sha256']
        pins[str(prior)] = change['before_sha256']; pins[str(live)] = change['after_sha256']
        before = prior.read_text('utf-8-sig').replace('\r\n','\n')
        after = live.read_text('utf-8-sig').replace('\r\n','\n')
        assert '  textureType: 0\n' in before or '  textureType: 1\n' in before
        restored = after
        additions = ['    flipGreenChannel: 0\n', '  swizzle: 50462976\n', '    ignorePlatformSupport: 0\n', '    customData: \n', '    spriteCustomMetadata:\n      entries: []\n']
        added = []
        for line in additions:
            assert line not in before and line in restored, (live.name,line)
            count = restored.count(line); restored = restored.replace(line,'')
            added.append(dict(text=line,count=count))
        replacements = [('  serializedVersion: 12\n','  serializedVersion: 13\n'),('  ignoreMasterTextureLimit: 0\n','  ignoreMipmapLimit: 0\n'),('  - serializedVersion: 3\n','  - serializedVersion: 4\n'),('  spritePackingTag: \n','  mipmapLimitGroupName: \n')]
        replaced = []
        for old, new in replacements:
            assert before.count(old) == restored.count(new) > 0
            count = restored.count(new); restored = restored.replace(new,old)
            replaced.append(dict(before=old,after=new,count=count))
        marker = '  pSDRemoveMatte: 0\n'
        removed = '  pSDShowRemoveMatteOption: 0\n'
        assert removed not in restored and before.count(removed) == restored.count(marker) == 1
        restored = restored.replace(marker,marker+removed)
        assert restored == before, 'Unexplained source texture settings change: '+str(live)
        rows.append(dict(path=str(live),before_sha256=sha(prior),after_sha256=sha(live),exact_inverse_comparison_equal=True,added_sdk_fields=added,replaced_sdk_fields=replaced,removed_editor_field=removed,identity_swizzle_rgba=True,green_channel_flip_disabled=True,diff=''.join(difflib.unified_diff(before.splitlines(True),after.splitlines(True)))))
    bindings = read(W / 'medieval-torch-active-texture-bindings-v156/RESULT.v1.json')
    for path, digest in bindings['input_pins'].items():
        assert sha(path) == digest, path
        pins[path] = digest
    for path,digest in run['output_pins'].items():
        assert sha(path) == digest, path
        pins[path] = digest
    pins[str(Path(__file__).resolve())] = sha(Path(__file__).resolve())
    O.mkdir()
    result = dict(status='private_texture_sdk_migrations_exactly_classified',finished_utc=datetime.now(timezone.utc).isoformat(),private_meta_changes_classified=len(rows),rows=rows,native_importer_semantic_restoration_verified=run['private_importer_semantic_restoration_verified'],native_sampler_dimension_format_mips_independently_verified=True,original_source_inputs_unchanged=True,original_version_byte_parity=False,source_color_normal_shader_equivalence_qualified=False,full_import=False,input_pins=pins)
    (O / 'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps(dict(private_meta_changes_classified=len(rows),exact_inverse_comparisons_equal=True,full_import=False)))

if __name__ == '__main__': main()
