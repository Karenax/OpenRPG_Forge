"""Stage native RGB8 transport/CPU expansion without changing pinned engine builds."""
from pathlib import Path
import hashlib
import json
import os
import shutil
import subprocess
import sys

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'delta-rgb8-native-transport-candidate-v1'
REPO = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
BASE = W / 'delta-foliage-texture-gpu-v2'
GPU = W / 'delta-foliage-texture-gpu-v3'
CAPTURE = W / 'unity-delta-foliage-texture-storage-v2/export'
TARGET = W / 'source-graph-private-boundary-v1/target-rust198-explicit-domain-v8'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def replace(text, before, after):
    assert text.count(before) == 1, 'Unexpected source patch context'
    return text.replace(before, after)


def main():
    assert not ROOT.exists()
    parity_path = W / 'delta-foliage-rgb24-expansion-gpu-parity-v1/RESULT.v1.json'
    parity = read(parity_path)
    assert parity['rgb24_expansion_raw_sampling_verified'] and parity['mip_levels_compared'] == 37
    wire_path = REPO / 'crates/forge_assets/src/source_graph_bundle/imported_storage.rs'
    engine_path = REPO / 'crates/forge_engine/src/source_graph_imported_texture.rs'
    route_path = REPO / 'crates/forge_engine/src/source_graph_glb/captured_texture.rs'
    blob_path = REPO / 'crates/forge_assets/src/source_graph_bundle.rs'
    pins = {str(p): sha(p) for p in [wire_path, engine_path, route_path, blob_path, parity_path,
        CAPTURE / 'RESULT.json', GPU / 'INPUT.v1.json', Path(__file__).resolve()]}
    wire = replace(wire_path.read_text('utf-8-sig'), '    Rgba8Unorm,', '    Rgb8Unorm,\n    Rgba8Unorm,')
    wire = replace(wire, '            Self::Rgba8Unorm => (1, 4),', '            Self::Rgb8Unorm => (1, 3),\n            Self::Rgba8Unorm => (1, 4),')
    wire = replace(wire, '            Self::Rgba8Unorm => "R8G8B8A8_UNorm",',
                   '            Self::Rgb8Unorm => "R8G8B8_UNorm",\n            Self::Rgba8Unorm => "R8G8B8A8_UNorm",')
    engine = replace(engine_path.read_text('utf-8-sig'), '    Rgba8Unorm,', '    Rgb8Unorm,\n    Rgba8Unorm,')
    engine = replace(engine, '            Self::Rgba8Unorm => (TextureFormat::Rgba8Unorm, 4, false),',
        '            Self::Rgb8Unorm => (TextureFormat::Rgba8Unorm, 3, false),\n            Self::Rgba8Unorm => (TextureFormat::Rgba8Unorm, 4, false),')
    engine = replace(engine, '    image.data = Some(bytes.to_vec());', '''    image.data = Some(if matches!(metadata.format, SourceGraphImportedTextureFormat::Rgb8Unorm) {
        // Source RGB24 bytes/mip hashes remain native. WebGPU uses RGBA8;
        // opaque alpha is independently measured on the original RGB images.
        let converted_len = bytes.len().checked_div(3).and_then(|n| n.checked_mul(4))
            .ok_or("RGB8 expansion overflow")?;
        if converted_len > 512 * 1024 * 1024 {
            return Err("Expanded RGB8 image allocation limit".into());
        }
        let mut rgba = Vec::with_capacity(converted_len);
        for pixel in bytes.chunks_exact(3) {
            rgba.extend_from_slice(&[pixel[0], pixel[1], pixel[2], 255]);
        }
        rgba
    } else {
        bytes.to_vec()
    });''')
    route = replace(route_path.read_text('utf-8-sig'),
        '        SourceGraphImportedStorageFormat::Rgba8Unorm => {',
        '        SourceGraphImportedStorageFormat::Rgb8Unorm => SourceGraphImportedTextureFormat::Rgb8Unorm,\n        SourceGraphImportedStorageFormat::Rgba8Unorm => {')
    ROOT.mkdir()
    (ROOT / 'src').mkdir()
    candidates = {}
    for path, name, text in [(wire_path, 'imported_storage.rs', wire), (engine_path, 'imported_texture.rs', engine),
                             (route_path, 'captured_texture.rs', route)]:
        target = ROOT / 'src' / name
        target.write_text(text, encoding='utf-8')
        candidates[str(path)] = dict(original_sha256=pins[str(path)], candidate=str(target), candidate_sha256=sha(target))
    original_blob = blob_path.read_text('utf-8-sig')
    offset = original_blob.index('pub struct SourceGraphBlob {')
    struct = original_blob[original_blob.rfind('#[derive', 0, offset):original_blob.index('\n}', offset) + 2]
    (ROOT / 'src/source_graph_bundle.rs').write_text('use forge_core::ContentHash;\nuse serde::{Deserialize,Serialize};\n' + struct +
        '\n#[path="imported_storage.rs"] pub mod imported_storage;\n', encoding='utf-8')
    shutil.copyfile(REPO / 'crates/forge_engine/src/source_graph_sampler.rs', ROOT / 'src/sampler.rs')
    pins[str(REPO / 'crates/forge_engine/src/source_graph_sampler.rs')] = sha(REPO / 'crates/forge_engine/src/source_graph_sampler.rs')
    native = read(CAPTURE / 'RESULT.json')
    old = read(GPU / 'INPUT.v1.json')
    cases = {row['asset_id']: (i, row) for i, row in enumerate(native['cases'])}
    rows = []
    for row in old['rows']:
        if not row['rgb24_expansion']:
            continue
        ordinal, captured = cases[row['asset_id']]
        assert captured['graphics_format'] == 'R8G8B8_UNorm'
        storage = Path(row['original_storage'])
        data = storage.read_bytes()
        assert sha(storage) == row['original_storage_sha256']
        pins[str(storage)] = sha(storage)
        offset, mips = 0, []
        for mip in captured['mips']:
            count = mip['width'] * mip['height'] * 3
            assert mip['bytes'] == count
            buffer = data[offset:offset + count]
            mips.append(dict(level=mip['level'], width=mip['width'], height=mip['height'], offset=offset,
                             bytes=count, sha256=hashlib.sha256(buffer).hexdigest()))
            offset += count
        assert offset == len(data)
        sampler = row['resolved_sampler']
        descriptor = dict(schema_version=1, storage=dict(buffer_view=0, sha256=sha(storage)),
            capture=dict(buffer_view=1, sha256=sha(CAPTURE / 'RESULT.json')), capture_case_index=ordinal,
            width=row['width'], height=row['height'], format='rgb8_unorm',
            mips=[{k: m[k] for k in ['width', 'height', 'offset', 'bytes', 'sha256']} for m in mips],
            sampler=dict(filter_mode=sampler['filter_mode'], wrap=[sampler[k] for k in ['wrap_u', 'wrap_v', 'wrap_w']],
                anisotropy=sampler['anisotropy'], mip_bias=sampler['mip_bias'], source_mipmaps_enabled=row['source_mipmaps_enabled']))
        rows.append(dict(row, storage=str(storage), storage_sha256=sha(storage), storage_bytes=len(data),
                         gpu_format='Rgb8Unorm', mips=mips, capture_json=str(CAPTURE / 'RESULT.json'), wire_descriptor=descriptor))
    assert len(rows) == 3 and sum(len(row['mips']) for row in rows) == 37
    main = (BASE / 'src/main.rs').read_text('utf-8-sig')
    main = replace(main, 'mod imported_texture;', 'mod imported_texture;\nmod source_graph_bundle;')
    main = replace(main, '        let format = match r["gpu_format"].as_str().unwrap() {',
        '        let format = match r["gpu_format"].as_str().unwrap() {\n            "Rgb8Unorm" => SourceGraphImportedTextureFormat::Rgb8Unorm,')
    main = replace(main, '        let prepared = prepare_source_graph_imported_texture(&data, &metadata).unwrap();', '''        let descriptor: source_graph_bundle::imported_storage::ImportedTextureStorage =
            serde_json::from_value(r["wire_descriptor"].clone()).unwrap();
        let capture = std::fs::read(r["capture_json"].as_str().unwrap()).unwrap();
        let views: Vec<&[u8]> = vec![&data, &capture];
        assert_eq!(descriptor.validate(&views).unwrap(), data.as_slice());
        let mut malformed = descriptor.clone();
        malformed.format = source_graph_bundle::imported_storage::ImportedStorageFormat::Rgba8Unorm;
        assert!(malformed.validate(&views).is_err(), "RGB24 cannot masquerade as captured RGBA8");
        let prepared = prepare_source_graph_imported_texture(&data, &metadata).unwrap();''')
    main = main.replace('assert_eq!(results.len(), 18);', 'assert_eq!(results.len(), 3);')
    main = main.replace('assert_eq!(total_mips, 225);', 'assert_eq!(total_mips, 37);').replace('"gpu_sampled_textures":18', '"gpu_sampled_textures":3')
    (ROOT / 'src/main.rs').write_text(main, encoding='utf-8')
    shutil.copyfile(BASE / 'src/sample.wgsl', ROOT / 'src/sample.wgsl')
    cargo = (BASE / 'Cargo.toml').read_text('utf-8-sig').replace('openrpg-delta-foliage-texture-gpu-v1', 'openrpg-delta-rgb8-transport-candidate-v1')
    cargo += '\nserde={version="=1.0.228",features=["derive"]}\n'
    (ROOT / 'Cargo.toml').write_text(cargo, encoding='utf-8')
    env = os.environ.copy()
    env.update(TEMP=str(ROOT), TMP=str(ROOT), CARGO_TARGET_DIR=str(TARGET), CARGO_INCREMENTAL='0')
    with (ROOT / 'lock.log').open('xb') as log:
        result = subprocess.run(['C:/Users/karen/.cargo/bin/cargo.exe', '+1.98.0', 'generate-lockfile', '--offline'],
            cwd=ROOT, env=env, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    assert result.returncode == 0
    for path in (ROOT / 'src').iterdir():
        pins[str(path)] = sha(path)
    for path in [ROOT / 'Cargo.toml', ROOT / 'Cargo.lock']:
        pins[str(path)] = sha(path)
    assert all(sha(p) == digest for p, digest in pins.items())
    with (ROOT / 'INPUT.v1.json').open('x', encoding='utf-8') as stream:
        json.dump(dict(rows=rows, source_proof_pins=pins), stream, indent=2)
    pins[str(ROOT / 'INPUT.v1.json')] = sha(ROOT / 'INPUT.v1.json')
    with (ROOT / 'PREPARED.v1.json').open('x', encoding='utf-8') as stream:
        json.dump(dict(candidate_files=candidates, input_pins=pins, exact_source_blob_struct_copied=True,
            original_capture_json_unchanged=True, expected_rgb24_textures=3, expected_mips=37,
            current_checkout_changed=False, renderer_installed=False, full_import=False), stream, indent=2)
    print(json.dumps(dict(candidate_sources=3, native_rgb24_textures=3, mips=37, current_checkout_changed=False)))


if __name__ == '__main__':
    main()
