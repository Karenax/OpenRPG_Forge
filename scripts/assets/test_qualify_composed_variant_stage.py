import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, r'E:\CodexData\skills\openrpg-import-assets\scripts')
import asset_import
from publish_variant_transition import digest
from qualify_composed_variant_stage import qualify
from test_publish_variant_transition import setup_fixture


class ComposedQualificationTests(unittest.TestCase):
    def test_refusal_is_recorded_as_parity_not_as_success(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            vault, stage, plan, recipes, existing, _ = setup_fixture(root)
            plan['candidate_count'] = 1
            item = plan['candidates'][0]
            baseline = root / 'baseline.json'
            baseline.write_text(json.dumps({'schema': 'openrpg.variant-export-baseline.v1',
                'stage_manifest_sha256': digest(stage / 'transition.json'),
                'provenance': {'state': 'metadata_equivalence_only', 'sources': []},
                'exports': {item['retired_id']: {'sha256': item['retired_sha256'],
                    'outcome': 'error', 'error': 'missing license'}}}), encoding='utf8')
            recipe_doc = {'state': 'prepared_only', 'registry_sha256': plan['registry_sha256'], 'recipes': recipes}
            studio = Path(existing['studio'])
            receipt = qualify(vault, stage, plan, recipe_doc, baseline, studio, digest(studio), asset_import, 'Studio opened')
            self.assertEqual(receipt['checks']['native_legacy_export'], 'baseline_matched')
            self.assertEqual(receipt['evidence']['successful_exports'], 0)
            self.assertEqual(receipt['evidence']['matching_preexisting_failures'], 1)
            with self.assertRaisesRegex(ValueError, 'Studio differs'):
                qualify(vault, stage, plan, recipe_doc, baseline, studio, '0' * 64, asset_import, 'Studio opened')
            with self.assertRaisesRegex(ValueError, 'current descriptor differs'):
                qualify(vault, stage, plan, recipe_doc, baseline, studio, digest(studio), asset_import,
                        'Studio opened', defer_payload_recheck=True)
            document = json.loads(baseline.read_bytes())
            del document['provenance']
            baseline.write_text(json.dumps(document), encoding='utf8')
            log = root / 'native.log'
            log.write_text('Verified 1 real legacy references against staged snapshot; 0 exports succeeded, 1 reproduced existing failures\ntest result: ok. 2 passed; 0 failed')
            receipt = qualify(vault, stage, plan, recipe_doc, baseline, studio, digest(studio),
                              asset_import, 'Studio opened', native_log=log)
            self.assertEqual(receipt['evidence']['native_source_stages']['state'], 'direct_native_test')
            log.write_text('test result: ok. 2 passed; 0 failed')
            with self.assertRaisesRegex(ValueError, 'native stage log incomplete'):
                qualify(vault, stage, plan, recipe_doc, baseline, studio, digest(studio),
                        asset_import, 'Studio opened', native_log=log)


if __name__ == '__main__':
    unittest.main()
