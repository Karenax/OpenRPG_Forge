"""Reject thumbnail reuse when identical prefabs resolve different dependencies."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, re

W = Path('F:/OpenRPG_ImportWork')
V = Path('F:/OpenRPG_AssetVault')
O = W/'mountain-thumbnail-reuse-review-v863'
assert not O.exists()

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))

pointer = V/'metadata/asset-source-support-registry.v1.json'
registry = read(pointer)
catalog_path = V/registry['path']
assert sha(catalog_path) == registry['sha256']
cards = {a['id']:a for a in read(catalog_path)['assets']}
frontier_path = W/'source-thumbnail-reuse-frontier-v862/RESULT.v1.json'
frontier = read(frontier_path)
assert frontier['catalog_revision'] == registry['revision']
pins = {str(p):sha(p) for p in [Path(__file__),pointer,catalog_path,frontier_path]}
manifests = {}

def manifest(card):
    path = Path(card['source_manifest_path'])
    if str(path) not in manifests:
        assert sha(path) == card['source_manifest_sha256']
        document = read(path)
        assert document['archive_sha256'] == card['source_archive_sha256']
        files = {}
        for row in document['files']:
            if row['kind'] == 'asset':
                assert row['guid'] not in files
                files[row['guid']] = row
        manifests[str(path)] = (path, document, files)
        pins[str(path)] = card['source_manifest_sha256']
    return manifests[str(path)]

rows = []
for candidate in frontier['candidate_rows']:
    if candidate['pack_id'] != 'hivemind.mountain-ancient-mountain':
        continue
    card = cards[candidate['id']]
    source_path = V/card['source_file']
    assert sha(source_path) == card['source_sha256']
    pins[str(source_path)] = card['source_sha256']
    references = set(re.findall(r'guid:\s*([a-f0-9]{32})', source_path.read_text('utf-8-sig')))
    left_path, _, left = manifest(card)
    donor_checks = []
    for donor_reference in candidate['donors']:
        donor = cards[donor_reference['id']]
        assert all(card.get(k) == donor.get(k) for k in ['source_sha256','source_meta_sha256','source_guid','original_lod_level'])
        right_path, _, right = manifest(donor)
        differences = []
        for guid in sorted(references):
            a, b = left.get(guid), right.get(guid)
            if a is None or b is None:
                differences.append(dict(guid=guid, reason='unresolved_direct_dependency'))
                continue
            if a['sha256'] == b['sha256']:
                continue
            for base, row in [(left_path.parent,a),(right_path.parent,b)]:
                path = base/row['stored_path']
                assert path.resolve().is_relative_to(base.resolve())
                assert sha(path) == row['sha256']
                pins[str(path)] = row['sha256']
            differences.append(dict(guid=guid, reason='different_direct_dependency_bytes',
                                    left_source_path=a['source_path'], right_source_path=b['source_path'],
                                    left_sha256=a['sha256'], right_sha256=b['sha256']))
        donor_checks.append(dict(donor_id=donor['id'], differences=differences,
                                 reuse_refused=bool(differences), full_dependency_closure_verified=False))
    rows.append(dict(id=card['id'],donor_checks=donor_checks,
                     all_donors_refused=all(d['reuse_refused'] for d in donor_checks)))
assert len(rows) == 47
O.mkdir()
result = dict(status='mountain_identical_prefabs_reuse_reviewed_against_direct_dependencies',
              cards_reviewed=len(rows),cards_all_donors_refused=sum(r['all_donors_refused'] for r in rows),
              rows=rows,input_pins=pins,images_admitted=0,full_import=False,
              full_dependency_closure_verified=False,finished_utc=datetime.now(timezone.utc).isoformat())
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2),'utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ['rows','input_pins']}))
