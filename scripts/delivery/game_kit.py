"""Assemble a private platform game kit from pinned native build receipts.

This is a preparation candidate, not a build, install, licence or release verdict.
The output contains an embedded-manifest input and a separate adjacent kit tree;
the Studio distribution must close and embed both through its own pinned receipts.
"""
from __future__ import annotations

import argparse
from dataclasses import dataclass
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import stat
import subprocess
import sys
import tempfile

import application_sources
import build_delivery
import complete_rebuild
import linux_build_evidence
import runtime_notices

SOURCE = Path(__file__).resolve().parents[2]
TARGET = 'x86_64-pc-windows-msvc'
PLATFORM = 'windows-x64'
STANDALONE = 'FORGE_LAUNCHER_STANDALONE_FRONTEND_READY'
AUBE = b'FORGE_LAUNCHER_AUBE_FRONTEND_READY'
STANDALONE_TAURI_CONFIG = {
    'productName': 'OpenRPG Forge',
    'app': {'windows': [{
        'label': 'main', 'title': 'Jeu', 'width': 1600, 'height': 960,
        'minWidth': 1280, 'minHeight': 720, 'resizable': True, 'maximized': True,
        'fullscreen': False, 'decorations': True, 'transparent': False,
        'shadow': True, 'backgroundColor': '#071b1b',
    }]},
}
LAUNCHER = 'bin/forge-game-launcher.exe'
RUNTIME = 'bin/forge-module.exe'
LINUX_PLATFORM = 'linux-x64'
LINUX_LAUNCHER = 'bin/forge-game-launcher'
LINUX_RUNTIME = 'bin/forge-module'
PLATFORM_BINARIES = {PLATFORM:(LAUNCHER,RUNTIME), LINUX_PLATFORM:(LINUX_LAUNCHER,LINUX_RUNTIME)}
MAX_RECEIPT = 64 * 1024 * 1024
MAX_FILES = 250_000
MAX_FILE_BYTES = 16 * 1024 * 1024 * 1024
MAX_TOTAL_BYTES = 256 * 1024 * 1024 * 1024
NOTICE_SCHEMAS = {
    'INDEX.json': 'forge.rust-dependency-notices.v2',
    'SUPPLEMENT-NOTICES.json': 'forge.rust-notice-supplements.v1',
    'FORGE-ORIGINAL-NOTICES.json': 'forge.original-code-notices.v1',
    'ASSET-NOTICES.json': 'forge.asset-notice-supplements.v1',
}


@dataclass(frozen=True)
class VerifiedAssembledGameKit:
    root: Path
    kit_root: Path
    manifest: dict
    manifest_bytes: bytes
    manifest_sha256: str
    receipt: dict
    receipt_sha256: str


def require(condition, message):
    if not condition:
        raise ValueError(message)


def standalone_tauri_config(frontend, repo=SOURCE):
    # Tauri's untagged FrontendDist tries Url before Directory. A Windows drive
    # path such as E:\\... parses as an `e:` URL and silently embeds no assets.
    # Resolve the admitted temporary directory from the launcher config instead.
    relative = os.path.relpath(frontend, Path(repo) / 'apps/forge_launcher').replace('\\', '/')
    return {**STANDALONE_TAURI_CONFIG, 'build': {'frontendDist': relative}}


def read_document(path):
    data = application_sources.read_regular(path, MAX_RECEIPT, 'build receipt')
    value = json.loads(data)
    require(isinstance(value, dict), 'Build receipt must be a JSON object')
    return value


def physical_file(path):
    path = Path(path)
    application_sources.ordinary(path)
    require(path.is_file() and path.stat().st_size > 0, f'Physical nonempty file required: {path}')
    return path


def contains_bytes(path, needle):
    carry = b''
    with physical_file(path).open('rb') as stream:
        while block := stream.read(1024 * 1024):
            data = carry + block
            if needle in data:
                return True
            carry = data[-(len(needle)-1):]
    return False


def require_windows_x64_executable(path):
    path = physical_file(path)
    with path.open('rb') as stream:
        header = stream.read(64)
        require(len(header) == 64 and header[:2] == b'MZ', f'Windows x64 PE executable required: {path}')
        offset = int.from_bytes(header[60:64], 'little')
        require(64 <= offset <= min(path.stat().st_size - 26, 16 * 1024 * 1024),
                f'Invalid PE header offset: {path}')
        stream.seek(offset)
        pe = stream.read(26)
    require(pe[:4] == b'PE\0\0' and pe[4:6] == b'\x64\x86' and pe[24:26] == b'\x0b\x02',
            f'Windows x64 PE executable required: {path}')


def verify_launcher(path):
    path = physical_file(path)
    require_windows_x64_executable(path)
    before = build_delivery.digest(path)
    require(not contains_bytes(path, AUBE), 'Reference Aube marker is present in the generic launcher')
    result = subprocess.run([str(path), '--verify-distributable'], capture_output=True,
                            text=True, encoding='utf-8', timeout=30, check=False)
    require(result.returncode == 0 and result.stdout in (STANDALONE, STANDALONE + '\n'),
            'Launcher did not prove its standalone embedded frontend')
    require(build_delivery.digest(path) == before, 'Launcher changed during its verification')
    return {'executable_sha256': before, 'command': '--verify-distributable',
            'exit_code': 0, 'marker': STANDALONE}


def has_build_command(proof, repo, component, feature):
    for row in proof.get('commands', []):
        if not isinstance(row, dict) or not isinstance(row.get('arguments'), list):
            continue
        args = [str(value) for value in row['arguments']]
        if len(args) < 2 or Path(args[0]).name.lower() not in ('cargo', 'cargo.exe') or args[1] != 'build':
            continue
        if not all(flag in args for flag in ('--release', '--locked', '--offline')):
            continue
        if '--features' not in args or args.index('--features') + 1 >= len(args):
            continue
        if args[args.index('--features') + 1].split(',') != [feature]:
            continue
        if component == 'launcher':
            if '--manifest-path' not in args or args.index('--manifest-path') + 1 >= len(args):
                continue
            if Path(args[args.index('--manifest-path') + 1]).resolve() != repo / 'apps/forge_launcher/Cargo.toml':
                continue
        else:
            if not all(flag in args for flag in ('--no-default-features', '-p', '--bin')):
                continue
            if any(args.index(flag) + 1 >= len(args) for flag in ('-p', '--bin')):
                continue
            if (args[args.index('-p') + 1] != 'forge_runtime' or
                    args[args.index('--bin') + 1] != 'forge-module' or
                    Path(row.get('cwd', '')).resolve() != repo):
                continue
        return True
    return False


