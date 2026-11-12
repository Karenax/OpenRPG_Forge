"""Materialize an isolated candidate package with autonomous logical GLBs.

The input is a full-tree shared-overlay manifest. This is a non-destructive
export helper: it does not publish the overlay, change the Vault, delete source
files, or make existing direct-path consumers overlay-aware.
"""
import argparse
import contextlib
import hashlib
import json
import os
from pathlib import Path

import vault_storage as storage
import candidate_legacy_models
from unity_shared_overlay import UnitySourceResolver, configured_resolver, inventory_tree, materialize
from exclusive_source_delete import ExclusiveSource


CATALOG_NAMES = ('catalog.promote.v1.json', 'catalog.index.v1.json', 'catalog.v1.json')


def catalog_models(catalog):
    """Return catalog-declared GLB paths and logical hashes across known formats."""
    if not isinstance(catalog, dict):
        raise ValueError('invalid candidate catalog')
    if isinstance(catalog.get('assets'), list):
        entries = [(item.get('model', item.get('file')), item.get('sha256')) for item in catalog['assets']]
    elif isinstance(catalog.get('files'), list):
        entries = [(item.get('path'), item.get('sha256')) for item in catalog['files']]
    elif isinstance(catalog.get('materials'), list) and isinstance(catalog.get('programs'), list):
        entries = []
    else:
        raise ValueError('unsupported candidate catalog format')
    models = {}
    for value, pin in entries:
        if not isinstance(value, str) or not value.casefold().endswith('.glb'):
            continue
        rel = UnitySourceResolver._relative(value)
        key = rel.as_posix().casefold()
        if not storage.valid_hex(pin, 64):
            raise ValueError('invalid candidate model SHA')
        if key in models and models[key] != pin:
            raise ValueError('conflicting model pins')
        models[key] = pin
    return models


def export(vault, manifest_path, destination, work_root):
    vault = vault.resolve(strict=True)
    raw = manifest_path.read_bytes()
    manifest = json.loads(raw)
    if manifest.get('schema') != 'openrpg.unity-shared-overlay.v1':
        raise ValueError('invalid overlay manifest')
    rows = {}
    for row in manifest['files']:
        key = UnitySourceResolver._relative(row['logical']).as_posix().casefold()
        if key in rows:
            raise ValueError('duplicate logical file')
        rows[key] = row
    catalog_name = next((name for name in CATALOG_NAMES if name.casefold() in rows), None)
    if catalog_name is None:
        raise ValueError('candidate catalog missing')
    resolver = UnitySourceResolver(vault, manifest_path, storage.sha(raw))
    with resolver.open_verified(catalog_name) as stream:
        catalog = json.load(stream)
    models = catalog_models(catalog)
    missing = set(models) - set(rows)
    legacy = {}
    historical_paths = {}
    legacy_registry_sha = None
    if missing:
        identities = {}
        for item in catalog.get('assets', []):
            value = item.get('model', item.get('file'))
            if not isinstance(value, str) or not value.casefold().endswith('.glb'):
                continue
            key = UnitySourceResolver._relative(value).as_posix().casefold()
            if key in missing:
                ident = item.get('id')
                if not isinstance(ident, str) or not ident or key in identities:
                    raise ValueError('ambiguous historical candidate model')
                identities[key] = ident
                historical_paths[key] = UnitySourceResolver._relative(value)
        if set(identities) != missing or len(set(identities.values())) != len(identities):
            raise ValueError('candidate catalog references missing historical models: ' + str(len(missing)))
        wanted = {ident: models[key] for key, ident in identities.items()}
        by_id, legacy_registry_sha = candidate_legacy_models.load_recipes(vault, wanted)
        legacy = {key: by_id[ident] for key, ident in identities.items()}
    if {key for key in rows if key.endswith('.glb')} != set(models) - missing:
        raise ValueError('candidate contains GLBs absent from its catalog')
    result = materialize(vault, manifest_path, destination, work_root)
    converted = []
    for key, expected in models.items():
        row = rows.get(key)
        relative = UnitySourceResolver._relative(row['logical']) if row else historical_paths[key]
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        source = storage.confined(vault, vault / UnitySourceResolver._relative(row['vault_path'])) if row else None
        temporary = target.with_name(target.name + '.materializing')
        if temporary.exists():
            raise ValueError('temporary model already exists')
        digest = hashlib.sha256()
        total = 0
        with temporary.open('xb') as stream:
            blocks = storage.chunks(vault, source) if row else candidate_legacy_models.chunks(vault, legacy[key], expected)
            for block in blocks:
                stream.write(block)
                digest.update(block)
                total += len(block)
            stream.flush()
            os.fsync(stream.fileno())
        if digest.hexdigest() != expected:
            raise ValueError('logical candidate model SHA differs')
        os.replace(temporary, target)
        converted.append({'logical': relative.as_posix(), 'sha256': expected, 'bytes': total,
                          'source': 'vault_file' if row else 'historical_recipe'})
    if manifest_path.read_bytes() != raw:
        raise ValueError('manifest changed during export')
    if legacy_registry_sha is not None and storage.sha((vault / 'metadata/asset-import-registry.v1.json').read_bytes()) != legacy_registry_sha:
        raise ValueError('historical model registry changed during export')
    return {'state': 'candidate_materialized', 'manifest_sha256': storage.sha(raw),
            'files': result['files'], 'logical_bytes_before_model_expansion': result['logical_bytes'],
            'shared_files': result['shared_files'], 'catalog': catalog_name, 'models': converted,
            'historical_registry_sha256': legacy_registry_sha,
            'destination': str(destination),
            'limitations': 'Export only; candidate consumers and production overlay are not integrated.'}


