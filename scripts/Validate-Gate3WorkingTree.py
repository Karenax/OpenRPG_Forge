"""Validate the shared checkout and retain receipts without making source copies."""

from pathlib import Path, PurePosixPath
import argparse
import datetime
import hashlib
import json
import os
import re
import subprocess
import sys
import tomllib

SOURCE = Path(__file__).resolve().parents[1]
PREFIXES = ("apps/", "crates/", "tools/", "third_party/", ".cargo/", "scripts/", ".github/", "examples/")
ROOT_INPUTS = {"Cargo.toml", "Cargo.lock", "rust-toolchain.toml", "deny.toml", ".gitignore"}
# Acquired static inputs retained by the currently installed Studio .5.
LOCAL_ASSET_MANIFEST_SHA256 = '3d7f2dd80580b65dee3da1b88b3d826d0d98aac940621f85be794787dcb18ac6'


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def physical_path(path, root):
    for part in (path, *path.parents):
        if part == root.parent:
            break
        attributes = part.lstat()
        require(not part.is_symlink() and not getattr(attributes, 'st_file_attributes', 0) & 0x400,
                f'Reparse input refused: {part}')


def safe_relative(name):
    require(isinstance(name, str) and name and '\\' not in name and ':' not in name,
            f'Invalid artifact path: {name}')
    path = PurePosixPath(name)
    require(not path.is_absolute() and all(part not in ('', '.', '..') for part in name.split('/')),
            f'Invalid artifact path: {name}')
    return name


