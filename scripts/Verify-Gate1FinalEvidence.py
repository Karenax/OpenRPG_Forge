"""Recheck pinned local evidence without launching a renderer or copying upstream assets."""
import argparse
import importlib.util
import json
from pathlib import Path
import subprocess


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(filename))
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


capture = module('capture', 'Capture-WorldSketch.py')
video = module('video', 'Capture-BridgeVideo.py')
studio_motion = module('studio_motion', 'gate1_studio_motion_evidence.py')


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def require(condition, message):
    if not condition:
        raise ValueError(message)


def pinned_inputs(manifest):
    for source in manifest['inputs'].values():
        require(capture.digest(Path(source['path'])) == source['sha256'], 'input changed: ' + source['path'])


def verify_pin(path, expected, files):
    actual = capture.digest(path)
    require(actual == expected, 'pinned evidence changed: ' + str(path))
    files[str(path)] = actual


def reference_observation(report):
    """Describe capture conditions; neither a hash nor sample presence proves an A/B."""
    size = report.get('size')
    require(isinstance(size, list) and len(size) == 2
            and all(type(value) is int and value > 0 for value in size),
            'invalid reference resolution')
    require(isinstance(report.get('stats'), list)
            and isinstance(report.get('errors'), list), 'invalid reference observations')
    require(isinstance(report.get('gpu'), str) and bool(report['gpu'].strip())
            and isinstance(report.get('quality'), str) and bool(report['quality'].strip()),
            'missing reference renderer or quality')
    return {'resolution': size, 'renderer_reported': report['gpu'],
            'quality_label': report['quality'], 'statistics_records': len(report['stats']),
            'reported_errors': len(report['errors']),
            'matched_benchmark_verified': False,
            'scope': 'historical_reference_metadata_not_profile_equivalence'}


def closure_supplement(root, files):
    """Verify the retained native capture and editable kit, without claiming human approval."""
    acquisition = root / 'r1-final-integrated-gpu-20260910'
    replay = root / 'r1-final-integrated-gpu-replay-20260910'
    preparation = root / 'r1-kit-human-ready-20260910'
    verify_pin(acquisition / 'candidate_frame286.rdc',
               '0484634ec6122cd72665fc360d6ef3401d21e076e2409d4aa006d2a9267131c7', files)
    verify_pin(replay / 'analysis.json',
               '698a36c47f17f55d767c8044baedeaa55b171e8e70afe80cb76b1a9c8cf13a31', files)
    verify_pin(preparation / 'inspection.json',
               '56b6be98b503e5e82f8017819e13f46b3cff0b86f94b4f76c43dcb012d0cf98d', files)
    inputs = read(acquisition / 'inputs.json')['pins']
    require(len(inputs) == 8 and len({p['Path'] for p in inputs}) == 8,
            'integrated GPU input coverage changed')
    for source in inputs:
        verify_pin(Path(source['Path']), source['Hash'], files)
    analysis = read(replay / 'analysis.json')
    require(analysis['status'] == 'PASS' and not analysis['capture']['degraded']
            and analysis['capture']['pipeline_type'] == 'Vulkan'
            and analysis['gpu_durations']['status'] == 'PASS_REPLAY_ONLY',
            'native replay qualification changed')
    inspection = read(preparation / 'inspection.json')
    require(inspection['human_retouch_performed'] is False
            and inspection['publication_approved'] is False, 'preparation is not human approval')
    assets = inspection['assets']
    require(len(assets) == 6 and len({a['id'] for a in assets}) == 6, 'incomplete editable kit')
    for asset in assets:
        require(asset['round_trip_equal'] is True, 'editable kit round trip differs')
        verify_pin(Path(asset['source']), asset['source_sha256'], files)
        verify_pin(Path(asset['blend']), asset['blend_sha256'], files)
    for folder in [acquisition, replay]:
        host = read(folder / 'host-result.json')
        require(host['status'] == 'PASS' and host['process_exited'] is True,
                'GPU proof host did not finish successfully')
        for name in ['host-result.json', 'smoke.json']:
            files[str(folder / name)] = capture.digest(folder / name)
    files[str(acquisition / 'inputs.json')] = capture.digest(acquisition / 'inputs.json')
    return {'native_gpu': {'backend': 'Vulkan', 'frames': 1, 'metrics': analysis['metrics'],
                           'timings_scope': 'replay_only_not_live_frame_time'},
            'editable_kit_files': len(assets), 'human_retouch_verified': False,
            'studio_gpu_qualified': False}


