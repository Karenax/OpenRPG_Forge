"""Map verified library IDs to physical paths; keep uncovered files explicit."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json

V = Path('F:/OpenRPG_AssetVault')
W = Path('F:/OpenRPG_ImportWork')
OUT = W / 'vault-indexed-coverage-plan-v1'


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    assert not OUT.exists(), 'Preserve existing mapping receipt'
    audit_path = W / 'asset-library-indexed-payload-integrity-resume-v2/PAYLOAD-RESULT.v1.json'
    audit = read(audit_path)
    run = read(audit_path.parent / 'RUN-RESULT.v1.json')
    assert sha(audit_path) == run['payload_receipt_sha256']
    assert audit['passed_count'] == 26735 and audit['refused_count'] == 0
    audited = {r['id']: r for r in audit['rows']}
    registry_path = V / 'metadata/asset-import-registry.v1.json'
    support_path = V / 'metadata/asset-source-support-registry.v1.json'
    assert sha(registry_path) == audit['registry_sha256']
    assert sha(support_path) == audit['source_support_registry_sha256']
    inventory_path = W / 'vault-physical-batch-plan-v1/INVENTORY.jsonl'
    inventory = {r['path']: r for r in map(json.loads, inventory_path.read_text('utf-8-sig').splitlines())}
    coverage, seen, catalogs, pins = {}, set(), {}, {}
    def cover(path, expected, kind, asset_id):
        absolute = path if path.is_absolute() else V / path
        relative = absolute.relative_to(V).as_posix()
        assert relative in inventory, relative
        prior = coverage.get(relative)
        if prior:
            assert prior['expected_sha256'] == expected, relative
            prior['asset_ids'].append(asset_id)
        else:
            coverage[relative] = dict(path=relative, expected_sha256=expected, evidence_kind=kind,
                                      asset_ids=[asset_id], bytes=inventory[relative]['bytes'],
                                      mtime_ns=inventory[relative]['mtime_ns'])
    for pack in read(registry_path)['packs'].values():
        details_path = V / pack['path'] / 'details.v1.json'
        assert sha(details_path) == pack['files']['details.v1.json']
        pins[str(details_path)] = pack['files']['details.v1.json']
        for item in read(details_path)['assets']:
            key = item['id']
            assert key not in seen and audited[key]['model_full_sha_checked']
            seen.add(key)
            model = Path(item['model_path'])
            cover(model, item['model_sha256'], 'completed_indexed_model_sha_audit', key)
            catalog_path = Path(item['source_catalog'])
            if catalog_path not in catalogs:
                catalogs[catalog_path] = {r['id']: r for r in read(catalog_path)['assets']}
                pins[str(catalog_path)] = sha(catalog_path)
            catalog = catalogs[catalog_path][key]
            assert catalog['sha256'] == item['model_sha256']
            relative_model = Path(catalog['model'])
            assert model.parts[-len(relative_model.parts):] == relative_model.parts
            root = model
            for _ in relative_model.parts:
                root = root.parent
            if catalog.get('preview'):
                assert audited[key]['preview_png_sha_checked']
                cover(root / catalog['preview'], catalog['preview_sha256'], 'completed_indexed_preview_sha_audit', key)
            else:
                assert not audited[key]['preview_png_sha_checked']
    support_registry = read(support_path)
    source_catalog = V / support_registry['path']
    assert sha(source_catalog) == support_registry['sha256']
    pins[str(source_catalog)] = support_registry['sha256']
    archives = set()
    for item in read(source_catalog)['assets']:
        key = item['id']
        assert key not in seen and audited[key]['source_and_meta_full_sha_checked']
        seen.add(key)
        for field, hash_field in [('source_file', 'source_sha256'), ('source_meta_file', 'source_meta_sha256'),
                                 ('source_archive', 'source_archive_sha256')]:
            cover(Path(item[field]), item[hash_field], 'completed_indexed_source_sha_audit', key)
        archives.add(item['source_archive'])
        if item.get('preview'):
            assert audited[key]['preview_png_sha_checked']
            cover(Path(item['preview']), item['preview_sha256'], 'completed_indexed_preview_sha_audit', key)
        else:
            assert not audited[key]['preview_png_sha_checked']
    assert seen == set(audited) and len(archives) == 38
    # Preserve actual reads from the first physical batch separately from audit mapping.
    ledger_path = W / 'vault-physical-batch-plan-v1/checks/00000.RESULT.v1.jsonl'
    for line in ledger_path.read_text('utf-8-sig').splitlines():
        row = json.loads(line)
        assert inventory[row['path']]['mtime_ns'] == row['mtime_ns']
        if row['path'] in coverage:
            assert coverage[row['path']]['expected_sha256'] == row['actual_current_sha256']
        else:
            coverage[row['path']] = dict(path=row['path'], expected_sha256=row['actual_current_sha256'],
                evidence_kind='physical_batch_actual_sha_read', asset_ids=[], bytes=row['bytes'], mtime_ns=row['mtime_ns'])
    remaining = [r for k, r in inventory.items() if k not in coverage]
    remaining.sort(key=lambda r: (r['baseline_metadata_equal'], r['path']))
    OUT.mkdir()
    (OUT / 'COVERED.json').write_text(json.dumps(list(coverage.values()), ensure_ascii=False, indent=2), 'utf-8')
    batch_dir = OUT / 'remaining-batches'
    batch_dir.mkdir()
    batches, chunk, size = [], [], 0
    def flush():
        nonlocal chunk, size
        if chunk:
            path = batch_dir / f'{len(batches):05d}.json'
            path.write_text(json.dumps(chunk, ensure_ascii=False, indent=2), 'utf-8')
            batches.append(dict(path=str(path), files=len(chunk), bytes=size, sha256=sha(path)))
            chunk, size = [], 0
    for row in remaining:
        if chunk and (len(chunk) >= 512 or size + row['bytes'] > 2 * 1024**3):
            flush()
        chunk.append(row)
        size += row['bytes']
    flush()
    for path, expected in pins.items():
        assert sha(Path(path)) == expected
    result = dict(status='indexed_audit_paths_and_first_physical_batch_mapped_to_inventory',
        finished_utc=datetime.now(timezone.utc).isoformat(), indexed_ids_mapped=len(seen),
        covered_physical_paths=len(coverage), remaining_physical_paths=len(remaining),
        remaining_bytes=sum(r['bytes'] for r in remaining), remaining_batches=batches,
        registry_pins={str(registry_path): audit['registry_sha256'], str(support_path): audit['source_support_registry_sha256']},
        source_metadata_pins=pins, inventory_sha256=sha(inventory_path), audit_receipt_sha256=sha(audit_path),
        whole_current_vault_qualified=False, full_import=False,
        note='Completed audit evidence is reused by exact IDs and declared content hashes. Inventory mtime/size is a change detector, not a new content hash. All uncovered files remain scheduled for actual SHA reads.')
    (OUT / 'RESULT.v1.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
    print(json.dumps({k: result[k] for k in ('status', 'indexed_ids_mapped', 'covered_physical_paths', 'remaining_physical_paths', 'remaining_bytes')}))
    print(f'Remaining durable SHA batches: {len(batches)}')


if __name__ == '__main__':
    main()
