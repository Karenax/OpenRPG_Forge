"""Compare full recorded walks, never a selected favorable frame."""
import argparse
import hashlib
import json
import math
import copy
from pathlib import Path


def distribution(samples):
    if not samples or not all(math.isfinite(v) and v>0 for v in samples):
        raise ValueError('invalid frame samples')
    ordered = sorted(samples)
    count = len(samples)
    slow = ordered[-math.ceil(count/100):]
    return {'samples':count,'mean_fps':1000*count/math.fsum(samples),
            'p95_ms':ordered[math.ceil(.95*count)-1],
            'one_percent_low_fps':1000*len(slow)/math.fsum(slow)}


def comparable_water(report, refracted_candidate, candidate):
    water = copy.deepcopy(report['water_projection'])
    water.pop('animation_updates', None)
    if refracted_candidate:
        observed = water['forward_reflection_quality']
        fields = {'refraction_linear_steps': 'refraction_steps',
                  'refraction_max_path_m': 'refraction_max_depth_m'}
        for field, requested in fields.items():
            if candidate:
                if (observed.get(field) != observed['requested'][requested]
                        or not observed['projection_matches']):
                    raise ValueError('refraction candidate budget not projected')
                del observed[field]
            elif field in observed:
                raise ValueError('baseline unexpectedly includes refraction trace')
    return water


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('root',type=Path)
    parser.add_argument('--refraction-candidate', action='store_true',
                        help='Require the candidate-only refracted budget observations; compare all other water fields unchanged')
    args = parser.parse_args()
    root = args.root
    reports, pins, rows, hashes = [], [], [], {}
    for i in range(4):
        directory = root/f'run-{i}'
        data = (directory/'report.json').read_bytes()
        report = json.loads(data.decode('utf-8-sig'))
        process = json.loads((directory/'process.json').read_text(encoding='utf-8-sig'))
        if not process['exited'] or process['exit_code'] != 0: raise ValueError('run not complete')
        if report['schema'] != 'forge.walk-moving-measurement' or report['requested_ticks'] != 1800 or report['virtual_time_frozen']:
            raise ValueError('unexpected walk protocol')
        samples, poses = report['samples_ms'],report['poses']
        if len(samples) != len(poses) or not report['settled_grounded']:
            raise ValueError('incomplete walk')
        stats = distribution(samples)
        for key in ('mean_fps','p95_ms','one_percent_low_fps'):
            if abs(stats[key]-report['frame'][key])>1e-8: raise ValueError('Rust metric mismatch')
        thirds = []
        for start,end in ((1,600),(601,1200),(1201,1800)):
            thirds.append(distribution([s for s,p in zip(samples,poses) if start<=p['replay_tick']<=end]))
        rows.append({'run':i,'kind':'baseline' if i in (0,3) else 'candidate','full':stats,'thirds':thirds})
        reports.append(report)
        pins.append(json.loads((directory/'pins.json').read_text(encoding='utf-8-sig')))
        hashes[f'run-{i}/report.json'] = hashlib.sha256(data).hexdigest()
    if pins[0] != pins[3] or pins[1] != pins[2]: raise ValueError('inputs changed within a candidate')
    common_files = set.intersection(*(set(p) for p in pins))
    if len(common_files)<4 or any(len({p[name] for p in pins})!=1 for name in common_files):
        raise ValueError('common inputs changed')
    maps = [{p['replay_tick']:p for p in r['poses']} for r in reports]
    ticks = sorted(set.intersection(*(set(m) for m in maps)))
    if len(ticks)<900: raise ValueError('insufficient common route coverage')
    max_position, max_boom = 0,0
    for tick in ticks:
        reference = maps[0][tick]
        for poses in maps[1:]:
            pose = poses[tick]
            max_position = max(max_position,math.dist(reference['position_m'],pose['position_m']))
            max_boom = max(max_boom,abs(reference['camera_boom_distance_m']-pose['camera_boom_distance_m']))
            if any(reference[k]!=pose[k] for k in ('camera_pitch_radians','camera_yaw_radians')):
                raise ValueError('camera orientation changed')
    if max_position>.01 or max_boom>.01: raise ValueError('trajectory difference exceeds 1 cm')
    for index, report in enumerate(reports[1:], 1):
        if any(report[k]!=reports[0][k] for k in ('window','axis','shadow_projection')):
            raise ValueError('window, walk or shadows differ')
        # Only this frame-count-dependent counter is excluded, not water policy.
        if comparable_water(report, args.refraction_candidate, index in (1, 2)) != comparable_water(reports[0], args.refraction_candidate, False):
            raise ValueError('water or terrain changed')
    output = {'schema':'forge.foliage-atlas-abba','schema_version':1,'rows':rows,
              'report_hashes':hashes,'common_ticks':len(ticks),'max_position_delta_m':max_position,
              'max_camera_boom_delta_m':max_boom,'common_inputs':sorted(common_files),
              'runtime_promoted':False,'human_accepted':False,'target_gpu_qualified':False}
    output['refraction_candidate_comparison'] = args.refraction_candidate
    with (root/'comparison.json').open('x',encoding='utf-8') as stream:
        json.dump(output,stream,indent=2)
    for row in rows:
        print(row['run'],row['kind'],'full',round(row['full']['mean_fps'],2),
              'forest',round(row['thirds'][2]['mean_fps'],2),'p95',round(row['thirds'][2]['p95_ms'],2))
    print('COMMON_ROUTE_PASS',len(ticks),max_position,max_boom)


if __name__ == '__main__':
    main()
