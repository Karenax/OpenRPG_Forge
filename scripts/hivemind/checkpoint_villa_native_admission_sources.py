"""Retain exact admitted source code before viewport implementation continues."""
import datetime
import hashlib
import json
from pathlib import Path
import shutil

REPO = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
WORK = Path('F:/OpenRPG_ImportWork')
ROOT = WORK / 'villa-207-canonical-source-checkpoint-v1'
FIRST = WORK / 'native-scene-check-queue-recovery-v2/RUN-RESULT.v1.json'
REMAINING = WORK / 'villa-remaining-canonical-admission-v1/RUN-RESULT.v1.json'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def main():
    assert not ROOT.exists(), 'Existing source checkpoint preserved'
    first, remaining = read(FIRST), read(REMAINING)
    assert first['status'] == 'actual_module_tests_and_20_native_scene_admissions_verified'
    assert first['canonical_scenes'] == 20 and first['negative_controls_passed'] == 2
    assert remaining['status'] == '187_remaining_native_scenes_canonically_admitted_runtime_pending'
    assert remaining['canonical_scenes'] == 187 and len(remaining['closed_batches']) == 10
    report_pins = {str(path): sha(path) for path in [FIRST, REMAINING, Path(__file__).resolve()]}
    inputs = dict(first['input_pins'])
    for path, expected in remaining['input_pins'].items():
        assert path not in inputs or inputs[path] == expected, 'Admission stages disagree on source'
        inputs[path] = expected
    selected = {}
    for name, expected in inputs.items():
        path = Path(name)
        try:
            relative = path.relative_to(REPO)
        except ValueError:
            continue
        assert path.is_file()
        selected[path] = (relative, expected)
    assert selected, 'No qualified repository sources identified'
    ROOT.mkdir()
    result = {'status': 'preserving_admitted_source_code', 'rows': [],
              'native_admitted_scenes': 207, 'gpu_rendered': False,
              'studio_verified': False, 'full_import': False}
    try:
        for path, (relative, expected) in selected.items():
            assert sha(path) == expected, 'Qualified code changed before checkpoint: ' + str(path)
            target = ROOT / 'sources' / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(path, target)
            assert sha(target) == sha(path) == expected, 'Source copy differs'
            result['rows'].append({'original': str(path), 'copy': str(target), 'sha256': expected})
        for path, (_, expected) in selected.items():
            assert sha(path) == expected, 'Qualified code changed during checkpoint'
        for path, expected in report_pins.items():
            assert sha(path) == expected, 'Terminal admission report changed'
        result.update(status='207_native_admissions_and_exact_code_checkpoint_retained',
            source_files_preserved=len(selected), report_pins=report_pins,
            all_selected_source_hashes_reopened_before_after=True,
            source_snapshot_does_not_qualify_future_viewport_edits=True)
    except Exception as error:
        result.update(status='refused_or_unverified', error=str(error))
    result['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2) + '\n', 'utf-8')
    print(json.dumps({key: value for key, value in result.items() if key not in ['rows', 'report_pins']}))
    assert result['status'] != 'refused_or_unverified', result.get('error')


if __name__ == '__main__':
    main()
