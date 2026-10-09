"""Compare actual source/native uniform clocks; never qualify image parity."""
import argparse
import hashlib
import json
import math
from pathlib import Path

COMMIT = '1438b1abfcaee3267092b75573014f4d9b4a983c'


def compare(source, native):
    failures = []
    measurements = []
    reference = source.get('animation_clocks') or {}
    observed = native.get('diagnostic') or {}
    if source.get('source_commit') != COMMIT:
        failures.append('Reference commit missing or different')
    if source.get('status') != 'ready_reference_only':
        failures.append('Reference acquisition is not ready')

    def field(obj, *keys):
        for key in keys:
            if not isinstance(obj, dict):
                return None
            obj = obj.get(key)
        return obj

    def numeric(value):
        return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)

    def value(name, expected, actual, tolerance=0.0001):
        valid = numeric(expected) and numeric(actual)
        difference = actual - expected if valid else None
        passed = valid and abs(difference) <= tolerance
        measurements.append(dict(name=name, source=expected if numeric(expected) else None,
                                 native=actual if numeric(actual) else None,
                                 difference=difference, tolerance=tolerance, passed=passed))
        if not passed:
            failures.append(f'{name}: missing, invalid or different uniform')

    value('scene_global_time', reference.get('global_time_s'), field(observed, 'scene_wind', 'time'))
    value('virtual_global_time', reference.get('global_time_s'), observed.get('virtual_time_s'))
    value('plant_time', reference.get('global_time_s'), field(observed, 'wind_pose', 'time'))
    value('ocean_time', reference.get('ocean_time_s'), field(observed, 'ocean_clock', 'completed', 'time'))
    cloud = reference.get('cloud_clock')
    for i, key in [(1, 'time'), (2, 'dt')]:
        value(f'cloud_{key}', cloud[i] if isinstance(cloud, list) and len(cloud) == 4 else None,
              field(observed, 'cloud_clock', key))
    for group in ['scene_wind', 'wind_pose']:
        value(f'{group}_speed', reference.get('wind_speed'), field(observed, group, 'speed'))
        expected = reference.get('wind_direction')
        actual = field(observed, group, 'direction')
        for i in range(2):
            value(f'{group}_direction_{i}', expected[i] if isinstance(expected, list) and len(expected) == 2 else None,
                  actual[i] if isinstance(actual, list) and len(actual) == 2 else None)
    if observed.get('comparison_hold') is not True or observed.get('pending') is not False:
        failures.append('Native comparison is not held with shadow staging drained')
    ocean = observed.get('ocean_clock') or {}
    if ocean.get('paused') is not True or ocean.get('quiescent') is not True:
        failures.append('Native ocean transaction is not paused and drained')
    submitted = field(observed, 'cloud_clock', 'submitted_step')
    encoded = field(observed, 'cloud_clock', 'encoded_step')
    def counter(value):
        return isinstance(value, int) and not isinstance(value, bool) and 1 <= value <= 0xffffffff

    if not counter(submitted) or not counter(encoded) or encoded < submitted:
        failures.append('Submitted cloud transaction has not been encoded')
    return dict(schema='forge.tidewater.animation-clock-pair', version=1,
                status='uniform_clocks_match' if not failures else 'failed_closed',
                failures=failures, measurements=measurements,
                visual_parity=False, gpu_completion_verified=False, temporal_parity_qualified=False,
                limitations=['Uniform times only; camera, source frame index and temporal histories require separate checks.',
                             'Plant gust displacement and pendulum phases are not observed by this receipt.',
                             'Encoded commands are not a GPU completion fence or a pixel comparison.'])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('native', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    data = []
    pins = []
    for path in [args.source, args.native]:
        raw = path.read_bytes()
        data.append(json.loads(raw))
        pins.append(dict(path=str(path.resolve()), sha256=hashlib.sha256(raw).hexdigest()))
    result = compare(*data)
    result['inputs'] = pins
    with args.output.open('x', encoding='utf-8') as stream:
        json.dump(result, stream, indent=2, allow_nan=False)
        stream.write('\n')
    print(json.dumps(dict(status=result['status'], failures=len(result['failures']))))
    return 0 if not result['failures'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
