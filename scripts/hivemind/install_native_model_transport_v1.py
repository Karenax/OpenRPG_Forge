"""Install verified packet/reader changes after preserving the finished audit."""
from pathlib import Path
import datetime
import hashlib
import json
import re
import shutil
import subprocess

W = Path('F:/OpenRPG_ImportWork')
R = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
ROOT = W / 'native-model-transport-installed-v1'


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    assert not ROOT.exists(), 'Immutable installation receipt already exists'
    audit_root = W / 'asset-library-indexed-payload-integrity-resume-v2'
    audit = read(audit_root / 'RUN-RESULT.v1.json')
    assert audit['status'] == 'indexed_payloads_and_referenced_archive_sha_verified'
    assert audit['actual_exit_code'] == 0 and audit['entries_verified'] == 26735
    assert sha(audit_root / 'PAYLOAD-RESULT.v1.json') == audit['payload_receipt_sha256']
    proof = read(W / 'model-native-large-carrier-v2/RESULT.v1.json')
    assert proof['ordinary_tests_passed'] == 45 and proof['actual_rocks_test_passed'] == 1
    assert proof['model_bytes'] == 709944920 and proof['encode_decode_retained_body_allocation']
    before = read(W / 'model-native-headroom-v1/PREIMAGES.v1.json')
    kept = read(W / 'model-wire-inplace-encoding-v1/AUDIT-PIN-PRESERVATION.v1.json')
    native = Path(before['canonical_native_path'])
    wire = Path(kept['canonical_path'])
    assert sha(native) == before['canonical_native_sha256']
    assert sha(wire) == kept['canonical_sha256']
    audit_pins = read(audit_root / 'PREPARED.v1.json')['input_pins']
    for path, digest in audit_pins.items():
        assert sha(path) == digest, path
    ROOT.mkdir()
    snapshots = ROOT / 'audit-input-snapshot'
    snapshots.mkdir()
    preserved = []
    for index, (path, digest) in enumerate(audit_pins.items()):
        destination = snapshots / f'{index:02d}-{Path(path).name}'
        shutil.copyfile(path, destination)
        assert sha(destination) == digest
        preserved.append(dict(original=path, snapshot=str(destination), sha256=digest))
    for name in ('RUN-RESULT.v1.json', 'PREPARED.v1.json', 'PAYLOAD-RESULT.v1.json'):
        shutil.copyfile(audit_root / name, snapshots / ('audit-' + name))
    native_text = (W / 'model-native-large-carrier-v2/asset_library_native.candidate.rs').read_text('utf-8')
    native_text, count = re.subn(r'#\[path\s*=\s*"[^"]*/asset_library_native/history\.rs"\]\s*\nmod history;',
                                 'mod history;', native_text, count=1)
    assert count == 1, 'Restore the normal canonical history module location'
    marker = '#[cfg(test)]\nmod actual_large_carrier_probe {'
    assert native_text.count(marker) == 1
    native_text = native_text.split(marker, 1)[0].rstrip() + '\n'
    wire_text = (W / 'model-native-large-carrier-v2/asset_library_model_wire.candidate.rs').read_text('utf-8')
    assert 'pub const MAX_MODEL_BYTES: usize = 1024 * 1024 * 1024;' in wire_text
    assert 'read_model_limited(&path, MAX_MODEL)' in native_text
    native.write_text(native_text, 'utf-8')
    wire.write_text(wire_text, 'utf-8')
    subprocess.run(['C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin/rustfmt.exe',
                    '--edition', '2024', '--config', 'skip_children=true', str(native), str(wire)], check=True)
    result = dict(status='native_model_transport_installed_canonical_tests_pending',
                  installed_files={str(native): sha(native), str(wire): sha(wire)},
                  preimages_and_finished_audit_inputs_preserved=preserved,
                  finished_audit_receipt_sha256=sha(audit_root / 'PAYLOAD-RESULT.v1.json'),
                  indexed_entries_sha_verified=26735, referenced_archives_sha_verified=38,
                  canonical_source_installed=True, browser_bundle_rebuilt=False,
                  source_graph_web_reader_budget_changed=False,
                  browser_editor_qualified=False, gpu_runtime_qualified=False, full_import=False,
                  installed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (ROOT / 'INSTALLED.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k: v for k, v in result.items() if k not in ('installed_files', 'preimages_and_finished_audit_inputs_preserved')}))


if __name__ == '__main__':
    main()