def has_frontend_build_command(proof, repo):
    root = repo / 'apps/forge_launcher'
    for row in proof.get('commands', []):
        if not isinstance(row, dict) or not isinstance(row.get('arguments'), list):
            continue
        args = [str(value) for value in row['arguments']]
        if (len(args) == 2 and Path(args[0]).name.lower() in ('node', 'node.exe') and
                Path(args[1]).resolve() == root / 'scripts/build-aube.mjs' and
                Path(row.get('cwd', '')).resolve() == root):
            return True
    return False


def verify_builds(repo, source_manifest_path, source_manifest, launcher, launcher_proof, runtime, runtime_proof):
    manifest_hash = build_delivery.digest(source_manifest_path)
    require(source_manifest.get('schema') == 'forge.delivery.shared-source-inputs.v1' and
            Path(source_manifest.get('source_root', '')).resolve() == repo and
            complete_rebuild.source_inputs_equal(complete_rebuild.shared_source_inputs(repo), source_manifest),
            'Current shared source inventory differs from build inputs')
    require(build_delivery.covers_source_scope(source_manifest, 'apps/forge_launcher') and
            build_delivery.covers_source_scope(source_manifest, 'apps/forge_runtime'),
            'Launcher and runtime source scopes must be pinned')
    project_notice = physical_file(repo / 'THIRD_PARTY_NOTICES.md')
    require(source_manifest.get('working_tree_files', {}).get('THIRD_PARTY_NOTICES.md') ==
            build_delivery.digest(project_notice), 'Project notice differs from the pinned build checkout')
    physical_file(launcher)
    physical_file(runtime)
    require_windows_x64_executable(launcher)
    require_windows_x64_executable(runtime)
    require(launcher.resolve() != runtime.resolve() and build_delivery.digest(launcher) != build_delivery.digest(runtime),
            'Launcher and module runtime must be distinct')
    for executable, proof in ((launcher, launcher_proof), (runtime, runtime_proof)):
        build_delivery.validate_shared_native_application(repo, executable, proof, manifest_hash)
    require(launcher_proof.get('application_role') == 'standalone-launcher' and
            launcher_proof.get('profile') == 'release' and
            launcher_proof.get('frontend_profile') == 'standalone' and
            launcher_proof.get('cargo_features') == ['standalone'] and
            launcher_proof.get('frontend_builder_marker') == 'STANDALONE_FRONTEND_READY' and
            has_frontend_build_command(launcher_proof, repo) and
            has_build_command(launcher_proof, repo, 'launcher', 'standalone'),
            'A current standalone launcher release receipt is required')
    env = launcher_proof.get('build_environment', {})
    require(env.get('FORGE_LAUNCHER_PROFILE') == 'standalone', 'Launcher frontend profile is not pinned')
    try:
        config = json.loads(env.get('TAURI_CONFIG', ''))
    except (ValueError, TypeError) as error:
        raise ValueError('Launcher Tauri config is not pinned JSON') from error
    frontend_value = launcher_proof.get('frontend')
    require(isinstance(frontend_value, str) and frontend_value, 'Launcher frontend path is missing')
    frontend = Path(frontend_value)
    temporary_root = (repo.parent / '.build/tmp').resolve(strict=True)
    require(frontend.is_absolute() and '..' not in frontend.parts and
            frontend.parent.resolve() == temporary_root,
            'Launcher frontend must be a direct child of the shared temporary directory')
    require(launcher_proof.get('frontend_builder_output') == str(frontend),
            'Launcher frontend builder output differs from its receipt')
    require(config == standalone_tauri_config(frontend, repo) and
            launcher_proof.get('tauri_config_sha256') == runtime_notices.sha(env['TAURI_CONFIG'].encode('utf-8')) and
            env.get('FORGE_LAUNCHER_DIST') == str(frontend) and
            env.get('FORGE_LAUNCHER_TEMP_ROOT') == str(temporary_root) and
            env.get('FORGE_AUBE_SOURCE') == str(repo / 'apps/forge_launcher/canonical-ui'),
            'Launcher Tauri config or frontend build environment differs')
    verification = launcher_proof.get('launcher_verification', {})
    require(isinstance(verification, dict) and verification.get('command') == '--verify-distributable' and
            verification.get('marker') == STANDALONE and verification.get('exit_code') == 0 and
            verification.get('executable_sha256') == build_delivery.digest(launcher),
            'Launcher build verification is missing or differs from the executable')
    orchestrator = source_manifest.get('files', {}).get('scripts/delivery/complete_rebuild.py', {}).get('sha256')
    require(isinstance(orchestrator, str) and
            launcher_proof.get('orchestrator_sha256') == runtime_proof.get('orchestrator_sha256') == orchestrator,
            'Native build orchestrator differs from the pinned source manifest')
    application_sources.ordinary(frontend)
    require(frontend.is_dir() and launcher_proof.get('frontend') == str(frontend) and
            launcher_proof.get('frontend_pins') == complete_rebuild.frontend_inputs(frontend),
            'Launcher frontend differs from its build receipt')
    require(runtime_proof.get('application_role') == 'module-game' and
            runtime_proof.get('profile') == 'release' and
            has_build_command(runtime_proof, repo, 'runtime', 'module-game'),
            'A current module-game runtime release receipt is required')
    return manifest_hash


