"""Reopen the exact ten active textures and retain whole original importer metadata."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import shutil
import struct

W = Path('F:/OpenRPG_ImportWork')
OUT = W / 'townsmith-s-paint-texture-inventory-v1'

def read(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))

def sha(p):
    with p.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

def main():
    assert not OUT.exists()
    generation_path = W / 'townsmith-s-paint-wgsl-v1/typed-program-v1/GENERATION.v1.json'
    manifest_path = W / 'hivemind-run/townsmith/source.v1.json'
    proof_path = W / 'townsmith-s-paint-wgsl-reopen-v1/RESULT.v1.json'
    proof = read(proof_path)
    for p, expected in proof['input_pins'].items():
        assert sha(Path(p)) == expected, p
    generation = read(generation_path)
    manifest = read(manifest_path)
    assert sorted(generation['textures'].values()) == list(range(10))
    assert len(generation['normal_texture_indices']) == 3
    pins = {str(p): sha(p) for p in [generation_path, manifest_path, proof_path, Path(__file__)]}
    rows = []
    for guid, index in generation['textures'].items():
        source_path = manifest['guid_paths'][guid]
        selected = [r for r in manifest['files'] if r['guid'] == guid]
        asset_rows = [r for r in selected if r['kind'] == 'asset']
        meta_rows = [r for r in selected if r['kind'] == 'asset.meta']
        assert len(asset_rows) == len(meta_rows) == 1, (guid, selected)
        pair = []
        for r in [asset_rows[0], meta_rows[0]]:
            p = manifest_path.parent / r['stored_path']
            assert p.is_file() and p.stat().st_size == r['bytes'] and sha(p) == r['sha256'], str(p)
            pins[str(p)] = r['sha256']
            pair.append(p)
        asset, meta = pair
        with asset.open('rb') as f:
            header = f.read(33)
        assert header[:8] == b'\x89PNG\r\n\x1a\n' and header[12:16] == b'IHDR', str(asset)
        width, height, bit_depth, color_type = struct.unpack('>IIBB', header[16:26])
        assert width > 0 and height > 0
        metadata = meta.read_text(encoding='utf-8-sig')
        assert 'guid: ' + guid in metadata and 'TextureImporter:' in metadata
        private = W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4/project' / source_path
        private_meta = Path(str(private) + '.meta')
        assert private.is_file() and private_meta.is_file()
        assert sha(private) == sha(asset) and sha(private_meta) == sha(meta), guid
        pins[str(private)] = sha(private)
        pins[str(private_meta)] = sha(private_meta)
        rows.append({'guid': guid, 'authored_texture_index': index, 'source_path': source_path,
                     'source_file': str(asset), 'source_sha256': sha(asset),
                     'original_meta': str(meta), 'original_meta_sha256': sha(meta),
                     'private_import_source_identical': True,
                     'png_width': width, 'png_height': height,
                     'png_bit_depth': bit_depth, 'png_color_type': color_type,
                     'normal_profile_index': generation['normal_texture_indices'].get(guid),
                     'effective_unity_format_qualified': False,
                     'effective_mips_sampler_and_normal_decoder_qualified': False})
    OUT.mkdir()
    for row in rows:
        destination = OUT / (str(row['authored_texture_index']).zfill(2) + '-' + row['guid'] + '.meta')
        shutil.copy2(row['original_meta'], destination)
        assert sha(destination) == row['original_meta_sha256']
        pins[str(destination)] = sha(destination)
        row['preserved_importer_metadata'] = str(destination)
    result = {'status': 'ten_active_original_textures_and_private_import_sources_sha_verified',
              'sub_lot': 'L03.TOWNSMITH.SHADER.S_PAINT.TEXTURES.SOURCES',
              'finished_utc': datetime.now(timezone.utc).isoformat(),
              'textures_reopened': 10, 'independent_normal_profiles': 3,
              'original_sources_changed': False, 'rows': rows, 'input_pins': pins,
              'texture_gpu_storage_converted': False, 'full_import': False,
              'complete_assets_delivered_increment': 0,
              'next_action': 'Capture native Unity imported formats, complete mip chains, samplers and decoder profiles without changing importer settings'}
    (OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in result.items() if k not in ['rows', 'input_pins']}))

if __name__ == '__main__':
    main()