def read_json(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def is_build_input(name):
    """Orchestration/acquisition stays in the checkout ledger, not the build map.

    Compiled verification harnesses are owned under forge_tidewater/examples.
    source_inventories refuses direct product includes back into orchestration
    before excluding it; consumed static assets retain their separate pins.
    """
    return (name in ROOT_INPUTS or name.startswith(PREFIXES)) and not (
        name.startswith('scripts/hivemind/'))


def require_product_owned_includes():
    """Reject explicit Rust includes and Cargo paths back into orchestration."""
    manifests = [SOURCE / 'Cargo.toml', *(SOURCE / '.cargo').rglob('*.toml')]
    for directory in ('apps', 'crates', 'tools'):
        manifests.extend((SOURCE / directory).rglob('Cargo.toml'))
        for path in (SOURCE / directory).rglob('*.rs'):
            if any(part in ('target', 'vendor') for part in path.parts):
                continue
            source = '\n'.join(line for line in path.read_text(encoding='utf-8').splitlines()
                               if not line.lstrip().startswith('//'))
            macros = re.findall(r'\binclude(?:_str|_bytes)?!\s*\((.*?)\)\s*;', source, re.S)
            macros += re.findall(r'#\[path\s*=\s*(.*?)\]', source, re.S)
            for macro in macros:
                target = ''.join(re.findall(r'"([^"]*)"', macro)).replace('\\', '/').casefold()
                require('hivemind' not in target.split('/'),
                        f'Product source includes orchestration input: {path}')

    def compilation_paths(value):
        if isinstance(value, dict):
            for key, child in value.items():
                if key == 'path' and isinstance(child, str):
                    yield child
                elif key in ('members', 'default-members') and isinstance(child, list):
                    yield from (item for item in child if isinstance(item, str))
                else:
                    yield from compilation_paths(child)
        elif isinstance(value, list):
            for child in value:
                yield from compilation_paths(child)

    for path in manifests:
        if not path.is_file() or any(part in ('target', 'vendor') for part in path.parts):
            continue
        document = tomllib.loads(path.read_text(encoding='utf-8-sig'))
        for target in compilation_paths(document):
            require('hivemind' not in target.replace('\\', '/').casefold().split('/'),
                    f'Product manifest references orchestration input: {path}')


def has_reparse_attribute(path):
    return path.is_symlink() or bool(getattr(path.lstat(), 'st_file_attributes', 0) & 0x400)


def source_inventories():
    require_product_owned_includes()
    def git_names(*arguments, paths=None):
        command = ["git", "ls-files", *arguments, "-z"]
        if paths is not None:
            if not paths:
                return set()
            command.extend(["--", *paths])
        return set(filter(None, subprocess.check_output(
            command, cwd=SOURCE,
        ).decode("utf-8").split("\0")))

    tracked = git_names("--cached")
    ordinary_roots = []
    for path in SOURCE.iterdir():
        if has_reparse_attribute(path):
            require(not is_build_input(path.name) and not is_build_input(path.name + '/'),
                    f'Reparse build root refused: {path}')
        else:
            ordinary_roots.append(path.name)
    # Never ask Git to recurse into an archival junction. Only its already
    # tracked filenames are read as informative checkout entries below.
    names = tracked | git_names("--others", "--exclude-standard", paths=ordinary_roots)
    # Cargo targets include local, untracked modules and examples. Deleted tracked
    # inputs remain explicit; HEAD alone does not describe this working tree.
    for name in names.copy():
        if is_build_input(name) and Path(name).name == "Cargo.toml" and (SOURCE / name).is_file():
            lock = Path(name).with_name("Cargo.lock").as_posix()
            if (SOURCE / lock).is_file():
                names.add(lock)
    cargo_configuration = SOURCE / ".cargo"
    if cargo_configuration.exists():
        physical_path(cargo_configuration, SOURCE)
        for directory, directories, files in os.walk(cargo_configuration, followlinks=False):
            for name in directories + files:
                path = Path(directory) / name
                physical_path(path, SOURCE)
                if path.is_file():
                    names.add(path.relative_to(SOURCE).as_posix())
    files = {}
    for name in sorted(names):
        path = SOURCE / name
        if is_build_input(name):
            require(not path.is_symlink(), f'Reparse source refused: {path}')
        if not path.exists():
            require(name in tracked, f'Build input disappeared during inventory: {path}')
            files[name] = None
            continue
        if is_build_input(name):
            physical_path(path, SOURCE)
        require(path.is_file(), f'Non-file build input refused: {path}')
        files[name] = sha(path)
    return {name: pin for name, pin in files.items() if is_build_input(name)}, files


def source_inputs():
    return source_inventories()[0]


def working_tree_inventory(files):
    entries = {}
    for name in files:
        if is_build_input(name):
            continue
        path = SOURCE / name
        for part in (path, *path.parents):
            if part == SOURCE.parent:
                break
            if part.exists() and has_reparse_attribute(part):
                entries[name] = {'path': name, 'resolved_path': str(path.resolve()),
                                 'reparse': True, 'informative_only': True, 'kind': 'file'}
                break
    for path in SOURCE.iterdir():
        if has_reparse_attribute(path):
            entries.setdefault(path.name, {'path': path.name, 'resolved_path': str(path.resolve()),
                                           'reparse': True, 'informative_only': True,
                                           'kind': 'directory' if path.is_dir() else 'file'})
    return {'schema': 'forge.gate3.working-tree-inputs.v1', 'source': str(SOURCE),
            'files': files, 'fingerprint_sha256': fingerprint(files),
            'informative_reparse_entries': [entries[name] for name in sorted(entries)]}


def fingerprint(files):
    return hashlib.sha256(json.dumps(files, sort_keys=True).encode()).hexdigest()


def changed_inputs(before, after):
    return sorted(name for name in before.keys() | after.keys()
                  if name not in before or name not in after or before[name] != after[name])


def working_tree_changes(before, after):
    return [{'path': name, 'before_sha256': before.get(name), 'after_sha256': after.get(name)}
            for name in changed_inputs(before, after)]


def write(path, data):
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False), encoding="utf-8")


def executed_paths(log_text, target_dir):
    tokens = re.findall(r'^\s*Running\s+.+?\s+\((.+)\)\s*$', log_text, re.MULTILINE)
    require(tokens, 'Cargo test log contains no executed test binaries')
    paths = set()
    for token in tokens:
        path = Path(token)
        if not path.is_absolute():
            path = SOURCE / path
        physical_path(path, target_dir)
        path = path.resolve()
        require(path.is_relative_to(target_dir.resolve()), f'Test binary outside common Cargo cache: {path}')
        require(path.is_file(), f'Executed test binary missing: {path}')
        paths.add(path)
    return sorted(paths)


def binary_pins(log_text, target_dir):
    return [{'path': str(path), 'sha256': sha(path), 'bytes': path.stat().st_size}
            for path in executed_paths(log_text, target_dir)]


