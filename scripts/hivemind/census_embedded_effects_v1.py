"""Read retained prefab/scene sources by pack; retain complete direct-effect IR.

No source scripts execute, no Vault writes, and no runtime qualification follows
from the presence of a serialized component or a prefab reference.
"""
import collections
import datetime
import hashlib
import importlib.util
import json
import re
from pathlib import Path

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'embedded-effect-source-census-v1'
COVERAGE = W / 'review/source-format-coverage.v1.json'
OLD = W / 'hivemind-effect-source-ir-v1/RESULT.v1.json'
HELPER = W / 'hivemind-effect-source-ir-v1/capture_source_ir.py'
EFFECT_TYPES = {'ParticleSystem', 'ParticleSystemRenderer', 'VisualEffect', 'VFXRenderer'}
HEADER = re.compile(r'^--- !u!(\d+) &(-?\d+)( stripped)?[ \t]*\r?$', re.M)
ROOT_TYPE = re.compile(r'^([A-Za-z][A-Za-z0-9_]*):[ \t]*\r?$', re.M)
PREFAB_REF = re.compile(r'^  m_SourcePrefab: \{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: (\d+)\}[ \t]*\r?$', re.M)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def load(path):
    return json.loads(path.read_text('utf-8-sig'))


def write(path, data):
    with path.open('x', encoding='utf-8') as stream:
        json.dump(data, stream, ensure_ascii=False, allow_nan=False, indent=2)


def progress(data):
    temporary = ROOT / 'PROGRESS.tmp.json'
    temporary.write_text(json.dumps(data), 'utf-8')
    temporary.replace(ROOT / 'PROGRESS.v1.json')


def sections(raw):
    text = raw.decode('utf-8-sig')
    matches = list(HEADER.finditer(text))
    if not matches:
        raise ValueError('No serialized Unity document headers')
    ids = set()
    result = []
    for index, header in enumerate(matches):
        if header[2] in ids:
            raise ValueError('Duplicate serialized document identity')
        ids.add(header[2])
        body = text[header.end():matches[index + 1].start() if index + 1 < len(matches) else len(text)]
        roots = list(ROOT_TYPE.finditer(body))
        if len(roots) != 1:
            raise ValueError('Expected one document root')
        result.append((header[2], roots[0][1], body))
    return result


