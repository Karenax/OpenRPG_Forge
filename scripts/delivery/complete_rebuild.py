"""Build the shared checkout; retain the explicit frozen workflow for archives only."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
from datetime import datetime, timezone


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def write(path, value):
    path.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')


SOURCE_SCOPES = ['crates', 'apps', 'tools', 'third_party', 'scripts', 'examples',
                 'Cargo.toml', 'Cargo.lock', 'rust-toolchain.toml', '.cargo', '.gitignore',
                 '.github', 'deny.toml']
MAX_VALIDATED_CARGO_BUILD_JOBS = 64


def git_paths(source, *arguments, full_checkout=False):
    command = ['git', '-C', str(source), *arguments]
    if not full_checkout:
        command.extend(['--', *SOURCE_SCOPES])
    output = subprocess.check_output(command)
    return sorted(set(filter(None, output.decode('utf-8').split('\0'))))


def regular_input(source, path):
    for part in [path, *path.parents]:
        if part == source.parent:
            break
        if part.is_symlink() or getattr(part.lstat(), 'st_file_attributes', 0) & 0x400:
            raise ValueError(f'Reparse input refused: {part}')
    if not path.is_file():
        raise ValueError(f'Non-file build input refused: {path}')


def shared_source_inputs(source):
    """Use the common build map; retain the complete checkout inventory as evidence.

    Only Hivemind Python orchestration is outside the compiled-input map. The
    common classifier retains every other format there, including Rust includes.
    Git state and informative checkout changes do not alter compiled source bytes.
    """
    validator = gate3_validator(source)
    build_files, checkout_files = validator.source_inventories()
    tracked = git_paths(source, 'ls-files', '-z', '--cached', full_checkout=True)
    untracked = git_paths(source, 'ls-files', '-z', '--others', '--exclude-standard', full_checkout=True)
    modified = git_paths(source, 'diff', '--name-only', '-z', 'HEAD', full_checkout=True)
    local = sorted(checkout_files.keys() - set(tracked) - set(untracked))
    pins = {name: {'sha256': pin, 'bytes': (source / name).stat().st_size}
            for name, pin in build_files.items() if pin is not None}
    deleted = [name for name, pin in build_files.items() if pin is None]
    return {'schema': 'forge.delivery.shared-source-inputs.v1',
            'source_root': str(source), 'scope': SOURCE_SCOPES,
            'head': subprocess.check_output(['git', '-C', str(source), 'rev-parse', 'HEAD'],
                                            text=True).strip(),
            'contains_uncommitted_work': bool(modified or untracked),
            'tracked': tracked, 'modified': modified, 'untracked': untracked,
            'ignored_local_inputs': local, 'deleted': deleted, 'files': pins,
            'working_tree_files': checkout_files,
            'source_scope_exclusions': ['scripts/hivemind/**/*.py (orchestration only)']}


def frontend_inputs(frontend):
    pins = []
    for path in sorted(frontend.rglob('*'), key=lambda path: path.relative_to(frontend).as_posix()):
        if path.is_symlink() or getattr(path.lstat(), 'st_file_attributes', 0) & 0x400:
            raise ValueError(f'Reparse frontend entry refused: {path}')
        if path.is_file():
            regular_input(frontend, path)
            pins.append({'path': path.relative_to(frontend).as_posix(),
                         'sha256': sha(path), 'bytes': path.stat().st_size})
    return pins


def source_file_hashes(manifest):
    """Compare source bytes and deletions independently of receipt metadata."""
    result = {name: pin['sha256'] for name, pin in manifest['files'].items()}
    for name in manifest['deleted']:
        if name in result:
            raise ValueError(f'Source input is both present and deleted: {name}')
        result[name] = None
    return dict(sorted(result.items()))


def source_inputs_equal(before, after):
    return (before.get('source_root') == after.get('source_root') and
            source_file_hashes(before) == source_file_hashes(after))


def informative_checkout_changes(before, after, validator):
    return validator.working_tree_changes(before['working_tree_files'], after['working_tree_files'])


def gate3_validator(source):
    path = source / 'scripts/Validate-Gate3WorkingTree.py'
    specification = importlib.util.spec_from_file_location('forge_shared_gate3_validation', path)
    if specification is None or specification.loader is None:
        raise ValueError('Common Gate 3 validator is unavailable')
    validator = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(validator)
    if validator.SOURCE.resolve() != source.resolve():
        raise ValueError('Gate 3 validator belongs to another checkout')
    return validator


def shared_frontend(frontend, source_files, validator):
    closed = validator.validate_frontend(
        frontend, source_files, require_assets=True,
        expected_asset_manifest_sha256=validator.LOCAL_ASSET_MANIFEST_SHA256)
    return [{'path': name, **pin} for name, pin in sorted(closed['files'].items())]


def shared_build_environment(cache):
    env = dict(os.environ, CARGO_TARGET_DIR=str(cache), CARGO_BUILD_JOBS='1',
               CARGO_INCREMENTAL='0', CARGO_NET_OFFLINE='true', NO_COLOR='true',
               RUSTFLAGS='-C debuginfo=0', CARGO_TERM_COLOR='never')
    env.pop('TAURI_CONFIG', None)
    return env


def adopt_validated_cargo_jobs(environment, env):
    """Copy only the authenticated receipt's bounded Cargo job intent."""
    raw = environment.get('CARGO_BUILD_JOBS')
    if (not isinstance(raw, str) or not raw.isascii() or
            not raw.isdecimal()):
        raise ValueError('Validated CARGO_BUILD_JOBS must be an ASCII decimal string')
    jobs = int(raw)
    if raw != str(jobs) or not 1 <= jobs <= MAX_VALIDATED_CARGO_BUILD_JOBS:
        raise ValueError(
            f'Validated CARGO_BUILD_JOBS must be between 1 and '
            f'{MAX_VALIDATED_CARGO_BUILD_JOBS}')
    # The caller records this same map in the adoption proof. Do not accept
    # the receipt by merely dropping jobs from the environment comparison.
    env['CARGO_BUILD_JOBS'] = str(jobs)
    return str(jobs)