def verify_linux_builds(repo, source_path, source_manifest, launcher, launcher_proof_path,
                        runtime, runtime_proof_path, args):
    """Admit the two ELF artifacts through the original Linux core evidence."""
    require(sys.platform == 'linux', 'Linux game kit assembly requires a Linux host')
    require(source_manifest.get('schema') == 'forge.gate7.vm-source-snapshot.v1' and
            source_manifest.get('development_checkout') is False,
            'A verified Linux source snapshot is required')
    for name, loaded in (('scripts/delivery/game_kit.py',Path(__file__)),
                         ('scripts/delivery/linux_build_evidence.py',Path(linux_build_evidence.__file__))):
        pin=source_manifest.get('files',{}).get(name,{})
        require(linux_build_evidence.physical(loaded) and physical_file(loaded).stat().st_size == pin.get('bytes') and
                build_delivery.digest(loaded) == pin.get('sha256'), f'Running Linux kit reader differs: {name}')
    core_directory = runtime_proof_path.parent.resolve(strict=True)
    require(source_path.resolve() == core_directory/'source-manifest.json' and
            launcher_proof_path.resolve() == core_directory/'launcher-build-proof.json' and
            runtime_proof_path.resolve() == core_directory/'linux-core-artifacts.json' and
            launcher.resolve() == core_directory/'forge-launcher-prototype' and
            runtime.resolve() == core_directory/'forge-module',
            'Linux core proofs and executables must share their verified result directory')
    values = [getattr(args,name,None) for name in ('linux_started','linux_capture_receipt','linux_source_zip')]
    require(all(isinstance(value,(str,Path)) and str(value) and linux_build_evidence.physical(Path(value))
                for value in values),
            'Linux run, capture receipt and source ZIP are required')
    started_path, capture_path, source_zip = (physical_file(value) for value in values)
    expected_started=getattr(args,'linux_started_sha256',None)
    expected_capture=getattr(args,'linux_capture_sha256',None)
    core_sha=getattr(args,'linux_core_proof_sha256',None)
    require(all(isinstance(value,str) and re.fullmatch(r'[0-9a-f]{64}',value)
                for value in (expected_started,expected_capture,core_sha)) and
            build_delivery.digest(started_path) == expected_started and
            build_delivery.digest(capture_path) == expected_capture and
            build_delivery.digest(runtime_proof_path) == core_sha,
            'Explicit Linux run, capture and core proof SHA-256 pins are required')
    started, capture = read_document(started_path), read_document(capture_path)
    require(started.get('schema') == 'forge.gate7.linux-editor-launcher-started.v1' and
            started.get('suite') in ('core','all') and started.get('standalone_launcher_requested') is True and
            started.get('gate_closed') is False and started.get('launcher_qualified') is False and
            started.get('source_manifest_sha256') == build_delivery.digest(source_path),
            'Linux launch receipt does not describe this standalone core build')
    cargo_cache=getattr(args,'cargo_cache',None)
    cargo_target=getattr(args,'cargo_target',None) or os.environ.get('CARGO_TARGET_DIR')
    require(isinstance(cargo_cache,(str,Path)) and isinstance(cargo_target,(str,Path)) and
            Path(cargo_cache).as_posix() == started.get('cargo_home','')+'/registry' and
            Path(cargo_target).as_posix() == started.get('cargo_target'),
            'Linux notice resolution must use the core build Cargo home and target')
    proof=linux_build_evidence.verify_linux_core(core_directory,source_zip,started,core_sha,capture_receipt=capture)
    require(proof['launcher_proof_sha256'] == build_delivery.digest(launcher_proof_path) and
            proof['binaries'][0]['sha256'] == build_delivery.digest(runtime) and
            proof['binaries'][1]['sha256'] == build_delivery.digest(launcher),
            'Linux core executable identities differ from their proofs')
    verification={'executable_sha256':proof['binaries'][1]['sha256'], 'command':'--verify-distributable',
                  'exit_code':0, 'marker':STANDALONE, 'source':'linux-sdk-observation',
                  'launcher_proof_sha256':proof['launcher_proof_sha256']}
    return {'source_manifest_sha256':build_delivery.digest(source_path),
            'launcher_sha256':proof['binaries'][1]['sha256'], 'runtime_sha256':proof['binaries'][0]['sha256'],
            'verification':verification, 'source_zip_sha256':started['source_sha256'],
            'started_sha256':expected_started, 'capture_sha256':expected_capture, 'core_proof_sha256':core_sha,
            'cargo_home':started['cargo_home'], 'cargo_target':started['cargo_target']}


def current_launcher_dependencies(repo, registry):
    registry = Path(registry).resolve(strict=True)
    require(registry.name.casefold() == 'registry', 'Cargo cache must be the Cargo registry directory')
    env = dict(os.environ, CARGO_HOME=str(registry.parent), CARGO_NET_OFFLINE='true')
    command = ['cargo', 'metadata', '--manifest-path', str(repo / 'apps/forge_launcher/Cargo.toml'),
               '--format-version', '1', '--locked', '--offline', '--filter-platform', TARGET,
               '--features', 'standalone']
    result = subprocess.run(command, cwd=repo, env=env, capture_output=True, check=False)
    require(result.returncode == 0, 'Cannot verify the current standalone launcher Cargo closure offline')
    collector_path = repo / 'scripts/tidewater/collect_rust_notices.py'
    spec = importlib.util.spec_from_file_location('forge_kit_notice_collector', collector_path)
    require(spec is not None and spec.loader is not None, 'Notice collector is unavailable')
    collector = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(collector)
    return set(collector.dependencies(json.loads(result.stdout), 'forge_launcher_prototype'))


def verify_launcher_notice_scope(args, repo, prepared):
    graph = read_document(args.runtime_notice_graph)
    require(any(isinstance(scope, dict) and scope.get('name') == 'launcher-standalone' and
                scope.get('target') == TARGET and
                scope.get('graphs', {}).get('all_nondev', {}).get('exit_code') == 0
                for scope in graph.get('scopes', [])), 'Standalone launcher notice graph is missing')
    files = dict(prepared.files)
    index = json.loads(files['licenses/rust-runtime-candidate/INDEX.json'])
    matches = [request for request in index.get('requests', []) if isinstance(request, dict) and
               request.get('package') == 'forge_launcher_prototype' and request.get('target') == TARGET and
               request.get('features') == ['standalone'] and request.get('no_default_features') is False and
               request.get('include_root_dev') is False]
    require(len(matches) == 1, 'Standalone launcher notice request is missing or ambiguous')
    ids = matches[0].get('package_ids')
    require(isinstance(ids, list) and ids and len(ids) == len(set(ids)) and
            set(ids) == current_launcher_dependencies(repo, args.cargo_cache),
            'Standalone launcher dependency closure differs from its notice candidate')
    return {'target': TARGET, 'request': 'launcher-standalone', 'package_count': len(ids),
            'package_ids_sha256': runtime_notices.sha(json.dumps(sorted(ids)).encode())}


def verify_linux_launcher_notices(scope):
    require(isinstance(scope,dict) and type(scope.get('package_count')) is int and scope['package_count'] > 0 and
            all(isinstance(scope.get(key),str) and re.fullmatch(r'[0-9a-f]{64}',scope[key])
                for key in ('metadata_sha256','current_package_ids_sha256','captured_package_ids_sha256',
                            'normalized_package_ids_sha256')), 'Linux standalone launcher notice closure is incomplete')
    command=scope.get('command')
    require(isinstance(command,list) and len(command)==12 and command[:3]==['cargo','metadata','--manifest-path'] and
            isinstance(command[3],str) and command[3].replace('\\','/').endswith('/apps/forge_launcher/Cargo.toml') and
            command[4:]==['--format-version','1','--locked','--offline','--filter-platform',
                          runtime_notices.LINUX_TARGET,'--features','standalone'],
            'Linux standalone launcher notice command differs')
    return scope


