"""Pinned local Studio supplement; no process launch or graphical qualification."""
import math
import hashlib


PINS = {
    'studio/forge-studio-candidate.exe': '814d9f6de591c7cde6a78fd880fc52e6863393353a85c5a102d957541ca77ec9',
    'web/forge_studio_bg.wasm': 'c15bf38c10455af5d97f08d7569b3c4239263ffb87d80c928f04939be737dc2d',
    'studio-motion.json': '1973eb31480fe6848cff8ff98e06888c7d25373ecaf2463884b8ab757da87e5e',
    'studio-motion.png': '467a1fc565088e40f0268afc9fb74f82fc3cbdb612761ab34e911a5e90b4b00b',
    'source-inputs.json': '08603e9ed18562c4eb1f43f52ff92b8ce0d079419c90401bcc2cd019b2662298',
    'studio/build-proof.json': '4cc0977addfc581f73a0ceaa4c33fa2f9633669ae86685b2c510f19aa177e4d4',
    'cleanup.json': '127fec6307e0b97fafc75e2f3a0b88fec7284d558bbba169da940f6c99bcde11',
}

BANK_PINS = {
    'studio/forge-studio-candidate.exe': 'aae61b18688531cc12c0fa67e3be13a43e935c49f66d2dd5250fe383a1b6f455',
    'web/forge_studio_bg.wasm': 'b8370fa0f6e7c3db7b7220e8829b3ac185c1c835a3653ad38a89257fd5e2756b',
    'resume-2/studio-motion.json': '0c45e8555f7f85d6db006740f08968ed510b55b25d5f4d5160611f059eadb3ff',
    'resume-2/cleanup.json': '02a2116711fc851dde050021919f3d6cbd41207678066e7b15f6642dec1cbd32',
    'source-inputs.json': 'e25b542b8783e2160105528fe83976192ea63f1d02e36b8ad1affa35747a330f',
    'studio/build-proof.json': 'f3e8ef35fee6c42f812b36b23947e920fbeffd35ac04db68c1b785c6552b6ee7',
}

OVERLAP_PINS = {
    'studio/forge-studio-candidate.exe': '8f1e20030f14be5b2b2b84a29408394252600b7e2b0872df9fadac9ef8b13d90',
    'web/forge_studio_bg.wasm': '5fe12b0cd747d317511b1ea6db9e704ed370e157ab296b2973a511b26d533745',
    'studio-motion.json': '1be33875721d511bd005cc66b0e0ad22710f25bb15fb0f9f6a78422f28accbb9',
    'studio-motion.png': '8f85581ff11112545e89ab6df1b544b04ebeffc0ca3f963d36aee954dbe66b9e',
    'source-inputs.json': '2cde74b63bb77f2e7a44110d502026c72b075cec6c84f002bd30b8235ef2b661',
    'studio/build-proof.json': 'b445633b8ee3db31d22f90aa2b53639e5680af7d68f08796b4b26dc252bb6d3d',
    'cleanup.json': 'e77c636030ef04424e3b4bcc0161d567ddb9fc9ee3835179395eaf1ba9854394',
}

CANDIDATES = {
    'preparation-budget': ('r1-preparation-budget-20260911', PINS, 283),
    'bank-reuse': ('r1-terrain-bank-reuse-20260911', BANK_PINS, 283),
    'preparation-overlap': ('r1-mesh-preparation-overlap-20260911', OVERLAP_PINS, 284),
}


def contained(folder, name):
    path = (folder / name).resolve(strict=True)
    require(path.is_relative_to(folder.resolve()), 'evidence outside candidate directory')
    return path


