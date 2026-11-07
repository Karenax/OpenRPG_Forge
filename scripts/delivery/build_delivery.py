"""Assemble a local preview from explicit, existing native inputs, never rebuild/qualify them.

The client needs only the native executables and detached .forgepack files.
Development Python is not shipped or needed on the client.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import ntpath
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile

from complete_rebuild import shared_source_inputs, source_inputs_equal
import application_sources
import runtime_notices


def digest(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def write(path, value):
    Path(path).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def regular(path):
    stat = path.lstat()
    if path.is_symlink() or getattr(stat, "st_file_attributes", 0) & 0x400:
        raise ValueError(f"Link/reparse point refused: {path}")


def copy(source, destination):
    source, destination = Path(source), Path(destination)
    regular(source)
    if source.is_file():
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, destination)
        if digest(source) != digest(destination):
            raise ValueError(f"Source changed while copying: {source}")
        return
    destination.mkdir(parents=True, exist_ok=True)
    for entry in sorted(source.iterdir()):
        # User saves and derived caches are not an authoring seed.
        if entry.name in {".game-saves", ".git", "target", "__pycache__"}:
            continue
        copy(entry, destination / entry.name)


def run(executable, *args):
    result = subprocess.run([str(executable), *map(str, args)], capture_output=True, text=True,
                            encoding="utf-8", check=False)
    if result.returncode:
        raise RuntimeError(result.stderr[-5000:])
    return json.loads(result.stdout)


def descriptor(id, label, kind, version, source_revision, product=None, *, platform='windows-x64'):
    if platform not in ('windows-x64','linux-x64'):
        raise ValueError('Unsupported application package platform')
    return dict(schema="forge.delivery.package.v1", id=id, label=label, version=version,
                kind=kind, platform=platform if kind == "applications" else "any",
                source_revision=source_revision, rights="private_local_only",
                license_paths=["PROVENANCE.md"], files=[], product=product)


def package(cli, staging, delivery, d):
    description = staging.parent / f"{d['id']}.descriptor.json"
    write(description, d)
    output = delivery / f"{d['id']}.forgepack"
    result = run(cli, "pack", staging, description, output)
    write(delivery / f"{d['id']}.manifest.json", result["descriptor"])
    return dict(file=output.name, id=d["id"], kind=d["kind"], version=d["version"],
                manifest_sha256=result["manifest_sha256"], archive_sha256=result["archive_sha256"],
                bytes=output.stat().st_size, rights=d["rights"], status="local_preview")


def verify_package_stream(path, expected_descriptor, manifest_sha256, archive_sha256):
    """Read every payload byte; the CLI's inspect operation checks only its header."""
    path = Path(path)
    application_sources.ordinary(path)
    if not path.is_file():
        raise ValueError('Package must be a physical file')
    with path.open('rb') as stream:
        if stream.read(8) != b'FORGEPK1':
            raise ValueError('Package signature differs')
        raw_size = stream.read(8)
        if len(raw_size) != 8:
            raise ValueError('Truncated package header')
        size = int.from_bytes(raw_size, 'little')
        if not 0 < size <= 64 * 1024 * 1024:
            raise ValueError('Package manifest size exceeds its limit')
        raw = stream.read(size)
        if len(raw) != size or hashlib.sha256(raw).hexdigest() != manifest_sha256:
            raise ValueError('Package manifest hash differs')
        descriptor = json.loads(raw)
        if descriptor != expected_descriptor:
            raise ValueError('Package manifest differs from the pinned descriptor')
        files = descriptor.get('files')
        if not isinstance(files, list) or not 0 < len(files) <= 250_000:
            raise ValueError('Invalid package inventory')
        previous = ''
        for entry in files:
            if not isinstance(entry, dict) or set(entry) != {'path', 'bytes', 'sha256'}:
                raise ValueError('Invalid package entry')
            name = application_sources.relative(entry['path'])
            length, pin = entry['bytes'], entry['sha256']
            if (name <= previous or type(length) is not int or not 0 <= length <= 16 * 1024**3 or
                    not isinstance(pin, str) or not re.fullmatch(r'[0-9a-f]{64}', pin)):
                raise ValueError('Invalid or unordered package entry')
            previous, remaining, hasher = name, length, hashlib.sha256()
            while remaining:
                block = stream.read(min(1024 * 1024, remaining))
                if not block:
                    raise ValueError(f'Truncated package file: {name}')
                hasher.update(block)
                remaining -= len(block)
            if hasher.hexdigest() != pin:
                raise ValueError(f'Package file hash differs: {name}')
        if stream.read(1):
            raise ValueError('Package contains trailing data')
    if digest(path) != archive_sha256:
        raise ValueError('Package archive hash differs')
    return descriptor


