"""Add exact new effect cases and retained scan refusals to the batch plan."""
import collections
import datetime
import json
from pathlib import Path

from prepare_effect_execution_batches_v1 import sha, load, W

ROOT = W / 'embedded-effect-execution-batches-v1'
OLD = W / 'effect-execution-batches-v1/RESULT.v1.json'
CENSUS = W / 'embedded-effect-source-census-v1/RESULT.v1.json'


def main():
    assert not ROOT.exists(), 'Preserve earlier batch plan'
    old, census = load(OLD), load(CENSUS)
    assert census['packs_checked'] == len(census['packs']) == 48
    pins = {str(OLD): sha(OLD), str(CENSUS): sha(CENSUS), str(Path(__file__).resolve()): sha(__file__)}
    old_keys = {(e['source_manifest'], e['guid']) for b in old['batches']
                for e in b['entries'] if e['kind'] == 'unresolved_effect_prefab'}
    assert len(old_keys) == 85
    direct_keys, reused_keys = set(), set()
    groups, refusals = collections.defaultdict(list), []
    for receipt in census['packs']:
        assert sha(receipt['receipt']) == receipt['sha256']
        pins[receipt['receipt']] = receipt['sha256']
        pack = load(receipt['receipt'])
        manifest = pack['source_manifest']
        pins[manifest] = pack['manifest_sha256']
        assert sha(manifest) == pack['manifest_sha256']
        for row in pack['rows']:
            if row['scan_error']:
                refusals.append(dict(source=row['source'], source_sha256=row['source_sha256'],
                    meta=row['meta'], meta_sha256=row['meta_sha256'], source_manifest=manifest,
                    error=row['scan_error'], required_action='native serialized scene inspection',
                    source_preserved=True, converted=False))
            if 'ir_path' not in row:
                continue
            key = (manifest, row['guid'])
            assert key not in direct_keys
            direct_keys.add(key)
            if key in old_keys:
                reused_keys.add(key)
                continue
            for path, expected in [(row['source'], row['source_sha256']),
                                   (row['meta'], row['meta_sha256']),
                                   (row['ir_path'], row['ir_sha256'])]:
                assert sha(path) == expected
                pins[path] = expected
            ir = load(row['ir_path'])
            assert ir['source'] == row['source'] and ir['source_sha256'] == row['source_sha256']
            assert ir['archive_sha256'] == pack['archive_sha256'] and ir['license'] == pack['license']
            assert ir['all_original_component_fields_retained'] and not ir['runtime_converted']
            entry = dict(row, source_manifest=manifest, archive_sha256=pack['archive_sha256'],
                         license=pack['license'], private_only=True,
                         id=f"embedded-effect.{Path(manifest).parent.name}.{row['guid']}",
                         dependency_closure_verified=False, studio_verified=False)
            groups[(manifest, row['format'])].append(entry)
    assert direct_keys >= old_keys and reused_keys == old_keys
    assert len(direct_keys) == 174 and len(refusals) == census['scan_errors'] == 6
    batches = []
    for (manifest, fmt), entries in sorted(groups.items()):
        entries.sort(key=lambda e: e['guid'])
        for start in range(0, len(entries), 5):
            subset = entries[start:start + 5]
            batches.append(dict(batch_id=f"L06.{Path(manifest).parent.name}.embedded-{fmt[1:]}.{start//5+1:03}",
                expected=len(subset), entries=subset, stage='source_ir_verified_conversion_pending',
                remaining=['resolve inheritance, overrides and exact resource subassets',
                           'observe native behavior and rendering in matching source context',
                           'convert effects, compare trajectories, lifetime and transparency',
                           'asset-specific image, metadata and actual Studio/runtime recipe']))
    assert sum(b['expected'] for b in batches) == 89
    for row in refusals:
        for field in ['source', 'meta']:
            assert sha(row[field]) == row[field + '_sha256']
            pins[row[field]] = row[field + '_sha256']
    for path, expected in pins.items():
        assert sha(path) == expected, 'Changed pinned input: ' + path
    result = dict(schema='openrpg.embedded-effect-execution-batches.v1',
        checked_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        status='89_new_direct_effect_sources_partitioned_with_6_scan_refusals_retained',
        source_files_censused=8830, packs=48, direct_effect_sources=174,
        existing_prefab_sources_reused=85, existing_vfx_graph_sources_retained=139,
        newly_partitioned_sources=89, batch_count=len(batches), batches=batches,
        scan_refusals=refusals, input_pins=pins, old_batches_receipt=str(OLD),
        full_collection_effect_coverage_verified=False, inherited_effects_resolved=False,
        new_assets_converted=0, studio_verified=False, runtime_verified=False,
        vault_modified=False, full_import=False)
    ROOT.mkdir()
    (ROOT / 'RESULT.v1.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), 'utf-8')
    lines = ['# Nouveaux sous-lots des effets intégrés', '',
        '48 packs examinés : 8 830 prefabs/scènes, 174 sources avec effets directs.',
        'Les 85 cas anciens et 139 graphes restent dans le plan précédent ; aucun lot validé refait.',
        '89 nouveaux cas répartis ci-dessous ; six refus de lecture conservés séparément.',
        'Héritage des prefabs, sous-assets, conversion et recette restent à terminer.', '',
        '| Sous-lot | Sources | État |', '|---|---:|---|']
    lines += [f"| {b['batch_id']} | {b['expected']} | IR source vérifié ; conversion restante |" for b in batches]
    lines += ['', '## Refus à examiner nativement', '']
    lines += [f"- `{r['source']}` : `{r['error']}`" for r in refusals]
    (ROOT / 'SOUS_LOTS.md').write_text('\n'.join(lines) + '\n', 'utf-8')
    print(json.dumps(dict(batch_count=len(batches), new_sources=89, scan_refusals=6, full_import=False)))


if __name__ == '__main__':
    main()
