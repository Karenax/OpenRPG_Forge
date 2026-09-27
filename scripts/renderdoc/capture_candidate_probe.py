"""Development capture only: PNG readiness is not an atomic GPU-state receipt.

Uses the pinned candidate of 2026-09-08; never promotes a Gate or overlay metric.
Launched target has a bounded frame exit. Host must inspect its PID on failure.
"""
import hashlib
import json
from pathlib import Path
import time

from gate1_renderdoc_replay import result_succeeded


def write_new(path, value):
    with path.open('x', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2)


def digest(path):
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(chunk)
    return value.hexdigest()


def api_ready(name, presenting, supported):
    if not presenting:
        return False
    if name != 'Vulkan' or not supported:
        raise RuntimeError('Presenting API is not supported Vulkan: ' + name)
    return True


def legacy_inputs(output):
    evidence = Path('E:/OpenRPG_Forge/.gate1-evidence-runtime')
    baseline = evidence / 'r1-fixed-gpu-settle-20260908'
    with (baseline / 'pins.json').open(encoding='utf-8-sig') as stream:
        pins = json.load(stream)
    for pin in pins:
        if digest(Path(pin['Path'])).upper() != pin['Hash'].upper():
            raise RuntimeError('Candidate input changed: ' + pin['Path'])
    runtimes = [Path(pin['Path']) for pin in pins if pin['Path'].endswith('.exe')]
    if len(runtimes) != 1:
        raise RuntimeError('Expected one pinned runtime')
    runtime = runtimes[0].resolve(strict=True)
    if digest(runtime).upper() != '281218D783D8BDA33D481E8D8AB214C4F2D92DE7A42A1EF3316642B31CC65423':
        raise RuntimeError('Unexpected runtime build')
    kit = evidence / 'r1-conifer-sprays-20260907/runtime'
    png = output / 'readiness.png'
    arguments = [
        '--world-sketch-visual-valley', '--world-sketch-input',
        str(evidence / 'r1-camera-set-20260906-c/world-sketch.v1.json'),
        '--asset-kit-build', str(kit / 'asset-kit-build.v1.json'),
        '--asset-kit-preview', str(kit / 'runtime-preview.v1.json'),
        '--prepare-vegetation-catalog', str(kit / 'prototype-catalog.v2.json'),
        '--prototype-forest-batches', '--fixed-proof-view', 'human_bridge',
        '--weather', 'clear', '--quality', 'high', '--no-vsync',
        '--capture', str(png), '--capture-frame', '300', '--exit-after-frames', '1800',
    ]
    return runtime, pins, arguments, png


def bridge_inputs(output, manifest_path):
    """Rebuild an allowlisted command from pinned inputs, never execute manifest commands."""
    manifest_path = Path(manifest_path).resolve(strict=True)
    with manifest_path.open(encoding='utf-8') as stream:
        manifest = json.load(stream)
    if (manifest.get('schema') != 'forge.bridge-video-evidence'
            or manifest.get('status') != 'video_checks_passed_review_pending'
            or manifest.get('weather') not in ('clear', 'rain')
            or manifest.get('requested_quality') not in ('low', 'medium', 'high', 'ultra')
            or manifest.get('forest_mode') != 'batches'):
        raise RuntimeError('Unsupported bridge evidence manifest')
    inputs = {}
    pins = [{'Path': str(manifest_path), 'Hash': digest(manifest_path)}]
    names = ['runtime', 'world_sketch', 'kit_build', 'preview', 'forest_catalog']
    for flag, name in [('forward_water_candidate_requested', 'water_medium'),
                       ('cloud_layer_requested', 'cloud_profile')]:
        requested = manifest.get(flag, False)
        if not isinstance(requested, bool) or requested != (name in manifest['inputs']):
            raise RuntimeError('Inconsistent optional candidate input: ' + name)
        if requested:
            names.append(name)
    for name in names:
        pin = manifest['inputs'][name]
        path = Path(pin['path']).resolve(strict=True)
        if digest(path).lower() != pin['sha256'].lower():
            raise RuntimeError('Bridge input hash mismatch: ' + name)
        inputs[name] = path
        pins.append({'Path': str(path), 'Hash': pin['sha256']})
    arguments = ['--world-sketch-visual-valley', '--world-sketch-input', str(inputs['world_sketch']),
                 '--asset-kit-build', str(inputs['kit_build']), '--asset-kit-preview', str(inputs['preview']),
                 '--prepare-vegetation-catalog', str(inputs['forest_catalog']), '--prototype-forest-batches',
                 '--bridge-crossing-probe', '--bridge-frames', str(output / 'frames'),
                 '--weather', manifest['weather'], '--quality', manifest['requested_quality']]
    if 'water_medium' in inputs:
        arguments.extend(['--water-depth-preview', '--water-medium-input',
                          str(inputs['water_medium']), '--water-reflection-preview'])
    if 'cloud_profile' in inputs:
        arguments.extend(['--cloud-layer-preview', str(inputs['cloud_profile'])])
    # Screenshot readback lags rendering: capture after the defect, not an exact frame-pair claim.
    return inputs['runtime'], pins, arguments, output / 'frames' / 'frame-0162.png'