def verify_studio_notice_audit(repo, source, verified_kit, audit_path, audit_sha256):
    """Tie the existing Studio metadata audit to this checkout and staged notice index."""
    import game_kit
    if not isinstance(audit_sha256, str) or not re.fullmatch(r'[0-9a-f]{64}', audit_sha256):
        raise ValueError('Explicit Studio notice audit SHA-256 is required')
    audit_path = game_kit.physical_file(audit_path)
    if digest(audit_path) != audit_sha256:
        raise ValueError('Studio notice audit pin differs')
    audit = game_kit.read_document(audit_path)
    inputs = audit.get('inputs_sha256_before')
    if (audit.get('schema') != 'forge.gate7.studio-notice-scope-audit.v1' or
            audit.get('changed_inputs') != [] or not isinstance(inputs, dict) or
            inputs != audit.get('inputs_sha256_after') or
            audit.get('index_sha256_matches_recalculation') is not True or
            audit.get('distribution_qualified') is not False or audit.get('gate_closed') is not False):
        raise ValueError('Studio notice audit is incomplete or claims qualification')
    special = {'collector', 'index', 'recalculation_receipt', 'graph_receipt'}
    for name, pin in inputs.items():
        if name in special:
            continue
        relative = application_sources.relative(name)
        path = game_kit.physical_file(repo / relative)
        if digest(path) != pin or (name in source['files'] and source['files'][name]['sha256'] != pin):
            raise ValueError(f'Studio notice audit input differs: {name}')
    required = {'.cargo/config.toml', 'Cargo.toml', 'Cargo.lock', 'apps/forge_studio/Cargo.toml'}
    if not required.issubset(inputs) or inputs.get('collector') != source.get('files', {}).get(
            'scripts/tidewater/collect_rust_notices.py', {}).get('sha256'):
        raise ValueError('Studio notice audit is not linked to its collector and Cargo inputs')
    index_path = verified_kit.kit_root / 'licenses/rust-runtime-candidate/INDEX.json'
    if (digest(index_path) != inputs.get('index') or
            verified_kit.receipt.get('runtime_notices', {}).get('graph_receipt_sha256') != inputs.get('graph_receipt')):
        raise ValueError('Studio notice audit does not match the kit notice chain')
    index = game_kit.read_document(index_path)
    supplemental_indices = (
        game_kit.read_document(index_path.with_name('SUPPLEMENT-NOTICES.json')),
        game_kit.read_document(index_path.with_name('FORGE-ORIGINAL-NOTICES.json')),
    )
    covered_ids = {item['package_id'] for notice_index in (index, *supplemental_indices)
                   for item in notice_index.get('packages', []) if item.get('notices')}
    results = audit.get('results')
    if not isinstance(results, list) or len(results) != 2:
        raise ValueError('Studio native and Web notice scopes are required')
    unresolved = []
    for target, features, request_features, no_default in (
            ('x86_64-pc-windows-msvc', ['tauri/custom-protocol'], [], False),
            ('wasm32-unknown-unknown', ['web'], ['forge_studio/web'], True)):
        matches = [item for item in results if isinstance(item, dict) and item.get('target') == target]
        requests = [item for item in index.get('requests', []) if item.get('package') == 'forge_studio' and
                    item.get('target') == target and item.get('features') == request_features and
                    item.get('no_default_features') is no_default and item.get('include_root_dev') is False]
        if len(matches) != 1 or len(requests) != 1:
            raise ValueError('Studio notice scope is missing or ambiguous')
        result, request = matches[0], requests[0]
        ids = request.get('package_ids')
        if not isinstance(ids, list) or not ids or len(ids) != len(set(ids)):
            raise ValueError('Studio notice inventory IDs are invalid')
        ids_sha = hashlib.sha256(json.dumps(sorted(ids)).encode()).hexdigest()
        missing_notices = sorted(set(ids) - covered_ids)
        if (Path(request.get('manifest', '')).resolve() != repo / 'Cargo.toml' or
                Path(result.get('manifest', '')).resolve() != repo / 'apps/forge_studio/Cargo.toml' or
                result.get('features') != features or result.get('index_request_features') != request_features or
                result.get('no_default_features') is not no_default or result.get('include_root_dev') is not False or
                result.get('ids_equal') is not True or result.get('resolved_only') != [] or
                result.get('index_only') != [] or result.get('resolved_ids_missing_from_bundle') != [] or
                result.get('resolved_package_count') != len(ids) or result.get('index_package_count') != len(ids) or
                result.get('resolved_ids_sha256') != ids_sha or result.get('index_ids_sha256') != ids_sha or
                result.get('unresolved_identity_cases') != missing_notices):
            raise ValueError('Studio notice scope does not match the pinned index')
        unresolved.extend(missing_notices)
    return {'audit_sha256': audit_sha256, 'index_sha256': inputs['index'],
            'unresolved_identity_cases': sorted(unresolved), 'asset_rights_qualified': False,
            'distribution_qualified': False}