def inventory(root):
    root = Path(root)
    application_sources.ordinary(root)
    require(root.is_dir(), 'Game kit inventory requires a physical directory')
    pins, stack, total = [], [root], 0
    while stack:
        directory = stack.pop()
        with os.scandir(directory) as entries:
            for entry in entries:
                path = Path(entry.path)
                # Windows DirEntry.stat may report zero device/inode; lstat does not.
                metadata = path.lstat()
                require(not stat.S_ISLNK(metadata.st_mode) and not (getattr(metadata, 'st_file_attributes', 0) & 0x400),
                        f'Link or reparse entry refused in game kit: {path}')
                if stat.S_ISDIR(metadata.st_mode):
                    stack.append(path)
                    continue
                require(stat.S_ISREG(metadata.st_mode), f'Non-file entry refused in game kit: {path}')
                name = application_sources.relative(path.relative_to(root).as_posix())
                require(0 < metadata.st_size <= MAX_FILE_BYTES and len(pins) < MAX_FILES,
                        'Game kit inventory exceeds the reader limits or contains an empty file')
                application_sources.ordinary(path)
                with path.open('rb') as stream:
                    opened = os.fstat(stream.fileno())
                    require((opened.st_dev, opened.st_ino, opened.st_size, opened.st_mtime_ns) ==
                            (metadata.st_dev, metadata.st_ino, metadata.st_size, metadata.st_mtime_ns),
                            f'Game kit file changed before reading: {path}')
                    digest = hashlib.file_digest(stream, 'sha256').hexdigest()
                    finished = os.fstat(stream.fileno())
                require((finished.st_dev, finished.st_ino, finished.st_size, finished.st_mtime_ns) ==
                        (opened.st_dev, opened.st_ino, opened.st_size, opened.st_mtime_ns),
                        f'Game kit file changed while reading: {path}')
                pins.append({'path': name, 'sha256': digest, 'bytes': metadata.st_size})
                total += metadata.st_size
                require(total <= MAX_TOTAL_BYTES, 'Game kit inventory exceeds the reader limits')
    pins.sort(key=lambda item: item['path'])
    require(pins, 'Game kit inventory is empty')
    names = set()
    for item in pins:
        name = application_sources.relative(item['path']).lower()
        require(name not in names and name not in {'package-manifest.json', 'game/launcher.json'} and
                not name.startswith('.forge-storage/'), 'Game kit paths collide or are reserved')
        names.add(name)
    for name in names:
        parts = name.split('/')
        require(all('/'.join(parts[:i]) not in names for i in range(1, len(parts))),
                'Game kit file/directory conflict')
    return pins


def verify_staged_notices(kit_root, files):
    lookup = {entry['path']: entry for entry in files}
    notice_name = 'licenses/THIRD_PARTY_NOTICES.md'
    project = application_sources.read_regular(kit_root / notice_name, runtime_notices.MAX_FILE,
                                               'staged project notice')
    require(runtime_notices.sha(project) == lookup[notice_name]['sha256'], 'Project notice changed')
    text = project.decode('utf-8')
    while '](' in text:
        _, _, text = text.partition('](')
        target, closing, text = text.partition(')')
        require(bool(closing), 'Incomplete project notice link')
        if not target.startswith(('https://', 'http://', '#')):
            application_sources.relative(target)
            require('licenses/' + target in lookup, 'Project notice reference is absent from the kit')
    values = {}
    prefix = 'licenses/rust-runtime-candidate/'
    for name, schema in NOTICE_SCHEMAS.items():
        path = prefix + name
        data = application_sources.read_regular(kit_root / path, runtime_notices.MAX_FILE, 'staged notice index')
        require(runtime_notices.sha(data) == lookup[path]['sha256'], 'Notice index changed')
        document = runtime_notices.document(data, 'staged notice index')
        require(document.get('schema') == schema, f'Unsupported staged notice index: {name}')
        pending = [document]
        while pending:
            item = pending.pop()
            if isinstance(item, str) and item.startswith('texts/'):
                require(re.fullmatch(r'texts/[0-9a-f]{64}\.txt', item) is not None and
                        prefix + item in lookup and lookup[prefix + item]['sha256'] == item[6:-4],
                        'Staged content-addressed notice is missing or differs')
            elif isinstance(item, list):
                pending.extend(item)
            elif isinstance(item, dict):
                pending.extend(item.values())
        values[name] = document
    base_hash = lookup[prefix + 'INDEX.json']['sha256']
    supplement_hash = lookup[prefix + 'SUPPLEMENT-NOTICES.json']['sha256']
    require(all(values[name].get('base_index_sha256') == base_hash for name in tuple(NOTICE_SCHEMAS)[1:]) and
            values['FORGE-ORIGINAL-NOTICES.json'].get('supplement_index_sha256') == supplement_hash,
            'Staged notice index chain differs')


