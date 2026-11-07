"""Portable, read-only checks for a pinned Gate 7 Linux build; no installation verdict."""
import ast
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import stat
import zipfile

LAUNCHER_SOURCE_INPUTS = (
    'Cargo.lock', 'rust-toolchain.toml', 'apps/forge_launcher/Cargo.toml',
    'apps/forge_launcher/Cargo.lock', 'apps/forge_launcher/src/main.rs',
    'apps/forge_launcher/scripts/build-aube.mjs', 'apps/forge_launcher/scripts/desktop.js',
    'apps/forge_launcher/canonical-ui/index.html', 'apps/forge_launcher/canonical-ui/styles.css',
    'scripts/delivery/game_kit.py', 'scripts/delivery/complete_rebuild.py',
)
# Preserve the original v1 list for historical launcher receipts. New builds
# explicitly bind the installed-game bootstrap in the v2 source inventory.
LAUNCHER_SOURCE_INPUTS_V2 = (*LAUNCHER_SOURCE_INPUTS, 'apps/forge_launcher/src/installer.rs')
STANDALONE_MARKER = 'FORGE_LAUNCHER_STANDALONE_FRONTEND_READY'
AUBE_MARKER = b'FORGE_LAUNCHER_AUBE_FRONTEND_READY'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read_json(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def require(condition, message):
    if not condition:
        raise ValueError(message)


def safe_name(name):
    require(isinstance(name, str) and name and '\\' not in name and ':' not in name,
            f'Unsafe relative path: {name!r}')
    path = PurePosixPath(name)
    require(not path.is_absolute() and all(part not in ('', '.', '..') for part in name.split('/')),
            f'Unsafe relative path: {name!r}')
    return name


def physical_file(path):
    try:
        if not physical(path):
            return False
        metadata = Path(path).lstat()
        return stat.S_ISREG(metadata.st_mode) and not (getattr(metadata, 'st_file_attributes', 0) & 0x400)
    except FileNotFoundError:
        return False


def launcher_contract_constants(source):
    wanted = {'STANDALONE', 'AUBE', 'STANDALONE_TAURI_CONFIG'}
    constants = {}
    for node in ast.parse(source).body:
        if isinstance(node, ast.Assign) and len(node.targets) == 1 and isinstance(node.targets[0], ast.Name):
            name = node.targets[0].id
            if name in wanted:
                require(name not in constants, f'Duplicate launcher contract constant: {name}')
                constants[name] = ast.literal_eval(node.value)
    require(set(constants) == wanted and constants['STANDALONE'] == STANDALONE_MARKER and
            constants['AUBE'] == AUBE_MARKER, 'Shared standalone launcher markers differ')
    config = constants['STANDALONE_TAURI_CONFIG']
    require(isinstance(config, dict) and config.get('productName') == 'OpenRPG Forge' and
            isinstance(config.get('app', {}).get('windows'), list) and
            len(config['app']['windows']) == 1 and config['app']['windows'][0].get('label') == 'main' and
            config['app']['windows'][0].get('title') == 'Jeu', 'Shared standalone Tauri config differs')
    return config


def linux_elf_x64(path, *, require_executable=True):
    require(physical_file(path), f'Physical Linux launcher required: {path}')
    with path.open('rb') as stream:
        header = stream.read(64)
    require(len(header) == 64 and header[:4] == b'\x7fELF' and header[4:7] == b'\x02\x01\x01' and
            int.from_bytes(header[16:18], 'little') in (2, 3) and
            int.from_bytes(header[18:20], 'little') == 62 and
            (not require_executable or os.access(path, os.X_OK)),
            'Executable Linux x86-64 ELF launcher required')


def contains_bytes(path, needle):
    carry = b''
    with path.open('rb') as stream:
        while block := stream.read(1024 * 1024):
            data = carry + block
            if needle in data:
                return True
            carry = data[-(len(needle)-1):]
    return False


def physical(path):
    try:
        for item in reversed((Path(path), *Path(path).parents)):
            metadata = item.lstat()
            if stat.S_ISLNK(metadata.st_mode) or getattr(metadata, 'st_file_attributes', 0) & 0x400:
                return False
        return True
    except OSError:
        return False


def verify_linux_launcher(result, started, path, source_manifest_sha, toolchain_bytes, contract_bytes):
    record = result.get('standalone_launcher', {})
    proof_path = path/'launcher-build-proof.json'
    if (result.get('standalone_launcher_requested') is not True or not physical(proof_path) or
            not proof_path.is_file() or sha(proof_path) != record.get('proof_sha256')):
        raise ValueError('Standalone Linux launcher proof differs')
    proof = read_json(proof_path)
    source_path = path/'source-manifest.json'
    if not physical(source_path) or not source_path.is_file() or sha(source_path) != source_manifest_sha:
        raise ValueError('Standalone launcher source manifest differs')
    source_manifest = read_json(source_path)
    schema = proof.get('schema')
    if schema == 'forge.gate7.linux-standalone-launcher-build.v1':
        input_names = LAUNCHER_SOURCE_INPUTS
    elif schema == 'forge.gate7.linux-standalone-launcher-build.v2':
        input_names = LAUNCHER_SOURCE_INPUTS_V2
    else:
        raise ValueError('Unsupported standalone launcher proof schema')
    files = source_manifest.get('files')
    if not isinstance(files, dict) or any(name not in files for name in input_names):
        raise ValueError('Standalone launcher source manifest is incomplete')
    expected_inputs = {name:files[name] for name in input_names}
    for name, data in (('rust-toolchain.toml',toolchain_bytes), ('scripts/delivery/game_kit.py',contract_bytes)):
        if (not isinstance(data, bytes) or len(data) != expected_inputs[name]['bytes'] or
                hashlib.sha256(data).hexdigest() != expected_inputs[name]['sha256']):
            raise ValueError('Standalone launcher contract source differs')
    contract_config = launcher_contract_constants(contract_bytes.decode('utf-8'))
    expected_target = started['cargo_target']
    expected_frontend = f"/qualification/tmp/standalone-launcher-{started['run_id']}"
    if (proof.get('status') != 'BUILD_ONLY' or proof.get('run_id') != started['run_id'] or
            proof.get('platform') != 'linux-x64' or proof.get('target_triple') != 'x86_64-unknown-linux-gnu' or
            proof.get('source_sha256') != started['source_sha256'] or
            proof.get('source_manifest_sha256') != source_manifest_sha or
            proof.get('runner_sha256') != started['runner_sha256'] or proof.get('source_inputs') != expected_inputs or
            proof.get('source_unchanged') is not True or proof.get('source_transport_unchanged') is not True or
            proof.get('sdk_image_sha256') != started.get('image') or proof.get('cargo_home') != started.get('cargo_home') or
            proof.get('cargo_target') != expected_target or
            proof.get('isolated_owner_sha256') != started.get('isolated_owner_sha256') or
            proof.get('cargo_build_jobs') != str(started.get('cargo_jobs')) or proof.get('network') != 'none' or
            proof.get('frontend_profile') != 'standalone' or proof.get('frontend_path') != expected_frontend or
            proof.get('frontend_temporary_removed') is not True or proof.get('tools_unchanged') is not True or
            proof.get('aube_marker_absent') is not True or proof.get('elf_class') != 'x86_64' or
            proof.get('verification_marker') != STANDALONE_MARKER or
            any(proof.get(key) is not False for key in ('runtime_verified', 'installation_verified', 'distribution_qualified'))):
        raise ValueError('Standalone Linux launcher identity or scope differs')
    frontend = proof.get('frontend_files', {})
    if not isinstance(frontend, dict) or set(frontend) != {'index.html', 'styles.css', 'desktop.js'}:
        raise ValueError('Standalone launcher frontend inventory differs')
    pins = []
    for name, pin in sorted(frontend.items()):
        file = path/('launcher-frontend-'+name)
        if (not isinstance(pin, dict) or not physical(file) or not file.is_file() or
                file.stat().st_size != pin.get('bytes') or sha(file) != pin.get('sha256')):
            raise ValueError('Standalone launcher frontend evidence differs')
        pins.append({'path':name, **pin})
    state = hashlib.sha256(json.dumps(pins, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
    tauri = proof.get('tauri_config', {})
    tauri_bytes = json.dumps(tauri, ensure_ascii=False, separators=(',', ':')).encode()
    windows = tauri.get('app', {}).get('windows', []) if isinstance(tauri, dict) else []
    if (proof.get('frontend_state_sha256') != state or proof.get('tauri_config_sha256') != hashlib.sha256(tauri_bytes).hexdigest() or
            tauri != {**contract_config,'build':{'frontendDist':expected_frontend}} or
            not isinstance(windows, list) or len(windows) != 1 or windows[0].get('label') != 'main' or windows[0].get('title') != 'Jeu'):
        raise ValueError('Standalone launcher frontend or Tauri config differs')
    import tomllib
    channel = tomllib.loads(toolchain_bytes.decode('utf-8')).get('toolchain', {}).get('channel')
    tools = proof.get('tools', {})
    if (not isinstance(channel, str) or not isinstance(tools, dict) or set(tools) != {'node','cargo','rustc'} or
            any(not isinstance(item, dict) or not str(item.get('path','')).startswith('/') or
                not re.fullmatch(r'[0-9a-f]{64}', item.get('sha256','')) or not item.get('version')
                for item in tools.values()) or
            any(not tools[name]['version'].startswith(f'{name} {channel} ') or
                not str(tools[name].get('toolchain_path','')).startswith('/') or
                not re.fullmatch(r'[0-9a-f]{64}', tools[name].get('toolchain_sha256',''))
                for name in ('cargo','rustc'))):
        raise ValueError('Standalone launcher build tool identities differ')
    launcher_root = f"/qualification/sources/{started['run_id']}/apps/forge_launcher"
    binary = path/'forge-launcher-prototype'
    commands = [
        ('frontend', ['node', f'{launcher_root}/scripts/build-aube.mjs'], 'launcher-frontend-build.log'),
        ('cargo', ['cargo','build','--manifest-path',f'{launcher_root}/Cargo.toml','--features','standalone',
                   '--release','--locked','--offline','-j',str(started['cargo_jobs'])], 'launcher-native-build.log'),
        ('verify', [f'{expected_target}/release/forge-launcher-prototype','--verify-distributable'],
         'launcher-distributable-check.log'),
    ]
    steps = proof.get('steps', [])
    if not isinstance(steps, list) or len(steps) != len(commands):
        raise ValueError('Standalone launcher build steps differ')
    for step, (name, command, log_name) in zip(steps, commands):
        file = path/log_name
        if (step.get('name') != name or step.get('command') != command or step.get('cwd') != launcher_root or
                step.get('log') != log_name or step.get('exit_code') != 0 or not physical(file) or
                not file.is_file() or sha(file) != step.get('sha256')):
            raise ValueError(f'Standalone launcher build step differs: {name}')
    markers = [line for line in (path/'launcher-frontend-build.log').read_text(encoding='utf-8').splitlines()
               if line.startswith('STANDALONE_FRONTEND_READY')]
    if markers != [f'STANDALONE_FRONTEND_READY output={expected_frontend}']:
        raise ValueError('Standalone launcher frontend builder marker differs')
    stderr = path/'launcher-distributable-stderr.log'
    if (steps[-1].get('stderr_log') != stderr.name or not physical(stderr) or not stderr.is_file() or
            sha(stderr) != steps[-1].get('stderr_sha256') or
            (path/'launcher-distributable-check.log').read_bytes() not in
            (STANDALONE_MARKER.encode(), (STANDALONE_MARKER+'\n').encode()) or
            not physical(binary) or not binary.is_file() or proof.get('binary_name') != binary.name or
            binary.stat().st_size != proof.get('binary_bytes') or sha(binary) != proof.get('binary_sha256') or
            proof.get('binary_sha256') != record.get('binary_sha256') or contains_bytes(binary, AUBE_MARKER)):
        raise ValueError('Standalone launcher executable or marker differs')
    linux_elf_x64(binary, require_executable=False)
    return proof


SOURCE_ROOTS = ('apps', 'crates', 'tools', 'third_party', 'examples', '.cargo', '.github', 'scripts', 'docs')
SOURCE_READER = 'scripts/delivery/linux_build_evidence.py'
MODULE_COMMAND = ['cargo', 'build', '--locked', '--offline', '--release', '-p', 'forge_runtime',
                  '--no-default-features', '--features', 'module-game', '--bin', 'forge-module']


def verify_snapshot(source_zip, expected_source_sha, expected_manifest_sha, manifest_path, capture_receipt=None):
    """Verify the ZIP identity and its manifest without extracting or executing it.

    The whole-ZIP SHA binds all member bytes. The producer's complete read and
    the Linux runner's checked extraction remain separate recorded evidence.
    """
    source_zip, manifest_path = Path(source_zip), Path(manifest_path)
    require(physical(source_zip) and physical_file(source_zip) and physical(manifest_path) and
            physical_file(manifest_path) and manifest_path.stat().st_size <= 64*1024*1024 and
            re.fullmatch(r'[0-9a-f]{64}', expected_source_sha or '') and
            re.fullmatch(r'[0-9a-f]{64}', expected_manifest_sha or '') and sha(source_zip) == expected_source_sha,
            'Linux source ZIP differs from its explicit pin')
    size = source_zip.stat().st_size
    with zipfile.ZipFile(source_zip) as archive:
        names = archive.namelist()
        require(len(names) == len(set(names)), 'Duplicate ZIP members are forbidden')
        meta = archive.getinfo('__forge_vm_source_manifest__.json')
        require(not meta.is_dir() and meta.file_size <= 64 * 1024 * 1024, 'Invalid source manifest member')
        raw = archive.read(meta)
        require(hashlib.sha256(raw).hexdigest() == expected_manifest_sha and
                sha(manifest_path) == expected_manifest_sha and manifest_path.read_bytes() == raw,
                'Linux source manifest differs from its ZIP')
        manifest = json.loads(raw)
        files = manifest.get('files', {})
        require(manifest.get('schema') == 'forge.gate7.vm-source-snapshot.v1' and
                tuple(manifest.get('roots', ())) == SOURCE_ROOTS and
                manifest.get('development_checkout') is False and isinstance(files, dict) and
                set(names) == {*files, '__forge_vm_source_manifest__.json'},
                'Linux source ZIP inventory differs')
        for name, pin in files.items():
            safe_name(name)
            member = archive.getinfo(name)
            require(isinstance(pin, dict) and type(pin.get('bytes')) is int and pin['bytes'] >= 0 and
                    isinstance(pin.get('sha256'), str) and re.fullmatch(r'[0-9a-f]{64}', pin['sha256']) and
                    not member.is_dir() and not stat.S_ISLNK(member.external_attr >> 16) and
                    member.file_size == pin['bytes'], f'Linux source ZIP member differs: {name}')
        values = {}
        for name in ('rust-toolchain.toml', 'scripts/delivery/game_kit.py', SOURCE_READER):
            require(name in files and files[name]['bytes'] <= 16 * 1024 * 1024,
                    f'Linux source contract missing or oversized: {name}')
            data = archive.read(name)
            require(len(data) == files[name]['bytes'] and hashlib.sha256(data).hexdigest() == files[name]['sha256'],
                    f'Linux source contract differs: {name}')
            values[name] = data
    require(source_zip.stat().st_size == size and sha(source_zip) == expected_source_sha,
            'Linux source ZIP changed during verification')
    if capture_receipt is not None:
        require(isinstance(capture_receipt, dict) and capture_receipt.get('full_read_verified') is True and
                capture_receipt.get('sha256') == expected_source_sha and capture_receipt.get('archive_bytes') == size and
                capture_receipt.get('files') == len(files), 'Linux source capture receipt differs')
    return manifest, values


def verify_linux_core(directory, source_zip, started, expected_proof_sha256, *, capture_receipt=None):
    """Read a pinned core build only; never infer an installation or gate verdict."""
    directory = Path(directory)
    require(physical(directory) and directory.is_dir() and isinstance(started, dict) and
            re.fullmatch(r'[a-z0-9-]{1,48}', started.get('run_id', '')) and
            re.fullmatch(r'sha256:[0-9a-f]{64}', started.get('image', '')) and
            started.get('network') == 'none' and started.get('cargo_target_mode') == 'isolated-final' and
            type(started.get('cargo_jobs')) is int and started['cargo_jobs'] in (1, 2, 3),
            'Linux core run identity differs')
    run, source_sha = started['run_id'], started.get('source_sha256')
    target = f'/qualification/isolated-targets/{run}'
    owner = {'schema':'forge.gate7.linux-isolated-target.v1', 'run_id':run,
             'source_sha256':source_sha, 'target':target, 'cargo_home':'/qualification/cargo-home'}
    owner_sha = hashlib.sha256((json.dumps(owner, sort_keys=True, separators=(',', ':'))+'\n').encode()).hexdigest()
    require(started.get('cargo_home') == owner['cargo_home'] and started.get('cargo_target') == target and
            started.get('isolated_owner_sha256') == owner_sha, 'Linux isolated target identity differs')
    proof_path = directory/'linux-core-artifacts.json'
    require(physical(proof_path) and physical_file(proof_path) and proof_path.stat().st_size <= 16*1024*1024 and
            re.fullmatch(r'[0-9a-f]{64}', expected_proof_sha256 or '') and sha(proof_path) == expected_proof_sha256,
            'Linux core artifacts proof differs')
    proof = read_json(proof_path)
    source_manifest_sha = started.get('source_manifest_sha256')
    manifest, source_contract = verify_snapshot(source_zip, source_sha, source_manifest_sha,
                                                 directory/'source-manifest.json', capture_receipt)
    require(proof.get('schema') == 'forge.gate7.linux-core-artifacts.v1' and proof.get('status') == 'BUILD_ONLY' and
            proof.get('run_id') == run and proof.get('platform') == 'linux-x64' and
            proof.get('source_sha256') == source_sha and proof.get('source_manifest_sha256') == source_manifest_sha and
            proof.get('runner_sha256') == started.get('runner_sha256') and
            re.fullmatch(r'[0-9a-f]{64}', proof.get('runner_sha256', '')) and
            proof.get('sdk_image_sha256') == started['image'] and proof.get('cargo_home') == owner['cargo_home'] and
            proof.get('cargo_target') == target and proof.get('isolated_owner_sha256') == owner_sha and
            proof.get('target_initially_empty') is True and proof.get('cargo_build_jobs') == str(started['cargo_jobs']) and
            proof.get('network') == 'none' and proof.get('source_unchanged') is True and
            proof.get('source_transport_unchanged') is True and
            proof.get('evidence_reader_sha256') == manifest['files'][SOURCE_READER]['sha256'] and
            all(proof.get(key) is False for key in ('runtime_verified', 'installation_verified',
                                                     'distribution_qualified', 'gate_closed')),
            'Linux core artifacts identity or scope differs')
    check = proof.get('runtime_check', {})
    log = directory/'09-module-runtime-build.log'
    require(isinstance(check, dict) and check.get('name') == 'module-runtime-build' and
            check.get('command') == MODULE_COMMAND and check.get('cwd') == '.' and
            check.get('exit_code') == 0 and check.get('log') == log.name and
            physical(log) and physical_file(log) and sha(log) == check.get('sha256'),
            'Linux module runtime build command or log differs')
    binaries = proof.get('binaries')
    require(isinstance(binaries, list) and [item.get('name') for item in binaries if isinstance(item, dict)] ==
            ['forge-module', 'forge-launcher-prototype'] and len(binaries) == 2,
            'Linux core binary inventory differs')
    for item in binaries:
        name = item['name']
        binary, ldd = directory/name, directory/(name+'.ldd.log')
        require(physical(binary) and physical_file(binary) and type(item.get('bytes')) is int and
                item['bytes'] > 0 and binary.stat().st_size == item['bytes'] and
                sha(binary) == item.get('sha256') and item.get('ldd_log') == ldd.name and
                item.get('ldd_exit_code') == 0 and physical(ldd) and physical_file(ldd) and
                ldd.stat().st_size <= 4*1024*1024 and sha(ldd) == item.get('ldd_sha256') and
                'not found' not in ldd.read_text(encoding='utf-8', errors='replace').lower(),
                f'Linux core executable or dependency log differs: {name}')
        linux_elf_x64(binary, require_executable=False)
    require(binaries[0]['sha256'] != binaries[1]['sha256'], 'Linux core executables must be distinct')
    launcher_record = {'standalone_launcher_requested':True, 'standalone_launcher':{
        'proof_sha256':proof.get('launcher_proof_sha256'), 'binary_sha256':binaries[1]['sha256']}}
    verify_linux_launcher(launcher_record, started, directory, source_manifest_sha,
                          source_contract['rust-toolchain.toml'], source_contract['scripts/delivery/game_kit.py'])
    require(sha(proof_path) == expected_proof_sha256 and
            all(physical(directory/item['name']) and sha(directory/item['name']) == item['sha256'] and
                sha(directory/item['ldd_log']) == item['ldd_sha256'] for item in binaries),
            'Linux core proof or executables changed during verification')
    return proof


def verify_linux_studio(directory, source_zip, started_path, capture_path, *, expected_result_sha256,
                        expected_started_sha256, expected_capture_sha256, expected_kit_receipt_sha256):
    """Verify the actual Linux run before assembling a private Studio package."""
    directory = Path(directory)
    started_path, capture_path = Path(started_path), Path(capture_path)
    result_path = directory/'receipt.json'
    for path, pin in ((result_path, expected_result_sha256), (started_path, expected_started_sha256),
                      (capture_path, expected_capture_sha256)):
        require(re.fullmatch(r'[0-9a-f]{64}', pin or '') and physical(path) and physical_file(path) and
                path.stat().st_size <= 64*1024*1024 and sha(path) == pin,
                'Linux Studio run, launch or capture receipt differs')
    require(re.fullmatch(r'[0-9a-f]{64}',expected_kit_receipt_sha256 or ''), 'Linux kit receipt pin required')
    started, capture, result = read_json(started_path), read_json(capture_path), read_json(result_path)
    require(started.get('schema') == 'forge.gate7.linux-editor-launcher-started.v1' and
            started.get('suite') == 'all' and started.get('deferred_frontend') is True and
            started.get('standalone_launcher_requested') is True and started.get('linux_game_kit_requested') is True and
            started.get('gate_closed') is False and started.get('launcher_qualified') is False and
            result.get('schema') == 'forge.gate7.linux-editor-launcher-build.v1' and
            result.get('suite') == 'all' and result.get('succeeded') is True and
            result.get('run_id') == started.get('run_id') and result.get('source_sha256') == started.get('source_sha256') and
            result.get('source_manifest_sha256') == started.get('source_manifest_sha256') and
            result.get('runner_sha256') == started.get('runner_sha256') and
            result.get('cargo_target') == started.get('cargo_target') and
            result.get('source_unchanged') is True and not result.get('changed_sources') and
            result.get('source_transport_unchanged') is True and result.get('frontend_unchanged') is True and
            result.get('linux_game_kit_requested') is True and result.get('gate_closed') is False,
            'Linux Studio run is incomplete or does not match its launch')
    core = verify_linux_core(directory, source_zip, started, result.get('linux_core_artifacts_sha256'),
                             capture_receipt=capture)
    checks=result.get('checks')
    expected_command=['cargo','build','--release','--locked','--offline','-p','forge_studio','--bin','forge-studio',
                      '--features','tauri/custom-protocol']
    matches=[check for check in checks if isinstance(check,dict) and check.get('name') == 'studio-build'] \
            if isinstance(checks,list) else []
    require(len(matches) == 1 and all(isinstance(check,dict) and check.get('exit_code') == 0 for check in checks),
            'Linux Studio checks are missing or incomplete')
    check=matches[0]
    log=directory/check.get('log','')
    require(check.get('command') == expected_command and check.get('cwd') == '.' and
            isinstance(check.get('log'),str) and re.fullmatch(r'[0-9]{2}-studio-build\.log',check['log']) and
            physical(log) and physical_file(log) and sha(log) == check.get('sha256'),
            'Linux Studio build command or log differs')
    binaries=result.get('binaries')
    expected_names=['forge-module','forge-studio','forge-launcher-prototype','forge_delivery']
    require(isinstance(binaries,list) and len(binaries) == len(expected_names) and
            [item.get('name') for item in binaries if isinstance(item,dict)] == expected_names,
            'Linux Studio binary inventory differs')
    lookup={item['name']:item for item in binaries}
    for item in core['binaries']:
        require(all(lookup[item['name']].get(key) == item[key] for key in ('sha256','bytes')),
                'Linux Studio core binary differs')
    for name in ('forge-studio','forge_delivery'):
        item=lookup[name]; binary=directory/name; ldd=directory/(name+'.ldd.log')
        require(physical(binary) and physical_file(binary) and type(item.get('bytes')) is int and item['bytes'] > 0 and
                binary.stat().st_size == item['bytes'] and sha(binary) == item.get('sha256') and
                physical(ldd) and physical_file(ldd) and ldd.stat().st_size <= 4*1024*1024 and
                'not found' not in ldd.read_text(encoding='utf-8',errors='replace').lower(),
                f'Linux Studio executable or SDK dependency differs: {name}')
        linux_elf_x64(binary,require_executable=False)
    require(sha(directory/'forge-studio.ldd.log') == lookup['forge-studio'].get('ldd_sha256'),
            'Linux Studio SDK dependency log differs')
    delivery=result.get('delivery_cli',{})
    delivery_command=['cargo','build','--locked','--offline','--release','--manifest-path',
                      'tools/forge_delivery/Cargo.toml','--bin','forge_delivery']
    delivery_log=directory/'delivery-release-build.log'
    require(delivery.get('status') == 'BUILD_ONLY' and delivery.get('command') == delivery_command and
            delivery.get('cwd') == '.' and delivery.get('exit_code') == 0 and
            delivery.get('log') == delivery_log.name and physical(delivery_log) and physical_file(delivery_log) and
            sha(delivery_log) == delivery.get('log_sha256') and
            delivery.get('binary_sha256') == lookup['forge_delivery']['sha256'] and
            delivery.get('binary_bytes') == lookup['forge_delivery']['bytes'] and
            delivery.get('ldd_log') == 'forge_delivery.ldd.log' and delivery.get('ldd_exit_code') == 0 and
            sha(directory/'forge_delivery.ldd.log') == delivery.get('ldd_sha256'),
            'Linux delivery service build proof differs')
    kit=result.get('linux_game_kit',{})
    assembly=directory/'linux-game-kit-assembly-receipt.json'
    embedded=directory/'linux-game-kit-overlay-manifest.json'
    stage=directory/'linux-game-kit-overlay-stage.json'
    require(kit.get('status') == 'PRIVATE_STAGED_ONLY' and
            kit.get('assembly_receipt_sha256') == expected_kit_receipt_sha256 and
            all(kit.get(key) is False for key in ('runtime_verified','installation_verified','distribution_qualified')) and
            all(physical(path) and physical_file(path) for path in (assembly,embedded,stage)) and
            sha(assembly) == expected_kit_receipt_sha256 and sha(embedded) == kit.get('manifest_sha256') and
            sha(stage) == kit.get('overlay_stage_sha256'), 'Linux Studio kit overlay proof differs')
    overlay=read_json(stage)
    require(overlay.get('schema') == 'forge.studio.game-kit-stage.v1' and
            overlay.get('kit_assembly_receipt_sha256') == expected_kit_receipt_sha256 and
            overlay.get('source_manifest_sha256') == started.get('source_manifest_sha256') and
            overlay.get('manifest_sha256') == kit.get('manifest_sha256') and overlay.get('build_only') is True and
            all(overlay.get(key) is False for key in ('runtime_verified','installation_verified','distribution_qualified')),
            'Linux Studio overlay receipt differs')
    for phase in ('before','after'):
        entry=kit.get(phase+'_studio_verification',{})
        phase_log=directory/('linux-game-kit-'+phase+'-studio.log')
        require(entry.get('exit_code') == 0 and physical(phase_log) and physical_file(phase_log) and
                sha(phase_log) == entry.get('log_sha256'), 'Linux Studio kit revalidation is incomplete')
    require(sha(result_path) == expected_result_sha256 and sha(started_path) == expected_started_sha256 and
            sha(capture_path) == expected_capture_sha256 and sha(assembly) == expected_kit_receipt_sha256,
            'Linux Studio evidence changed during verification')
    return {'source_manifest_sha256':started['source_manifest_sha256'], 'source_zip_sha256':started['source_sha256'],
            'studio_sha256':lookup['forge-studio']['sha256'], 'delivery_cli_sha256':lookup['forge_delivery']['sha256'],
            'kit_receipt_sha256':expected_kit_receipt_sha256, 'kit_manifest_sha256':kit['manifest_sha256'],
            'overlay_stage_sha256':kit['overlay_stage_sha256'], 'result_sha256':expected_result_sha256,
            'started_sha256':expected_started_sha256, 'capture_sha256':expected_capture_sha256,
            'cargo_target':started['cargo_target'], 'cargo_home':started['cargo_home']}
