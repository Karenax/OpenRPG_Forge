"""Find Viking alpha-mode candidates whose base texture alpha is fully opaque.

Read-only analysis: pixel opacity is not a runtime or visual qualification.
Requires Pillow. All differing non-alpha rendering controls are rejected.
"""
import collections
import copy
import hashlib
import io
import json
import re
import struct
from pathlib import Path

from PIL import Image
import vault_storage
from audit_variant_exact_candidates import diffs
from variant_legacy_recipe import build_recipe

ROOT = Path(r'F:\OpenRPG_AssetVault')
BASE = Path(r'E:\OpenRPG_Forge\.build\asset-conversion')
REPORTS = Path(r'E:\OpenRPG_Forge\Preuves-et-analyses\rapports\audit-variantes-hdrp-urp-20261110')


def permitted_difference(path):
    return (path == '$/extras/source_asset_id'
            or bool(re.fullmatch(r'\$/materials/\d+/(alphaMode|alphaCutoff \(missing\))', path))
            or bool(re.fullmatch(r'\$/materials/\d+/extensions/OPENRPG_source_fragment/source_program_sha256', path))
            or bool(re.fullmatch(r'\$/materials/\d+/extras/material_program_sha256', path)))


def glb_document(raw):
    if raw[:4] != b'glTF' or len(raw) < 28:
        raise ValueError('invalid GLB')
    length = struct.unpack_from('<I', raw, 12)[0]
    if 28 + length > len(raw) or raw[16:20] != b'JSON':
        raise ValueError('invalid GLB JSON')
    binary_length, kind = struct.unpack_from('<II', raw, 20 + length)
    if kind != 0x004e4942 or 28 + length + binary_length != len(raw):
        raise ValueError('invalid GLB binary')
    return json.loads(raw[20:20 + length]), memoryview(raw)[28 + length:]


def texture_alpha_range(doc, binary, material, cache):
    fragment = material.get('extensions', {}).get('OPENRPG_source_fragment', {})
    if fragment.get('base_png') is not None or fragment.get('world_blend') is not None:
        return None
    pbr = material.get('pbrMetallicRoughness', {})
    factor = pbr.get('baseColorFactor', [1, 1, 1, 1])
    if len(factor) != 4 or factor[3] != 1:
        return None
    texture = pbr.get('baseColorTexture')
    if not isinstance(texture, dict) or not isinstance(texture.get('index'), int):
        return None
    tex = doc['textures'][texture['index']]
    image = doc['images'][tex['source']]
    if image.get('mimeType') != 'image/png' or 'bufferView' not in image:
        return None
    view = doc['bufferViews'][image['bufferView']]
    if view.get('buffer') != 0:
        return None
    start, length = view.get('byteOffset', 0), view['byteLength']
    if start < 0 or length <= 0 or start + length > len(binary):
        raise ValueError('invalid image buffer')
    data = bytes(binary[start:start + length])
    digest = hashlib.sha256(data).hexdigest()
    if digest not in cache:
        with Image.open(io.BytesIO(data)) as bitmap:
            bitmap.load()
            cache[digest] = bitmap.convert('RGBA').getchannel('A').getextrema()
    return cache[digest]


def fully_opaque_texture(doc, binary, material, cache):
    return texture_alpha_range(doc, binary, material, cache) == (255, 255)


