"""Prepare one real texture GPU job; remove historical fixed-batch constants."""
from pathlib import Path
import hashlib
import json
import shutil

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'firepit-active-texture-gpu-probe-v1'
OLD = W / 'source-graph-imported-texture-gpu-probe-v2'
CAPTURE = W / 'unity-delta-firepit-active-texture-storage-v1'

def read(p):
    return json.loads(p.read_text('utf-8-sig'))

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def main():
    assert not ROOT.exists()
    status = read(CAPTURE / 'RUN-RESULT.v1.json')
    assert status['unity_exit_code'] == 0 and status['textures_verified'] == 1
    receipt = CAPTURE / 'INDEPENDENT-REOPEN.v1.json'
    assert sha(receipt) == status['independent_receipt_sha256']
    proof = read(receipt)
    pins = dict(proof['input_pins']); pins.update(proof['output_pins'])
    for p in [receipt, CAPTURE / 'RUN-RESULT.v1.json', Path(__file__).resolve(),
              OLD / 'src/main.rs', OLD / 'Cargo.toml', OLD / 'Cargo.lock']:
        pins[str(p)] = sha(p)
    for p, expected in pins.items():
        assert sha(Path(p)) == expected, 'changed native input: ' + p
    ROOT.mkdir(); (ROOT / 'src').mkdir()
    main_source = (OLD / 'src/main.rs').read_text('utf-8')
    replacements = {
        'assert_eq!(results.len(), 44);': 'assert_eq!(results.len(), value["rows"].as_array().unwrap().len());',
        'assert_eq!(total_mips, 528);': 'assert_eq!(total_mips, value["rows"].as_array().unwrap().iter().map(|r| r["mips"].as_array().unwrap().len()).sum::<usize>());',
        '"gpu_sampled_textures":44': '"gpu_sampled_textures":results.len()',
    }
    for before, after in replacements.items():
        assert main_source.count(before) == (2 if 'gpu_sampled_textures' in before else 1)
        main_source = main_source.replace(before, after)
    (ROOT / 'src/main.rs').write_text(main_source, 'utf-8')
    for name in ['imported_texture.rs', 'sampler.rs', 'sample.wgsl']:
        source = OLD / 'src' / name
        pins[str(source)] = sha(source)
        shutil.copyfile(source, ROOT / 'src' / name)
    name = 'openrpg-firepit-active-texture-gpu-probe-v1'
    manifest = (OLD / 'Cargo.toml').read_text('utf-8').replace('openrpg-imported-texture-gpu-probe-v2', name)
    (ROOT / 'Cargo.toml').write_text(manifest, 'utf-8')
    lock = (OLD / 'Cargo.lock').read_text('utf-8')
    assert lock.count('name = "openrpg-imported-texture-gpu-probe-v2"') == 1
    (ROOT / 'Cargo.lock').write_text(lock.replace('name = "openrpg-imported-texture-gpu-probe-v2"', 'name = "' + name + '"'), 'utf-8')
    native = read(CAPTURE / 'export/RESULT.json')['cases'][0]
    assert native['graphics_format'] == 'RGBA_DXT5_SRGB' and native['mip_count'] == 11
    raw = CAPTURE / 'export/000/raw-whole.bin'
    mips = []; offset = 0
    for mip in native['mips']:
        p = CAPTURE / f"export/000/mip-{mip['level']:02}.bin"
        mips.append(dict(**mip, offset=offset, sha256=sha(p)))
        offset += mip['bytes']
    assert offset == raw.stat().st_size
    prepared = read(CAPTURE / 'PREPARED.v1.json')
    row = dict(asset_id=native['asset_id'], unity_guid=native['actual_guid'], unity_file_id=native['actual_file_id'],
        original_png=prepared['source_original_png'], original_png_sha256=sha(Path(prepared['source_original_png'])),
        original_meta=prepared['source_original_meta'], original_meta_sha256=sha(Path(prepared['source_original_meta'])),
        width=native['width'], height=native['height'], gpu_format='Bc3RgbaUnormSrgb',
        unity_graphics_format=native['graphics_format'], is_srgb=True, storage=str(raw),
        storage_sha256=sha(raw), storage_bytes=offset, mips=mips,
        resolved_sampler={k:native[k] for k in ['filter_mode','wrap_u','wrap_v','wrap_w','anisotropy','mip_bias']},
        compressed=True, requires_texture_compression_bc=True, normal_decode_qualified=False,
        orientation_qualified=False, gpu_sampling_qualified=False, source_mipmaps_enabled=True)
    input_path = ROOT / 'INPUT.v1.json'
    input_path.write_text(json.dumps(dict(rows=[row], source_proof_pins=pins, full_import=False, gpu_rendered=False), indent=2), 'utf-8')
    outputs = {str(p):sha(p) for p in (ROOT / 'src').iterdir()}
    outputs.update({str(p):sha(p) for p in [input_path, ROOT / 'Cargo.toml', ROOT / 'Cargo.lock']})
    for p, expected in pins.items():
        assert sha(Path(p)) == expected, 'changed source after preparation: ' + p
    (ROOT / 'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins, output_pins=outputs,
        expected_textures=1, expected_mips=11, actual_sampler_reused=True, old_fixed_batch_counts_removed=True,
        compiled=False, gpu_executed=False, unity_sampling_parity_qualified=False, full_import=False), indent=2), 'utf-8')
    print(json.dumps(dict(prepared_textures=1, prepared_mips=11, compiled=False, gpu_executed=False)))

if __name__ == '__main__':
    main()