def verify_binary(pin, target_dir):
    path = Path(pin['path'])
    require(path.is_absolute() and path.is_relative_to(target_dir.resolve()),
            f'Test binary outside common Cargo cache: {path}')
    physical_path(path, target_dir)
    require(path.is_file() and path.stat().st_size == pin['bytes'] and sha(path) == pin['sha256'],
            f'Native test binary changed: {path}')
    return path


def release_artifact_paths(target_dir):
    suffix = '.exe' if os.name == 'nt' else ''
    release = target_dir.resolve() / 'release'
    return {stage: release / f'{name}{suffix}' for stage, name in (
        ('runtime-release', 'forge_runtime'), ('studio-release', 'forge-studio'),
        ('native-game-release', 'examples/tidewater_plant_probe'))}


def release_artifact_pin(path, target_dir):
    require(path.is_absolute() and path.is_relative_to(target_dir.resolve()),
            f'Release artifact outside common Cargo cache: {path}')
    physical_path(path, target_dir)
    require(path.is_file(), f'Release artifact absent: {path}')
    return {'path': str(path), 'sha256': sha(path), 'bytes': path.stat().st_size}


def verify_release_artifacts(pins, expected_paths, target_dir):
    require(isinstance(pins, list) and len(pins) == len(expected_paths),
            'Release artifact inventory is incomplete')
    for pin, path in zip(pins, expected_paths):
        require(pin == release_artifact_pin(path, target_dir), f'Release artifact changed: {path}')


def validate_frontend(frontend, source_files, require_assets=False, expected_asset_manifest_sha256=None):
    """Close the generated frontend and authenticate both common hook receipts."""
    physical_path(frontend, frontend)
    require(frontend.is_dir(), f'Canonical frontend is absent: {frontend}')
    files = {}
    for directory, directories, names in os.walk(frontend, followlinks=False):
        for name in directories + names:
            path = Path(directory) / name
            physical_path(path, frontend)
            if path.is_file():
                relative = path.relative_to(frontend).as_posix()
                files[relative] = {'sha256': sha(path), 'bytes': path.stat().st_size}
    for name in ('index.html', 'styles.css', 'tidewater-webgpu-compat.js',
                 'forge-navigation-worker.js', 'forge_navigation.js', 'forge_navigation_bg.wasm',
                 'forge-navigation-worker-manifest.json', 'forge-studio-local-assets-manifest.json'):
        require(name in files, f'Canonical frontend is incomplete: {name}')
    for expression in (r'forge_studio[^/]*\.js', r'forge_studio[^/]*_bg\.wasm'):
        require(any(re.fullmatch(expression, name) for name in files),
                f'Canonical Trunk artifact missing: {expression}')
    worker = read_json(frontend / 'forge-navigation-worker-manifest.json')
    require(worker.get('schema') == 'forge.navigation-worker-build' and worker.get('version') == 1
            and worker.get('build_only') is True and worker.get('runtime_verified') is False
            and worker.get('source_unchanged_during_build') is True and worker.get('configuration') == 'release',
            'Navigation worker receipt is incomplete or incompatible')
    expected_worker = {'Cargo.toml', 'Cargo.lock', 'crates/forge_navigation/Cargo.toml',
                       'crates/forge_navigation/build.rs', 'third_party/rerecast-0.4.0/Cargo.toml',
                       'apps/forge_studio/forge-navigation-worker.js'}
    expected_worker.update(name for name, pin in source_files.items() if pin is not None and
                           name.startswith(('crates/forge_navigation/src/', 'third_party/rerecast-0.4.0/src/')))
    worker_sources = worker.get('source_pins', [])
    require(len(worker_sources) == len(expected_worker)
            and {pin['path'] for pin in worker_sources} == expected_worker,
            'Navigation worker source inventory is not closed')
    for pin in worker_sources:
        require(source_files.get(pin['path']) == pin['sha256'],
                f'Navigation worker was not built from the common source pin: {pin["path"]}')
    artifacts = worker.get('artifacts', [])
    expected_artifacts = {'forge-navigation-worker.js', 'forge_navigation.js', 'forge_navigation_bg.wasm'}
    require(len(artifacts) == 3 and {pin['path'] for pin in artifacts} == expected_artifacts,
            'Navigation worker artifact inventory is not closed')
    for pin in artifacts:
        require(files[pin['path']]['sha256'] == pin['sha256'], f'Navigation worker artifact changed: {pin["path"]}')
    assets = read_json(frontend / 'forge-studio-local-assets-manifest.json')
    require(assets.get('schema') == 'forge.studio.local-assets.v1' and assets.get('build_only') is True
            and assets.get('runtime_verified') is False and assets.get('gate_qualified') is False
            and assets.get('orchestrator_sha256') == source_files.get('scripts/Stage-StudioLocalAssets.ps1'),
            'Local static asset receipt is incomplete or incompatible')
    actual_assets = {name: pin['sha256'] for name, pin in files.items()
                     if name.startswith(('forge-imports/', 'forge-kit/'))}
    if assets.get('assets_present') is True:
        require(assets.get('status') == 'STAGED' and assets.get('source_unchanged_during_staging') is True
                and assets.get('destination_verified') is True and assets.get('files') == actual_assets,
                'Staged static asset receipt does not close the frontend assets')
        imports = sum(name.startswith('forge-imports/') for name in actual_assets)
        kit = sum(name.startswith('forge-kit/') for name in actual_assets)
        require(imports > 0 and kit > 0 and assets.get('import_files') == imports and assets.get('kit_files') == kit,
                'Static asset counts differ from their receipt')
        asset_root = Path(assets['asset_root'])
        manifest = asset_root / 'frontend-static-inputs.json'
        physical_path(manifest, asset_root)
        require(sha(manifest) == assets.get('source_manifest_sha256')
                and read_json(manifest).get('files') == actual_assets,
                'Local acquisition manifest differs from the staged frontend receipt')
        if expected_asset_manifest_sha256 is not None:
            require(assets['source_manifest_sha256'] == expected_asset_manifest_sha256,
                    'Local acquisition differs from the approved Gate 3 static input manifest')
    else:
        require(not require_assets, 'A local installable release requires forge-imports and forge-kit')
        require(assets.get('assets_present') is False and assets.get('status') == 'LOCAL_ASSETS_ABSENT'
                and assets.get('files') == {} and not actual_assets
                and assets.get('import_files') == 0 and assets.get('kit_files') == 0
                and assets.get('source_manifest_sha256') is None,
                'Absent local assets must be declared explicitly without staged content')
    return {'frontend': str(frontend), 'files': dict(sorted(files.items())),
            'assets_present': assets['assets_present'],
            'local_asset_manifest_sha256': assets.get('source_manifest_sha256'),
            'worker_manifest_sha256': files['forge-navigation-worker-manifest.json']['sha256'],
            'asset_receipt_sha256': files['forge-studio-local-assets-manifest.json']['sha256']}


