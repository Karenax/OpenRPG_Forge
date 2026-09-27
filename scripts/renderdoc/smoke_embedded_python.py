"""Pinned QRenderDoc SDK smoke; optional explicitly requested development capture."""
import json
import os
from pathlib import Path
import sys
import traceback

exit_code = 1
report = {'schema': 'forge-renderdoc-python-smoke/v1', 'status': 'FAIL',
          'capture_launched': False, 'gpu_replay_executed': False,
          'python_version': sys.version}
try:
    import qrenderdoc
    import renderdoc

    expected_profile = Path(os.environ['FORGE_RD_SMOKE_PROFILE']).resolve(strict=True)
    actual_profile = Path(str(qrenderdoc.ConfigFilePath('UI.config'))).resolve(strict=True)
    if os.path.normcase(str(expected_profile)) != os.path.normcase(str(actual_profile)):
        raise RuntimeError('QRenderDoc used an unexpected profile')
    with actual_profile.open(encoding='utf-8-sig') as source:
        config = json.load(source)
        # QRenderDoc persists defaults on startup: only required invariants are fixed.
        if (config.get('rdocConfigData') != 1
                or config.get('Analytics_TotalOptOut') is not True
                or config.get('CheckUpdate_AllowChecks') is not False):
            raise RuntimeError('Disposable profile privacy settings changed')
    model_directory = Path(os.environ['FORGE_RD_SMOKE_MODEL']).resolve(strict=True)
    sys.path.insert(0, str(model_directory))
    try:
        from gate1_renderdoc_model import analyze_document
        import gate1_renderdoc_replay
        with (model_directory / 'fixtures/nominal_nested_actions.json').open(encoding='utf-8') as source:
            result = analyze_document(json.load(source))
        expected = {'draws': 4, 'instances': 7, 'triangles': 5, 'lines': 2, 'points': 15}
        if result['status'] != 'PASS' or any(result['metrics'][key] != value for key, value in expected.items()):
            raise RuntimeError('Embedded nominal accounting mismatch')
        if not callable(gate1_renderdoc_replay.analyze_capture):
            raise RuntimeError('Replay adapter import failed')
    finally:
        del sys.path[0]
    report.update(synthetic_nominal_metrics=expected, profile_isolated=True,
                  embedded_unittest_run=False, replay_adapter_imported=True)
    required_api = ['OpenCaptureFile', 'ExecuteAndInject', 'CreateTargetControl']
    if not all(callable(getattr(renderdoc, name, None)) for name in required_api):
        raise RuntimeError('Required RenderDoc entry point missing')
    report.update(status='PASS', api_entry_points=required_api)
    if os.environ.get('FORGE_RD_REPLAY_CAPTURE'):
        report.update(status='FAIL', gpu_replay_executed=None, replay_attempted=True)
        sys.path.insert(0, str(model_directory))
        try:
            from capture_candidate_probe import digest, write_new
            capture = Path(os.environ['FORGE_RD_REPLAY_CAPTURE']).resolve(strict=True)
            expected_hash = os.environ['FORGE_RD_REPLAY_SHA256'].lower()
            if digest(capture) != expected_hash:
                raise RuntimeError('Replay capture hash mismatch')
            folder = Path(os.environ['FORGE_RD_SMOKE_OUTPUT']).parent
            renderdoc.SetDebugLogFile(str(folder / 'replay-live.log'))
            write_new(folder / 'replay-input.json', {'path': str(capture), 'sha256': expected_hash})
            document, analysis = gate1_renderdoc_replay.analyze_capture(
                renderdoc, capture, gpu_durations=os.environ.get('FORGE_RD_GPU_DURATIONS') == '1',
                inspect_events=[int(x) for x in os.environ['FORGE_RD_INSPECT_EVENTS'].split(',')]
                if os.environ.get('FORGE_RD_INSPECT_EVENTS') else None)
            write_new(folder / 'actions.json', document)
            write_new(folder / 'analysis.json', analysis)
            if digest(capture) != expected_hash:
                raise RuntimeError('Capture changed during replay')
            report.update(status='PASS', gpu_replay_executed=True,
                          metrics=analysis['metrics'], qualification=False)
        finally:
            del sys.path[0]
    if os.environ.get('FORGE_RD_CAPTURE_PROBE') == '1':
        report.update(status='FAIL', capture_probe_requested=True,
                      capture_launched=None, gpu_replay_executed=None)
        sys.path.insert(0, str(model_directory))
        try:
            from capture_candidate_probe import run
            report.update(run(renderdoc, Path(os.environ['FORGE_RD_SMOKE_OUTPUT']).parent,
                              model_directory.parent.parent,
                              os.environ.get('FORGE_RD_BRIDGE_MANIFEST')))
            report['status'] = 'PASS'
        finally:
            del sys.path[0]
    exit_code = 0
except Exception as error:
    report['error'] = str(error)
    traceback.print_exc()
finally:
    output = Path(os.environ['FORGE_RD_SMOKE_OUTPUT'])
    with output.open('x', encoding='utf-8') as destination:
        json.dump(report, destination, indent=2)
raise SystemExit(exit_code)
