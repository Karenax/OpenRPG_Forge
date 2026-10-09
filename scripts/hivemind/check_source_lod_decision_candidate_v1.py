"""Qualify a generic Rust decision candidate against measured native LOD boundaries."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import struct
import subprocess
import sys

W = Path('F:/OpenRPG_ImportWork')
OUT = W / 'source-lod-decision-candidate-v1'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
CODE = r'''//! Selection from an explicit native comparison rule and current projected height.
//! The caller owns projection, LOD bias, renderer state and per-camera visibility.
#[derive(Clone, Copy, Debug)]
pub(crate) enum ComparisonRule { StrictGreater, GreaterOrEqual }

pub(crate) fn select_level(height: f32, thresholds: &[f32], rule: ComparisonRule)
    -> Result<Option<usize>, &'static str>
{
    if height.is_nan() || height < 0.0 || thresholds.is_empty() {
        return Err("Invalid projected height or absent original LOD thresholds");
    }
    if thresholds.iter().any(|value| !value.is_finite() || *value < 0.0 || *value > 1.0)
        || thresholds.windows(2).any(|pair| pair[0] < pair[1]) {
        return Err("Original LOD thresholds must be finite and descend without alteration");
    }
    Ok(thresholds.iter().position(|threshold| match rule {
        ComparisonRule::StrictGreater => height > *threshold,
        ComparisonRule::GreaterOrEqual => height >= *threshold,
    }))
}
'''


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def literal(value):
    bits = struct.unpack('<I', struct.pack('<f', value))[0]
    return f'f32::from_bits(0x{bits:08x})'


def main():
    OUT.mkdir(exist_ok=True)
    source = OUT / 'source_lod_decision.rs'
    if not source.exists():
        source.write_text(CODE, 'utf-8')
    if len(sys.argv) > 1 and sys.argv[1] == 'prepare':
        print('Generic decision candidate prepared on F; native comparator qualification still required.')
        return
    assert not (OUT / 'RESULT.v1.json').exists(), 'Preserve prior qualification'
    review_path = W / 'rocks-firepit-native-lod-boundary-reopen-v1/RESULT.v1.json'
    review = read(review_path)
    assert review['native_metric_comparison_rule_qualified'], 'Native boundary rule is not yet proven'
    assert len(review['comparison_rule_candidates']) == 1
    rule = {'strict_greater': 'StrictGreater', 'greater_or_equal': 'GreaterOrEqual'}[review['comparison_rule_candidates'][0]]
    native_path = W / 'rocks-firepit-native-lod-boundary-v1/NATIVE-RESULT.v1.json'
    assert sha(native_path) == review['input_pins'][str(native_path)]
    assembly_path = W / 'rocks-firepit-scene-bindings-v1/RESULT.v1.json'
    groups = {}
    for scene in read(assembly_path)['rows']:
        prefab = next(n['source_node']['gameobject']['asset_path'] for n in scene['nodes'] if n['source_node']['path'] == 'root')
        for group in scene['original_lod_groups']:
            groups[(prefab, group['node_path'])] = group
    cases = []
    for sample in read(native_path)['samples']:
        fields = {f['name']: f for f in sample['native_fields']}
        thresholds = ','.join(literal(level['screen_relative_height']) for level in groups[(sample['prefab_path'], sample['node_path'])]['levels'])
        height = literal(fields['activeRelativeScreenSize']['float_value'])
        level = fields['activeLODLevel']['integer_value']
        expected = 'None' if level == -1 else f'Some({level})'
        cases.append(f'assert_eq!(select_level({height}, &[{thresholds}], ComparisonRule::{rule}), Ok({expected}));')
    assert len(cases) == review['samples']
    harness = OUT / 'harness.rs'
    harness.write_text('#[path = "source_lod_decision.rs"] mod candidate;\nuse candidate::*;\n'
        + '#[test] fn every_native_boundary_case_matches(){\n' + '\n'.join(cases) + '\n}\n'
        + '''#[test] fn invalid_height_is_refused(){
            assert!(select_level(f32::NAN,&[0.5],ComparisonRule::StrictGreater).is_err());
            assert!(select_level(-0.1,&[0.5],ComparisonRule::GreaterOrEqual).is_err());
        }
        #[test] fn altered_original_threshold_order_is_refused(){
            assert!(select_level(0.3,&[0.2,0.5],ComparisonRule::StrictGreater).is_err());
        }
        #[test] fn nonfinite_or_absent_thresholds_are_refused(){
            assert!(select_level(0.3,&[f32::NAN],ComparisonRule::StrictGreater).is_err());
            assert!(select_level(0.3,&[],ComparisonRule::GreaterOrEqual).is_err());
        }
        ''', 'utf-8')
    paths = [source, harness, review_path, native_path, assembly_path, Path(__file__)]
    pins = {str(p): sha(p) for p in paths}
    commands = [[str(BIN / 'rustc.exe'), '--edition', '2024', '--test', str(harness), '-o', str(OUT / 'decision-tests.exe')],
                [str(OUT / 'decision-tests.exe'), '--nocapture']]
    for index, command in enumerate(commands):
        log = OUT / f'CHECK-{index}.log'
        with log.open('xb') as stream:
            code = subprocess.run(command, stdout=stream, stderr=subprocess.STDOUT).returncode
        assert code == 0, log
        pins[str(log)] = sha(log)
    assert '4 passed; 0 failed' in (OUT / 'CHECK-1.log').read_text('utf-8-sig')
    for path, expected in pins.items():
        assert sha(Path(path)) == expected
    result = dict(status='generic_rust_lod_decision_candidate_matches_every_native_boundary_sample',
        finished_utc=datetime.now(timezone.utc).isoformat(), native_cases_verified=len(cases), actual_tests_passed=4,
        explicit_native_comparison_rule=rule, input_pins=pins, canonical_installed=False,
        camera_projection_qualified=False, renderer_visibility_qualified=False,
        forge_lod_runtime_qualified=False, full_import=False)
    (OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k: result[k] for k in ('status', 'native_cases_verified', 'actual_tests_passed', 'explicit_native_comparison_rule')}))


if __name__ == '__main__':
    main()
