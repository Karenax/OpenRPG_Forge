"""Build the shared checkout; retain the explicit frozen workflow for archives only."""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import stat
import subprocess
import tomllib
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

    Hivemind orchestration/acquisition is outside the compiled-input map. The
    common validator refuses Rust includes and Cargo paths back into that directory.
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
            'source_scope_exclusions': ['scripts/hivemind/** (orchestration/acquisition only; product includes refused)']}


def frontend_inputs(frontend):
    frontend = Path(frontend)
    for item in (frontend, *frontend.parents):
        if item.is_symlink() or getattr(item.lstat(), 'st_file_attributes', 0) & 0x400:
            raise ValueError(f'Reparse frontend path refused: {item}')
    pins = []
    stack = [frontend]
    while stack:
        with os.scandir(stack.pop()) as entries:
            for entry in entries:
                path = Path(entry.path)
                metadata = path.lstat()
                if stat.S_ISLNK(metadata.st_mode) or getattr(metadata, 'st_file_attributes', 0) & 0x400:
                    raise ValueError(f'Reparse frontend entry refused: {path}')
                if stat.S_ISDIR(metadata.st_mode):
                    stack.append(path)
                elif stat.S_ISREG(metadata.st_mode):
                    regular_input(frontend, path)
                    pins.append({'path': path.relative_to(frontend).as_posix(),
                                 'sha256': sha(path), 'bytes': metadata.st_size})
                else:
                    raise ValueError(f'Non-file frontend entry refused: {path}')
    pins.sort(key=lambda pin: pin['path'])
    return pins