def verify_linux_studio_notice_audit(repo, source, verified_kit, audit_path, audit_sha256, graph_path, observed):
    """Bind the existing notice audit to this ZIP, then replay both native Linux closures offline."""
    import game_kit
    if not isinstance(audit_sha256,str) or not re.fullmatch(r'[0-9a-f]{64}',audit_sha256):
        raise ValueError('Explicit Linux Studio notice audit SHA-256 is required')
    audit_path=game_kit.physical_file(audit_path)
    if digest(audit_path) != audit_sha256:
        raise ValueError('Linux Studio notice audit pin differs')
    audit=game_kit.read_document(audit_path)
    pins=audit.get('cargo_input_sha256_before')
    notices=verified_kit.receipt.get('runtime_notices',{})
    index_path=verified_kit.kit_root/'licenses/rust-runtime-candidate/INDEX.json'
    graph_path=game_kit.physical_file(graph_path)
    if (audit.get('schema') != 'forge.gate7.linux-studio-cli-notice-preflight.v1' or
            audit.get('passed') is not True or audit.get('distribution_qualified') is not False or
            audit.get('gate_closed') is not False or audit.get('linux_snapshot_or_cargo_replayed') is not False or
            not isinstance(pins,dict) or not pins or pins != audit.get('cargo_input_sha256_after') or
            audit.get('candidate_sha256') != notices.get('candidate_receipt_sha256') or
            audit.get('graph_sha256') != notices.get('graph_receipt_sha256') or
            audit.get('graph_sha256') != digest(graph_path) or audit.get('index_sha256') != digest(index_path) or
            audit.get('runtime_notices_sha256') != source.get('files',{}).get('scripts/delivery/runtime_notices.py',{}).get('sha256')):
        raise ValueError('Linux Studio notice audit does not match the pinned candidate')
    for name, pin in pins.items():
        application_sources.relative(name)
        if (source.get('files',{}).get(name,{}).get('sha256') != pin or
                runtime_notices.sha(runtime_notices._snapshot_file(repo,source,name)) != pin):
            raise ValueError(f'Linux Studio notice source pin differs: {name}')
    index=game_kit.read_document(index_path); graph=game_kit.read_document(graph_path)
    scopes=audit.get('scopes')
    if not isinstance(scopes,dict):
        raise ValueError('Linux Studio notice scopes are missing')
    original=source.get('repository')
    windows_origin=isinstance(original,str) and bool(ntpath.splitdrive(original)[0])
    if not isinstance(original,str) or not original:
        raise ValueError('Linux snapshot original source root is missing')
    specifications=(
        ('studio-custom-protocol','forge_studio','x86_64-unknown-linux-gnu',['tauri/custom-protocol'],False,'Cargo.toml'),
        ('delivery-cli','forge_delivery','x86_64-unknown-linux-gnu',[],False,'tools/forge_delivery/Cargo.toml'),
        ('studio-wasm','forge_studio','wasm32-unknown-unknown',['forge_studio/web'],True,'Cargo.toml'),
        ('launcher-standalone','forge_launcher_prototype','x86_64-unknown-linux-gnu',['standalone'],False,
         'apps/forge_launcher/Cargo.toml'))
    for role,package,target,features,no_default,cargo_manifest in specifications:
        scope=scopes.get(role,{})
        expected_manifest=(ntpath.join(original,*cargo_manifest.split('/')) if windows_origin else
                           str(Path(original)/cargo_manifest))
        actual_manifest=scope.get('manifest')
        same_manifest=(isinstance(actual_manifest,str) and
            (ntpath.normcase(ntpath.normpath(actual_manifest)) == ntpath.normcase(ntpath.normpath(expected_manifest))
             if windows_origin else actual_manifest == expected_manifest))
        ids=scope.get('package_ids')
        matches=[request for request in index.get('requests',[]) if isinstance(request,dict) and
                 request.get('package') == package and request.get('target') == target and
                 request.get('features') == features and request.get('no_default_features') is no_default and
                 request.get('include_root_dev') is False]
        command=['cargo','metadata','--manifest-path',actual_manifest,'--format-version','1','--locked','--offline',
                 '--filter-platform',target]
        if features: command += ['--features',','.join(features)]
        if no_default: command.append('--no-default-features')
        if (not same_manifest or scope.get('package') != package or scope.get('target') != target or
                scope.get('features') != features or scope.get('no_default_features') is not no_default or
                scope.get('metadata_command') != command or not isinstance(ids,list) or not ids or
                len(ids) != len(set(ids)) or scope.get('package_count') != len(ids) or
                scope.get('package_ids_sha256') != hashlib.sha256(json.dumps(sorted(ids)).encode()).hexdigest() or
                len(matches) != 1 or set(matches[0].get('package_ids',[])) != set(ids)):
            raise ValueError(f'Linux Studio notice scope differs: {role}')
    registry=Path(observed['cargo_home'])/'registry'; target=Path(observed['cargo_target'])
    if (not game_kit.linux_build_evidence.physical(registry) or not registry.is_dir() or
            not game_kit.linux_build_evidence.physical(target) or not target.is_dir()):
        raise ValueError('Linux Studio notice replay needs the original Cargo registry and target')
    env={key:value for key,value in os.environ.items() if not key.upper().startswith(
        ('CARGO_','RUSTC','RUSTDOC','RUSTFLAGS','RUSTUP_TOOLCHAIN'))}
    env.update(CARGO_HOME=str(registry.parent),CARGO_TARGET_DIR=str(target),CARGO_NET_OFFLINE='true')
    replay={}
    for role,package,features,cargo_manifest in (
            ('studio-custom-protocol','forge_studio',['tauri/custom-protocol'],'Cargo.toml'),
            ('delivery-cli','forge_delivery',[],'tools/forge_delivery/Cargo.toml')):
        replay[role]=runtime_notices._linux_notice_scope(repo,source,graph,index,original,windows_origin,env,
            name=role,package=package,cargo_manifest=cargo_manifest,features=features,no_default=False)
        if replay[role]['captured_package_ids_sha256'] != scopes[role]['package_ids_sha256']:
            raise ValueError(f'Linux Studio notice replay differs: {role}')
    if digest(audit_path) != audit_sha256 or digest(graph_path) != audit['graph_sha256'] or digest(index_path) != audit['index_sha256']:
        raise ValueError('Linux Studio notice evidence changed during replay')
    return {'audit_sha256':audit_sha256,'index_sha256':audit['index_sha256'],
            'native_linux_offline_replay':replay,'wasm_scope':'pinned-preflight',
            'asset_rights_qualified':False,'distribution_qualified':False}


