"""Respect the original Position/Normal-only convex collider layout."""
from pathlib import Path
BASE = Path(__file__).with_name('package_house_attribute_buffers_v665.py')
code = BASE.read_text('utf-8-sig')
old = "        assert set(descriptors['target']) == set(semantics.values())\n        for uv in ['TEXCOORD_0','TEXCOORD_1']:\n            assert descriptors['target'][uv]['sha256'] == descriptors['source'][uv]['sha256']"
new = """        roles = {u['role'] for u in mesh['source_mesh_usages']}
        assert roles <= {'mesh_filter', 'mesh_collider'} and roles
        expected = set(semantics.values()) if 'mesh_filter' in roles else {'POSITION', 'NORMAL'}
        assert set(descriptors['target']) == set(descriptors['source']) == expected
        for uv in ['TEXCOORD_0','TEXCOORD_1']:
            if uv in expected:
                assert descriptors['target'][uv]['sha256'] == descriptors['source'][uv]['sha256']"""
assert code.count(old) == 1
code = code.replace(old, new).replace('v665', 'v666').replace('420', '312')
code = code.replace("pins = {str(p): sha(p) for p in", "pins = {str(p): sha(p) for p in")
code = code.replace("source_cases =", "pins[str(BASE)] = sha(BASE)\nsource_cases =")
exec(compile(code, str(BASE) + '#original-collider-layout', 'exec'), globals())
