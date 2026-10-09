"""Retain migrated private context while measuring only the original Detail mask.

Both versions and a byte diff are retained. This never qualifies the full prefab.
"""
from pathlib import Path
import difflib
import hashlib
import json
import re
import sys

HERE = Path(__file__).resolve().parent
BASE = HERE / 'queue_source_null_mask_hdrp_v1.py'
ROOT = Path('F:/OpenRPG_ImportWork/unity-source-null-mask-hdrp-v2')
MIGRATIONS = []


def retain_private_context(origin, target, source_digest):
    # Only non-target material context may differ. Every other source remains strict.
    assert origin.suffix.lower() == '.mat', 'Non-material source changed: ' + str(target)
    original = origin.read_text('utf-8-sig')
    private = target.read_text('utf-8-sig')
    shader_pattern = r'm_Shader:\s*\{\s*fileID:\s*(-?\d+),\s*guid:\s*([a-f0-9]{32}),\s*type:\s*(\d+)\s*\}'
    original_shader = re.search(shader_pattern, original)
    private_shader = re.search(shader_pattern, private)
    assert original_shader and private_shader
    assert original_shader.groups() == private_shader.groups(), 'Private shader identity changed'
    meta = Path(str(origin) + '.meta').read_text('utf-8-sig')
    assert 'abc00000000012210656512993269337' not in meta, 'Target material must remain original'
    private_bytes = target.read_bytes()
    private_digest = hashlib.sha256(private_bytes).hexdigest()
    directory = ROOT / 'preserved-private-context' / source_digest
    directory.mkdir(parents=True, exist_ok=False)
    (directory / 'original.mat').write_bytes(origin.read_bytes())
    (directory / 'private.mat').write_bytes(private_bytes)
    diff = ''.join(difflib.unified_diff(original.splitlines(True), private.splitlines(True),
                                       fromfile='original-source', tofile='existing-private-context'))
    (directory / 'difference.patch').write_text(diff, 'utf-8')
    MIGRATIONS.append(dict(original_path=str(origin), private_path=str(target),
                           original_sha256=source_digest, private_sha256=private_digest,
                           shader_identity=list(original_shader.groups()),
                           preserved_directory=str(directory),
                           context_qualified_as_original=False, target_material=False))
    return private_digest


source = BASE.read_text('utf-8-sig')
old = "if target.exists(): assert sha(target)==digest,'Existing private source differs; retain it: '+str(target)"
new = "if target.exists() and sha(target)!=digest: digest=retain_private_context(origin,target,digest)"
assert source.count(old) == 1
source = source.replace(old, new)
source = source.replace("ROOT=W/'unity-source-null-mask-hdrp-v1'", "ROOT=W/'unity-source-null-mask-hdrp-v2'")
source = source[:source.index("if __name__=='__main__':")]
namespace = dict(__file__=str(Path(__file__).resolve()), __name__='null_mask_v2',
                 retain_private_context=retain_private_context)
exec(compile(source, str(BASE), 'exec'), namespace)
original_save = namespace['save']


def save_with_context(name, data):
    if name == 'PREPARED.v1.json':
        data.update(existing_private_material_context=MIGRATIONS,
                    original_full_scene_context_qualified=False,
                    target_material_and_geometry_require_original_hashes=True)
        data['input_pins'][str(BASE)] = namespace['sha'](BASE)
        for row in MIGRATIONS:
            for filename in ('original.mat', 'private.mat', 'difference.patch'):
                path = Path(row['preserved_directory']) / filename
                data['input_pins'][str(path)] = namespace['sha'](path)
        namespace['check'](data['input_pins'])
        original_save('PRIVATE-CONTEXT.v1.json', dict(materials=MIGRATIONS,
                      original_full_scene_context_qualified=False,
                      original_sources_modified=False, existing_private_files_overwritten=False))
    original_save(name, data)


namespace['save'] = save_with_context
if __name__ == '__main__':
    namespace['run' if len(sys.argv)>1 and sys.argv[1]=='run' else 'prepare']()