def completed_receipt(evidence, mode, current_files, target_dir=None):
    """Require the complete current driver/source/log chain, without running checks."""
    manifest_path = evidence / f'{mode}-source-manifest.json'
    result_path = evidence / f'{mode}-result.json'
    manifest, result = read_json(manifest_path), read_json(result_path)
    driver = sha(Path(__file__))
    require(result.get('schema') == 'forge.gate3.validation.v1' and result.get('mode') == mode
            and result.get('status') == 'PASS' and result.get('source_unchanged') is True
            and result.get('changed_inputs') == [] and result.get('source_manifest_sha256') == sha(manifest_path),
            f'{mode} receipt is missing, incomplete or not PASS')
    require(manifest.get('schema') == 'forge.gate3.source-inputs.v1'
            and Path(manifest['source']).resolve() == SOURCE and manifest.get('driver_sha256') == driver
            and result.get('driver_sha256') == driver and manifest.get('files') == current_files
            and manifest.get('fingerprint_sha256') == fingerprint(current_files),
            f'{mode} receipt belongs to different sources or validation driver')
    if target_dir is not None:
        require(Path(manifest['target_dir']).resolve() == target_dir.resolve(), f'{mode} used a different common Cargo cache')
    environment = manifest.get('build_environment')
    require(isinstance(environment, dict) and environment == result.get('build_environment')
            and environment.get('CARGO_TARGET_DIR') == manifest['target_dir'],
            f'{mode} build environment differs between manifest and receipt')
    trees = []
    for label in ('before', 'after'):
        path = evidence / f'{mode}-working-tree-{label}.json'
        tree = read_json(path)
        require(tree.get('schema') == 'forge.gate3.working-tree-inputs.v1'
                and tree.get('source') == str(SOURCE)
                and tree.get('fingerprint_sha256') == fingerprint(tree['files'])
                and sha(path) == result.get(f'working_tree_{label}_sha256')
                and {name: pin for name, pin in tree['files'].items() if is_build_input(name)} == current_files,
                f'{mode} full checkout inventory differs: {label}')
        notes = tree.get('informative_reparse_entries')
        require(isinstance(notes, list) and all(
            note.get('reparse') is True and note.get('informative_only') is True
            and note.get('kind') in {'file', 'directory'}
            and not is_build_input(note['path']) and not is_build_input(note['path'] + '/')
            and Path(note['resolved_path']).is_absolute() for note in notes),
            f'{mode} informative reparse metadata is incomplete or includes a build input')
        trees.append(tree['files'])
    require(manifest.get('working_tree_before_sha256') == result['working_tree_before_sha256']
            and result.get('working_tree_changes') == working_tree_changes(*trees),
            f'{mode} informative checkout changes differ from their inventory proofs')
    rows = result.get('stages', [])
    require(len(rows) == len(STAGES[mode]), f'{mode} receipt has an incomplete stage list')
    for row, (name, command, cwd) in zip(rows, STAGES[mode]):
        log = evidence / f'{name}.log'
        require(row.get('stage') == name and row.get('exit_code') == 0 and row.get('validation_error') is None
                and row.get('command') == command
                and Path(row['cwd']).resolve() == cwd.resolve() and Path(row['log']).resolve() == log
                and sha(log) == row.get('log_sha256'), f'{mode} stage/log proof differs: {name}')
    if mode == 'wasm':
        frontend = validate_frontend(SOURCE / 'apps/forge_studio/dist', current_files)
        require(result.get('frontend') == frontend and rows[-1].get('frontend') == frontend,
                'Canonical frontend differs from the completed common WASM receipt')
    if mode == 'release':
        cache = Path(manifest['target_dir']).resolve()
        paths = release_artifact_paths(cache)
        verify_release_artifacts(result.get('artifacts'), list(paths.values()), cache)
        for row in rows:
            verify_release_artifacts(row.get('artifacts'), [paths[row['stage']]], cache)
            require(row.get('frontend_unchanged') is True,
                    'Release stage did not close its embedded frontend')
        frontend = release_frontend(evidence, current_files, cache)
        require(result.get('frontend') == frontend
                and result.get('wasm_result_sha256') == sha(evidence / 'wasm-result.json'),
                'Release receipt differs from its completed common WASM/frontend proof')
    return manifest, result