def collect_overlap_parity(root, folder, pins, files, read, verify_pin):
    """Recheck retained parity reports and their payloads; do not rerun their scripts."""
    reports = {}
    for name, directory, digest, count in [
        ('native', 'r1-overlap-native-parity-20260911',
         'a359ca5ecf7f2a07e8574fa40ef8f32d9d44909c64c4c8fed8642d9b3e06a57d', 8),
        ('medium_ultra', 'r1-overlap-studio-medium-ultra-20260911',
         '49511d6ad7587bc4806bf4bc4e0d0b79d32930c21458c8b26b051708f1c766a7', 5),
    ]:
        target = root / directory
        verify_pin(target / 'verification.json', digest, files)
        report = read(target / 'verification.json')
        require(len(report['sha256']) == count and report['gate1_pass'] is False,
                'invalid retained parity coverage')
        for path, expected in report['sha256'].items():
            verify_pin(contained(target, path), expected, files)
        reports[name] = report
    native = root / 'r1-overlap-native-parity-20260911'
    verify_pin(native / 'source-inputs.json', OVERLAP_PINS['source-inputs.json'], files)
    require(read(native / 'source-inputs.json') == pins, 'different native source snapshot')
    proof = read(folder / 'studio/build-proof.json')
    for pin in proof['frontend_pins']:
        verify_pin(contained(folder / 'studio/web', pin['path']), pin['sha256'].lower(), files)
    require(reports['native']['native_tiers'] == ['high', 'low', 'medium', 'ultra', 'high']
            and [c['tier'] for c in reports['medium_ultra']['comparisons']] == ['medium', 'ultra'],
            'incomplete retained profile coverage')
    return {'status': 'retained_functional_parity_reports_and_payloads_rechecked',
            'reports': reports, 'profiles': ['low', 'medium', 'high', 'ultra'],
            'new_runtime_acquisition': False, 'semantic_comparator_rerun': False,
            'pixel_equivalence_verified': False, 'fluidity_qualified': False,
            'human_acceptance': False, 'gate1_pass': False}


def source_drift_allowed(candidate, path, old, new):
    """One reviewed documentary change, never a generic source-drift waiver."""
    return (candidate == 'bank-reuse'
            and path.replace('\\', '/') == 'crates/forge_engine/README.md'
            and old.lower() == '4a84ca12d6d6c0213d2d1cac480f850ab6ca6d93542464fad8080773f7183c03'
            and new == '0773afe7706259184afeaeeb79336c98a457248243733cf73f8f914dd45d0961')


def require(condition, message):
    if not condition:
        raise ValueError(message)


def horizontal(vector):
    require(isinstance(vector, list) and len(vector) == 3
            and all(type(v) in (int, float) and math.isfinite(v) for v in vector),
            'invalid movement vector')
    return math.hypot(vector[0], vector[2])


def assess(report):
    motion = report['observations']['quality_motion']
    require(motion['schema'] == 'forge.studio-quality-motion-proof'
            and motion['schema_version'] == 1
            and motion['protocol'] == 'prewarmed_bridge_cycle'
            and motion['status'] == 'PASS: GPU High→Low→High en déplacement',
            'unqualified motion protocol')
    warmup = motion['profile_preparation']
    require(warmup['phase'] == 'ready' and warmup['error'] is None
            and [r['tier'] for r in warmup['completed']] == ['low', 'medium', 'high', 'ultra', 'high'],
            'incomplete profile preparation')
    records = motion['records']
    require([r['tier'] for r in records] == ['high', 'low', 'high'], 'incomplete moving cycle')
    sources = records[0]['quality']['effective']['plan']['source_bindings']
    require(set(sources) == {'terrain', 'vegetation', 'environment', 'water', 'weather'},
            'incomplete source bindings')
    ticks = []
    for record in records:
        require(horizontal(record['velocity']) > 0.05
                and horizontal(record['effective_translation']) > 0.0001, 'stationary record')
        quality = record['quality']
        application, effective, gpu = (quality[name] for name in ['application', 'effective', 'gpu_submission'])
        identity = application['identity']
        require(application['status'] == effective['status'] == 'plan_application_ready'
                and application['gpu_ready'] is True and effective['gpu_ready'] is True
                and application['error'] is None and effective['error'] is None
                and identity == gpu['identity']
                and identity['plan_hash'] == effective['plan_hash']
                and identity['adapter_generation'] == effective['plan']['adapter_generation'],
                'stale or incomplete GPU receipt')
        require(effective['plan']['source_bindings'] == sources
                and effective['plan']['requested_tier'] == quality['requested_tier'] == record['tier'],
                'source or tier mismatch')
        require(gpu['queue_work_completed'] is True and gpu['matching_view_ready'] is True
                and gpu['pipelines_failed'] == gpu['pipelines_pending'] == 0,
                'GPU work not ready')
        require(gpu['assets']['inventory_ready'] is True
                and gpu['effects']['public_resources_ready'] is True
                and not gpu['effects']['missing'], 'GPU resources missing')
        ticks.append(record['fixed_tick'])
    require(all(type(t) is int and t > 0 for t in ticks) and ticks[0] < ticks[1] < ticks[2],
            'unordered moving observations')
    require(len(motion['diagnostic_trace']) == 3, 'missing temporal trace')
    return {'status': 'functional_prewarmed_moving_cycle_verified',
            'protocol': motion['protocol'], 'recorded_ticks': ticks,
            'source_bindings': sources, 'profile_preparation': warmup,
            'diagnostic_trace': motion['diagnostic_trace'],
            'fluidity_qualified': False, 'visual_review_by_collector': False,
            'studio_gpu_measurements_qualified': False,
            'same_candidate_as_historical_capture_series': False}