def release_stage_command(validator, name):
    for stage_name, command, cwd in validator.STAGES['release']:
        if stage_name == name:
            return command, cwd
    raise ValueError(f'Validated release stage is absent: {name}')


def validated_gate3_inputs(validator, evidence, stage, source_files, cache, env):
    """Adopt authenticated outputs; completed_receipt performs the common closure."""
    mode = 'wasm' if stage == 'prepare-frontend' else 'release'
    manifest, receipt = validator.completed_receipt(evidence, mode, source_files, cache)
    environment = receipt.get('build_environment', {})
    adopt_validated_cargo_jobs(environment, env)
    keys = ('CARGO_TARGET_DIR', 'CARGO_BUILD_JOBS', 'CARGO_INCREMENTAL',
            'CARGO_NET_OFFLINE', 'RUSTFLAGS', 'CARGO_TERM_COLOR', 'NO_COLOR')
    if (manifest.get('build_environment') != environment or
            any(environment.get(key) != env.get(key) for key in keys) or
            environment.get('TAURI_CONFIG') is not None):
        raise ValueError('Validated Gate 3 build environment differs from the common delivery intent')
    frontend = receipt['frontend']
    if (frontend.get('assets_present') is not True or
            frontend.get('local_asset_manifest_sha256') != validator.LOCAL_ASSET_MANIFEST_SHA256):
        raise ValueError('Validated common frontend lacks the approved complete local assets')
    pins = [{'path': name, **pin} for name, pin in sorted(frontend['files'].items())]
    links = {
        'evidence': str(evidence), 'mode': mode,
        'driver_sha256': receipt['driver_sha256'],
        'source_manifest_sha256': sha(evidence / f'{mode}-source-manifest.json'),
        'result_sha256': sha(evidence / f'{mode}-result.json'),
        'frontend': frontend,
    }
    executable = None
    if stage in ('studio', 'game'):
        relative = ('release/forge-studio.exe' if stage == 'studio'
                    else 'release/examples/tidewater_plant_probe.exe')
        executable = cache / relative
        # completed_receipt verifies the exact release artifact inventory and
        # its WASM/source/log chain. Retain the original artifact pin in the
        # delivery adoption instead of rebuilding or copying a candidate.
        pin = next((pin for pin in receipt['artifacts']
                    if Path(pin['path']).resolve() == executable), None)
        if pin is None:
            label = 'Studio' if stage == 'studio' else 'native game'
            raise ValueError(f'Validated common {label} release artifact is absent')
        links.update(artifact=pin)
        if stage == 'studio':
            links.update(
                wasm_source_manifest_sha256=sha(evidence / 'wasm-source-manifest.json'),
                wasm_result_sha256=sha(evidence / 'wasm-result.json'))
    return executable, pins, links, environment