def release_frontend(evidence, current_files, target_dir):
    _, wasm = completed_receipt(evidence, 'wasm', current_files, target_dir)
    frontend = SOURCE / 'apps/forge_studio/dist'
    current = validate_frontend(frontend, current_files, require_assets=True,
                                expected_asset_manifest_sha256=LOCAL_ASSET_MANIFEST_SHA256)
    require(wasm.get('frontend') == current and wasm['stages'][-1].get('frontend') == current,
            'Canonical frontend differs from the completed common WASM receipt')
    configuration = read_json(SOURCE / 'apps/forge_studio/tauri.conf.json')
    main_window = next((window for window in configuration.get('app', {}).get('windows', [])
                        if window.get('label') == 'main'), {})
    require(configuration.get('build', {}).get('frontendDist') == 'dist'
            and main_window.get('url') == 'index.html?authoring=1',
            'Canonical Tauri release must embed dist and open authoring')
    return current


STAGES = {
    "native": [
        ("format", ["cargo", "fmt", "--all", "--", "--check"], SOURCE),
        ("diff", ["git", "diff", "--check"], SOURCE),
        ("clippy", ["cargo", "clippy", "--workspace", "--all-targets", "--locked", "--offline", "--", "-D", "warnings"], SOURCE),
        ("tests", ["cargo", "test", "--workspace", "--all-targets", "--locked", "--offline"], SOURCE),
    ],
    "underwater": [
        ("underwater", ["cargo", "test", "--locked", "--offline", "-p", "forge_engine", "--lib", "tidewater_underwater::tests::combined_surface_shader_keeps_the_near_lens_guard"], SOURCE),
    ],
    "wasm": [
        ("webgpu-engine", ["cargo", "check", "--locked", "--offline", "--target", "wasm32-unknown-unknown", "-p", "forge_engine", "--no-default-features", "--features", "web"], SOURCE),
        ("studio-webgpu-bundle", ["trunk", "build", "--release", "--config", "Trunk.toml"], SOURCE / "apps/forge_studio"),
    ],
    "release": [
        ("runtime-release", ["cargo", "build", "--locked", "--offline", "--release", "-p", "forge_runtime"], SOURCE),
        # Compile the same native Tauri target without rewriting Cargo.toml.
        # The canonical dist was built and authenticated by the wasm stage.
        ("studio-release", ["cargo", "build", "--release", "--locked", "--offline", "-p", "forge_studio", "--bin", "forge-studio", "--features", "tauri/custom-protocol"], SOURCE),
        # This is the actual separate game configured beside the shared Studio.
        ("native-game-release", ["cargo", "build", "--release", "--locked", "--offline", "-p", "forge_tidewater", "--example", "tidewater_plant_probe"], SOURCE),
    ],
    "supply": [
        ("deny", ["cargo", "deny", "check", "licenses", "bans", "sources", "advisories"], SOURCE),
        ("audit", ["pwsh", "-NoProfile", "-File", "scripts/Test-RustSec.ps1"], SOURCE),
    ],
}