def studio_product(platform='windows-x64'):
    if platform not in ('windows-x64','linux-x64'):
        raise ValueError('Unsupported Studio platform')
    suffix = '' if platform == 'linux-x64' else '.exe'
    return {'schema':'forge.delivery.product.v3', 'id':'openrpg-forge', 'title':'OpenRPG Forge Studio',
            'apps':{'editor':{'executable':'bin/forge-studio'+suffix, 'args':[], 'env':{
                'FORGE_AUTHORING_ROOT':'{workspace}',
                'FORGE_NATIVE_GAME_EXECUTABLE':'{app}/bin/game-export-kit/bin/forge-module'+suffix,
                'FORGE_NATIVE_GAME_CONTENT_ROOT':'{app}/bin/game-export-kit',
                'FORGE_NATIVE_GAME_SESSION_ROOT':'{user}/game-sessions',
                'OPENRPG_ASSET_VAULT':'{pack:asset-library}'},
                'clear_env_prefixes':['FORGE_AUTHORING_', 'FORGE_TIDEWATER_', 'FORGE_NATIVE_GAME_',
                                      'FORGE_MODULE_ASSET_', 'OPENRPG_ASSET_VAULT', 'WEBVIEW2_']}},
            'required_packs':[], 'optional_packs':['asset-library', 'reference-assets'], 'authoring_seed':None}


