"""Measure source/private material changes; never authorize a migration or render."""
import datetime
import hashlib
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, 'F:/OpenRPG_AssetVault/cache/skill-dev-python')
import yaml

W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'hdrp-v4-material-migration-fields-v1'
PRIOR = W / 'hdrp-v4-failure-and-material-copy-analysis-v1/RESULT.v1.json'
HEADER = re.compile(r'^--- !u!(\d+) &(-?\d+)\s*$', re.MULTILINE)


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


class StrictLoader(yaml.SafeLoader):
    pass


def mapping(loader, node, deep=False):
    result = {}
    for key_node, value_node in node.value:
        key = loader.construct_object(key_node, deep=True)
        if key in result:
            raise ValueError(f'Duplicate YAML key: {key}')
        result[key] = loader.construct_object(value_node, deep=True)
    return result


StrictLoader.add_constructor(yaml.resolver.BaseResolver.DEFAULT_MAPPING_TAG, mapping)


def documents(path):
    raw = path.read_text('utf-8-sig')
    headers = HEADER.findall(raw)
    stripped = '\n'.join(line for line in raw.splitlines() if not line.startswith('%'))
    stripped = HEADER.sub('---', stripped)
    docs = list(yaml.load_all(stripped, Loader=StrictLoader))
    assert len(headers) == len(docs) and len(set(headers)) == len(headers)
    result = {f'{kind}:{file_id}': doc for (kind, file_id), doc in zip(headers, docs)}
    for doc in result.values():
        if 'Material' not in doc:
            continue
        saved = doc['Material'].get('m_SavedProperties', {})
        for category in ('m_TexEnvs', 'm_Ints', 'm_Floats', 'm_Colors'):
            values = saved.get(category, [])
            if not isinstance(values, list):
                raise ValueError(f'Expected property list: {category}')
            converted = {}
            for item in values:
                if not isinstance(item, dict) or len(item) != 1:
                    raise ValueError(f'Invalid material property: {category}')
                name, value = next(iter(item.items()))
                if name in converted:
                    raise ValueError(f'Duplicate material property: {category}/{name}')
                converted[name] = value
            if category in saved:
                saved[category] = converted
    return result


def differences(before, after, path=''):
    result = []
    if isinstance(before, dict) and isinstance(after, dict):
        for key in sorted(before.keys() | after.keys(), key=str):
            child = f'{path}/{key}'
            if key not in before:
                result.append(dict(path=child, kind='added', after=after[key]))
            elif key not in after:
                result.append(dict(path=child, kind='removed', before=before[key]))
            else:
                result.extend(differences(before[key], after[key], child))
    elif type(before) is not type(after) or before != after:
        result.append(dict(path=path, kind='changed', before=before, after=after))
    return result


def main():
    assert not ROOT.exists(), 'Preserve previous field analysis'
    prior = json.loads(PRIOR.read_text('utf-8-sig'))
    assert prior['materials'] == prior['original_materials_unchanged'] == 42
    pins = {str(PRIOR): sha(PRIOR), str(Path(__file__).resolve()): sha(__file__),
            str(Path(yaml.__file__)): sha(yaml.__file__)}
    rows = []
    for row in prior['rows']:
        original, private = Path(row['original']), Path(row['private_copy'])
        assert sha(original) == row['original_sha256']
        assert sha(private) == row['private_sha256'], 'Private copy changed since refusal analysis'
        meta = Path(str(original) + '.meta')
        assert sha(meta) == prior['input_pins'][str(meta)]
        pins.update({str(original): row['original_sha256'], str(private): row['private_sha256'],
                     str(meta): prior['input_pins'][str(meta)]})
        before, after = documents(original), documents(private)
        diff = differences(before, after)
        original_properties_changed = [d for d in diff if '/m_SavedProperties/' in d['path']
                                       and d['kind'] != 'added']
        properties_added = [d for d in diff if '/m_SavedProperties/' in d['path']
                            and d['kind'] == 'added']
        rows.append(dict(material_guid=row['material_guid'], original=str(original),
            private_copy=str(private), byte_identical=not row['private_copy_changed'],
            original_document_identities_retained=before.keys() == after.keys(),
            original_serialized_properties_unchanged=not original_properties_changed,
            original_property_changes=original_properties_changed, added_properties=properties_added,
            all_field_differences=diff, migration_accepted=False, gpu_verified=False))
    assert len(rows) == 42 and all(sha(p) == h for p, h in pins.items()), 'Inputs changed during analysis'
    result = dict(status='42_source_and_private_material_field_comparisons_retained',
        recorded_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), materials=42,
        byte_identical=sum(r['byte_identical'] for r in rows),
        materials_with_field_differences=sum(bool(r['all_field_differences']) for r in rows),
        materials_retaining_original_serialized_properties=sum(r['original_serialized_properties_unchanged'] for r in rows),
        added_property_occurrences=sum(len(r['added_properties']) for r in rows), rows=rows,
        input_pins=pins, input_pins_unchanged=True, sources_modified=False,
        migration_accepted=False, native_materials_verified=0, gpu_verified=False,
        studio_verified=False, full_import=False,
        next_action='Observe native values and effects of added properties, keyword changes and selected pipeline before any explicit private-copy migration policy; keep byte guards in existing controllers unchanged')
    payload = json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False)
    ROOT.mkdir()
    (ROOT / 'RESULT.v1.json').write_text(payload, 'utf-8')
    print(json.dumps({k: result[k] for k in ('status', 'materials', 'byte_identical',
        'materials_with_field_differences', 'materials_retaining_original_serialized_properties',
        'added_property_occurrences', 'migration_accepted', 'full_import')}))


if __name__ == '__main__':
    main()