def main():
    # Cargo diagnostics include Unicode graph lines on Windows redirected pipes.
    # Keep a failed stage readable without raising a second encoding exception.
    if hasattr(sys.stdout, 'reconfigure'):
        sys.stdout.reconfigure(encoding='utf-8', errors='backslashreplace')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=STAGES)
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--target-dir", type=Path, default=Path(os.environ.get("CARGO_TARGET_DIR", "target")))
    args = parser.parse_args()
    evidence = args.evidence.resolve()
    evidence.mkdir(parents=True, exist_ok=True)
    manifest_path = evidence / f"{args.mode}-source-manifest.json"
    tree_before_path = evidence / f'{args.mode}-working-tree-before.json'
    tree_after_path = evidence / f'{args.mode}-working-tree-after.json'
    owned = [manifest_path, evidence / f'{args.mode}-result.json', evidence / f'{args.mode}-progress.json',
             tree_before_path, tree_after_path]
    owned.extend(evidence / f'{name}.log' for name, _, _ in STAGES[args.mode])
    for path in owned:
        if path.exists():
            parser.error(f"Previous evidence preserved: {path}")
    env = os.environ.copy()
    env.update(CARGO_TARGET_DIR=str(args.target_dir.resolve()), CARGO_BUILD_JOBS=env.get("CARGO_BUILD_JOBS", "1"), CARGO_TERM_COLOR="never", NO_COLOR="true", CARGO_NET_OFFLINE="true")
    # A candidate override must never redirect the canonical Tauri embedding.
    env.pop('TAURI_CONFIG', None)
    build_environment = {name: env.get(name) for name in (
        'CARGO_TARGET_DIR', 'CARGO_BUILD_JOBS', 'CARGO_NET_OFFLINE',
        'CARGO_INCREMENTAL', 'RUSTFLAGS', 'RUST_TEST_THREADS', 'CARGO_TERM_COLOR', 'NO_COLOR')}
    before, tree_before = source_inventories()
    write(tree_before_path, working_tree_inventory(tree_before))
    frontend = None
    if args.mode == 'release':
        frontend = release_frontend(evidence, before, args.target_dir.resolve())
    write(manifest_path, {
        'schema': 'forge.gate3.source-inputs.v1',
        "source": str(SOURCE), "base_commit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=SOURCE).decode().strip(),
        "scope": "Shared product build inputs, including local Cargo locks/configurations; non-consumed orchestration/acquisition separately inventoried after include guards",
        "fingerprint_sha256": fingerprint(before), "files": before,
        "driver_sha256": sha(Path(__file__)), "target_dir": env["CARGO_TARGET_DIR"],
        'build_environment': build_environment,
        'working_tree_before_sha256': sha(tree_before_path),
    })
    rows = []
    artifacts = []
    for name, command, cwd in STAGES[args.mode]:
        log = evidence / f"{name}.log"
        if log.exists():
            parser.error(f"Previous evidence preserved: {log}")
        start = datetime.datetime.now(datetime.timezone.utc).isoformat()
        print(f"START {name} shared-source={SOURCE}", flush=True)
        write(evidence / f"{args.mode}-progress.json", {"stage": name, "status": "RUNNING", "command": command, "started_utc": start})
        stage_error = None
        try:
            with log.open("xb") as output:
                result = subprocess.run(command, cwd=cwd, env=env, stdout=output, stderr=subprocess.STDOUT)
            exit_code = result.returncode
        except Exception as failure:
            exit_code, stage_error = 1, str(failure)
        row = {"stage": name, "command": command, "cwd": str(cwd), "started_utc": start,
               "ended_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "exit_code": exit_code,
               "log": str(log), "log_sha256": sha(log)}
        if name in {"tests", "underwater"}:
            counts = re.findall(r"test result: ok\. (\d+) passed; (\d+) failed; (\d+) ignored;", log.read_text(encoding="utf-8", errors="replace"))
            row.update(passed=sum(int(a) for a, _, _ in counts), ignored=sum(int(c) for _, _, c in counts), suites=len(counts))
        try:
            if exit_code == 0 and name == 'tests':
                row['test_binaries'] = binary_pins(log.read_text(encoding='utf-8', errors='replace'), args.target_dir.resolve())
            if exit_code == 0 and name == 'studio-webgpu-bundle':
                frontend = validate_frontend(SOURCE / 'apps/forge_studio/dist', before)
                row['frontend'] = frontend
            if args.mode == 'release':
                require(validate_frontend(SOURCE / 'apps/forge_studio/dist', before, require_assets=True,
                                          expected_asset_manifest_sha256=LOCAL_ASSET_MANIFEST_SHA256) == frontend,
                        'Canonical frontend changed during release compilation')
                row['frontend_unchanged'] = True
                if exit_code == 0:
                    pin = release_artifact_pin(release_artifact_paths(args.target_dir)[name], args.target_dir.resolve())
                    row['artifacts'] = [pin]
                    artifacts.append(pin)
                    verify_release_artifacts(artifacts,
                                             list(release_artifact_paths(args.target_dir).values())[:len(artifacts)],
                                             args.target_dir.resolve())
        except Exception as failure:
            stage_error = str(failure)
        row['validation_error'] = stage_error
        rows.append(row)
        after, tree_after = source_inventories()
        write(tree_after_path, working_tree_inventory(tree_after))
        changed = changed_inputs(before, after)
        tree_changed = working_tree_changes(tree_before, tree_after)
        status = "FAILED" if exit_code or changed or stage_error else "IN_PROGRESS"
        receipt = {'schema': 'forge.gate3.validation.v1', 'mode': args.mode,
                   'driver_sha256': sha(Path(__file__)), "source_manifest_sha256": sha(manifest_path),
                   "source_unchanged": not changed, "changed_inputs": changed, "stages": rows,
                   "status": status, 'runtime_verified': False, 'gate_qualified': False,
                   'build_environment': build_environment}
        receipt.update(working_tree_changes=tree_changed,
                       working_tree_before_sha256=sha(tree_before_path),
                       working_tree_after_sha256=sha(tree_after_path))
        if frontend is not None:
            receipt['frontend'] = frontend
        if args.mode == 'release':
            receipt['wasm_result_sha256'] = sha(evidence / 'wasm-result.json')
            receipt['artifacts'] = artifacts
        write(evidence / f"{args.mode}-result.json", receipt)
        print(f"END {name} exit={exit_code} source_unchanged={not changed}", flush=True)
        if status == 'FAILED':
            write(evidence / f"{args.mode}-progress.json", {"stage": name, "status": status})
            if stage_error:
                print(stage_error, flush=True)
            print(log.read_text(encoding="utf-8", errors="replace")[-6500:], flush=True)
            return exit_code or 1
    receipt["status"] = "PASS"
    write(evidence / f"{args.mode}-result.json", receipt)
    write(evidence / f"{args.mode}-progress.json", {"status": "PASS", "ended_utc": datetime.datetime.now(datetime.timezone.utc).isoformat()})
    print(f"PASS {args.mode}", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