def assemble_studio_only(args, *, repo=None):
    """Stage a private editor package from platform-matched build receipts."""
    import game_kit
    platform=getattr(args,'platform','windows-x64')
    if platform not in ('windows-x64','linux-x64'):
        raise ValueError('Unsupported Studio platform')
    repo = (repo or Path(__file__).resolve().parents[2]).resolve(strict=True)
    output = Path(args.output)
    if (not output.is_absolute() or '..' in output.parts or not output.parent.is_dir() or
            output.exists() or output.is_symlink() or output.parent.resolve().is_relative_to(repo)):
        raise ValueError('Choose a fresh absolute output outside the checkout')
    application_sources.ordinary(output.parent)
    if not isinstance(args.version, str) or not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9.+_-]{0,127}', args.version):
        raise ValueError('Studio package version is invalid')
    source_path = game_kit.physical_file(args.source_manifest)
    cli = game_kit.physical_file(args.delivery_cli)
    cli_proof_path = game_kit.physical_file(args.delivery_cli_proof)
    studio = game_kit.physical_file(args.studio)
    studio_proof = game_kit.physical_file(args.studio_proof)
    for value in (args.delivery_cli_proof_sha256, args.studio_proof_sha256, args.kit_receipt_sha256):
        if not isinstance(value, str) or not re.fullmatch(r'[0-9a-f]{64}', value):
            raise ValueError('Explicit lowercase SHA-256 receipt pins are required')
    linux_fields=('linux_source_zip','linux_started','linux_capture_receipt','linux_started_sha256','linux_capture_sha256')
    linux_values=[getattr(args,name,None) for name in linux_fields]
    if (platform == 'linux-x64' and (sys.platform != 'linux' or not all(linux_values))) or \
            (platform == 'windows-x64' and any(linux_values)):
        raise ValueError('Linux Studio packaging requires its native ZIP, launch and capture receipts')
    linux_context=({'source_zip':args.linux_source_zip,'started_path':args.linux_started,
                    'capture_path':args.linux_capture_receipt,'started_sha256':args.linux_started_sha256,
                    'capture_sha256':args.linux_capture_sha256} if platform == 'linux-x64' else None)

    def check_inputs():
        source_hash = digest(source_path)
        source = game_kit.read_document(source_path)
        if platform == 'linux-x64':
            if (source.get('schema') != 'forge.gate7.vm-source-snapshot.v1' or
                    cli_proof_path != studio_proof or args.delivery_cli_proof_sha256 != args.studio_proof_sha256 or
                    source_path != studio_proof.parent/'source-manifest.json' or
                    cli != studio_proof.parent/'forge_delivery' or studio != studio_proof.parent/'forge-studio'):
                raise ValueError('Linux Studio and delivery service must use the same verified run')
            if digest(cli_proof_path) != args.delivery_cli_proof_sha256:
                raise ValueError('Native build proof pin differs')
            reader=runtime_notices._snapshot_module(repo,source,'scripts/gate6/capture_inputs.py',
                                                     'forge_linux_delivery_snapshot_capture')
            runtime_notices._verify_extracted_snapshot(repo,source,reader)
            observed=game_kit.linux_build_evidence.verify_linux_studio(studio_proof.parent,args.linux_source_zip,
                args.linux_started,args.linux_capture_receipt,expected_result_sha256=args.studio_proof_sha256,
                expected_started_sha256=args.linux_started_sha256,expected_capture_sha256=args.linux_capture_sha256,
                expected_kit_receipt_sha256=args.kit_receipt_sha256)
            game_kit.linux_build_evidence.linux_elf_x64(cli)
            game_kit.linux_build_evidence.linux_elf_x64(studio,require_executable=False)
            if (observed['source_manifest_sha256'] != source_hash or observed['studio_sha256'] != digest(studio) or
                    observed['delivery_cli_sha256'] != digest(cli)):
                raise ValueError('Linux Studio or delivery executable differs from its build proof')
            verified=game_kit.read_assembled(args.kit_root,args.kit_receipt_sha256,source_hash,
                                             expected_platform=game_kit.LINUX_PLATFORM)
            audit=verify_linux_studio_notice_audit(repo,source,verified,args.studio_notice_audit,
                args.studio_notice_audit_sha256,studio_proof.parent/'linux-kit-notice-graph.json',observed)
            return source_hash,observed['delivery_cli_sha256'],verified,audit
        if (source.get('schema') != 'forge.delivery.shared-source-inputs.v1' or
                Path(source.get('source_root', '')).resolve() != repo or
                not source_inputs_equal(shared_source_inputs(repo), source)):
            raise ValueError('Current shared source inventory required')
        if digest(cli_proof_path) != args.delivery_cli_proof_sha256 or digest(studio_proof) != args.studio_proof_sha256:
            raise ValueError('Native build proof pin differs')
        proof = game_kit.read_document(cli_proof_path)
        validate_shared_native_application(repo, cli, proof, source_hash)
        command = proof.get('commands')
        expected = ['cargo', 'build', '--manifest-path', str(repo / 'tools/forge_delivery/Cargo.toml'),
                    '--bin', 'forge_delivery', '--release', '--locked', '--offline', '-j']
        if (proof.get('application_role') != 'delivery-cli' or proof.get('profile') != 'release' or
                proof.get('built_executable_sha256') != proof['executable_sha256'] or
                proof.get('orchestrator_sha256') != source.get('files', {}).get(
                    'scripts/delivery/complete_rebuild.py', {}).get('sha256') or
                not isinstance(command, list) or len(command) != 1 or
                command[0].get('cwd') != str(repo / 'tools/forge_delivery') or
                command[0].get('arguments', [])[:-1] != expected or
                not str(command[0].get('arguments', [])[-1:][0] if command[0].get('arguments') else '').isdigit() or
                not 1 <= int(command[0]['arguments'][-1]) <= 64):
            raise ValueError('Delivery CLI build proof does not match the dedicated release stage')
        game_kit.require_windows_x64_executable(cli)
        verified = game_kit.read_assembled(args.kit_root, args.kit_receipt_sha256, source_hash)
        audit = verify_studio_notice_audit(repo, source, verified, args.studio_notice_audit,
                                           args.studio_notice_audit_sha256)
        registry = getattr(args, 'cargo_cache', None)
        if registry is None:
            raise ValueError('Windows Studio packaging requires the delivery build Cargo registry')
        replay = runtime_notices.windows_delivery_cli_scope(repo, source, registry,
            verified.kit_root / 'licenses/rust-runtime-candidate/INDEX.json')
        built_home = proof.get('build_environment', {}).get('CARGO_HOME')
        if not isinstance(built_home, str) or Path(built_home).resolve() != Path(replay['cargo_home']):
            raise ValueError('Windows delivery notice registry differs from its build Cargo home')
        audit['windows_delivery_cli_offline_replay'] = replay
        return source_hash, proof['executable_sha256'], verified, audit

    before = check_inputs()
    temp = Path(tempfile.mkdtemp(prefix='.forge-game-kit-', dir=output.parent.resolve()))
    try:
        work, payload = temp / 'applications', temp / 'payload'
        bins = work / 'bin'
        bins.mkdir(parents=True)
        payload.mkdir()
        suffix='' if platform == 'linux-x64' else '.exe'
        copy(studio, bins / ('forge-studio'+suffix))
        adjacent = game_kit.stage_studio_adjacent(args.kit_root, studio, studio_proof, source_path,
                    args.kit_receipt_sha256, args.studio_proof_sha256, bins, repo=repo,
                    expected_platform=platform,linux_context=linux_context)
        (work / 'PROVENANCE.md').write_text(
            'Private local preparation. Runtime, installation, asset rights and redistribution are not qualified.\n',
            encoding='utf-8')
        description = descriptor('forge-studio', 'OpenRPG Forge Studio', 'applications', args.version,
                                 before[0], studio_product(platform),platform=platform)
        description['license_paths'] += ['bin/game-export-kit/' + name for name in before[2].manifest['license_paths']]
        package_entry = package(cli, work, payload, description)
        packed = game_kit.read_document(payload / 'forge-studio.manifest.json')
        expected = {**description, 'files': game_kit.inventory(work)}
        verify_package_stream(payload / package_entry['file'], expected, package_entry['manifest_sha256'],
                              package_entry['archive_sha256'])
        if packed != expected:
            raise ValueError('Reported package descriptor differs from staged Studio')
        delivery_path=payload / ('forge-delivery'+suffix)
        copy(cli, delivery_path)
        if platform == 'linux-x64':
            os.chmod(delivery_path,0o755)
        if digest(delivery_path) != before[1]:
            raise ValueError('Staged delivery CLI differs')
        write(payload / 'delivery.json', {'schema':'forge.delivery.index.v1', 'packages':[package_entry]})
        assembly_receipt={
            'schema':'forge.delivery.private-studio-assembly.v1', 'status':'PRIVATE_STAGED_ONLY',
            'source_manifest_sha256':before[0], 'delivery_cli_sha256':before[1],
            'delivery_cli_proof_sha256':args.delivery_cli_proof_sha256,
            'studio_proof_sha256':args.studio_proof_sha256, 'kit_assembly_receipt_sha256':args.kit_receipt_sha256,
            'notice_audit':before[3], 'adjacent_kit':adjacent, 'package':package_entry,
            'runtime_verified':False, 'installation_verified':False, 'distribution_qualified':False}
        if platform == 'linux-x64':
            assembly_receipt['platform']=platform
        write(payload / 'studio-assembly-receipt.json', assembly_receipt)
        if check_inputs() != before or game_kit.inventory(work) != expected['files']:
            raise ValueError('Inputs changed during Studio package preparation')
        if output.exists() or output.is_symlink():
            raise ValueError('Studio package output appeared during assembly')
        os.rename(payload, output)
        return {'schema':'forge.delivery.private-studio-assembly-result.v1',
                'receipt_sha256':digest(output / 'studio-assembly-receipt.json'),
                'package_sha256':package_entry['archive_sha256'], 'distribution_qualified':False}
    finally:
        if temp.exists():
            game_kit.cleanup_owned(temp, temp.parent)