def main():
    assert not ROOT.exists(), 'Keep prior receipts'
    original = load(OLD)
    helper_hash = digest(HELPER.read_bytes())
    assert original['source_pins'][str(HELPER)] == helper_hash
    spec = importlib.util.spec_from_file_location('trusted_effect_ir', HELPER)
    parser = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(parser)
    coverage_hash = digest(COVERAGE.read_bytes())
    coverage = load(COVERAGE)
    assert len(coverage['manifests']) == 48
    previous = {(row['source_manifest'], row['guid']): row for row in original['rows']}
    ROOT.mkdir()
    (ROOT / 'packs').mkdir()
    (ROOT / 'cases').mkdir()
    packs = []
    count = collections.Counter()
    for ordinal, manifest_row in enumerate(coverage['manifests']):
        path = Path(manifest_row['manifest'])
        manifest_hash = digest(path.read_bytes())
        assert manifest_hash == manifest_row['manifest_sha256']
        manifest = load(path)
        assert manifest['private_only'] is True and manifest['license']
        source_root = Path(manifest['source_root'])
        files = {(entry['source_path'], entry['kind']): entry for entry in manifest['files']}
        pins = {str(path): manifest_hash}
        rows = []
        for entry in manifest['files']:
            suffix = Path(entry['source_path']).suffix.lower()
            if entry['kind'] != 'asset' or suffix not in {'.prefab', '.unity'}:
                continue
            source = source_root / entry['source_path']
            assert source.resolve().is_relative_to(source_root.resolve())
            raw = source.read_bytes()
            assert digest(raw) == entry['sha256'], str(source)
            pins[str(source)] = entry['sha256']
            meta_entry = files[(entry['source_path'], 'asset.meta')]
            meta = Path(str(source) + '.meta')
            meta_raw = meta.read_bytes()
            assert digest(meta_raw) == meta_entry['sha256'] and meta_entry['guid'] == entry['guid']
            assert re.search(r'^guid: ' + re.escape(entry['guid']) + r'\s*$', meta_raw.decode('utf-8-sig'), re.M)
            pins[str(meta)] = meta_entry['sha256']
            row = dict(source=str(source), guid=entry['guid'], source_sha256=entry['sha256'],
                       meta=str(meta), meta_sha256=meta_entry['sha256'], format=suffix,
                       direct_effect_component_counts={}, prefab_reference_candidates=[],
                       scan_error=None, runtime_converted=False)
            try:
                docs = sections(raw)
                types = collections.Counter(typ for _, typ, _ in docs)
                row['direct_effect_component_counts'] = {typ: types[typ] for typ in sorted(EFFECT_TYPES) if types[typ]}
                for fid, typ, body in docs:
                    if typ == 'PrefabInstance':
                        for ref in PREFAB_REF.finditer(body):
                            row['prefab_reference_candidates'].append(dict(owner_file_id=fid,
                                file_id=ref[1], guid=ref[2], source_path=manifest['guid_paths'].get(ref[2]),
                                subasset_resolved=False))
                if row['direct_effect_component_counts']:
                    parsed = parser.parse(raw)
                    assert collections.Counter(doc['type'] for doc in parsed) == types
                    case = ROOT / 'cases' / f'{ordinal:02}-{entry["guid"]}.json'
                    write(case, dict(schema='openrpg.embedded-effect-complete-source-ir.v1',
                        source=str(source), source_sha256=entry['sha256'], source_manifest=str(path),
                        archive_sha256=manifest['archive_sha256'], license=manifest['license'],
                        private_only=True, documents=parsed, all_original_component_fields_retained=True,
                        runtime_converted=False, dependency_closure_verified=False))
                    row['ir_path'] = str(case)
                    row['ir_sha256'] = digest(case.read_bytes())
                    row['previously_in_effect_ir'] = (str(path), entry['guid']) in previous
                    count['direct_effect_sources'] += 1
                    count['newly_discovered_direct_effect_sources'] += not row['previously_in_effect_ir']
            except (ValueError, parser.yaml.YAMLError) as exc:
                row['scan_error'] = str(exc)
                count['scan_errors'] += 1
            rows.append(row)
            count['source_files_checked'] += 1
        assert all(digest(Path(pin).read_bytes()) == expected for pin, expected in pins.items())
        pack_result = dict(source_manifest=str(path), manifest_sha256=manifest_hash,
            archive_sha256=manifest['archive_sha256'], license=manifest['license'], private_only=True,
            source_files_checked=len(rows), input_pins=pins, rows=rows,
            source_hashes_checked_before_after=True, full_effect_coverage_verified=False,
            prefab_inheritance_resolved=False, runtime_converted=False, vault_modified=False)
        pack_file = ROOT / 'packs' / f'{ordinal:02}.json'
        write(pack_file, pack_result)
        packs.append(dict(receipt=str(pack_file), sha256=digest(pack_file.read_bytes()),
                          source_manifest=str(path), source_files_checked=len(rows)))
        progress(dict(status='scanning_retained_prefab_and_scene_effect_components',
                      packs_checked=len(packs), packs_expected=48, **count))
    assert digest(COVERAGE.read_bytes()) == coverage_hash and digest(HELPER.read_bytes()) == helper_hash
    write(ROOT / 'RESULT.v1.json', dict(schema='openrpg.embedded-effect-source-census.v1',
        checked_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        status='serialized_prefab_and_scene_effect_census_finished', packs_checked=len(packs),
        input_pins={str(COVERAGE):coverage_hash, str(HELPER):helper_hash,
                    str(Path(__file__).resolve()):digest(Path(__file__).read_bytes())},
        scope='All manifest-indexed prefab and scene files from the 48 retained source manifests',
        **count, packs=packs, remaining=['Resolve nested prefab inheritance and overrides',
        'Inspect script-generated and non-prefab/non-scene effects', 'Resolve resource subassets',
        'Convert behavior and compare native rendering and temporal behavior', 'Studio and runtime recipe'],
        full_effect_coverage_verified=False, new_assets_converted=0, studio_verified=False,
        runtime_verified=False, vault_modified=False, full_import=False))
    print(json.dumps(dict(packs_checked=len(packs), **count)))


if __name__ == '__main__':
    main()
