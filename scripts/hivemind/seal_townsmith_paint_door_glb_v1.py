"""Seal carrier/geometry integrity only; rendering and full prefab remain open."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'townsmith-paint-door-glb-reopen-v1'


def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))


def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


assert not ROOT.exists()
assembly = W / 'townsmith-paint-door-glb-v1'
check = W / 'townsmith-paint-door-glb-check-v1'
expected = read(assembly / 'EXPECTED.v1.json')
result = read(check / 'RESULT.v1.json')
assert result['compile_exit_code'] == result['test_exit_code'] == 0
assert '50 passed; 0 failed; 0 ignored' in (check / 'tests.stdout.log').read_text(encoding='utf-8')
assert sha(expected['glb']) == expected['sha256']
pins = {**read(assembly / 'PREPARED.v1.json')['input_pins'], **result['input_pins']}
for p in [assembly / 'EXPECTED.v1.json', assembly / 'PREPARED.v1.json', check / 'RESULT.v1.json',
          check / 'tests.stdout.log', check / 'tests.stderr.log', Path(expected['glb']), Path(__file__)]:
    pins[str(p)] = sha(p)
for p, h in pins.items():
    assert sha(p) == h, p
ROOT.mkdir()
proof = {'status': 'three_door_lods_complete_material_carrier_reopened',
         'finished_utc': datetime.now(timezone.utc).isoformat(), 'glb': expected['glb'], 'glb_sha256': expected['sha256'],
         'bytes': expected['bytes'], 'lod_meshes': 3, 'vertices': sum(r['vertices'] for r in expected['meshes']),
         'visible_indices': sum(r['visible_indices'] for r in expected['meshes']),
         'retained_non_colour_submeshes': sum(r['retained_submeshes'] for r in expected['meshes']),
         'materials': 1, 'native_textures': 10, 'tests_passed_including_prior_regressions': 50,
         'all_embedded_views_byte_equal_to_inputs': True,
         'reader_epoch': 'Live reader/attribute modules frozen at assembly; tested normal-swizzle contract candidate. Pending guarded common installation.',
         'normal_swizzle_fix_installed': False, 'gpu_draw_executed': False, 'source_unity_render_parity_qualified': False,
         'full_prefab_assembled': False, 'source_shadow_policy_qualified': False, 'lod_routing_qualified': False,
         'forge_editor_verified': False, 'full_import': False, 'complete_assets_delivered_increment': 0, 'input_pins': pins}
(ROOT / 'RESULT.v1.json').write_text(json.dumps(proof, indent=2), encoding='utf-8')
print(json.dumps({k: v for k, v in proof.items() if k != 'input_pins'}))
