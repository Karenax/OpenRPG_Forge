"""Reconcile original archives, nested variants and the documented batch scope.

This reopens manifests and lineage, not the archive payload bytes. Historical
archive integrity evidence remains dated and is never promoted to a fresh hash.
"""
import csv
import datetime
import hashlib
import json
from pathlib import Path

W = Path('F:/OpenRPG_ImportWork')
V = Path('F:/OpenRPG_AssetVault')
OUT = W / 'collection-archive-lineage-reconciliation-v1'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    assert not OUT.exists(), 'Previous lineage evidence preserved'
    collection_path = V / 'metadata/hivemind/hivemind-collection.intake.v1.json'
    prior_path = W / 'review/root-archive-integrity-current-F-v1/RESULT.json'
    csv_path = W / 'SUIVI_LOTS_HIVEMIND/INVENTAIRE_PAR_PACK.csv'
    pins = {str(p): sha(p) for p in [collection_path, prior_path, csv_path, Path(__file__).resolve()]}
    collection = json.loads(collection_path.read_text('utf-8-sig'))
    prior = json.loads(prior_path.read_text('utf-8-sig'))
    assert prior['status'] == 'PASS_ALL_24_ORIGINAL_PURCHASED_ARCHIVES_HASH_VERIFIED_CURRENT_F'
    assert pins[str(collection_path)] == prior['original_collection_sha256']
    assert len(collection['packs']) == collection['archive_count'] == 24
    historical = {row['pack_id']: row for row in prior['rows']}
    nodes = {}
    by_hash = {}
    for folder in sorted((W / 'hivemind-run').iterdir()):
        if not folder.is_dir() or not (folder / 'source.v1.json').exists():
            continue
        manifests = [folder / 'source.v1.json']
        variants = folder / 'variants'
        if variants.is_dir():
            manifests += sorted(p / 'source.v1.json' for p in variants.iterdir()
                                if p.is_dir() and (p / 'source.v1.json').exists())
        for path in manifests:
            data = json.loads(path.read_text('utf-8-sig'))
            pins[str(path)] = sha(path)
            pack = 'hivemind.' + folder.name
            if path.parent != folder:
                pack += '.variant-' + path.parent.name
            assert pack not in nodes and data['archive_sha256'] not in by_hash
            assert data['license'] == collection['license'] and data['private_only']
            assert not data['ai_content_generation_used']
            source = Path(data['source_root'])
            assert source.resolve().is_relative_to((W / 'hivemind-run').resolve()) and source.is_dir()
            node = dict(pack_id=pack, manifest=str(path), manifest_sha256=pins[str(path)],
                        archive_sha256=data['archive_sha256'], archive_path_declared=data['archive_path'],
                        source_root=str(source), nested=data['nested_packages'], source_entries=len(data['files']))
            nodes[pack] = node
            by_hash[node['archive_sha256']] = node
    with csv_path.open(encoding='utf-8-sig', newline='') as stream:
        tracked = {row['pack_id'] for row in csv.DictReader(stream)}
    assert set(nodes) == tracked, 'Imported manifests and batch inventory differ'
    edges, roots, refusals = [], [], []
    for pack in collection['packs']:
        node = nodes[pack['id']]
        assert node['archive_sha256'] == pack['sha256']
        preserved = historical[pack['id']]
        assert preserved['sha256'] == pack['sha256'] and preserved['bytes'] == pack['bytes']
        archive = Path(preserved['canonical_archive_F'])
        assert archive.resolve().is_relative_to(V.resolve())
        assert archive.is_file() and archive.stat().st_size == pack['bytes']
        roots.append(dict(pack_id=pack['id'], canonical_archive=str(archive), bytes=pack['bytes'],
                          declared_sha256=pack['sha256'], archive_payload_rehashed_now=False))
    root_ids = {row['pack_id'] for row in roots}
    children = set()
    for parent in nodes.values():
        for nested in parent['nested']:
            child = by_hash.get(nested['sha256'])
            if child is None:
                refusals.append(dict(parent_pack=parent['pack_id'], reason='Nested archive has no extracted manifest', nested=nested))
                continue
            assert child['pack_id'] not in root_ids and child['pack_id'] not in children, 'Ambiguous archive lineage'
            source_path = Path(child['archive_path_declared'])
            assert source_path.resolve().is_relative_to(Path(parent['source_root']).resolve())
            assert source_path.is_file() and source_path.stat().st_size == nested['bytes']
            expected_path = Path(parent['manifest']).parent / nested['stored_path']
            assert expected_path.resolve() == source_path.resolve()
            children.add(child['pack_id'])
            edges.append(dict(parent_pack=parent['pack_id'], child_pack=child['pack_id'],
                              nested_guid=nested['guid'], source_asset_path=nested['source_path'],
                              archive_sha256=nested['sha256'], declared_bytes=nested['bytes'],
                              archive_payload_rehashed_now=False))
    assert children | root_ids == set(nodes), 'Unconnected imported variant retained'
    assert all(sha(Path(p)) == expected for p, expected in pins.items()), 'Manifest changed during reconciliation'
    result = dict(status='archive_lineage_and_batch_scope_reconciled' if not refusals else 'missing_nested_variant_retained',
                  observed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  original_store_directory_exists=Path(collection['source_root']).is_dir(),
                  source_directory_current_enumeration_completed=False,
                  purchased_root_archives=len(roots), imported_manifests=len(nodes), nested_variant_edges=len(edges),
                  tracked_pack_ids=len(tracked), source_entries=sum(row['source_entries'] for row in nodes.values()),
                  roots=roots, edges=edges, manifests=list(nodes.values()), refusals=refusals, input_pins=pins,
                  historical_archive_hash_receipt=str(prior_path), historical_archive_hash_finished=prior.get('closed_utc'),
                  archive_payloads_rehashed_now=False, asset_conversion_count_added=0,
                  gpu_verified=False, studio_verified=False, full_import=False)
    OUT.mkdir()
    (OUT / 'RESULT.v1.json').write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps({k: v for k, v in result.items() if k not in ['roots', 'edges', 'manifests', 'input_pins']}))


if __name__ == '__main__':
    main()
