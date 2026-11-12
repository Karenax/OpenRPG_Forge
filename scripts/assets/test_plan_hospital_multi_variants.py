import csv
import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from plan_hospital_multi_variants import plan


class HospitalMultiTests(unittest.TestCase):
    def test_three_versions_become_two_pinned_aliases_and_exclude_pilot(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / 'metadata').mkdir()
            records, packs = [], {}
            for i, (ident, pipeline) in enumerate((('hivemind.h.main', 'hdrp'),
                    ('hivemind.h.variant-u.main', 'urp'), ('hivemind.h.variant-b.main', 'unknown'))):
                model = root / ('model%d.glb' % i)
                model.write_bytes(b'model')
                sha = str(i + 1) * 64
                record = [ident, 'p%d' % i, str(model), sha, pipeline]
                records.append(record)
                target = root / 'metadata' / ('p%d' % i)
                target.mkdir()
                detail = {'pack_id': record[1], 'assets': [{'id': ident, 'model_path': str(model), 'model_sha256': sha}]}
                profile = {'profiles': [{'external_id': ident, 'model_content_sha256': sha}]}
                files = {}
                for name, doc in [('details.v1.json', detail), ('profiles.v1.json', profile)]:
                    data = json.dumps(doc).encode()
                    (target / name).write_bytes(data)
                    files[name] = hashlib.sha256(data).hexdigest()
                packs[record[1]] = {'path': target.relative_to(root).as_posix(), 'files': files}
            registry = root / 'metadata/asset-import-registry.v1.json'
            registry.write_text(json.dumps({'packs': packs}))
            pin = hashlib.sha256(registry.read_bytes()).hexdigest()
            groups = root / 'groups.csv'
            with groups.open('w', encoding='utf-8-sig', newline='') as stream:
                writer = csv.DictWriter(stream, ['base_id', 'main_count', 'variant_count', 'records_json'])
                writer.writeheader()
                writer.writerow({'base_id': records[0][0], 'main_count': 1, 'variant_count': 2,
                                 'records_json': json.dumps(records)})
            comparisons = root / 'comparisons.jsonl'
            with comparisons.open('w') as stream:
                for old in records[1:]:
                    stream.write(json.dumps({'id': records[0][0] + '::' + old[0], 'status': 'compared',
                        'mesh_payloads_exact': True, 'transforms_exact': True,
                        'main_sha': records[0][3], 'variant_sha': old[3]}) + '\n')
            def recipe(vault, path, sha):
                return {'schema': 'openrpg.legacy-vault-descriptor.v1', 'logical_sha256': sha}
            with patch('vault_storage.confined', side_effect=lambda vault, path: path), \
                 patch('vault_storage.digest', return_value=records[0][3]), \
                 patch('plan_hospital_multi_variants.build_descriptor_recipe', side_effect=recipe):
                result, recipes = plan(root, groups, comparisons, pin, set())
                self.assertEqual(result['candidate_count'], 2)
                self.assertEqual(len({x['retained_id'] for x in result['candidates']}), 1)
                self.assertEqual(len(recipes['recipes']), 2)
                skipped, _ = plan(root, groups, comparisons, pin, {records[0][0]})
                self.assertEqual(skipped['candidate_count'], 0)


if __name__ == '__main__':
    unittest.main()