def collect(root, repository, files, read, verify_pin, candidate='preparation-budget'):
    require(candidate in CANDIDATES, 'unknown Studio candidate')
    bank = candidate == 'bank-reuse'
    directory, candidate_pins, source_count = CANDIDATES[candidate]
    folder = root / directory
    run = folder / 'resume-2' if bank else folder
    for name, digest in candidate_pins.items():
        verify_pin(folder / name, digest, files)
    pins = read(folder / 'source-inputs.json')
    require(pins['branch'] == 'Rust' and pins['clean_revision'] is False,
            'unexpected source revision claim')
    require(len(pins['files']) == source_count and len({p['path'] for p in pins['files']}) == source_count,
            'incomplete source pins')
    drift = []
    for pin in pins['files']:
        path = (repository / pin['path']).resolve(strict=True)
        require(path.is_relative_to(repository.resolve()), 'source outside repository')
        actual = hashlib.sha256(path.read_bytes()).hexdigest()
        if actual != pin['sha256'].lower():
            require(source_drift_allowed(candidate, pin['path'], pin['sha256'], actual),
                    'unreviewed source drift: ' + pin['path'])
            drift.append({'path': pin['path'], 'snapshot_sha256': pin['sha256'].lower(),
                          'current_sha256': actual, 'scope': 'reviewed_README_paragraph_move'})
        verify_pin(path, actual if drift and drift[-1]['path'] == pin['path'] else pin['sha256'].lower(), files)
    cleanup = read(run / 'cleanup.json')
    require(cleanup['remaining_pids'] == [] and len(set(cleanup['owned_pids'])) == (14 if bank else 8),
            'candidate cleanup receipt incomplete')
    result = assess(read(run / 'studio-motion.json'))
    result.update(candidate=candidate, source_head=pins['head'], source_files_verified=source_count - len(drift),
                  current_worktree_exact_match=not drift, reviewed_documentary_drift=drift,
                  viewport_png_verified=not bank,
                  normal_exit_verified=candidate == 'preparation-budget',
                  acquisition_limit=('PNG missing; forced root exit after minimized window' if bank else None),
                  candidate_directory=str(folder), clean_revision_verified=False,
                  cleanup_scope='recorded_termination_not_current_machine_inventory')
    if candidate == 'preparation-overlap':
        result['normal_exit_scope'] = 'not_reestablished_by_cleanup_receipt_without_exit_code'
        result['current_native_studio_parity'] = collect_overlap_parity(
            root, folder, pins, files, read, verify_pin)
    return result
