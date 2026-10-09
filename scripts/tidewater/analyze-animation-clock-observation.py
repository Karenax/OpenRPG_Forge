"""Measure clock drift and held-state changes; never qualify render parity."""
import argparse
import hashlib
import json
import math
from pathlib import Path

FIELDS = {
    'virtual_time': ('virtual_time_s',),
    'scene_wind_time': ('scene_wind', 'time'),
    'plant_time': ('wind_pose', 'time'),
    'plant_frame': ('wind_pose', 'frame_index'),
    'ocean_time': ('ocean_clock', 'completed', 'time'),
    'cloud_time': ('cloud_clock', 'time'),
    'cloud_dt': ('cloud_clock', 'dt'),
    'cloud_source_frame': ('cloud_clock', 'frame_index'),
    'cloud_submitted': ('cloud_clock', 'submitted_step'),
    'cloud_encoded': ('cloud_clock', 'encoded_step'),
}


def analyze(ledger):
    failures = []
    if ledger.get('schema') != 'forge.tidewater.animation-clock-observation' or ledger.get('version') != 1:
        failures.append('Wrong observation schema')
    if ledger.get('status') != 'observed' or ledger.get('failure') is not None:
        failures.append('Capture did not finish successfully')
    if ledger.get('listener_closed') is not True:
        failures.append('Owned listener not closed')
    samples = {}
    for phase in ('before', 'held', 'later'):
        diagnostic = (ledger.get(phase) or {}).get('diagnostic') or {}
        samples[phase] = {}
        for name, path in FIELDS.items():
            value = diagnostic
            for key in path:
                value = value.get(key) if isinstance(value, dict) else None
            valid = isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)
            samples[phase][name] = value if valid else None
            if not valid:
                failures.append(f'{phase}.{name}: missing or invalid measurement')
        if phase != 'before':
            ocean = diagnostic.get('ocean_clock') or {}
            if diagnostic.get('comparison_hold') is not True or diagnostic.get('pending') is not False:
                failures.append(f'{phase}: hold or shadow drain absent')
            if ocean.get('paused') is not True or ocean.get('quiescent') is not True:
                failures.append(f'{phase}: ocean pause or drain absent')
    changes = {}
    for name in FIELDS:
        a, b = samples['held'][name], samples['later'][name]
        changes[name] = b - a if a is not None and b is not None else None
    offsets = {}
    for phase, sample in samples.items():
        offsets[phase] = {}
        for name in ('scene_wind_time', 'plant_time', 'ocean_time', 'cloud_time'):
            a, b = sample[name], sample['virtual_time']
            offsets[phase][name] = a - b if a is not None and b is not None else None
    return dict(schema='forge.tidewater.animation-clock-analysis', version=1,
                status='measured' if not failures else 'failed_closed', failures=failures,
                samples=samples, offsets_from_virtual_time=offsets, changes_while_held=changes,
                clocks_frozen=(not failures and all(changes[name] == 0 for name in
                              ('virtual_time', 'scene_wind_time', 'plant_time', 'ocean_time', 'cloud_time'))),
                cloud_transactions_unchanged=(not failures and all(changes[name] == 0 for name in
                                             ('cloud_source_frame', 'cloud_submitted', 'cloud_encoded'))),
                visual_parity=False, temporal_parity_qualified=False, gpu_completion_verified=False,
                limitations=['Diagnostic uniform and submission counters only; GPU pixels and temporal history contents are not inspected.',
                             'The later sample may follow viewport resizing; changed cloud transactions can reflect that resize.'])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('ledger', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    raw = args.ledger.read_bytes()
    result = analyze(json.loads(raw))
    result['input'] = dict(path=str(args.ledger.resolve()), sha256=hashlib.sha256(raw).hexdigest())
    with args.output.open('x', encoding='utf-8') as stream:
        json.dump(result, stream, indent=2, allow_nan=False)
    print(json.dumps(dict(status=result['status'], failures=result['failures'])))
    return 0 if not result['failures'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