def launcher_frontend_state(frontend):
    """Fingerprint an isolated generated frontend; this is not a process lock."""
    frontend = Path(frontend)
    for path in (frontend, *frontend.parents):
        if path.exists() or path.is_symlink():
            if path.is_symlink() or getattr(path.lstat(), 'st_file_attributes', 0) & 0x400:
                raise ValueError(f'Reparse launcher frontend path refused: {path}')
    if not frontend.exists():
        return {'state': 'absent', 'files': []}
    if not frontend.is_dir():
        raise ValueError('Launcher frontend must be a physical directory')
    pins = frontend_inputs(frontend)
    encoded = json.dumps(pins, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode('utf-8')
    return {'state': hashlib.sha256(encoded).hexdigest(), 'files': pins}


def validate_standalone_launcher_frontend(frontend, pins):
    if ({entry['path'] for entry in pins} != {'index.html', 'styles.css', 'desktop.js'} or
            any(not 0 < entry['bytes'] <= 4 * 1024 * 1024 for entry in pins)):
        raise ValueError('Standalone launcher frontend inventory differs')
    html = (frontend / 'index.html').read_text(encoding='utf-8')
    styles = (frontend / 'styles.css').read_text(encoding='utf-8')
    if ('data-forge-launcher="standalone"' not in html or '<title>Jeu</title>' not in html or
            'desktop.js' not in html or any(text in html + styles for text in
            ('L’Aube des royaumes', 'L’AUBE DES ROYAUMES', 'assets/aube.png', 'content.js'))):
        raise ValueError('Standalone launcher frontend identity differs')


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


def studio_game_kit_state(root, frontend, receipt_sha256, source_manifest_sha256):
    """Recheck the staged kit and the two explicit Studio frontend inputs."""
    import stage_studio_game_kit
    staged = stage_studio_game_kit.verify_staged(root, frontend, receipt_sha256, source_manifest_sha256)
    if (not isinstance(staged, dict) or staged.get('schema') != 'forge.studio.game-kit-stage.v1' or
            staged.get('status') != 'PRIVATE_STAGED_ONLY' or
            staged.get('kit_assembly_receipt_sha256') != receipt_sha256 or
            staged.get('source_manifest_sha256') != source_manifest_sha256 or
            staged.get('manifest_file') != 'forge-game-export-v1.json'):
        raise ValueError('Studio game kit stage receipt differs')
    receipt_file = frontend / 'forge-game-export-staging-receipt.json'
    manifest_file = frontend / 'forge-game-export-v1.json'
    return {'assembly_root': str(Path(root).resolve()), 'assembly_receipt_sha256': receipt_sha256,
            'source_manifest_sha256': source_manifest_sha256, 'stage_receipt_sha256': sha(receipt_file),
            'manifest_sha256': sha(manifest_file), 'stage_receipt': staged}


def shared_build_environment(cache):
    env = dict(os.environ, CARGO_TARGET_DIR=str(cache), CARGO_BUILD_JOBS='1',
               CARGO_INCREMENTAL='0', CARGO_NET_OFFLINE='true', NO_COLOR='true',
               RUSTFLAGS='-C debuginfo=0', CARGO_TERM_COLOR='never')
    env.pop('TAURI_CONFIG', None)
    return env


def offline_frontend_tools(source, env, trunk_version, wasm_opt_path, wasm_opt_sha256, wasm_opt_version):
    """Pin the actual tools selected by PATH without compiling or downloading."""
    import application_sources
    if (not isinstance(trunk_version, str) or not re.fullmatch(r'\d+\.\d+\.\d+', trunk_version) or
            not isinstance(wasm_opt_version, str) or not re.fullmatch(r'\d+', wasm_opt_version) or
            not isinstance(wasm_opt_sha256, str) or not re.fullmatch(r'[0-9a-f]{64}', wasm_opt_sha256)):
        raise ValueError('Offline frontend requires explicit valid Trunk version and wasm-opt version/SHA-256')
    wasm_opt_path = Path(wasm_opt_path)
    if not wasm_opt_path.is_absolute() or '..' in wasm_opt_path.parts:
        raise ValueError('Offline wasm-opt must be an absolute physical path')
    application_sources.ordinary(wasm_opt_path)
    if not wasm_opt_path.is_file() or sha(wasm_opt_path) != wasm_opt_sha256:
        raise ValueError('Offline wasm-opt executable or SHA-256 differs')
    lock = tomllib.loads((source/'Cargo.lock').read_text(encoding='utf-8'))
    versions = {item['version'] for item in lock.get('package', []) if item.get('name') == 'wasm-bindgen'}
    toolchain = tomllib.loads((source/'rust-toolchain.toml').read_text(encoding='utf-8'))
    rust_version = toolchain.get('toolchain', {}).get('channel')
    if len(versions) != 1 or not isinstance(rust_version, str) or not re.fullmatch(r'\d+\.\d+\.\d+', rust_version):
        raise ValueError('Offline frontend requires one locked wasm-bindgen and a pinned Rust version')
    expected_bindgen = versions.pop()

    def selected(name):
        result = shutil.which(name, path=env.get('PATH', ''))
        if not result:
            raise ValueError(f'Offline frontend tool is missing from PATH: {name}')
        path = Path(result)
        application_sources.ordinary(path)
        if not path.is_file():
            raise ValueError(f'Offline frontend tool is not a physical file: {name}')
        return path.resolve(strict=True)

    def version(path, *arguments):
        try:
            value = subprocess.check_output([str(path), *arguments], cwd=source, env=env,
                                            text=True, encoding='utf-8', errors='replace', timeout=20).strip()
        except (OSError, subprocess.SubprocessError) as error:
            raise ValueError(f'Cannot read offline tool version: {path}') from error
        if not value or '\n' in value:
            raise ValueError(f'Invalid offline tool version: {path}')
        return value

    tools = {name:selected(name) for name in ('trunk', 'wasm-bindgen', 'wasm-opt', 'pwsh', 'cargo', 'rustc')}
    if tools['wasm-opt'] != wasm_opt_path.resolve(strict=True):
        raise ValueError('A different wasm-opt would be selected from PATH')
    outputs = {name:version(path, '--version') for name, path in tools.items() if name != 'pwsh'}
    outputs['pwsh'] = version(tools['pwsh'], '-NoProfile', '-Command', '$PSVersionTable.PSVersion.ToString()')
    powershell = re.match(r'^(\d+)\.(\d+)', outputs['pwsh'])
    if (outputs['trunk'] != f'trunk {trunk_version}' or
            outputs['wasm-bindgen'] != f'wasm-bindgen {expected_bindgen}' or
            not re.match(rf'^wasm-opt version {re.escape(wasm_opt_version)}(?:\s|$)', outputs['wasm-opt']) or
            not re.match(rf'^cargo {re.escape(rust_version)}(?:\s|$)', outputs['cargo']) or
            not re.match(rf'^rustc {re.escape(rust_version)}(?:\s|$)', outputs['rustc']) or
            not powershell or tuple(map(int, powershell.groups())) < (7, 2)):
        raise ValueError('Offline frontend tool version does not match the pinned build requirements')
    sysroot = Path(version(tools['rustc'], '--print', 'sysroot'))
    if not sysroot.is_absolute():
        raise ValueError('Rust sysroot must be absolute')
    result = {name:{'path':str(path), 'sha256':sha(path), 'version':outputs[name]}
              for name, path in tools.items()}
    for name in ('cargo', 'rustc'):
        binary = sysroot/'bin'/f'{name}.exe'
        application_sources.ordinary(binary)
        if not binary.is_file():
            raise ValueError(f'Rust toolchain binary is missing: {binary}')
        result[name]['toolchain_path'] = str(binary.resolve(strict=True))
        result[name]['toolchain_sha256'] = sha(binary)
    return result


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
    studio_kit_root = getattr(args, 'studio_game_kit_root', None)
    studio_kit_receipt = getattr(args, 'studio_game_kit_receipt_sha256', None)
    if bool(studio_kit_root) != bool(studio_kit_receipt) or (studio_kit_root and args.stage != 'studio'):
        raise ValueError('Studio game kit root and assembly receipt SHA must be supplied together for the Studio stage')
    validated_evidence = getattr(args, 'validated_gate3_evidence', None)
    shared_jobs = getattr(args, 'shared_jobs', None)
    if shared_jobs is not None:
        if type(shared_jobs) is not int or not 1 <= shared_jobs <= MAX_VALIDATED_CARGO_BUILD_JOBS:
            raise ValueError('Shared build job count is outside the supported range')
        if args.stage not in ('prepare-frontend', 'studio', 'module-game') or validated_evidence:
            raise ValueError('Shared job count applies only to a new frontend, Studio or module-game build')
    if studio_kit_root and validated_evidence:
        raise ValueError('A Studio with a new game kit must be built, not adopted from older evidence')
    if validated_evidence and args.stage not in ('prepare-frontend', 'studio', 'game'):
        raise ValueError('--validated-gate3-evidence adopts only the canonical frontend, Studio, or game release')
    frontend_offline = getattr(args, 'frontend_offline', False)
    frontend_trunk_version = getattr(args, 'frontend_trunk_version', None)
    frontend_wasm_opt = getattr(args, 'frontend_wasm_opt', None)
    frontend_wasm_opt_sha256 = getattr(args, 'frontend_wasm_opt_sha256', None)
    frontend_wasm_opt_version = getattr(args, 'frontend_wasm_opt_version', None)
    frontend_tool_args = (frontend_trunk_version, frontend_wasm_opt, frontend_wasm_opt_sha256,
                          frontend_wasm_opt_version)
    if ((frontend_offline and (args.stage != 'prepare-frontend' or validated_evidence or not all(frontend_tool_args))) or
            (not frontend_offline and any(value is not None for value in frontend_tool_args))):
        raise ValueError('Offline frontend mode requires a new frontend build and all explicit tool pins')
    if args.frontend_assets or args.previous_build:
        raise ValueError('Shared builds use the canonical Trunk asset hook, not candidate inputs')
    if not args.cache:
        raise ValueError('--cache is required for every shared stage, including the Trunk worker')
    cache = args.cache.resolve()
    base = args.evidence.resolve()
    launcher_before, launcher_frontend = None, None
    if args.stage in ('standalone-launcher', 'delivery-cli'):
        import application_sources
        if os.name != 'nt':
            raise ValueError('This delivery build stage currently supports Windows only')
        if not getattr(args, 'cargo_home', None) or not getattr(args, 'temp_dir', None):
            raise ValueError('Delivery build requires explicit Cargo home and temporary directories')
        for path in (cache, args.cargo_home.resolve(), args.temp_dir.resolve()):
            application_sources.ordinary(path)
            if not path.is_dir() or path.is_relative_to(source):
                raise ValueError('Delivery build directories must exist outside the checkout')
    if args.stage == 'standalone-launcher':
        if not getattr(args, 'launcher_frontend', None):
            raise ValueError('Standalone launcher requires an explicit frontend directory')
        if not 1 <= args.launcher_jobs <= MAX_VALIDATED_CARGO_BUILD_JOBS:
            raise ValueError('Standalone launcher job count is outside the supported range')
        temporary_root = (source.parent / '.build/tmp').resolve(strict=True)
        if args.temp_dir.resolve() != temporary_root:
            raise ValueError('Standalone launcher must use the shared temporary directory')
        launcher_frontend = Path(args.launcher_frontend)
        if (not launcher_frontend.is_absolute() or '..' in launcher_frontend.parts or
                launcher_frontend.parent.resolve() != temporary_root or launcher_frontend.exists() or
                launcher_frontend.is_symlink()):
            raise ValueError('Standalone frontend must be a fresh direct child of the shared temporary directory')
        launcher_before = launcher_frontend_state(launcher_frontend)
    if args.stage == 'delivery-cli' and not 1 <= args.delivery_jobs <= MAX_VALIDATED_CARGO_BUILD_JOBS:
        raise ValueError('Delivery CLI job count is outside the supported range')
    for location in (base, cache):
        if location == source or any(location.is_relative_to(source / scope)
                                     for scope in SOURCE_SCOPES):
            raise ValueError('Evidence and Cargo cache must stay outside build input scopes')
    env = shared_build_environment(cache)
    release = cache / 'release'
    if os.name == 'nt' and args.stage in ('studio', 'module-game', 'delivery-cli', 'standalone-launcher'):
        # Use an explicit Cargo target so the host proc macros keep their own CRT.
        # All shipped entry points must start on Windows before any installer can run.
        target = 'x86_64-pc-windows-msvc'
        env.pop('CARGO_ENCODED_RUSTFLAGS', None)
        env.update(CARGO_BUILD_TARGET=target, RUSTFLAGS='-C debuginfo=0 -C target-feature=+crt-static')
        release = cache / target / 'release'
    if shared_jobs is not None:
        env['CARGO_BUILD_JOBS'] = str(shared_jobs)
    frontend_tools = None
    if frontend_offline:
        overrides = ('CARGO_ENCODED_RUSTFLAGS', 'RUSTUP_TOOLCHAIN', 'RUSTC', 'RUSTC_WRAPPER',
                     'RUSTC_WORKSPACE_WRAPPER', 'CARGO_BUILD_RUSTC', 'CARGO_BUILD_RUSTC_WRAPPER',
                     'CARGO_BUILD_RUSTC_WORKSPACE_WRAPPER')
        if any(env.get(name) for name in overrides):
            raise ValueError('Offline frontend refuses inherited Rust compiler or flags overrides')
        opt = Path(frontend_wasm_opt)
        env['PATH'] = str(opt.parent) + os.pathsep + env.get('PATH', '')
        env['TRUNK_OFFLINE'] = 'true'
        env['TRUNK_SKIP_VERSION_CHECK'] = 'true'
        frontend_tools = offline_frontend_tools(source, env, frontend_trunk_version, opt,
                                                frontend_wasm_opt_sha256, frontend_wasm_opt_version)
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
        manifest_contents = json.loads(manifest.read_bytes())
        if not source_inputs_equal(manifest_contents, before):
            raise ValueError('Shared inputs differ from the previous stage; rebuild with fresh evidence')
    else:
        write(manifest, before)
        manifest_contents = before
    manifest_hash = sha(manifest)
    if args.stage == 'standalone-launcher':
        notice = 'THIRD_PARTY_NOTICES.md'
        if (not manifest_contents.get('working_tree_files', {}).get(notice) or
                manifest_contents['working_tree_files'][notice] != before.get('working_tree_files', {}).get(notice)):
            raise ValueError('Project notice differs from the pinned shared checkout')
    write(base / f'{args.stage}-source-before.json', before)
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
    executable, extra, error, studio_kit = None, {}, None, None
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
                shell = frontend_tools['pwsh']['path'] if frontend_tools else 'pwsh'
                run([shell, '-NoProfile', '-File', source / 'scripts/Invoke-StudioTrunk.ps1',
                     '-Action', 'build'], source / 'apps/forge_studio', 'shared-frontend-build')
                pins = shared_frontend(frontend, source_files, validator)
            extra.update({'frontend': str(frontend), 'frontend_pins': pins,
                          'cargo_build_jobs': env['CARGO_BUILD_JOBS'],
                          'configuration': 'canonical Trunk release and common post-build hooks'})
            if frontend_tools:
                extra.update({'offline_frontend_tools':frontend_tools, 'frontend_offline':True,
                              'frontend_tool_scope':'Direct tools and Rust compiler executables; '
                                  'system linkers, toolchain libraries and OS dependencies are not inventoried'})
        elif args.stage == 'studio':
            prepared = json.loads((base / 'prepare-frontend-build-proof.json').read_bytes())
            pins = adopted[1] if adopted else shared_frontend(frontend, source_files, validator)
            base_pins = pins
            overlay = {'forge-game-export-v1.json', 'forge-game-export-staging-receipt.json'}
            present_overlay = {pin['path'] for pin in pins if pin['path'] in overlay}
            if studio_kit_root:
                if (present_overlay not in (set(), overlay) or
                        any(pin['path'] in overlay for pin in prepared.get('frontend_pins', []))):
                    raise ValueError('Studio game kit overlay is partial or was already present in the base frontend')
                base_pins = [pin for pin in pins if pin['path'] not in overlay]
            if (prepared.get('schema') != 'forge.delivery.native-build.v1' or
                    prepared.get('status') != 'BUILD_ONLY' or
                    not prepared.get('shared_working_tree') or
                    prepared.get('source_root') != str(source) or
                    prepared.get('source_manifest_sha256') != sha(manifest) or
                    not prepared.get('source_stable_during_build') or
                    prepared.get('frontend_pins') != base_pins):
                raise ValueError('Shared frontend differs from its completed build receipt')
            if studio_kit_root:
                if not present_overlay:
                    import stage_studio_game_kit
                    stage_studio_game_kit.stage(studio_kit_root, frontend, studio_kit_receipt, manifest_hash)
                    pins = shared_frontend(frontend, source_files, validator)
                    if ({pin['path'] for pin in pins if pin['path'] in overlay} != overlay or
                            [pin for pin in pins if pin['path'] not in overlay] != base_pins):
                        raise ValueError('Studio base frontend changed while staging the game kit')
                studio_kit = studio_game_kit_state(studio_kit_root, frontend, studio_kit_receipt, manifest_hash)
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
                executable = release / 'forge-studio.exe'
            if frontend_inputs(frontend) != pins:
                raise ValueError('Canonical frontend changed during native embedding')
            if studio_kit and studio_game_kit_state(studio_kit_root, frontend, studio_kit_receipt, manifest_hash) != studio_kit:
                raise ValueError('Studio game kit changed during native embedding')
            extra.update({'frontend': str(frontend), 'frontend_pins': pins,
                     'entry_url': main_window['url'], 'authoring': True,
                     'cargo_build_jobs': env['CARGO_BUILD_JOBS'],
                     'tauri_config_sha256': sha(config),
                     'profile': 'release',
                     'configuration': 'canonical Studio; native release; Trunk release'})
            if studio_kit:
                extra['game_kit'] = studio_kit
        elif args.stage == 'module-game':
            # Studio playtests use the shared module entry point, independently
            # of the historical Tidewater demo's positional command line.
            module_jobs = str(shared_jobs if shared_jobs is not None else 4)
            env['CARGO_BUILD_JOBS'] = module_jobs
            run(['cargo', 'build', '-p', 'forge_runtime', '--no-default-features',
                 '--features', 'module-game', '--bin', 'forge-module', '--release',
                 '--locked', '--offline', '-j', module_jobs],
                source, 'shared-module-game-native-build')
            executable = release / 'forge-module.exe'
            extra = {'application_role': 'module-game', 'profile': 'release',
                     'cargo_build_jobs': module_jobs,
                     'configuration': 'shared forge_runtime module-game entry point'}
        elif args.stage == 'delivery-cli':
            import game_kit
            env.update(CARGO_HOME=str(args.cargo_home.resolve()), CARGO_BUILD_JOBS=str(args.delivery_jobs),
                       TEMP=str(args.temp_dir.resolve()), TMP=str(args.temp_dir.resolve()))
            for key in ('CARGO_ENCODED_RUSTFLAGS', 'RUSTUP_TOOLCHAIN'):
                env.pop(key, None)
            run(['cargo', 'build', '--manifest-path', source / 'tools/forge_delivery/Cargo.toml',
                 '--bin', 'forge_delivery', '--release', '--locked', '--offline', '-j', str(args.delivery_jobs)],
                source / 'tools/forge_delivery', 'shared-delivery-cli-native-build')
            executable = release / 'forge_delivery.exe'
            regular_input(cache, executable)
            game_kit.require_windows_x64_executable(executable)
            extra = {'application_role':'delivery-cli', 'profile':'release', 'built_executable_sha256':sha(executable)}
        elif args.stage == 'standalone-launcher':
            # Keep the isolated frontend until game_kit has verified and staged it.
            import game_kit
            launcher_root = source / 'apps/forge_launcher'
            frontend = launcher_frontend
            env.update(CARGO_HOME=str(args.cargo_home.resolve()), CARGO_BUILD_JOBS=str(args.launcher_jobs),
                       TEMP=str(args.temp_dir.resolve()), TMP=str(args.temp_dir.resolve()),
                       FORGE_LAUNCHER_PROFILE='standalone', FORGE_AUBE_SOURCE=str(launcher_root / 'canonical-ui'),
                       FORGE_LAUNCHER_DIST=str(frontend), FORGE_LAUNCHER_TEMP_ROOT=str(args.temp_dir.resolve()),
                       TAURI_CONFIG=json.dumps(game_kit.standalone_tauri_config(frontend, source), ensure_ascii=False,
                                               separators=(',', ':')))
            for key in ('NODE_OPTIONS', 'NODE_PATH', 'CARGO_ENCODED_RUSTFLAGS', 'RUSTUP_TOOLCHAIN'):
                env.pop(key, None)
            run([args.node, launcher_root / 'scripts/build-aube.mjs'], launcher_root,
                'standalone-launcher-frontend-build')
            frontend_log = base / 'standalone-launcher-frontend-build.log'
            if frontend_log.stat().st_size > 4 * 1024 * 1024:
                raise ValueError('Standalone launcher frontend log exceeds its budget')
            marker_lines = [line for line in frontend_log.read_text(encoding='utf-8').splitlines()
                            if line.startswith('STANDALONE_FRONTEND_READY')]
            if marker_lines != [f'STANDALONE_FRONTEND_READY output={frontend}']:
                raise ValueError('Standalone launcher frontend marker or output path differs')
            current_frontend = launcher_frontend_state(frontend)
            pins = current_frontend['files']
            validate_standalone_launcher_frontend(frontend, pins)
            if not source_inputs_equal(shared_source_inputs(source), before):
                raise ValueError('Shared inputs changed while building the launcher frontend')
            run(['cargo', 'build', '--manifest-path', launcher_root / 'Cargo.toml',
                 '--features', 'standalone', '--release', '--locked', '--offline', '-j', str(args.launcher_jobs)],
                launcher_root, 'standalone-launcher-native-build')
            executable = release / 'forge-launcher-prototype.exe'
            regular_input(cache, executable)
            if launcher_frontend_state(frontend) != current_frontend:
                raise ValueError('Launcher frontend changed during native embedding')
            verification = game_kit.verify_launcher(executable)
            if (verification.get('executable_sha256') != sha(executable) or
                    verification.get('command') != '--verify-distributable' or
                    verification.get('exit_code') != 0 or verification.get('marker') != game_kit.STANDALONE or
                    game_kit.contains_bytes(executable, game_kit.AUBE)):
                raise ValueError('Standalone launcher verification differs from the built executable')
            extra = {'application_role':'standalone-launcher', 'profile':'release',
                     'frontend_profile':'standalone', 'cargo_features':['standalone'],
                     'frontend':str(frontend), 'frontend_pins':pins,
                     'frontend_before':launcher_before, 'frontend_state_sha256':current_frontend['state'],
                     'frontend_builder_marker':'STANDALONE_FRONTEND_READY',
                     'frontend_builder_output':str(frontend),
                     'frontend_required_until_kit_assembly':True, 'launcher_verification':verification,
                     'tauri_config_sha256':hashlib.sha256(env['TAURI_CONFIG'].encode('utf-8')).hexdigest()}
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
                run(['cargo', 'rustc', '-p', 'forge_tidewater', '--example', 'tidewater_plant_probe',
                     '--locked', '--offline', '-j', '1',
                     '--config', 'profile.dev.package.forge_engine.opt-level=3',
                     '--config', 'profile.dev.package.forge_engine.debug=0',
                     '--config', 'profile.dev.package.forge_engine.debug-assertions=false',
                     '--config', 'profile.dev.package.forge_tidewater.opt-level=3',
                     '--config', 'profile.dev.package.forge_tidewater.debug=0',
                     '--config', 'profile.dev.package.forge_tidewater.debug-assertions=false',
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
    if frontend_offline and error is None:
        try:
            if (env.get('TRUNK_OFFLINE') != 'true' or env.get('TRUNK_SKIP_VERSION_CHECK') != 'true' or
                    offline_frontend_tools(source, env, frontend_trunk_version, Path(frontend_wasm_opt),
                                           frontend_wasm_opt_sha256, frontend_wasm_opt_version) != frontend_tools):
                raise ValueError('Offline frontend tools or environment changed during the build')
        except Exception as failure:
            error = failure
    if args.stage == 'standalone-launcher' and error is None:
        try:
            notice = 'THIRD_PARTY_NOTICES.md'
            if (sha(manifest) != manifest_hash or
                    after.get('working_tree_files', {}).get(notice) != manifest_contents['working_tree_files'][notice] or
                    launcher_frontend_state(frontend)['files'] != extra['frontend_pins'] or
                    sha(executable) != extra['launcher_verification']['executable_sha256']):
                raise ValueError('Standalone launcher source, frontend or binary changed before the receipt')
        except Exception as failure:
            error = failure
    if args.stage == 'studio' and studio_kit and error is None:
        try:
            if (sha(manifest) != manifest_hash or frontend_inputs(frontend) != extra['frontend_pins'] or
                    studio_game_kit_state(studio_kit_root, frontend, studio_kit_receipt, manifest_hash) != studio_kit):
                raise ValueError('Studio game kit or frontend changed before the build receipt')
        except Exception as failure:
            error = failure
    if args.stage == 'delivery-cli' and error is None:
        try:
            if sha(manifest) != manifest_hash or sha(executable) != extra['built_executable_sha256']:
                raise ValueError('Delivery CLI source or binary changed before the build receipt')
        except Exception as failure:
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
    environment_keys = ['CARGO_TARGET_DIR', 'CARGO_BUILD_JOBS', 'CARGO_INCREMENTAL',
                        'CARGO_NET_OFFLINE', 'RUSTFLAGS', 'CARGO_TERM_COLOR', 'NO_COLOR',
                        'CARGO_HOME', 'TEMP', 'TMP']
    if release != cache / 'release':
        environment_keys.append('CARGO_BUILD_TARGET')
    if args.stage == 'standalone-launcher':
        environment_keys += ['FORGE_LAUNCHER_PROFILE', 'FORGE_AUBE_SOURCE',
                             'FORGE_LAUNCHER_DIST', 'FORGE_LAUNCHER_TEMP_ROOT', 'TAURI_CONFIG']
    if frontend_offline:
        environment_keys += ['TRUNK_OFFLINE', 'TRUNK_SKIP_VERSION_CHECK']
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
             'build_environment': {key: env.get(key) for key in environment_keys},
             'runtime_verified': False, 'gate_qualified': False, **extra}
    if frontend_offline:
        proof['frontend_build_log_sha256'] = sha(base/'shared-frontend-build.log')
        proof['frontend_result_sha256'] = sha(base/'prepare-frontend-result.json')
    write(base / f'{args.stage}-build-proof.json', proof)
    print(json.dumps({'stage': args.stage, 'status': 'BUILD_ONLY',
                      'executable': proof['executable'], 'frontend': str(frontend)
                      if args.stage in ('prepare-frontend', 'studio', 'standalone-launcher') else None}), flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--evidence', type=Path, required=True)
    parser.add_argument('--stage', choices=['prepare-frontend', 'studio', 'game', 'module-game', 'delivery-cli',
                                            'standalone-launcher', 'reuse-launcher'], required=True)
    parser.add_argument('--cache', type=Path)
    parser.add_argument('--frontend-assets', type=Path)
    parser.add_argument('--previous-build', type=Path)
    parser.add_argument('--cargo-home', type=Path, help='Explicit shared Cargo home for standalone launcher')
    parser.add_argument('--temp-dir', type=Path, help='Explicit build temporary directory for standalone launcher')
    parser.add_argument('--launcher-jobs', type=int, default=1)
    parser.add_argument('--delivery-jobs', type=int, default=1)
    parser.add_argument('--shared-jobs', type=int,
                        help='Cargo jobs for a new shared frontend, Studio or module-game build '
                             '(defaults: 1, or 4 for module-game)')
    parser.add_argument('--frontend-offline', action='store_true',
                        help='Use only explicitly verified local tools for a fresh frontend build')
    parser.add_argument('--frontend-trunk-version', help='Expected local Trunk version')
    parser.add_argument('--frontend-wasm-opt', type=Path, help='Absolute path to the local wasm-opt executable')
    parser.add_argument('--frontend-wasm-opt-sha256', help='Expected lowercase wasm-opt SHA-256')
    parser.add_argument('--frontend-wasm-opt-version', help='Expected numeric Binaryen version')
    parser.add_argument('--node', default='node')
    parser.add_argument('--launcher-frontend', type=Path,
                        help='Fresh standalone frontend directory directly inside the shared temporary directory')
    parser.add_argument('--studio-game-kit-root', type=Path, help='Verified assembled private game kit directory')
    parser.add_argument('--studio-game-kit-receipt-sha256', help='SHA-256 of its assembly receipt')
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
    if args.shared_jobs is not None:
        parser.error('--shared-jobs requires --shared-working-tree')
    if args.frontend_offline or any(value is not None for value in (
            args.frontend_trunk_version, args.frontend_wasm_opt,
            args.frontend_wasm_opt_sha256, args.frontend_wasm_opt_version)):
        parser.error('Offline frontend tool pins require --shared-working-tree')
    if args.stage in ('module-game', 'standalone-launcher', 'delivery-cli'):
        parser.error(f'{args.stage} is available only for the shared working tree')
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
        raise ValueError('Historical launcher and Hub builds use the Gate 7 retirement archive.')
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
        run(['cargo', 'rustc', '-p', 'forge_tidewater', '--example', 'tidewater_plant_probe',
             '--locked', '--offline', '-j', '1',
             '--config', 'profile.dev.package.forge_engine.opt-level=3',
             '--config', 'profile.dev.package.forge_engine.debug=0',
             '--config', 'profile.dev.package.forge_engine.debug-assertions=false',
                     '--config', 'profile.dev.package.forge_tidewater.opt-level=3',
                     '--config', 'profile.dev.package.forge_tidewater.debug=0',
                     '--config', 'profile.dev.package.forge_tidewater.debug-assertions=false',
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
