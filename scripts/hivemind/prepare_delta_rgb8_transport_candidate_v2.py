"""Preserve the v1 dependency refusal; stage the candidate with the actual serde pin."""
from pathlib import Path
import hashlib
import json

HERE = Path(__file__).resolve().parent
OLD = Path('F:/OpenRPG_ImportWork/delta-rgb8-native-transport-candidate-v1')
NEW = Path('F:/OpenRPG_ImportWork/delta-rgb8-native-transport-candidate-v2')
BASE = HERE / 'prepare_delta_rgb8_transport_candidate_v1.py'


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    assert not NEW.exists()
    log = (OLD / 'lock.log').read_text('utf-8-sig')
    assert 'failed to select a version for `serde`' in log
    assert '=1.0.229' in log and 'serde v1.0.228' in log
    manifest = (OLD / 'Cargo.toml').read_text('utf-8-sig')
    core = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust/Cargo.toml')
    assert 'serde = { version = "=1.0.229"' in core.read_text('utf-8-sig')
    preserved = {str(p): digest(p) for p in sorted(OLD.rglob('*')) if p.is_file()}
    source = BASE.read_text('utf-8-sig')
    assert source.count('serde={version="=1.0.228"') == 1
    source = source.replace('delta-rgb8-native-transport-candidate-v1', 'delta-rgb8-native-transport-candidate-v2')
    source = source.replace('serde={version="=1.0.228"', 'serde={version="=1.0.229"')
    namespace = {'__name__': 'candidate_v2', '__file__': str(Path(__file__).resolve())}
    exec(compile(source, str(BASE), 'exec'), namespace)
    namespace['main']()
    assert all(digest(Path(p)) == sha for p, sha in preserved.items())
    with (NEW / 'DEPENDENCY-REFUSAL-REPAIR.v1.json').open('x', encoding='utf-8') as stream:
        json.dump(dict(v1_files_preserved=preserved, original_script_sha256=digest(BASE),
                       wrapper_sha256=digest(Path(__file__)), core_manifest_sha256=digest(core),
                       correction='serde exact version 1.0.229 matches actual forge_core',
                       native_capture_repeated=False, canonical_renderer_changed=False), stream, indent=2)


if __name__ == '__main__':
    main()