def read_assembled(root, expected_receipt_sha256, expected_source_manifest_sha256, *, expected_platform=PLATFORM):
    """Revalidate an assembled private kit against externally pinned receipts.

    The hashes authenticate byte identity for staging; only the native reader and
    real product runs can validate their own behavior or a distribution verdict.
    """
    require(all(isinstance(value, str) and re.fullmatch(r'[0-9a-f]{64}', value)
                for value in (expected_receipt_sha256, expected_source_manifest_sha256)),
            'Explicit SHA-256 pins for kit receipt and build sources are required')
    require(expected_platform in PLATFORM_BINARIES, 'Unsupported game kit platform')
    launcher_name, runtime_name = PLATFORM_BINARIES[expected_platform]
    root = Path(root)
    require(linux_build_evidence.physical(root), 'Assembled kit root must be physical')
    application_sources.ordinary(root)
    require(root.is_dir(), 'Assembled kit root must be a physical directory')
    require({entry.name for entry in root.iterdir()} ==
            {'game-kit-assembly-receipt.json', 'forge-kit', 'game-export-kit'},
            'Assembled kit root contains missing or unexpected entries')
    receipt_data = application_sources.read_regular(root / 'game-kit-assembly-receipt.json', MAX_RECEIPT,
                                                    'game kit assembly receipt')
    require(runtime_notices.sha(receipt_data) == expected_receipt_sha256, 'Game kit assembly receipt pin differs')
    receipt = runtime_notices.document(receipt_data, 'game kit assembly receipt')
    require(receipt.get('schema') == 'forge.delivery.private-game-kit-assembly.v1' and
            receipt.get('status') == 'PRIVATE_STAGED_ONLY' and receipt.get('platform') == expected_platform and
            receipt.get('source_revision') == expected_source_manifest_sha256 and
            receipt.get('source_manifest_sha256') == expected_source_manifest_sha256 and
            all(receipt.get(key) is False for key in ('runtime_verified', 'installation_verified', 'distribution_qualified')),
            'Game kit receipt is not a current private build-only candidate')
    require(all(isinstance(receipt.get(key), str) and re.fullmatch(r'[0-9a-f]{64}', receipt[key])
                for key in ('launcher_proof_sha256', 'runtime_proof_sha256', 'kit_manifest_sha256',
                            'launcher_sha256', 'runtime_sha256')), 'Game kit receipt pins are incomplete')
    manifest_dir = root / 'forge-kit'
    application_sources.ordinary(manifest_dir)
    require(manifest_dir.is_dir() and {entry.name for entry in manifest_dir.iterdir()} == {'game-export-v1.json'},
            'Game kit manifest directory differs')
    manifest_data = application_sources.read_regular(manifest_dir / 'game-export-v1.json', 8 * 1024 * 1024,
                                                     'game kit manifest')
    manifest_sha = runtime_notices.sha(manifest_data)
    require(manifest_sha == receipt['kit_manifest_sha256'], 'Game kit manifest differs from its receipt')
    manifest = runtime_notices.document(manifest_data, 'game kit manifest')
    expected_fields = {'schema', 'platform', 'version', 'source_revision', 'launcher_contract',
                       'launcher', 'module_runtime', 'rights', 'license_paths', 'files'}
    require(set(manifest) == expected_fields and manifest.get('schema') == 'forge.delivery.game-kit.v1' and
            manifest.get('platform') == expected_platform and manifest.get('source_revision') == expected_source_manifest_sha256 and
            manifest.get('launcher_contract') == 'forge.game-launcher.v2' and manifest.get('rights') == 'private_local_only' and
            manifest.get('launcher') == launcher_name and manifest.get('module_runtime') == runtime_name and
            isinstance(manifest.get('version'), str) and bool(manifest['version'].strip()) and
            len(manifest['version'].encode('utf-8')) <= 128, 'Game kit manifest is not a supported private candidate')
    kit_root = root / 'game-export-kit'
    application_sources.ordinary(kit_root)
    require(kit_root.is_dir(), 'Game kit content must be a physical directory')
    files = inventory(kit_root)
    require(files == manifest.get('files'), 'Game kit files differ from the manifest inventory')
    lookup = {entry['path']: entry for entry in files}
    licenses = sorted(name for name in lookup if name.startswith('licenses/'))
    required = {'licenses/THIRD_PARTY_NOTICES.md', *('licenses/rust-runtime-candidate/' + name
                for name in runtime_notices.INDEX_NAMES)}
    require(manifest.get('license_paths') == licenses and required.issubset(licenses), 'Game kit notices are incomplete')
    require(launcher_name in lookup and runtime_name in lookup and lookup[launcher_name]['sha256'] == receipt['launcher_sha256'] and
            lookup[runtime_name]['sha256'] == receipt['runtime_sha256'] and
            type(receipt.get('kit_file_count')) is int and receipt['kit_file_count'] == len(files) and
            type(receipt.get('kit_file_bytes')) is int and receipt['kit_file_bytes'] == sum(item['bytes'] for item in files),
            'Game kit binary or inventory receipt pins differ')
    verification = receipt.get('launcher_verification', {})
    require(isinstance(verification, dict) and verification.get('command') == '--verify-distributable' and
            verification.get('exit_code') == 0 and verification.get('marker') == STANDALONE and
            verification.get('executable_sha256') == receipt['launcher_sha256'],
            'Game kit launcher verification receipt differs')
    if expected_platform == PLATFORM:
        require_windows_x64_executable(kit_root / launcher_name)
        require_windows_x64_executable(kit_root / runtime_name)
    else:
        provenance=receipt.get('linux_provenance',{})
        require(isinstance(provenance,dict) and verification.get('source') == 'linux-sdk-observation' and
                verification.get('launcher_proof_sha256') == receipt['launcher_proof_sha256'] and
                provenance.get('core_proof_sha256') == receipt['runtime_proof_sha256'] and
                all(isinstance(provenance.get(name),str) and re.fullmatch(r'[0-9a-f]{64}',provenance[name])
                    for name in ('source_zip_sha256','started_sha256','capture_sha256','core_proof_sha256')) and
                provenance.get('cargo_home') == '/qualification/cargo-home' and
                isinstance(provenance.get('cargo_target'),str) and
                re.fullmatch(r'/qualification/isolated-targets/[a-z0-9-]{1,48}',provenance['cargo_target']),
                'Linux game kit build provenance differs')
        scope=verify_linux_launcher_notices(receipt.get('launcher_notices'))
        require(isinstance(receipt.get('runtime_notices'),dict) and
                receipt['runtime_notices'].get('launcher_standalone_scope') == scope,
                'Linux game kit launcher notice provenance differs')
        linux_build_evidence.linux_elf_x64(kit_root / launcher_name, require_executable=False)
        linux_build_evidence.linux_elf_x64(kit_root / runtime_name, require_executable=False)
    require(not contains_bytes(kit_root / launcher_name, AUBE), 'Reference Aube launcher refused')
    verify_staged_notices(kit_root, files)
    return VerifiedAssembledGameKit(root, kit_root, manifest, manifest_data, manifest_sha, receipt, expected_receipt_sha256)


