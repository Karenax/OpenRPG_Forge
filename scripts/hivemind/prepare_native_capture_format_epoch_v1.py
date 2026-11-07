"""Prove that concurrent formatting did not change the old common contracts."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import subprocess

S = Path(__file__).parent
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'native-capture-common-format-epoch-v1'
RUSTFMT = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin/rustfmt.exe')


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    assert not ROOT.exists()
    old = json.loads((W / 'native-texture-capture-common-install-v1/PREPARED.v1.json').read_text(encoding='utf-8'))
    ROOT.mkdir()
    rows = []
    pins = {str(Path(__file__)): sha(__file__), str(RUSTFMT): sha(RUSTFMT)}
    for live, frozen in old['historical_source_snapshots'].items():
        assert sha(frozen) == old['expected_live_source_pins'][live]
        outputs = []
        for label, path in [('historical', frozen), ('current', live)]:
            process = subprocess.run([str(RUSTFMT), '--edition', '2024', '--config', 'skip_children=true'], input=Path(path).read_bytes(), capture_output=True, creationflags=subprocess.CREATE_NO_WINDOW)
            out = ROOT / (Path(live).stem + '-' + label + '.rs')
            err = ROOT / (Path(live).stem + '-' + label + '.stderr.log')
            out.write_bytes(process.stdout)
            err.write_bytes(process.stderr)
            assert process.returncode == 0, (label, path, process.returncode)
            outputs.append(process.stdout)
            pins.update({str(p): sha(p) for p in (Path(path), out, err)})
        rows.append({'current_source': live, 'historical_snapshot': frozen,
                     'historical_sha256': sha(frozen), 'current_sha256': sha(live),
                     'canonical_rustfmt_output_equal': outputs[0] == outputs[1]})
    for path, expected in pins.items():
        assert sha(path) == expected, path
    success = all(r['canonical_rustfmt_output_equal'] for r in rows)
    result = {'status': 'common_source_change_is_canonical_formatting_only' if success else 'common_source_change_requires_semantic_review',
              'finished_utc': datetime.now(timezone.utc).isoformat(), 'rows': rows, 'input_pins': pins,
              'expected_live_source_pins': {r['current_source']: r['current_sha256'] for r in rows},
              'historical_source_snapshots': old['historical_source_snapshots'], 'full_import': False}
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps({k: v for k, v in result.items() if k != 'input_pins'}))


if __name__ == '__main__':
    main()