def shared_rebuild(args):
    source = Path(__file__).resolve().parents[2]
    actual_root = Path(subprocess.check_output(['git', '-C', str(source), 'rev-parse',
                                               '--show-toplevel'], text=True).strip()).resolve()
    if actual_root != source:
        raise ValueError('Run the delivery tool from the common Git checkout')
    if args.stage == 'reuse-launcher':
        raise ValueError('reuse-launcher is disabled; it does not rebuild the shared applications')
    validated_evidence = getattr(args, 'validated_gate3_evidence', None)
    if validated_evidence and args.stage not in ('prepare-frontend', 'studio', 'game'):
        raise ValueError('--validated-gate3-evidence adopts only the canonical frontend, Studio, or game release')
    if args.frontend_assets or args.previous_build:
        raise ValueError('Shared builds use the canonical Trunk asset hook, not candidate inputs')
    if not args.cache:
        raise ValueError('--cache is required for every shared stage, including the Trunk worker')
    cache = args.cache.resolve()
    base = args.evidence.resolve()
    for location in (base, cache):
        if location == source or any(location.is_relative_to(source / scope)
                                     for scope in SOURCE_SCOPES):
            raise ValueError('Evidence and Cargo cache must stay outside build input scopes')
    base.mkdir(parents=True, exist_ok=True)
    if (base / 'source').exists() or (base / 'frontend').exists():
        raise ValueError('Shared evidence must not contain a source or frontend copy')
    for name in [f'{args.stage}-commands.json', f'{args.stage}-build-proof.json',
                 f'{args.stage}-source-before.json', f'{args.stage}-source-after.json',
                 f'{args.stage}-result.json']:
        if (base / name).exists():
            raise ValueError(f'Stage evidence already exists: {name}; retain it and use fresh evidence')
    before = shared_source_inputs(source)
    source_files = source_file_hashes(before)
    validator = gate3_validator(source)
    manifest = base / 'source-inputs.json'
    if manifest.exists():
        if not source_inputs_equal(json.loads(manifest.read_bytes()), before):
            raise ValueError('Shared inputs differ from the previous stage; rebuild with fresh evidence')
    else:
        write(manifest, before)
    write(base / f'{args.stage}-source-before.json', before)
    env = shared_build_environment(cache)
    commands = []
    write(base / f'{args.stage}-commands.json', commands)

    def run(command, cwd, label):
        log_path = base / f'{label}.log'
        if log_path.exists():
            raise ValueError(f'Build log already exists: {log_path}')
        commands.append({'arguments': list(map(str, command)), 'cwd': str(cwd)})
        write(base / f'{args.stage}-commands.json', commands)
        print(f'Building {label} from {source}', flush=True)
        with log_path.open('x', encoding='utf-8') as log:
            result = subprocess.run(list(map(str, command)), cwd=cwd, env=env,
                                    stdout=log, stderr=log)
        if result.returncode:
            raise RuntimeError(f'{label} failed: {result.returncode}; log retained')

    frontend = source / 'apps/forge_studio/dist'
    executable, extra, error = None, {}, None
    try:
        adopted = None
        if validated_evidence:
            adopted = validated_gate3_inputs(validator, validated_evidence.resolve(), args.stage,
                                            source_files, cache, env)
        if args.stage == 'prepare-frontend':
            if adopted:
                _, pins, links, _ = adopted
                extra = {'validated_gate3': links, 'adopted_verified_output': True}
            else:
                run(['pwsh', '-NoProfile', '-File', source / 'scripts/Invoke-StudioTrunk.ps1',
                     '-Action', 'build'], source / 'apps/forge_studio', 'shared-frontend-build')
                pins = shared_frontend(frontend, source_files, validator)
            extra.update({'frontend': str(frontend), 'frontend_pins': pins,
                          'configuration': 'canonical Trunk release and common post-build hooks'})
        elif args.stage == 'studio':
            prepared = json.loads((base / 'prepare-frontend-build-proof.json').read_bytes())
            pins = adopted[1] if adopted else shared_frontend(frontend, source_files, validator)
            if (prepared.get('schema') != 'forge.delivery.native-build.v1' or
                    prepared.get('status') != 'BUILD_ONLY' or
                    not prepared.get('shared_working_tree') or
                    prepared.get('source_root') != str(source) or
                    prepared.get('source_manifest_sha256') != sha(manifest) or
                    not prepared.get('source_stable_during_build') or
                    prepared.get('frontend_pins') != pins):
                raise ValueError('Shared frontend differs from its completed build receipt')
            config = source / 'apps/forge_studio/tauri.conf.json'
            configuration = json.loads(config.read_text(encoding='utf-8-sig'))
            windows = configuration.get('app', {}).get('windows', [])
            main_window = next((window for window in windows if window.get('label') == 'main'), {})
            if (configuration.get('build', {}).get('frontendDist') != 'dist' or
                    main_window.get('url') != 'index.html?authoring=1'):
                raise ValueError('Canonical Studio configuration must embed dist and open authoring')
            if adopted:
                executable, _, links, _ = adopted
                extra = {'validated_gate3': links, 'adopted_verified_output': True}
            else:
                # Share the validator's exact Cargo target/features/profile and
                # canonical config, without running the source-rewriting Tauri CLI.
                command, cwd = release_stage_command(validator, 'studio-release')
                run(command, cwd, 'shared-studio-native-build')
                executable = cache / 'release/forge-studio.exe'
            if frontend_inputs(frontend) != pins:
                raise ValueError('Canonical frontend changed during native embedding')
            extra.update({'frontend': str(frontend), 'frontend_pins': pins,
                     'entry_url': main_window['url'], 'authoring': True,
                     'tauri_config_sha256': sha(config),
                     'profile': 'release',
                     'configuration': 'canonical Studio; native release; Trunk release'})
        else:
            if adopted:
                executable, pins, links, _ = adopted
                extra = {'validated_gate3': links, 'adopted_verified_output': True,
                         'frontend': str(frontend), 'frontend_pins': pins,
                         'profile': 'release',
                         'configuration': 'validated common native game release'}
                if frontend_inputs(frontend) != pins:
                    raise ValueError('Canonical frontend changed during native game adoption')
            else:
                env.pop('RUSTFLAGS', None)
                run(['cargo', 'rustc', '-p', 'forge_engine', '--example', 'tidewater_plant_probe',
                     '--locked', '--offline', '-j', '1',
                     '--config', 'profile.dev.package.forge_engine.opt-level=3',
                     '--config', 'profile.dev.package.forge_engine.debug=0',
                     '--config', 'profile.dev.package.forge_engine.debug-assertions=false',
                     '--', '-C', 'opt-level=3', '-C', 'debuginfo=0', '-C', 'debug-assertions=off'],
                    source, 'shared-game-native-build')
                executable = cache / 'debug/examples/tidewater_plant_probe.exe'
                extra = {'configuration': 'Cargo dev SDK; engine and native example optimized at level 3',
                         'workspace_dependencies_of_game_built_from_shared_sources': True}
        if executable:
            regular_input(cache, executable)
        if adopted:
            reopened = validated_gate3_inputs(validator, validated_evidence.resolve(), args.stage,
                                             source_files, cache, env)
            if reopened != adopted:
                raise ValueError('Validated Gate 3 outputs or receipt chain changed during adoption')
    except Exception as failure:
        error = failure
    try:
        after = shared_source_inputs(source)
    except Exception as failure:
        after = {'inventory_failed': str(failure)}
        if error is None:
            error = failure
    write(base / f'{args.stage}-source-after.json', after)
    stable = 'inventory_failed' not in after and source_inputs_equal(before, after)
    checkout_changes = informative_checkout_changes(before, after, validator) if 'inventory_failed' not in after else None
    write(base / f'{args.stage}-result.json',
          {'stage': args.stage, 'status': 'BUILD_ONLY' if stable and not error else 'FAIL',
           'source_stable_during_build': stable, 'error': str(error) if error else None,
            'commands': commands, 'working_tree_changes': checkout_changes,
            'runtime_verified': False, 'gate_qualified': False})
    if error:
        raise error
    if not stable:
        raise ValueError('Shared inputs changed during the build; receipts retained, build not accepted')
    proof = {'schema': 'forge.delivery.native-build.v1', 'status': 'BUILD_ONLY',
             'recorded_at_utc': datetime.now(timezone.utc).isoformat(),
             'shared_working_tree': True, 'source_root': str(source),
             'source_manifest_sha256': sha(manifest),
             'source_stable_during_build': True, 'cache': str(cache), 'commands': commands,
             'source_before_sha256': sha(base / f'{args.stage}-source-before.json'),
             'source_after_sha256': sha(base / f'{args.stage}-source-after.json'),
             'working_tree_changes': checkout_changes,
             'executable': str(executable) if executable else None,
             'executable_sha256': sha(executable) if executable else None,
             'orchestrator_sha256': sha(Path(__file__)),
             'rustc_version': None if adopted else
                 subprocess.check_output(['rustc', '--version', '-v'], text=True).strip(),
             'toolchain_source_sha256': source_files['rust-toolchain.toml'],
             'build_environment': {key: env.get(key) for key in
                                   ['CARGO_TARGET_DIR', 'CARGO_BUILD_JOBS', 'CARGO_INCREMENTAL',
                                    'CARGO_NET_OFFLINE', 'RUSTFLAGS', 'CARGO_TERM_COLOR', 'NO_COLOR']},
             'runtime_verified': False, 'gate_qualified': False, **extra}
    write(base / f'{args.stage}-build-proof.json', proof)
    print(json.dumps({'stage': args.stage, 'status': 'BUILD_ONLY',
                      'executable': proof['executable'], 'frontend': str(frontend)
                      if args.stage != 'game' else None}), flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--evidence', type=Path, required=True)
    parser.add_argument('--stage', choices=['prepare-frontend', 'studio', 'game', 'reuse-launcher'], required=True)
    parser.add_argument('--cache', type=Path)
    parser.add_argument('--frontend-assets', type=Path)
    parser.add_argument('--previous-build', type=Path)
    parser.add_argument('--validated-gate3-evidence', type=Path,
                        help='Authenticate existing common WASM/release receipts without compiling or copying outputs')
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--shared-working-tree', action='store_true',
                      help='Build the common checkout and standard dist/cache without product copies')
    mode.add_argument('--frozen-archive', action='store_true',
                      help='Historical frozen workflow; not the current shared product build')
    args = parser.parse_args()
    if args.shared_working_tree:
        shared_rebuild(args)
        return
    if args.validated_gate3_evidence:
        parser.error('--validated-gate3-evidence requires --shared-working-tree')
    base = args.evidence.resolve()
    source = base / 'source'
    manifest = base / 'source-inputs.json'
    pins = json.loads(manifest.read_bytes())['files']
    def verify():
        for name, pin in pins.items():
            if sha(source / name) != pin['sha256']:
                raise ValueError(f'Frozen input changed: {name}')
    verify()
    if args.stage == 'reuse-launcher':
        raise ValueError('Legacy Hub/CLI-only reuse is disabled. Use build_classic_installer.py --reuse-native-build with a proof covering apps/forge_launcher, Hub and CLI.')
    env = dict(os.environ, CARGO_INCREMENTAL='0', CARGO_NET_OFFLINE='true', NO_COLOR='true')
    if args.cache:
        env['CARGO_TARGET_DIR'] = str(args.cache.resolve())
    commands = []
    def run(command, cwd, label):
        commands.append({'arguments': list(map(str, command)), 'cwd': str(cwd)})
        write(base / f'{args.stage}-commands.json', commands)
        print(f'Building {label}', flush=True)
        with (base / f'{label}.log').open('w', encoding='utf-8') as log:
            result = subprocess.run(list(map(str, command)), cwd=cwd, env=env, stdout=log, stderr=log)
        if result.returncode:
            raise RuntimeError(f'{label} failed: {result.returncode}; log retained')
    if args.stage == 'prepare-frontend':
        frontend = base / 'frontend'
        frontend.mkdir(parents=True, exist_ok=True)
        (frontend / 'index.html').write_text(
            '<!doctype html>\n<html lang="fr"><head><meta charset="utf-8">'
            '<meta name="viewport" content="width=device-width,initial-scale=1">'
            '<title>OpenRPG Forge Studio</title><link rel="stylesheet" href="/styles.css">'
            '<script src="/tidewater-webgpu-compat.js"></script></head>\n'
            '<body><script type="module">import init from "/forge_studio.js"; '
            'await init({module_or_path:"/forge_studio_bg.wasm"});</script></body></html>\n',
            encoding='utf-8')
        if args.frontend_assets:
            asset_pins = {}
            for name in ['forge-kit', 'forge-imports']:
                origin = args.frontend_assets / name
                destination = frontend / name
                shutil.copytree(origin, destination)
                for file in origin.rglob('*'):
                    if file.is_file():
                        relative = file.relative_to(args.frontend_assets).as_posix()
                        pin = sha(file)
                        if sha(frontend / relative) != pin:
                            raise ValueError(f'Static frontend asset changed: {relative}')
                        asset_pins[relative] = pin
            write(base / 'frontend-static-inputs.json', {'origin': str(args.frontend_assets), 'files': asset_pins})
        print('FRONTEND_ASSETS_PREPARED', flush=True)
        return
    if not args.cache:
        raise ValueError('--cache is required for native builds')
    if args.stage == 'studio':
        env['RUSTFLAGS'] = '-C debuginfo=0'
        frontend = base / 'frontend'
        for name in ['index.html', 'forge_studio.js', 'forge_studio_bg.wasm', 'styles.css',
                     'tidewater-webgpu-compat.js', 'forge-navigation-worker.js',
                     'forge_navigation.js', 'forge_navigation_bg.wasm', 'forge-navigation-worker-manifest.json']:
            if not (frontend / name).is_file():
                raise ValueError(f'Frontend build incomplete: {name}')
        frontend_pins = [{'path': file.relative_to(frontend).as_posix(), 'sha256': sha(file)}
                         for file in sorted(frontend.rglob('*')) if file.is_file()]
        config = base / 'tauri-candidate.conf.json'
        relative = os.path.relpath(frontend, source / 'apps/forge_studio').replace('\\', '/')
        write(config, {'build': {'beforeBuildCommand': None, 'frontendDist': relative},
                       'app': {'windows': [{'label': 'main', 'title': 'OpenRPG Forge Studio · Édition locale',
                                            'url': 'index.html?authoring=1', 'width': 1920, 'height': 1080,
                                            'minWidth': 1100, 'minHeight': 700, 'resizable': True,
                                            'dragDropEnabled': False}]}, 'bundle': {'active': False}})
        # The Tauri CLI rewrites Cargo.toml. Supply its documented build config
        # to the native Cargo build instead, keeping the source snapshot intact.
        env['TAURI_CONFIG'] = config.read_text(encoding='utf-8')
        run(['cargo', 'build', '-p', 'forge_studio', '--bin', 'forge-studio',
             '--features', 'tauri/custom-protocol', '--locked', '--offline', '-j', '1'],
            source / 'apps/forge_studio', 'studio-native-build')
        executable = base / 'forge-studio.exe'
        shutil.copyfile(args.cache / 'debug/forge-studio.exe', executable)
        if frontend_pins != [{'path': file.relative_to(frontend).as_posix(), 'sha256': sha(file)}
                             for file in sorted(frontend.rglob('*')) if file.is_file()]:
            raise ValueError('Frontend changed during native embedding')
        extra = {'frontend_pins': frontend_pins,
                 'entry_url': 'index.html?authoring=1', 'authoring': True,
                 'frontend_static_input_manifest_sha256': sha(base / 'frontend-static-inputs.json'),
                 'configuration': 'native debug; WebAssembly release; navigation worker release'}
    else:
        env.pop('RUSTFLAGS', None)
        run(['cargo', 'rustc', '-p', 'forge_engine', '--example', 'tidewater_plant_probe',
             '--locked', '--offline', '-j', '1',
             '--config', 'profile.dev.package.forge_engine.opt-level=3',
             '--config', 'profile.dev.package.forge_engine.debug=0',
             '--config', 'profile.dev.package.forge_engine.debug-assertions=false',
             '--', '-C', 'opt-level=3', '-C', 'debuginfo=0', '-C', 'debug-assertions=off'],
            source, 'game-native-build')
        executable = base / 'forge-game.exe'
        shutil.copyfile(args.cache / 'debug/examples/tidewater_plant_probe.exe', executable)
        extra = {'configuration': 'Cargo dev SDK, engine and native example opt-level 3, assertions off',
                 'workspace_dependencies_of_game_built_from_frozen_sources': True}
    verify()
    proof = {'schema': 'forge.delivery.native-build.v1', 'status': 'BUILD_ONLY',
             'recorded_at_utc': datetime.now(timezone.utc).isoformat(),
             'source_manifest_sha256': sha(manifest), 'executable_sha256': sha(executable),
             'source_stable_during_build': True, 'cache': str(args.cache), 'commands': commands,
             'orchestrator_sha256': sha(Path(__file__)),
             'rustc_version': subprocess.check_output(['rustc', '--version', '-v'], text=True).strip(),
             'build_environment': {k: env.get(k) for k in ['CARGO_TARGET_DIR', 'CARGO_INCREMENTAL',
                                                        'CARGO_NET_OFFLINE', 'RUSTFLAGS', 'NO_COLOR']},
             'tauri_config_sha256': sha(base / 'tauri-candidate.conf.json') if args.stage == 'studio' else None,
             'runtime_verified': False, 'gate_qualified': False, **extra}
    write(base / f'{args.stage}-build-proof.json', proof)
    print(json.dumps({'stage': args.stage, 'status': 'BUILD_ONLY', 'executable': str(executable),
                      'sha256': proof['executable_sha256']}), flush=True)


if __name__ == '__main__':
    main()
