"""Read the two indexed Cliff cards using the current native product reader."""
from pathlib import Path
import ast, datetime, hashlib, json, os, subprocess
R = Path(__file__).resolve().parents[2]
W = Path('F:/OpenRPG_ImportWork')
O = W / 'house-cliff-lux-native-reader-v742'
INDEX = W / 'house-cliff-lux-index-v740/RESULT.v1.json'
B = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def main():
    assert not O.exists()
    index = json.loads(INDEX.read_text('utf-8'))
    assert index['cards_updated'] == 2 and index['images_installed'] == 4
    original_test = R / 'scripts/hivemind/run_house_08_cliff_01_native_v1.py'
    tree = ast.parse(original_test.read_text('utf-8'))
    blocks = [node.value.value for node in ast.walk(tree) if isinstance(node, ast.Assign)
              and any(isinstance(t, ast.Name) and t.id == 'rust' for t in node.targets)]
    assert len(blocks) == 1
    rust = blocks[0].replace('26735', '26736').replace('assert_eq!(rows.len(),1)', 'assert_eq!(rows.len(),2)')
    rust = rust.replace('"cards_verified":1', '"cards_verified":2').replace('one_source_card_search', 'two_source_cards_search')
    rust = rust.replace('format!("{:x}",Sha256::digest(&bytes))', 'Sha256::digest(&bytes).iter().map(|byte|format!("{byte:02x}")).collect::<String>()')
    O.mkdir()
    (O / 'src').mkdir()
    (O / 'temp').mkdir()
    A = R / 'apps/forge_studio/src'
    original = (A / 'asset_library_native.rs').read_text('utf-8')
    reader = original
    for module in ['history', 'physics_transport']:
        needle = 'mod ' + module + ';'
        assert reader.count(needle) == 1
        reader = reader.replace(needle, '#[path="' + (A / 'asset_library_native' / (module + '.rs')).as_posix() + '"] ' + needle)
    (O / 'src/asset_library_native.rs').write_text(reader, 'utf-8')
    (O / 'src/main.rs').write_text(rust, 'utf-8')
    cargo_seed = W / 'library-physics-v736/Cargo.toml'
    (O / 'Cargo.toml').write_text(cargo_seed.read_text('utf-8').replace('library-physics-v736', 'house-cliff-lux-native-reader-v742'), 'utf-8')
    env = os.environ.copy()
    env.update(RUSTC=str(B / 'rustc.exe'), RUSTDOC=str(B / 'rustdoc.exe'), CARGO_INCREMENTAL='0',
               CARGO_TARGET_DIR=str(W / 'firepit-static-glb-product-check-v2/target'),
               TEMP=str(O / 'temp'), TMP=str(O / 'temp'))
    with (O / 'lock.log').open('wb') as log:
        assert subprocess.call([str(B / 'cargo.exe'), 'generate-lockfile', '--offline'], cwd=O, env=env, stdout=log, stderr=log) == 0
    files = [Path(__file__), original_test, INDEX, cargo_seed, O / 'Cargo.toml', O / 'Cargo.lock', O / 'src/main.rs', O / 'src/asset_library_native.rs']
    files += [A / p for p in ['asset_library_native.rs', 'asset_library_native/history.rs', 'asset_library_native/physics_transport.rs', 'asset_library_protocol.rs', 'asset_library_model_wire.rs']]
    pins = {str(path): sha(path) for path in files}
    with (O / 'cargo.log').open('wb') as log:
        proc = subprocess.Popen([str(B / 'cargo.exe'), 'run', '--locked', '--offline', '-j', '1', '--', str(INDEX), str(O / 'NATIVE-RESULT.v1.json')], cwd=O, env=env, stdout=log, stderr=log)
        (O / 'JOB.v1.json').write_text(json.dumps(dict(pid=proc.pid)), 'utf-8')
        code = proc.wait()
    unchanged = all(sha(path) == digest for path, digest in pins.items())
    for path, digest in index['preview_pins'].items():
        assert sha(path) == digest, path
    native = json.loads((O / 'NATIVE-RESULT.v1.json').read_text('utf-8')) if code == 0 else {}
    result = dict(status='two_current_native_source_cards_search_filter_details_images_passed' if code == 0 and unchanged and native.get('cards_verified') == 2 else 'failed',
                  actual_exit_code=code, cards_verified=native.get('cards_verified', 0), input_pins=pins,
                  unchanged_input_pins=unchanged, utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  editor_visual_verified=False, full_import=False)
    (O / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({key: value for key, value in result.items() if key != 'input_pins'}))

if __name__ == '__main__':
    main()