def classify(a, b, bin_a, bin_b, expected, cache):
    actual = list(diffs(a, b))
    if actual != expected or not actual or not all(permitted_difference(p) for p in actual):
        return 'other_rendering_difference'
    if bytes(bin_a) != bytes(bin_b):
        return 'different_binary'
    for doc in (a, b):
        for mesh in doc.get('meshes', []):
            for primitive in mesh.get('primitives', []):
                if 'COLOR_0' in primitive.get('attributes', {}):
                    return 'vertex_color_requires_review'
    indices = {int(m.group(1)) for p in actual
               if (m := re.fullmatch(r'\$/materials/(\d+)/(?:alphaMode|alphaCutoff \(missing\))', p))}
    if not indices:
        return 'no_alpha_difference'
    for i in indices:
        left, right = a['materials'][i], b['materials'][i]
        if {left.get('alphaMode', 'OPAQUE'), right.get('alphaMode', 'OPAQUE')} != {'OPAQUE', 'MASK'}:
            return 'unsupported_alpha_modes'
        for doc, binary, material in ((a, bin_a, left), (b, bin_b, right)):
            cutoff = material.get('alphaCutoff', 0.5)
            if not isinstance(cutoff, (int, float)) or isinstance(cutoff, bool) or not 0 <= cutoff < 1:
                return 'unsupported_cutoff'
            if not fully_opaque_texture(doc, binary, material, cache):
                return 'texture_alpha_requires_review'
    return 'opaque_alpha_candidate'


def classify_double_sided(a, b, bin_a, bin_b, expected, cache):
    """Identify a deliberate choice of the double-sided rendering, not equivalence."""
    if list(diffs(a, b)) != expected:
        return 'other_rendering_difference'
    double = re.compile(r'\$/materials/(\d+)/doubleSided')
    normal = re.compile(r'\$/materials/(\d+)/extensions/OPENRPG_source_fragment/double_sided_normal_mode')
    indices = {int(match.group(1)) for path in expected if (match := double.fullmatch(path))}
    if not indices or any(not permitted_difference(p) and not double.fullmatch(p) and not normal.fullmatch(p) for p in expected):
        return 'other_rendering_difference'
    adapted = copy.deepcopy(b)
    for i in indices:
        left, right = a['materials'][i], b['materials'][i]
        left_mode = left.get('extensions', {}).get('OPENRPG_source_fragment', {}).get('double_sided_normal_mode')
        right_mode = right.get('extensions', {}).get('OPENRPG_source_fragment', {}).get('double_sided_normal_mode')
        if left.get('doubleSided') is not True or right.get('doubleSided') is not False:
            return 'other_rendering_difference'
        if left_mode not in ('flip', 'mirror', 'none') or right_mode != 'single_sided':
            return 'other_rendering_difference'
        adapted['materials'][i]['doubleSided'] = True
        adapted['materials'][i]['extensions']['OPENRPG_source_fragment']['double_sided_normal_mode'] = left_mode
    alpha_diffs = [p for p in expected if not double.fullmatch(p) and not normal.fullmatch(p)]
    return ('double_sided_choice_candidate'
            if classify(a, adapted, bin_a, bin_b, alpha_diffs, cache) == 'opaque_alpha_candidate'
            else 'other_rendering_difference')


def classify_mask_preference(a, b, bin_a, bin_b, expected, cache):
    """Choose retained double-sided mask when alpha zero would be lost by opaque rendering."""
    if list(diffs(a, b)) != expected or bytes(bin_a) != bytes(bin_b):
        return 'other_rendering_difference'
    double = re.compile(r'\$/materials/(\d+)/doubleSided')
    normal = re.compile(r'\$/materials/(\d+)/extensions/OPENRPG_source_fragment/double_sided_normal_mode')
    if any(not permitted_difference(p) and not double.fullmatch(p) and not normal.fullmatch(p) for p in expected):
        return 'other_rendering_difference'
    for doc in (a, b):
        for mesh in doc.get('meshes', []):
            for primitive in mesh.get('primitives', []):
                if 'COLOR_0' in primitive.get('attributes', {}):
                    return 'other_rendering_difference'
    doubles = {int(m.group(1)) for p in expected if (m := double.fullmatch(p))}
    indices = {int(m.group(1)) for p in expected
               if (m := re.fullmatch(r'\$/materials/(\d+)/(?:alphaMode|alphaCutoff \(missing\))', p))}
    if not doubles or not indices:
        return 'other_rendering_difference'
    for i in doubles:
        left, right = a['materials'][i], b['materials'][i]
        left_mode = left.get('extensions', {}).get('OPENRPG_source_fragment', {}).get('double_sided_normal_mode')
        right_mode = right.get('extensions', {}).get('OPENRPG_source_fragment', {}).get('double_sided_normal_mode')
        if (left.get('doubleSided') is not True or right.get('doubleSided') is not False
                or left_mode not in ('flip', 'mirror', 'none') or right_mode != 'single_sided'):
            return 'other_rendering_difference'
    masked_with_transparency = False
    for i in indices:
        left, right = a['materials'][i], b['materials'][i]
        lm, rm = left.get('alphaMode', 'OPAQUE'), right.get('alphaMode', 'OPAQUE')
        if {lm, rm} != {'OPAQUE', 'MASK'}:
            return 'other_rendering_difference'
        for doc, binary, material in ((a, bin_a, left), (b, bin_b, right)):
            cutoff = material.get('alphaCutoff', 0.5)
            if not isinstance(cutoff, (int, float)) or isinstance(cutoff, bool) or not 0 < cutoff < 1:
                return 'other_rendering_difference'
            bounds = texture_alpha_range(doc, binary, material, cache)
            if lm == 'OPAQUE' and bounds != (255, 255):
                return 'other_rendering_difference'
            if lm == 'MASK':
                if bounds not in ((255, 255), (0, 255)):
                    return 'other_rendering_difference'
                masked_with_transparency |= bounds == (0, 255)
    return 'double_sided_mask_choice_candidate' if masked_with_transparency else 'other_rendering_difference'


