"""Embed only the authenticated private game-kit manifest in a Studio frontend.

The existing static asset inventory remains untouched. This is a build input,
not evidence of a runnable game, installed Studio or distribution rights.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re

import application_sources
import game_kit

MANIFEST = 'forge-game-export-v1.json'
RECEIPT = 'forge-game-export-staging-receipt.json'
MAX_MANIFEST = 8 * 1024 * 1024
MAX_RECEIPT = 64 * 1024


def _digest(data):
    return hashlib.sha256(data).hexdigest()


def _json(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode('utf-8')


def _input_directory(path, label):
    path = Path(path)
    if not path.is_absolute() or '..' in path.parts:
        raise ValueError(f'{label} must be an absolute physical directory')
    application_sources.ordinary(path)
    if not path.is_dir():
        raise ValueError(f'{label} must be an existing physical directory')
    return path.resolve(strict=True)


def _inputs(assembled_root, frontend, receipt_sha256, source_sha256):
    for value in (receipt_sha256, source_sha256):
        if not isinstance(value, str) or not re.fullmatch(r'[0-9a-f]{64}', value):
            raise ValueError('Expected build and kit receipt SHA-256 pins are required')
    assembled_root = _input_directory(assembled_root, 'Assembled game kit')
    frontend = _input_directory(frontend, 'Studio frontend')
    if assembled_root == frontend or assembled_root.is_relative_to(frontend) or frontend.is_relative_to(assembled_root):
        raise ValueError('The assembled game kit and Studio frontend must be separate')
    return assembled_root, frontend


def _stage_receipt(verified, expected_receipt_sha256, expected_source_manifest_sha256, expected_platform):
    manifest = verified.manifest
    raw = verified.manifest_bytes
    if (not isinstance(manifest, dict) or not isinstance(raw, bytes) or not 0 < len(raw) <= MAX_MANIFEST
            or _digest(raw) != verified.manifest_sha256
            or verified.receipt_sha256 != expected_receipt_sha256
            or manifest.get('source_revision') != expected_source_manifest_sha256
            or manifest.get('platform') != expected_platform):
        raise ValueError('Verified kit manifest, assembly receipt or source pin differs')
    try:
        decoded = json.loads(raw)
    except (ValueError, UnicodeError) as error:
        raise ValueError('Verified kit manifest is not valid JSON') from error
    if decoded != manifest:
        raise ValueError('Verified kit manifest bytes and parsed contents differ')
    files = manifest.get('files')
    if not isinstance(files, list) or not files:
        raise ValueError('Verified kit inventory is missing')
    sizes = [item.get('bytes') for item in files if isinstance(item, dict)]
    if len(sizes) != len(files) or any(type(size) is not int or size <= 0 for size in sizes):
        raise ValueError('Verified kit inventory sizes are invalid')
    return {
        'schema': 'forge.studio.game-kit-stage.v1',
        'status': 'PRIVATE_STAGED_ONLY',
        'manifest_file': MANIFEST,
        'manifest_sha256': verified.manifest_sha256,
        'manifest_bytes': len(raw),
        'kit_assembly_receipt_sha256': expected_receipt_sha256,
        'source_manifest_sha256': expected_source_manifest_sha256,
        'kit_file_count': len(files),
        'kit_file_bytes': sum(sizes),
        'kit_inventory_sha256': _digest(_json(files)),
        'build_only': True,
        'runtime_verified': False,
        'installation_verified': False,
        'distribution_qualified': False,
    }


def _create(path, data, created):
    with path.open('xb') as stream:
        stat = os.fstat(stream.fileno())
        created.append((path, (stat.st_dev, stat.st_ino), data))
        stream.write(data)
        stream.flush()
        os.fsync(stream.fileno())


def _rollback(created):
    for path, identity, expected in reversed(created):
        if not os.path.lexists(path):
            continue
        application_sources.ordinary(path)
        with path.open('rb') as stream:
            stat = os.fstat(stream.fileno())
            actual = stream.read(len(expected) + 1)
        if (stat.st_dev, stat.st_ino) != identity or not expected.startswith(actual):
            raise ValueError(f'Cannot safely remove changed staging output: {path}')
        application_sources.ordinary(path)
        path.unlink()


def _read_kit(reader, root, receipt_sha256, source_sha256, expected_platform):
    if expected_platform not in game_kit.PLATFORM_BINARIES:
        raise ValueError('Unsupported Studio game kit platform')
    if expected_platform == game_kit.PLATFORM:
        return reader(root, receipt_sha256, source_sha256)
    return reader(root, receipt_sha256, source_sha256, expected_platform=expected_platform)


def verify_staged(assembled_root, frontend, expected_receipt_sha256,
                  expected_source_manifest_sha256, *, reader=None, expected_platform=game_kit.PLATFORM):
    """Recheck both overlay files and their source kit without altering anything."""
    assembled_root, frontend = _inputs(assembled_root, frontend, expected_receipt_sha256,
                                       expected_source_manifest_sha256)
    reader = reader or game_kit.read_assembled
    verified = _read_kit(reader, assembled_root, expected_receipt_sha256, expected_source_manifest_sha256, expected_platform)
    expected = _stage_receipt(verified, expected_receipt_sha256, expected_source_manifest_sha256, expected_platform)
    receipt_data = application_sources.read_regular(frontend / RECEIPT, MAX_RECEIPT, 'game kit staging receipt')
    manifest_data = application_sources.read_regular(frontend / MANIFEST, MAX_MANIFEST, 'embedded game kit manifest')
    try:
        receipt = json.loads(receipt_data)
    except (ValueError, UnicodeError) as error:
        raise ValueError('Game kit staging receipt is not valid JSON') from error
    if receipt != expected or manifest_data != verified.manifest_bytes:
        raise ValueError('Staged game kit receipt or manifest differs from the verified kit')
    return receipt


def stage(assembled_root, frontend, expected_receipt_sha256,
          expected_source_manifest_sha256, *, reader=None, expected_platform=game_kit.PLATFORM):
    """Create the manifest overlay only after checking the kit, then recheck it."""
    assembled_root, frontend = _inputs(assembled_root, frontend, expected_receipt_sha256,
                                       expected_source_manifest_sha256)
    reader = reader or game_kit.read_assembled
    manifest_path, receipt_path = frontend / MANIFEST, frontend / RECEIPT
    if os.path.lexists(manifest_path) or os.path.lexists(receipt_path):
        raise ValueError('A game kit manifest or staging receipt already exists; it is preserved')
    verified = _read_kit(reader, assembled_root, expected_receipt_sha256, expected_source_manifest_sha256, expected_platform)
    receipt = _stage_receipt(verified, expected_receipt_sha256, expected_source_manifest_sha256, expected_platform)
    created = []
    try:
        application_sources.ordinary(frontend)
        _create(manifest_path, verified.manifest_bytes, created)
        current = _read_kit(reader, assembled_root, expected_receipt_sha256, expected_source_manifest_sha256, expected_platform)
        if (_stage_receipt(current, expected_receipt_sha256, expected_source_manifest_sha256, expected_platform) != receipt
                or current.manifest_bytes != verified.manifest_bytes):
            raise ValueError('Game kit changed during manifest staging')
        _create(receipt_path, _json(receipt) + b'\n', created)
        verified_receipt = verify_staged(assembled_root, frontend, expected_receipt_sha256,
                                          expected_source_manifest_sha256, reader=reader, expected_platform=expected_platform)
        if verified_receipt != receipt:
            raise ValueError('Game kit changed while writing the staging receipt')
        return receipt
    except BaseException as error:
        try:
            _rollback(created)
        except (OSError, ValueError) as cleanup:
            raise ValueError(f'{error}; staging output preserved because cleanup was unsafe: {cleanup}') from error
        raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--assembled-root', required=True, type=Path)
    parser.add_argument('--frontend', required=True, type=Path)
    parser.add_argument('--kit-receipt-sha256', required=True)
    parser.add_argument('--source-manifest-sha256', required=True)
    parser.add_argument('--platform', choices=tuple(game_kit.PLATFORM_BINARIES), default=game_kit.PLATFORM)
    args = parser.parse_args()
    result = stage(args.assembled_root, args.frontend, args.kit_receipt_sha256,
                   args.source_manifest_sha256, expected_platform=args.platform)
    print(json.dumps(result, ensure_ascii=False, sort_keys=True))


if __name__ == '__main__':
    main()
