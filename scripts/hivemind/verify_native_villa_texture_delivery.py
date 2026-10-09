"""Reopen native texture evidence before GLB assembly; never qualify rendering."""
import datetime
import hashlib
import json
from pathlib import Path

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'villa-native-texture-delivery-reopen-v1'
RECEIPT = W / 'villa-unity-imported-texture-storage-v1/RESULT.v1.json'
CAPTURE = W / 'unity-villa-texture-source-probe-linear-v2/export/RESULT.json'
NORMALS = W / 'villa-native-scene-normal-bindings-v1/PLAN.v1.json'
FORMATS = {
    'Bc1RgbaUnorm': ('bc1_rgba_unorm', 'RGBA_DXT1_UNorm', 4, 8),
    'Bc1RgbaUnormSrgb': ('bc1_rgba_unorm_srgb', 'RGBA_DXT1_SRGB', 4, 8),
    'Bc3RgbaUnorm': ('bc3_rgba_unorm', 'RGBA_DXT5_UNorm', 4, 16),
    'Bc3RgbaUnormSrgb': ('bc3_rgba_unorm_srgb', 'RGBA_DXT5_SRGB', 4, 16),
    'Bc4RUnorm': ('bc4_r_unorm', 'R_BC4_UNorm', 4, 8),
    'Rgba8Unorm': ('rgba8_unorm', 'R8G8B8A8_UNorm', 1, 4),
}

def read(p):
    return json.loads(Path(p).read_text('utf-8-sig'))

def sha(p):
    with Path(p).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def require(value, reason):
    if not value:
        raise RuntimeError(reason)

def main():
    require(not ROOT.exists(), 'Existing evidence preserved; choose a new revision')
    ROOT.mkdir()
    state = {'schema': 'openrpg.native-texture-delivery-reopen.v1',
             'started_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
             'status': 'checking', 'rows': [], 'full_import': False,
             'glb_created': False, 'gpu_rendered': False, 'studio_verified': False}
    try:
        receipt, capture, normal = read(RECEIPT), read(CAPTURE), read(NORMALS)
        pins = {str(p): sha(p) for p in (RECEIPT, CAPTURE, NORMALS, Path(__file__).resolve())}
        for p, digest in receipt['source_pins'].items():
            require(sha(p) == digest, 'Texture capture evidence changed: ' + p)
            pins[p] = digest
        require(len(receipt['rows']) == 44, 'Expected all 44 captured textures')
        require(capture['failed'] == 0, 'Capture contains failed cases')
        cases = {}
        for index, case in enumerate(capture['cases']):
            key = (case['actual_guid'], str(case['actual_file_id']))
            require(key not in cases, 'Duplicate captured identity')
            cases[key] = (index, case)
        mip_count = 0
        for row in receipt['rows']:
            index, case = cases[(row['unity_guid'], row['unity_file_id'])]
            require(case['expected_identity_matches'] and not case['error'], 'Native identity failed')
            require(case['expected_guid'] == row['unity_guid'] and
                    str(case['expected_file_id']) == row['unity_file_id'], 'Expected identity changed')
            for field in ('original_png', 'original_meta', 'storage'):
                require(sha(row[field]) == row[field + '_sha256'], 'Physical source changed: ' + field)
                pins[row[field]] = row[field + '_sha256']
            meta_lines = Path(row['original_meta']).read_text('utf-8-sig').splitlines()
            require('guid: ' + row['unity_guid'] in meta_lines, 'Original importer GUID mismatch')
            name, native, block, size = FORMATS[row['gpu_format']]
            require(native == row['unity_graphics_format'] == case['graphics_format'], 'Format mismatch')
            require(row['width'] == case['width'] and row['height'] == case['height'], 'Dimensions mismatch')
            require(row['is_srgb'] == case['importer_srgb'] == name.endswith('_srgb'), 'Color space mismatch')
            sampler = row['resolved_sampler']
            for field in ('filter_mode', 'wrap_u', 'wrap_v', 'wrap_w', 'anisotropy', 'mip_bias'):
                require(sampler[field] == case[field], 'Sampler mismatch: ' + field)
            settings = json.loads(case['imported_settings_json'])
            enabled = settings['mipmaps']['m_EnableMipMap']
            require(enabled in (0, 1), 'Invalid native mipmap flag')
            data = Path(row['storage']).read_bytes()
            require(len(data) == row['storage_bytes'] == case['raw_whole_bytes'], 'Storage length mismatch')
            require(len(row['mips']) == case['mip_count'] == len(case['mips']), 'Mip count mismatch')
            offset, mips = 0, []
            for level, (mip, original) in enumerate(zip(row['mips'], case['mips'])):
                width, height = max(1, row['width'] >> level), max(1, row['height'] >> level)
                count = ((width + block - 1) // block) * ((height + block - 1) // block) * size
                require(mip['level'] == original['level'] == level and
                        mip['width'] == original['width'] == width and
                        mip['height'] == original['height'] == height and
                        mip['bytes'] == original['bytes'] == count and mip['offset'] == offset,
                        'Mip layout mismatch')
                require(hashlib.sha256(data[offset:offset + count]).hexdigest() == mip['sha256'], 'Mip hash mismatch')
                mips.append({k: mip[k] for k in ('width', 'height', 'offset', 'bytes', 'sha256')})
                offset += count
            require(offset == len(data), 'Trailing or missing mip bytes')
            require(enabled or len(mips) == 1, 'Disabled mipmaps with multiple levels')
            state['rows'].append({'asset_id': row['asset_id'], 'unity_guid': row['unity_guid'],
                'original_meta_sha256': row['original_meta_sha256'], 'original_png_sha256': row['original_png_sha256'],
                'storage_path': row['storage'], 'capture_path': str(CAPTURE),
                'descriptor_template': {'schema_version': 1,
                    'storage': {'sha256': row['storage_sha256']},
                    'capture': {'sha256': pins[str(CAPTURE)]}, 'capture_case_index': index,
                    'width': row['width'], 'height': row['height'], 'format': name, 'mips': mips,
                    'sampler': {'filter_mode': sampler['filter_mode'],
                        'wrap': [sampler[k] for k in ('wrap_u', 'wrap_v', 'wrap_w')],
                        'anisotropy': sampler['anisotropy'], 'mip_bias': sampler['mip_bias'],
                        'source_mipmaps_enabled': bool(enabled)}}})
            mip_count += len(mips)
        deferred = []
        for row in normal['rows']:
            missing = [{'material_index': material['material_index'], **slot}
                       for material in row['materials'] for slot in material['normal_slots']
                       if slot['proposed_mode'] is None]
            if missing:
                deferred.append({'asset_id': row['asset_id'], 'unresolved_reads': missing})
        state.update(status='44_native_texture_sources_reopened', input_pins=pins,
            verified_textures=len(state['rows']), verified_mips=mip_count,
            normal_deferred_scenes=deferred, normal_eligible_scenes=len(normal['rows']) - len(deferred),
            descriptor_templates_require_buffer_view_assignment=True)
    except Exception as error:
        state.update(status='refused', error=str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(state, indent=2), 'utf-8')
    print(json.dumps({k: v for k, v in state.items() if k not in ('rows', 'input_pins', 'normal_deferred_scenes')}))
    if state['status'] == 'refused':
        raise SystemExit(1)

if __name__ == '__main__':
    main()
