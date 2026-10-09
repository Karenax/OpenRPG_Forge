"""Prepare/apply seven tested mask files with original-byte and proof guards.

Preparation does not install anything. Application requires actual v4 tests.
"""
from pathlib import Path
import datetime
import hashlib
import json
import sys

R = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'source-graph-production-color-mask-install-v1'
STAGE = W / 'source-graph-production-color-mask-v2'
CHECK = W / 'source-graph-production-color-mask-check-v4'
SNAPSHOT = W / 'source-graph-production-color-mask-snapshot-v4'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def save(name, data):
    path = ROOT / (name + '.tmp')
    path.write_text(json.dumps(data, indent=2), 'utf-8')
    path.replace(ROOT / name)


def verify_rows(rows, installed=False):
    for row in rows:
        target = R / row['relative_path']
        expected = row['candidate_sha256'] if installed else row['original_sha256']
        if expected is None:
            assert not target.exists(), 'Concurrent creation: ' + str(target)
        else:
            assert target.is_file() and sha(target) == expected, 'Concurrent change: ' + str(target)
        assert sha(Path(row['candidate_path'])) == row['candidate_sha256']
        assert sha(SNAPSHOT / row['relative_path']) == row['candidate_sha256']


def prepare():
    assert not ROOT.exists()
    stage = read(STAGE / 'PREPARED.v1.json')
    pins = stage['input_pins']
    rows = []
    for relative in stage['changed_paths']:
        target = R / relative
        candidate = STAGE / 'candidate' / relative
        original = pins.get(str(target))
        if relative == 'apps/forge_studio/src/authoring_assets/source_graph_frame.rs':
            original = pins[str(W / 'delta-rgb8-frozen-canonical-loader-v6/snapshot' / relative)]
        if relative == 'crates/forge_engine/src/source_graph_color_write_mask.rs':
            assert original is None
        else:
            assert original is not None
        rows.append(dict(relative_path=relative, original_sha256=original,
                         candidate_path=str(candidate), candidate_sha256=sha(candidate)))
        assert stage['output_pins'][str(candidate)] == rows[-1]['candidate_sha256']
    assert len(rows) == 7
    verify_rows(rows)
    ROOT.mkdir()
    save('PREPARED.v1.json', dict(rows=rows, stage_receipt_sha256=sha(STAGE/'PREPARED.v1.json'),
                                 helper_sha256=sha(Path(__file__).resolve()),
                                 actual_tests_required=str(CHECK/'RUN-RESULT.v1.json'),
                                 installed=False, full_import=False))
    print(json.dumps(dict(preimages_verified=7, installed=False, actual_test_gate_pending=True)))


def apply():
    prepared = read(ROOT/'PREPARED.v1.json')
    assert not (ROOT/'INSTALL.v1.json').exists(), 'Retain existing installation receipt'
    assert sha(Path(__file__).resolve()) == prepared['helper_sha256']
    assert sha(STAGE/'PREPARED.v1.json') == prepared['stage_receipt_sha256']
    result = read(CHECK/'RUN-RESULT.v1.json')
    assert result.get('finished_utc') and result['status'] == 'actual_host_and_renderer_mask_integration_tests_passed'
    assert result['host_tests_passed'] == 17 and result['actual_renderer_tests_passed'] > 0
    assert result['source_pins_unchanged'] and result['frozen_pins_unchanged']
    proof = read(CHECK/'PREPARED.v1.json')
    for path, digest in proof['frozen_pins'].items():
        assert sha(Path(path)) == digest, 'Tested snapshot changed: ' + path
    rows = prepared['rows']
    verify_rows(rows)
    receipt = dict(status='installing', proof_sha256=sha(CHECK/'RUN-RESULT.v1.json'),
                   files=[], full_import=False, current_product_build_verified=False,
                   gpu_render_qualified=False)
    save('INSTALL.v1.json', receipt)
    try:
        for row in rows:
            target = R/row['relative_path']
            backup = ROOT/'before'/row['relative_path']
            if row['original_sha256'] is not None:
                assert sha(target) == row['original_sha256']
                backup.parent.mkdir(parents=True, exist_ok=True)
                backup.write_bytes(target.read_bytes())
                assert sha(backup) == row['original_sha256']
            else:
                assert not target.exists()
            temporary = target.with_name(target.name + '.hivemind-mask-install.tmp')
            assert not temporary.exists()
            temporary.write_bytes(Path(row['candidate_path']).read_bytes())
            assert sha(temporary) == row['candidate_sha256']
            if row['original_sha256'] is not None:
                assert sha(target) == row['original_sha256']
            else:
                assert not target.exists()
            temporary.replace(target)
            receipt['files'].append(dict(**row, installed_sha256=sha(target)))
            save('INSTALL.v1.json', receipt)
        verify_rows(rows, installed=True)
        receipt.update(status='tested_source_files_installed_product_build_pending',
                       installed_files=7, installed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    except Exception as error:
        receipt.update(status='installation_refused_or_partial', error=repr(error))
        raise
    finally:
        save('INSTALL.v1.json', receipt)
    print(json.dumps(dict(installed_files=7, current_product_build_verified=False, full_import=False)))


if __name__ == '__main__':
    apply() if len(sys.argv)>1 and sys.argv[1]=='apply' else prepare()