def run(rd, output, repository, bridge_manifest=None):
    rd.SetDebugLogFile(str(output / 'renderdoc-live.log'))
    runtime, pins, arguments, png = (bridge_inputs(output, bridge_manifest)
                                   if bridge_manifest else legacy_inputs(output))
    # All arguments are controlled and contain no quote or trailing backslash.
    if any('"' in arg or arg.endswith('\\') for arg in arguments):
        raise RuntimeError('Unsupported command-line quoting')
    command = ' '.join('"' + arg + '"' for arg in arguments)
    write_new(output / 'inputs.json', {'pins': pins, 'runtime': str(runtime),
              'arguments': arguments, 'qualification': False,
              'trigger_readback': str(png),
              'readiness': 'PNG readback only; no atomic capture-state receipt'})
    options = rd.CaptureOptions()
    options.allowVSync = False
    options.allowFullscreen = False
    options.apiValidation = False
    options.captureCallstacks = False
    options.hookIntoChildren = False
    options.refAllResources = False
    launch = rd.ExecuteAndInject(str(runtime), str(repository), command, [],
                                 str(output / 'candidate'), options, False)
    if not result_succeeded(rd, launch.result) or int(launch.ident) == 0:
        raise RuntimeError('Launch/injection failed: ' + str(launch.result))
    target = rd.CreateTargetControl('', int(launch.ident), 'ForgeCaptureProbe', True)
    if target is None:
        raise RuntimeError('Target control unavailable; inspect owned runtime')
    capture_message = None
    try:
        write_new(output / 'target.json', {'pid': int(target.GetPID()),
                  'ident': int(launch.ident), 'runtime': str(runtime)})
        deadline = time.monotonic() + 90
        triggered = False
        vulkan_ready = False
        while time.monotonic() < deadline:
            if not target.Connected():
                raise RuntimeError('Target disconnected before capture receipt')
            message = target.ReceiveMessage(None)
            if message.type != rd.TargetControlMessageType.Noop:
                with (output / 'target-events.jsonl').open('a', encoding='utf-8') as stream:
                    stream.write(json.dumps({'type': str(message.type), 'api': str(target.GetAPI()),
                                             'after_trigger': triggered}) + '\n')
            if message.type == rd.TargetControlMessageType.RegisterAPI:
                use = message.apiUse
                vulkan_ready = api_ready(str(use.name), bool(use.presenting), bool(use.supported))
            if message.type == rd.TargetControlMessageType.NewCapture:
                if not triggered:
                    raise RuntimeError('Unexpected capture before controlled trigger')
                captured = message.newCapture
                capture_message = dict(path=str(captured.path), local=bool(captured.local),
                                       byte_size=int(captured.byteSize),
                                       frame_number=int(captured.frameNumber), api=str(captured.api))
                break
            if not triggered and vulkan_ready and png.is_file() and png.stat().st_size > 8:
                if str(target.GetAPI()) != 'Vulkan':
                    raise RuntimeError('Target is not Vulkan: ' + str(target.GetAPI()))
                target.TriggerCapture(1)
                triggered = True
                write_new(output / 'trigger.json', {'pid': int(target.GetPID()),
                          'api': str(target.GetAPI()), 'atomic_readiness_verified': False})
            time.sleep(0.02)
        if capture_message is None:
            raise RuntimeError('No capture received within 90 seconds')
    finally:
        target.Shutdown()
        log = Path(str(rd.GetLogFile()))
        if log.is_file():
            with log.open('rb') as source, (output / 'renderdoc-preserved.log').open('xb') as dest:
                dest.write(source.read())
    write_new(output / 'capture-message.json', capture_message)
    path = Path(capture_message['path']).resolve(strict=True)
    path.relative_to(output.resolve(strict=True))
    if (not capture_message['local'] or path.suffix != '.rdc'
            or path.stat().st_size != capture_message['byte_size']):
        raise RuntimeError('Capture location/size mismatch')
    write_new(output / 'capture-pin.json', {'path': str(path), 'sha256': digest(path)})
    for pin in pins:
        if digest(Path(pin['Path'])).upper() != pin['Hash'].upper():
            raise RuntimeError('Candidate input changed during capture: ' + pin['Path'])
    return {'capture_launched': True, 'gpu_replay_executed': False,
            'capture_frame': capture_message['frame_number'],
            'capture_sha256': digest(path), 'replay_status': 'NOT RUN; separate process required',
            'qualification': False, 'atomic_readiness_verified': False}