def main():
    registry = ROOT / 'metadata/asset-import-registry.v1.json'
    before = registry.read_bytes()
    plan = json.loads((BASE / 'plan-viking-blocs-partages-238-20261111.json').read_bytes())
    previous = json.loads((REPORTS / 'viking-alpha-opaque-audit-v3-20261111.json').read_bytes())
    remaining = {x['retired_id'] for x in previous['results'] if x['state'] == 'other_rendering_difference'}
    candidates = [x for x in plan['candidates'] if x['retired_id'] in remaining]
    if len(candidates) != 40:
        raise ValueError('unexpected remaining Viking scope')
    historical = [json.loads(x) for x in (REPORTS / 'viking-transparence-materiaux-20261111.jsonl').read_text(encoding='utf8').splitlines() if x]
    by_id = {x['retired_id']: x for x in historical if 'retired_id' in x}
    results, cache = [], {}
    for i, row in enumerate(candidates, 1):
        entry = by_id[row['retired_id']]
        if entry.get('differences_truncated'):
            state = 'other_rendering_difference'
        else:
            first = vault_storage.read_bytes(ROOT, vault_storage.confined(ROOT, Path(row['retained_path'])), 512 * 1024 * 1024)
            second = vault_storage.read_bytes(ROOT, vault_storage.confined(ROOT, Path(row['retired_path'])), 512 * 1024 * 1024)
            build_recipe(first, second, row['retained_sha256'], row['retired_sha256'])
            a, ba = glb_document(first); b, bb = glb_document(second)
            state = classify(a, b, ba, bb, entry['differences'], cache)
            if state == 'other_rendering_difference':
                state = classify_double_sided(a, b, ba, bb, entry['differences'], cache)
            if state == 'other_rendering_difference':
                state = classify_mask_preference(a, b, ba, bb, entry['differences'], cache)
        results.append({'retained_id': row['retained_id'], 'retired_id': row['retired_id'], 'state': state})
        if i % 25 == 0:
            print(f'audited {i}/{len(candidates)} Viking pairs', flush=True)
    if registry.read_bytes() != before:
        raise ValueError('registry changed')
    result = {'state': 'analysis_only', 'registry_sha256': hashlib.sha256(before).hexdigest(),
              'counts': dict(collections.Counter(x['state'] for x in results)), 'results': results,
              'limitations': 'Opaque PNG alpha and static JSON controls only; double-sided choice differs visually; no runtime qualification.'}
    with (REPORTS / 'viking-alpha-opaque-audit-v4-20261111.json').open('x', encoding='utf8') as f:
        json.dump(result, f, ensure_ascii=False, indent=2); f.write('\n')
    print(json.dumps(result['counts']))


if __name__ == '__main__':
    main()