def covers_source_scope(manifest, scope):
    scopes = [value.rstrip('/') for value in manifest.get('scope', [])]
    declared = any(scope == value or scope.startswith(value + '/') for value in scopes)
    files = manifest.get('files', {})
    if isinstance(files, list):
        files = {entry['path']: entry for entry in files}
    return declared and f'{scope}/Cargo.toml' in files


def validate_shared_native_application(repo, executable, proof, manifest_hash):
    if (proof.get('schema') != 'forge.delivery.native-build.v1' or
            proof.get('status') != 'BUILD_ONLY' or not proof.get('shared_working_tree') or
            proof.get('source_root') != str(repo) or
            not proof.get('source_stable_during_build') or
            proof.get('source_manifest_sha256') != manifest_hash or
            Path(proof.get('executable', '')).resolve() != executable.resolve() or
            proof.get('executable_sha256') != digest(executable)):
        raise ValueError('Current application receipt required; historical or mismatched binary refused')


def main():
    parser = argparse.ArgumentParser(description='Assemble the private Forge Studio package')
    parser.add_argument('mode', choices=('studio-only',))
    for name in ('delivery-cli', 'delivery-cli-proof', 'studio', 'studio-proof', 'source-manifest',
                 'kit-root', 'studio-notice-audit', 'output'):
        parser.add_argument('--'+name, required=True, type=Path)
    for name in ('delivery-cli-proof-sha256', 'studio-proof-sha256', 'kit-receipt-sha256',
                 'studio-notice-audit-sha256', 'version'):
        parser.add_argument('--'+name, required=True)
    parser.add_argument('--platform', choices=('windows-x64','linux-x64'),default='windows-x64')
    parser.add_argument('--cargo-cache', type=Path, help='Cargo registry used for the Windows delivery build')
    for name in ('linux-source-zip','linux-started','linux-capture-receipt'):
        parser.add_argument('--'+name,type=Path)
    for name in ('linux-started-sha256','linux-capture-sha256'):
        parser.add_argument('--'+name)
    print(json.dumps(assemble_studio_only(parser.parse_args()), ensure_ascii=False))


if __name__ == "__main__":
    main()