def stage_studio_adjacent(assembled_root, studio_executable, studio_proof_path, source_manifest_path,
                          expected_assembly_receipt_sha256, expected_studio_proof_sha256, bin_directory, *, repo=None,
                          expected_platform=PLATFORM, linux_context=None):
    """Copy a verified kit beside a pinned Studio in an owned packaging directory."""
    import stage_studio_game_kit
    require(expected_platform in PLATFORM_BINARIES and
            ((expected_platform == PLATFORM and linux_context is None) or
             (expected_platform == LINUX_PLATFORM and isinstance(linux_context,dict))),
            'Studio packaging platform or Linux evidence differs')
    repo = (repo or SOURCE).resolve(strict=True)
    source_path = physical_file(source_manifest_path)
    studio_path = physical_file(studio_executable)
    proof_path = physical_file(studio_proof_path)
    require(isinstance(expected_studio_proof_sha256, str) and
            re.fullmatch(r'[0-9a-f]{64}', expected_studio_proof_sha256), 'Explicit Studio proof SHA-256 is required')
    assembled_root = Path(assembled_root)
    bin_directory = Path(bin_directory)
    application_sources.ordinary(bin_directory)
    require(bin_directory.is_absolute() and bin_directory.name.casefold() == 'bin' and bin_directory.is_dir(),
            'Studio packaging destination must be an existing physical bin directory')
    resolved_bin = bin_directory.resolve(strict=True)
    resolved_kit = assembled_root.resolve(strict=True)
    require(not resolved_bin.is_relative_to(repo) and not resolved_bin.is_relative_to(resolved_kit) and
            not resolved_kit.is_relative_to(resolved_bin), 'Studio packaging and source kit must be separate')
    destination = bin_directory / 'game-export-kit'
    require(not destination.exists() and not destination.is_symlink(), 'Adjacent game kit already exists')
    staged_studio = physical_file(bin_directory / ('forge-studio' if expected_platform == LINUX_PLATFORM else 'forge-studio.exe'))

    def verify_inputs():
        require(build_delivery.digest(proof_path) == expected_studio_proof_sha256, 'Studio build proof pin differs')
        source_hash = build_delivery.digest(source_path)
        source = read_document(source_path)
        if expected_platform == LINUX_PLATFORM:
            expected_keys={'source_zip','started_path','capture_path','started_sha256','capture_sha256'}
            require(set(linux_context) == expected_keys and sys.platform == 'linux' and
                    source.get('schema') == 'forge.gate7.vm-source-snapshot.v1' and
                    source_path == proof_path.parent/'source-manifest.json' and
                    studio_path == proof_path.parent/'forge-studio',
                    'Linux Studio packaging requires the pinned SDK snapshot and result directory')
            for name, loaded in (('scripts/delivery/game_kit.py',Path(__file__)),
                                 ('scripts/delivery/build_delivery.py',Path(build_delivery.__file__)),
                                 ('scripts/delivery/linux_build_evidence.py',Path(linux_build_evidence.__file__))):
                pin=source.get('files',{}).get(name,{})
                require(linux_build_evidence.physical(loaded) and loaded.stat().st_size == pin.get('bytes') and
                        build_delivery.digest(loaded) == pin.get('sha256'), f'Running Linux Studio reader differs: {name}')
            reader=runtime_notices._snapshot_module(repo,source,'scripts/gate6/capture_inputs.py',
                                                     'forge_linux_studio_snapshot_capture')
            runtime_notices._verify_extracted_snapshot(repo,source,reader)
            observed=linux_build_evidence.verify_linux_studio(proof_path.parent,linux_context['source_zip'],
                linux_context['started_path'],linux_context['capture_path'],
                expected_result_sha256=expected_studio_proof_sha256,
                expected_started_sha256=linux_context['started_sha256'],
                expected_capture_sha256=linux_context['capture_sha256'],
                expected_kit_receipt_sha256=expected_assembly_receipt_sha256)
            require(observed['source_manifest_sha256'] == source_hash and
                    build_delivery.digest(studio_path) == observed['studio_sha256'] and
                    build_delivery.digest(staged_studio) == observed['studio_sha256'],
                    'Packaged Linux Studio differs from its build proof')
            verified=read_assembled(assembled_root,expected_assembly_receipt_sha256,source_hash,
                                    expected_platform=LINUX_PLATFORM)
            frontend=repo/'apps/forge_studio/dist'
            stage_studio_game_kit.verify_staged(assembled_root,frontend,expected_assembly_receipt_sha256,
                                                 source_hash,expected_platform=LINUX_PLATFORM)
            require(verified.manifest_sha256 == observed['kit_manifest_sha256'] and
                    build_delivery.digest(frontend/'forge-game-export-v1.json') == observed['kit_manifest_sha256'] and
                    build_delivery.digest(frontend/'forge-game-export-staging-receipt.json') ==
                    observed['overlay_stage_sha256'], 'Linux Studio embedded kit differs from its build proof')
            return source_hash, {'executable_sha256':observed['studio_sha256'], 'linux_evidence':observed}, verified
        require(source.get('schema') == 'forge.delivery.shared-source-inputs.v1' and
                Path(source.get('source_root', '')).resolve() == repo and
                complete_rebuild.source_inputs_equal(complete_rebuild.shared_source_inputs(repo), source),
                'Studio build sources are stale')
        proof = read_document(proof_path)
        build_delivery.validate_shared_native_application(repo, studio_path, proof, source_hash)
        orchestrator = source.get('files', {}).get('scripts/delivery/complete_rebuild.py', {}).get('sha256')
        require(proof.get('application_role', 'studio') == 'studio' and proof.get('profile') == 'release' and
                proof.get('authoring') is True and isinstance(orchestrator, str) and
                proof.get('orchestrator_sha256') == orchestrator, 'A current Studio authoring build proof is required')
        require_windows_x64_executable(studio_path)
        require_windows_x64_executable(staged_studio)
        require(build_delivery.digest(staged_studio) == proof['executable_sha256'],
                'Packaged Studio differs from its build proof')
        verified = read_assembled(assembled_root, expected_assembly_receipt_sha256, source_hash)
        kit_proof = proof.get('game_kit')
        require(isinstance(kit_proof, dict) and Path(kit_proof.get('assembly_root', '')).resolve() == resolved_kit and
                kit_proof.get('assembly_receipt_sha256') == expected_assembly_receipt_sha256 and
                kit_proof.get('source_manifest_sha256') == source_hash and
                kit_proof.get('manifest_sha256') == verified.manifest_sha256,
                'Studio was not built with this verified game kit')
        frontend_value = proof.get('frontend')
        require(isinstance(frontend_value, str) and frontend_value, 'Studio frontend build input is missing')
        frontend = Path(frontend_value)
        require(frontend.resolve() == (repo / 'apps/forge_studio/dist').resolve(),
                'Studio frontend differs from the canonical build input')
        staged = stage_studio_game_kit.verify_staged(assembled_root, frontend,
                                                     expected_assembly_receipt_sha256, source_hash)
        overlay = {'forge-game-export-v1.json': kit_proof['manifest_sha256'],
                   'forge-game-export-staging-receipt.json': kit_proof.get('stage_receipt_sha256')}
        pins = proof.get('frontend_pins')
        require(isinstance(pins, list) and kit_proof.get('stage_receipt') == staged and
                all(sum(1 for item in pins if isinstance(item, dict) and item.get('path') == name and
                        item.get('sha256') == digest) == 1 for name, digest in overlay.items()) and
                build_delivery.digest(frontend/'forge-game-export-v1.json') == overlay['forge-game-export-v1.json'] and
                build_delivery.digest(frontend/'forge-game-export-staging-receipt.json') ==
                overlay['forge-game-export-staging-receipt.json'], 'Studio embedded game kit differs from its build proof')
        return source_hash, proof, verified

    before = verify_inputs()
    parent = bin_directory.resolve(strict=True)
    staging = Path(tempfile.mkdtemp(prefix='.forge-game-kit-', dir=parent))
    try:
        for entry in before[2].manifest['files']:
            relative = application_sources.relative(entry['path'])
            origin = physical_file(before[2].kit_root / relative)
            target = staging / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            with origin.open('rb') as source_stream, target.open('xb') as target_stream:
                shutil.copyfileobj(source_stream, target_stream, 1024 * 1024)
            require(target.stat().st_size == entry['bytes'] and build_delivery.digest(target) == entry['sha256'],
                    f'Adjacent game kit file differs: {relative}')
        require(inventory(staging) == before[2].manifest['files'] and verify_inputs() == before,
                'Studio or game kit changed during adjacent staging')
        require(not destination.exists() and not destination.is_symlink(), 'Adjacent game kit destination appeared')
        os.rename(staging, destination)
        require(inventory(destination) == before[2].manifest['files'], 'Adjacent game kit changed after staging')
        return {'schema':'forge.delivery.studio-adjacent-kit.v1', 'status':'PRIVATE_STAGED_ONLY',
                'source_manifest_sha256':before[0], 'studio_sha256':before[1]['executable_sha256'],
                'studio_proof_sha256':expected_studio_proof_sha256,
                'kit_assembly_receipt_sha256':expected_assembly_receipt_sha256,
                'kit_manifest_sha256':before[2].manifest_sha256,
                'kit_file_count':len(before[2].manifest['files']),
                'kit_file_bytes':sum(item['bytes'] for item in before[2].manifest['files']),
                'runtime_verified':False, 'installation_verified':False, 'distribution_qualified':False}
    except Exception as error:
        if staging.exists():
            try:
                cleanup_owned(staging, parent)
            except Exception as cleanup_error:
                raise RuntimeError(f'{error}; temporary kit preserved at {staging}: {cleanup_error}') from error
        raise