def cleanup_export(manifest_path, receipt_path, destination, work_root):
    """Remove an unchanged temporary export after checking every file's SHA."""
    work_root = work_root.resolve(strict=True)
    if destination.parent.resolve(strict=True) != work_root or not destination.is_dir():
        raise ValueError('destination must be a direct work-root child')
    raw = manifest_path.read_bytes()
    manifest = json.loads(raw)
    proof = json.loads(receipt_path.read_bytes())
    if (proof.get('state') != 'candidate_materialized' or
            proof.get('manifest_sha256') != storage.sha(raw) or
            Path(proof.get('destination', '')).resolve() != destination.resolve()):
        raise ValueError('invalid candidate export receipt')
    expected = {row['logical'].casefold(): row['sha256'] for row in manifest['files']}
    if len(expected) != len(manifest['files']):
        raise ValueError('duplicate manifest paths')
    catalog_name = proof.get('catalog')
    if catalog_name not in CATALOG_NAMES or catalog_name.casefold() not in expected:
        raise ValueError('invalid candidate catalog receipt')
    catalog = json.loads((destination / catalog_name).read_bytes())
    pins = catalog_models(catalog)
    models = {UnitySourceResolver._relative(row['logical']).as_posix().casefold(): row['sha256']
              for row in proof['models']}
    if pins != models:
        raise ValueError('candidate model receipt differs')
    expected.update(models)
    files, directories = inventory_tree(destination)
    expected_directories = {d.casefold() for d in manifest['directories']}
    for logical in models:
        parent = Path(logical).parent
        while parent != Path('.'):
            expected_directories.add(parent.as_posix().casefold())
            parent = parent.parent
    if (set(files) != set(expected) or
            {d.casefold() for d in directories} != expected_directories):
        raise ValueError('candidate export contents changed')
    with contextlib.ExitStack() as stack:
        handles = []
        for key, (logical, path) in files.items():
            handle = stack.enter_context(ExclusiveSource(path))
            if handle.sha256() != expected[key]:
                raise ValueError('candidate export changed: ' + logical)
            handles.append(handle)
        for handle in handles:
            handle.mark_delete()
    for directory in sorted(directories, key=lambda x: len(Path(x).parts), reverse=True):
        (destination / directory).rmdir()
    destination.rmdir()
    return {'state': 'candidate_export_removed', 'files': len(files)}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', required=True, type=Path)
    selection = p.add_mutually_exclusive_group(required=True)
    selection.add_argument('--manifest', type=Path)
    selection.add_argument('--package', type=Path)
    p.add_argument('--destination', required=True, type=Path)
    p.add_argument('--work-root', required=True, type=Path)
    p.add_argument('--receipt', required=True, type=Path)
    p.add_argument('--cleanup', action='store_true', help='remove an unchanged export using its receipt')
    a = p.parse_args()
    if a.receipt.parent.resolve(strict=True) != a.work_root.resolve(strict=True):
        p.error('receipt must be directly in the work root')
    if a.cleanup and not a.receipt.is_file():
        p.error('cleanup requires an existing receipt')
    if not a.cleanup and a.receipt.exists():
        p.error('export requires a new receipt')
    if a.package is not None:
        package = a.package if a.package.is_absolute() else a.vault / a.package
        resolver = configured_resolver(a.vault, package)
        if resolver is None or resolver.source_root != package.resolve(strict=True):
            p.error('complete candidate package overlay is not published')
        manifest = resolver.manifest_path
    else:
        manifest = a.manifest
    if a.cleanup:
        print(json.dumps(cleanup_export(manifest, a.receipt, a.destination, a.work_root)))
        raise SystemExit(0)
    outcome = export(a.vault, manifest, a.destination, a.work_root)
    with a.receipt.open('x', encoding='utf8') as stream:
        json.dump(outcome, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    print(json.dumps({'state': outcome['state'], 'files': outcome['files'],
                      'models': len(outcome['models']), 'shared_files': outcome['shared_files']}))
