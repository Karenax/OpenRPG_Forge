"""Recheck retained native measurement evidence; never assert a complete Gate 1 PASS."""
import argparse
import hashlib
import json
import math
from pathlib import Path


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def validate(metrics, replay):
    require(metrics['gpu']['backend'] == 'Vulkan', 'wrong native backend')
    rows = {row['path']: row for row in metrics['render_diagnostics']}
    for suffix in ['cpu', 'gpu']:
        row = rows['render/main_opaque_pass_3d/elapsed_' + suffix]
        value = row['value']
        require(type(value) in (float, int) and math.isfinite(value) and value >= 0
                and row['suffix'] == 'ms', 'invalid opaque pass measurement')
    memory = metrics['gpu']['memory']
    require(memory['status'] == 'available'
            and memory['scope'] == 'backend_allocator_not_resident_vram_or_process_total',
            'allocator scope unavailable or changed')
    totals = memory['allocator']
    require(type(totals['allocated_bytes']) is int and type(totals['reserved_bytes']) is int
            and 0 < totals['allocated_bytes'] <= totals['reserved_bytes'], 'invalid allocator totals')
    require(metrics['render_work']['gpu_draw_calls'] is None, 'overlay draws must remain unavailable')
    require(replay['status'] == 'PASS' and replay['capture']['pipeline_type'] == 'Vulkan'
            and not replay['capture']['degraded'] and replay['capture']['present_count'] == 1,
            'invalid native capture')
    for name in ['draws', 'instances', 'triangles']:
        require(type(replay['metrics'][name]) is int and replay['metrics'][name] > 0,
                'invalid submitted ' + name)
    return {'native_measurement_checks': 'PASS', 'studio_gpu': 'WAIVED_ADR_0012',
            'gate1_pass': False, 'warmup_qualified': False,
            'allocator': totals, 'replay_submitted': replay['metrics']}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--evidence-root', type=Path, required=True)
    args = parser.parse_args()
    root = args.evidence_root.resolve(strict=True)
    folder = root / 'r1-measures-closure-20260910-b'
    pins = {
        folder / 'capture.png': 'e6642e0d396171158950bfeb2476380d6f606e9e11f0332b2f945a58857c7351',
        folder / 'metrics.json': '1a2478efcb9d8089f63d24b7e6dafab9b22ba716ba4b98e7fb3abe787bee23a0',
        folder / 'receipt.json': 'c728c1e6189449784297c07b241619f71c5856a7e1fcdbed41676413dba6d52f',
        root / 'r1-final-integrated-gpu-replay-20260910/analysis.json':
            '698a36c47f17f55d767c8044baedeaa55b171e8e70afe80cb76b1a9c8cf13a31',
        root / 'r1-final-integrated-gpu-20260910/candidate_frame286.rdc':
            '0484634ec6122cd72665fc360d6ef3401d21e076e2409d4aa006d2a9267131c7',
    }
    for path, expected in pins.items():
        require(digest(path) == expected, 'changed evidence: ' + str(path))
    read = lambda path: json.loads(path.read_text(encoding='utf-8-sig'))
    receipt = read(folder / 'receipt.json')
    require(receipt['process_exited'] is True and receipt['exit_code'] == 0, 'unclean proof exit')
    for source in receipt['pins']:
        require(digest(Path(source['Path'])) == source['Hash'].lower(), 'changed source input')
    events = [json.loads(line) for line in (folder / 'stderr.log').read_text(encoding='utf-8').splitlines()
              if line.startswith('{')]
    require(not any(e.get('level') == 'ERROR' for e in events), 'runtime error')
    stops = [e for e in events if e.get('event_kind') == 'runtime_stopped']
    require(len(stops) == 1 and stops[0]['success'] and not stops[0]['runtime_errors_observed'],
            'missing successful shutdown')
    result = validate(read(folder / 'metrics.json'),
                      read(root / 'r1-final-integrated-gpu-replay-20260910/analysis.json'))
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