def cleanup_owned(staging, parent):
    require(staging.parent.resolve() == parent.resolve() and staging.name.startswith('.forge-game-kit-') and
            staging.is_dir() and not staging.is_symlink(), 'Unsafe temporary kit location')
    for folder, dirs, files in os.walk(staging, followlinks=False):
        for name in dirs + files:
            path = Path(folder) / name
            require(not path.is_symlink() and not (getattr(path.lstat(), 'st_file_attributes', 0) & 0x400),
                    'Unsafe entry in temporary kit; preserved for inspection')
    shutil.rmtree(staging)


def assemble(args, *, repo=None, launcher_verifier=verify_launcher):
    require(args.platform in PLATFORM_BINARIES, 'Unsupported game kit platform')
    repo = Path(repo or SOURCE)
    if args.platform == LINUX_PLATFORM:
        require(linux_build_evidence.physical(repo), 'Linux snapshot root must be physical')
    repo = repo.resolve(strict=True)
    launcher_name, runtime_name = PLATFORM_BINARIES[args.platform]
    require(isinstance(args.version, str) and bool(args.version.strip()) and
            len(args.version.encode('utf-8')) <= 128, 'Game kit version is invalid')
    output = Path(args.output)
    require(output.is_absolute() and '..' not in output.parts and not output.exists() and not output.is_symlink(),
            'A fresh absolute output path is required')
    if args.platform == LINUX_PLATFORM:
        require(linux_build_evidence.physical(output.parent), 'Linux game kit output parent must be physical')
    application_sources.ordinary(output.parent)
    require(output.parent.is_dir() and not output.parent.resolve().is_relative_to(repo),
            'Game kit output must be outside the source checkout')
    if args.platform == LINUX_PLATFORM:
        require(all(linux_build_evidence.physical(Path(value)) for value in
                    (args.source_manifest,args.launcher,args.module_runtime,args.launcher_proof,args.module_runtime_proof)),
                'Linux core evidence paths must be physical')
    source_path = physical_file(args.source_manifest)
    launcher_path, runtime_path = physical_file(args.launcher), physical_file(args.module_runtime)
    launcher_proof_path, runtime_proof_path = physical_file(args.launcher_proof), physical_file(args.module_runtime_proof)
    source = read_document(source_path)
    launcher_proof, runtime_proof = read_document(launcher_proof_path), read_document(runtime_proof_path)
    pins = {'source_manifest_sha256': build_delivery.digest(source_path),
            'launcher_proof_sha256': build_delivery.digest(launcher_proof_path),
            'runtime_proof_sha256': build_delivery.digest(runtime_proof_path)}
    if args.platform == PLATFORM:
        manifest_hash = verify_builds(repo, source_path, source, launcher_path, launcher_proof, runtime_path, runtime_proof)
        verification = launcher_verifier(launcher_path)
        runtime_sha = runtime_proof['executable_sha256']
        linux_build = None
    else:
        linux_build = verify_linux_builds(repo, source_path, source, launcher_path, launcher_proof_path,
                                          runtime_path, runtime_proof_path, args)
        manifest_hash = linux_build['source_manifest_sha256']
        verification = linux_build['verification']
        runtime_sha = linux_build['runtime_sha256']
    require(verification.get('executable_sha256') == build_delivery.digest(launcher_path) and
            verification.get('command') == '--verify-distributable' and
            verification.get('exit_code') == 0 and verification.get('marker') == STANDALONE,
            'Launcher verification does not match the pinned executable')
    require(not contains_bytes(launcher_path, AUBE), 'Reference Aube marker is present in the generic launcher')
    if args.platform == PLATFORM:
        args.shared_working_tree = True
        sources = application_sources.prepare_for_assembly(args, repo)
        notices = runtime_notices.prepare_for_assembly(args, repo, source)
        launcher_notices = verify_launcher_notice_scope(args, repo, notices)
    else:
        notices = runtime_notices.prepare_for_linux_snapshot(args, repo, args.linux_source_zip, source_path,
                    linux_build['source_zip_sha256'], manifest_hash, args.linux_capture_receipt, include_launcher=True)
        sources = application_sources.prepare_sources(repo, args.cargo_cache,
                    inventory_path=repo/'scripts/delivery/application_sources.json')
        launcher_notices = verify_linux_launcher_notices(notices.receipt.get('launcher_standalone_scope'))
    parent = output.parent.resolve(strict=True)
    staging = Path(tempfile.mkdtemp(prefix='.forge-game-kit-', dir=parent))
    require(staging.parent.resolve() == parent and not staging.is_symlink(), 'Unsafe temporary kit')
    try:
        kit = staging / 'game-export-kit'
        kit.mkdir()
        build_delivery.copy(launcher_path, kit / launcher_name)
        build_delivery.copy(runtime_path, kit / runtime_name)
        if args.platform == LINUX_PLATFORM:
            os.chmod(kit / launcher_name, 0o755)
            os.chmod(kit / runtime_name, 0o755)
        source_receipt = application_sources.stage_sources(sources, kit)
        notice_receipt = runtime_notices.stage(notices, kit)
        require(notice_receipt['project_notice_path'] == 'licenses/THIRD_PARTY_NOTICES.md',
                'Project notices are not at the required game kit path')
        files = inventory(kit)
        lookup = {entry['path']: entry for entry in files}
        require(lookup[launcher_name]['sha256'] == verification['executable_sha256'] and
                lookup[runtime_name]['sha256'] == runtime_sha, 'Staged binary differs')
        licenses = sorted(name for name in lookup if name.startswith('licenses/'))
        required = {'licenses/THIRD_PARTY_NOTICES.md', *('licenses/rust-runtime-candidate/'+name
                    for name in runtime_notices.INDEX_NAMES)}
        require(required.issubset(licenses), 'Required notices are missing')
        manifest = {'schema':'forge.delivery.game-kit.v1','platform':args.platform,'version':args.version,
                    'source_revision':manifest_hash,'launcher_contract':'forge.game-launcher.v2',
                    'launcher':launcher_name,'module_runtime':runtime_name,'rights':'private_local_only',
                    'license_paths':licenses,'files':files}
        encoded = (json.dumps(manifest, ensure_ascii=False, sort_keys=True, indent=2)+'\n').encode('utf-8')
        require(len(encoded) <= 8 * 1024 * 1024, 'Game kit manifest exceeds Studio budget')
        manifest_path = staging / 'forge-kit/game-export-v1.json'
        manifest_path.parent.mkdir()
        with manifest_path.open('xb') as stream: stream.write(encoded)
        require(inventory(kit) == files, 'Staged kit changed while writing its manifest')
        require(build_delivery.digest(source_path) == pins['source_manifest_sha256'] and
                build_delivery.digest(launcher_proof_path) == pins['launcher_proof_sha256'] and
                build_delivery.digest(runtime_proof_path) == pins['runtime_proof_sha256'],
                'Build receipt changed during staging')
        if args.platform == PLATFORM:
            require(verify_builds(repo, source_path, source, launcher_path, launcher_proof, runtime_path, runtime_proof) == manifest_hash,
                    'Sources or executables changed during staging')
            require(application_sources.prepare_for_assembly(args, repo) == sources and
                    runtime_notices.prepare_for_assembly(args, repo, source) == notices and
                    verify_launcher_notice_scope(args, repo, notices) == launcher_notices,
                    'Notices or source closure changed during staging')
        else:
            require(verify_linux_builds(repo, source_path, source, launcher_path, launcher_proof_path,
                                       runtime_path, runtime_proof_path, args) == linux_build and
                    runtime_notices.prepare_for_linux_snapshot(args, repo, args.linux_source_zip, source_path,
                        linux_build['source_zip_sha256'], manifest_hash, args.linux_capture_receipt,
                        include_launcher=True) == notices and
                    application_sources.prepare_sources(repo,args.cargo_cache,
                        inventory_path=repo/'scripts/delivery/application_sources.json') == sources,
                    'Linux sources, executables or notice closure changed during staging')
        proof = {'schema':'forge.delivery.private-game-kit-assembly.v1','utc':datetime.now(timezone.utc).isoformat(),
                 'status':'PRIVATE_STAGED_ONLY','platform':args.platform,'source_revision':manifest_hash,
                 **pins,'launcher_sha256':verification['executable_sha256'],
                 'runtime_sha256':runtime_sha,
                 'kit_manifest_sha256':runtime_notices.sha(encoded),'launcher_verification':verification,
                 'launcher_notices':launcher_notices,'runtime_notices':notice_receipt,'application_sources':source_receipt,
                 'kit_file_count':len(files),'kit_file_bytes':sum(entry['bytes'] for entry in files),
                 'runtime_verified':False,'installation_verified':False,'distribution_qualified':False}
        if linux_build is not None:
            proof['linux_provenance'] = {key:linux_build[key] for key in
                ('source_zip_sha256','started_sha256','capture_sha256','core_proof_sha256','cargo_home','cargo_target')}
        with (staging/'game-kit-assembly-receipt.json').open('x',encoding='utf-8',newline='\n') as stream:
            json.dump(proof,stream,ensure_ascii=False,indent=2);stream.write('\n')
        if args.platform == LINUX_PLATFORM:
            read_assembled(staging, build_delivery.digest(staging/'game-kit-assembly-receipt.json'),
                           manifest_hash, expected_platform=LINUX_PLATFORM)
        require(not output.exists() and not output.is_symlink(), 'Game kit destination appeared during staging')
        os.rename(staging, output)
        return proof
    except Exception as error:
        if staging.exists():
            try:
                cleanup_owned(staging, parent)
            except Exception as cleanup_error:
                raise RuntimeError(f'{error}; temporary kit preserved at {staging}: {cleanup_error}') from error
        raise


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('source-manifest','launcher','launcher-proof','module-runtime','module-runtime-proof',
                 'runtime-notice-bundle','runtime-notice-receipt','runtime-notice-graph','cargo-cache','output'):
        parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--version',required=True)
    parser.add_argument('--platform',choices=tuple(PLATFORM_BINARIES),default=PLATFORM)
    for name in ('linux-source-zip','linux-capture-receipt','linux-started'):
        parser.add_argument('--'+name,type=Path)
    for name in ('linux-capture-sha256','linux-started-sha256','linux-core-proof-sha256'):
        parser.add_argument('--'+name)
    parser.add_argument('--cargo-target',type=Path)
    args=parser.parse_args()
    require((args.platform==PLATFORM and os.name=='nt') or
            (args.platform==LINUX_PLATFORM and sys.platform=='linux'),
            'Game kit assembly requires its matching native platform')
    if args.platform == PLATFORM:
        require(not any(getattr(args,name) for name in ('linux_source_zip','linux_capture_receipt','linux_started',
                    'linux_capture_sha256','linux_started_sha256','linux_core_proof_sha256','cargo_target')),
                'Linux evidence options require linux-x64')
    print(json.dumps(assemble(args),ensure_ascii=False))


if __name__=='__main__':
    main()