def current_native_views(root, files):
    """The explicitly pinned overlap candidate, not the historical capture series."""
    folder = root / 'r1-overlap-twelve-views-20260911'
    verify_pin(folder / 'manifest.json',
               '844749e6ec7cba4ef8fddb276b25195577a47322c20ae1b9a27fcb50dc4a2323', files)
    manifest = read(folder / 'manifest.json')
    require(manifest['inputs']['runtime']['sha256'] ==
            'f7b39f7536dbcfcc8180daf6ac614e96b9804633f4cd2f5524fc5258ad27bd5d',
            'wrong current native candidate')
    require(manifest['status'] == 'capture_checks_passed_review_pending'
            and manifest['fixed_optical'] is True and manifest['quality'] == 'high',
            'incomplete current capture protocol')
    capture.verify_complete_coverage(manifest, read(Path(manifest['inputs']['world_sketch']['path'])))
    for source in manifest['inputs'].values():
        verify_pin(Path(source['path']), source['sha256'], files)
    selected = []
    for run in manifest['runs']:
        target = studio_motion.contained(folder, run['id'])
        observed = capture.verify_fixed(target, read(target / 'preview.json'), 'high', True)
        require(observed['sha256'] == run['sha256'], 'current capture changed')
        for name, digest in observed['sha256'].items():
            verify_pin(target / name, digest, files)
        selected.append(str(target / 'capture.png'))
    return {'status': 'current_native_twelve_views_rechecked', 'captures': selected,
            'human_accepted': False, 'performance_qualified': False,
            'temporal_stability_qualified': False, 'gate1_pass': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--evidence-root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--include-studio-motion', action='store_true',
                        help='Verify the pinned September 11 moving Studio candidate separately.')
    parser.add_argument('--studio-candidate', choices=list(studio_motion.CANDIDATES),
                        default='preparation-budget', help='Explicit candidate; never relabel historical captures.')
    parser.add_argument('--include-current-native-views', action='store_true',
                        help='Recheck the pinned overlap native clear/rain twelve-view series.')
    args = parser.parse_args()
    require(args.include_studio_motion or args.studio_candidate == 'preparation-budget',
            '--studio-candidate requires --include-studio-motion')
    require(not args.include_current_native_views or
            (args.include_studio_motion and args.studio_candidate == 'preparation-overlap'),
            'current native views require the preparation-overlap source checks')
    root = args.evidence_root.resolve(strict=True)
    output = args.output.resolve()
    require(not output.exists(), 'fresh output required')
    files = {}

    def pin(path):
        files[str(path)] = capture.digest(path)

    selected = []
    for directory, subset in [('r1-final-twelve-views-20260910-b', False),
                              ('r1-final-water-views-20260910', True)]:
        folder = root / directory
        manifest = read(folder / 'manifest.json')
        pinned_inputs(manifest)
        require(manifest['fixed_optical'] and manifest['quality'] == 'high', 'wrong capture mode')
        if not subset:
            capture.verify_complete_coverage(manifest, read(Path(manifest['inputs']['world_sketch']['path'])))
        else:
            require({run['id'] for run in manifest['runs']} == {'water_detail-clear', 'water_detail-rain'}
                    and len(manifest['runs']) == 2, 'water pair incomplete')
        for run in manifest['runs']:
            target = folder / run['id']
            observed = capture.verify_fixed(target, read(target / 'preview.json'), 'high', True)
            require(observed['sha256'] == run['sha256'], 'capture output changed')
            for name in observed['sha256']:
                pin(target / name)
            if subset or not run['id'].startswith('water_detail-'):
                selected.append(str(target / 'capture.png'))
        pin(folder / 'manifest.json')
    require(len(selected) == 12, 'selected twelve-view dossier incomplete')

    videos = []
    for name in ['bridge-shadow', 'bridge-shadow-ultra-rain']:
        folder = root / 'r1-motion-final-20260910' / name
        manifest = read(folder / 'manifest.json')
        pinned_inputs(manifest)
        observed = video.verify_frames(folder, read(Path(manifest['inputs']['preview']['path'])),
                                       quality=manifest['requested_quality'], forest=True, batches=True)
        require(observed['frame_sha256'] == manifest['frame_sha256'], 'video frames changed')
        require(observed['runtime_log_sha256'] == manifest['runtime_log_sha256'], 'video log changed')
        require(capture.digest(folder / 'bridge.mp4') == manifest['video_sha256'], 'video changed')
        pin(folder / 'manifest.json')
        pin(folder / 'bridge.mp4')
        videos.append({'path': str(folder / 'bridge.mp4'), 'quality': manifest['requested_quality'],
                       'weather': manifest['weather'], 'mode': 'offline_manual_time',
                       'performance_qualified': False})

    kit = root / 'r1-motion-final-20260910' / 'kit'
    audit_path = root / 'r1-final-kit-audit-20260910' / 'audit.json'
    audit = read(audit_path)
    require(capture.digest(kit / 'build.json') == audit['kit_sha256'], 'kit build changed')
    audited = {a['id']: a['sha256'] for a in audit['assets']}
    for asset in read(kit / 'build.json')['assets']:
        require(capture.digest(kit / asset['glb']['path']) == audited[asset['id']] == asset['glb']['sha256'],
                'audited asset changed')
    pin(audit_path)
    for name in ['oak-reimport', 'pine-reimport']:
        folder = root / 'r1-motion-final-20260910' / name
        for render in read(folder / 'inspection.json')['renders']:
            require(capture.digest(folder / render['png']) == render['sha256'], 'Blender render changed')
            pin(folder / render['png'])
        pin(folder / 'inspection.json')

    repository = Path(__file__).resolve().parent.parent
    cadle = repository / 'artifacts/cadle-gate-c/qualification-script-c/tools/out'
    references = {}
    for name in ['forest', 'vale']:
        references[name] = reference_observation(read(cadle / f'r1-ab-{name}-20260906' / 'report.json'))
        for filename in ['report.json', 'shot-ground.png']:
            pin(cadle / f'r1-ab-{name}-20260906' / filename)
    for filename in ['Capture-WorldSketch.py', 'Audit-Gate1Kit.cjs', 'Verify-Gate1FinalEvidence.py']:
        pin(Path(__file__).with_name(filename))
    supplement = closure_supplement(root, files)
    current_studio = None
    if args.include_studio_motion:
        current_studio = studio_motion.collect(root, repository, files, read, verify_pin, args.studio_candidate)
        pin(Path(__file__).with_name('gate1_studio_motion_evidence.py'))
    head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=repository, text=True).strip()
    native_views = current_native_views(root, files) if args.include_current_native_views else None
    result = {'schema': 'forge.gate1-final-evidence-index', 'schema_version': 1,
              'status': 'partial_not_gate_pass', 'git_head': head,
              'clean_revision_verified': False, 'artifact_hashes_not_clean_source_revision': True,
              'selected_captures': selected, 'reused_videos': videos, 'sha256': files,
              'closure_supplement': supplement,
              'current_studio_motion': current_studio,
              'current_native_views': native_views,
              'historical_references': references,
              'assessment_scope': {'profile_prewarm': (
                                   'pinned_studio_ready_5_of_5_report_verified_not_new_runtime_acquisition'
                                   if current_studio else 'not_evaluated_by_this_collector'),
                                   'studio_gpu_waiver': 'owner_decision_see_ADR_0012_not_a_measurement'},
              'limits': ['Original water pair occluded; replaced by camera-only pair with identical geometry.',
                         'Videos use High clear and Ultra rain, not a matched High weather comparison.',
                         'Blender render hashes rechecked; no new human retouch or approval.',
                         'Cadle references read in place, historical and not a matched current benchmark.',
                         'Native GPU replay verified; Studio supplement contains readiness receipts, not GPU timing measurements.',
                         'Historical Godot reference inspected separately; final comparative human verdict incomplete.']}
    output.parent.mkdir(parents=True, exist_ok=True)
    capture.save(output, result)
    print(json.dumps({'status': result['status'], 'selected_captures': len(selected),
                      'videos_rechecked': len(videos), 'pinned_files': len(files), 'output': str(output)}))


if __name__ == '__main__':
    main()
