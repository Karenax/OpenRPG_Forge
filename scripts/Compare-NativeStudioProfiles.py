"""Read-only semantic profile parity check; never a visual/performance gate verdict.

Inputs: native JSONL, then matching Studio diagnostic exports, in native stage order.
Use --studio-sessions to identify actual process restarts, not to hide stale receipts.
"""
import argparse
import hashlib
import json
from pathlib import Path


def require(condition, message):
    if not condition:
        raise ValueError(message)


def ready(observation):
    application = observation['application']
    effective = observation['effective']
    gpu = observation['gpu_submission']
    require(application['status'] == 'plan_application_ready', 'application not ready')
    require(application['gpu_ready'] is True and effective['gpu_ready'] is True, 'GPU not ready')
    require(application['error'] is None and effective['error'] is None, 'application error')
    require(application['projected_domains'] == [True] * 7, 'missing domain')
    require(application['identity'] == gpu['identity'], 'stale GPU receipt')
    require(effective['plan_hash'] == application['identity']['plan_hash'], 'wrong plan receipt')
    require(gpu['queue_work_completed'] is True and gpu['matching_view_ready'] is True, 'queue/view not ready')
    require(gpu['assets']['inventory_ready'] is True, 'assets not ready')
    require(gpu['effects']['public_resources_ready'] is True and gpu['effects']['missing'] == [], 'effects not ready')
    require(gpu['pipelines_failed'] == 0 and gpu['pipelines_pending'] == 0, 'pipelines not ready')
    require(effective['plan']['blockers'] == [], 'plan blocked')
    return application['identity']['sequence']


def compare(native, studio, sessions):
    require(len(native) == len(studio) == len(sessions) and len(native) >= 4, 'stage count mismatch')
    require(set(x['requested_tier'] for x in native) == {'low', 'medium', 'high', 'ultra'}, 'four tiers required')
    fields = ('effective', 'source_bindings', 'requested_policy_hash', 'resolution_policy', 'substitutions', 'terrain_constraints')
    previous_native = -1
    previous_studio = {}
    rows = []
    for index, (left, right, session) in enumerate(zip(native, studio, sessions)):
        nseq, sseq = ready(left), ready(right)
        require(nseq > previous_native, 'stale native sequence')
        require(sseq > previous_studio.get(session, -1), 'stale Studio sequence')
        previous_native, previous_studio[session] = nseq, sseq
        require(left['requested_tier'] == right['requested_tier'], f'tier mismatch at {index}')
        for field in fields:
            require(left['effective']['plan'][field] == right['effective']['plan'][field], f'{field} mismatch at {index}')
        require(left['effective']['plan']['source_bindings'] == native[0]['effective']['plan']['source_bindings'], 'sources changed during cycle')
        rows.append({'tier': left['requested_tier'], 'native_sequence': nseq, 'studio_sequence': sseq, 'studio_session': session, 'semantic_match': True, 'gpu_ready_both': True})
    require(native[0]['requested_tier'] == native[-1]['requested_tier'] == 'high', 'High return required')
    require(native[0]['effective']['plan']['effective'] == native[-1]['effective']['plan']['effective'], 'High not restored')
    return {'status': 'SEMANTIC_PROFILE_PARITY_PASS', 'stages': rows, 'gate_passed': False, 'pixel_equivalence_verified': False, 'performance_qualified': False}


def load_inputs(native_path, studio_paths):
    events = [json.loads(line) for line in native_path.read_text(encoding='utf-8-sig').splitlines() if line.strip()]
    require(not any(e.get('level') == 'ERROR' for e in events), 'native ERROR event')
    require(sum(e.get('event_kind') == 'quality_cycle_completed' for e in events) == 1, 'native cycle incomplete')
    native = []
    for event in events:
        if event.get('event_kind') == 'global_quality_cycle_observed':
            raw = event['observation']
            require(raw.startswith('Ok(') and raw.endswith(')'), 'native observation failed')
            native.append(json.loads(json.loads(raw[3:-1])))
    studio = [json.loads(path.read_text(encoding='utf-8-sig'))['observations']['quality'] for path in studio_paths]
    return native, studio


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('native', type=Path)
    parser.add_argument('studio', type=Path, nargs='+')
    parser.add_argument('--studio-sessions', nargs='+', required=True)
    args = parser.parse_args()
    result = compare(*load_inputs(args.native, args.studio), args.studio_sessions)
    result['inputs'] = [{'path': str(p.resolve()), 'sha256': hashlib.sha256(p.read_bytes()).hexdigest()} for p in [args.native, *args.studio]]
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
