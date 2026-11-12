import json
import tempfile
import unittest
from pathlib import Path

import publish_assets
import vault_storage as storage


class ConsolidatedGuardTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.vault = Path(self.temp.name).resolve()
        (self.vault/'metadata').mkdir()
        (self.vault/'pack').mkdir()
        self.stage = self.vault/'stage'
        self.stage.mkdir()
        self.alias = {'retired_id':'old', 'retired_model_sha256':'a'*64}
        self.current = {'pack_id':'pack', 'assets':[{'id':'keep', 'consolidated_variants':[self.alias]}]}
        detail = json.dumps(self.current).encode()
        (self.vault/'pack/details.v1.json').write_bytes(detail)
        registry = json.dumps({'packs':{'pack':{'path':'pack','files':{'details.v1.json':storage.sha(detail)}}}}).encode()
        (self.vault/'metadata/asset-import-registry.v1.json').write_bytes(registry)
        self.registry_sha = storage.sha(registry)

    def stage_doc(self, assets):
        detail = json.dumps({'pack_id':'pack','assets':assets}).encode()
        (self.stage/'details.v1.json').write_bytes(detail)
        (self.stage/'plan.json').write_text(json.dumps({'pack_id':'pack','expected_registry_sha256':self.registry_sha,
                                                        'files':{'details.v1.json':storage.sha(detail)}}))

    def test_preserving_alias_succeeds(self):
        self.stage_doc(self.current['assets'] + [{'id':'fresh'}])
        self.assertEqual(publish_assets.check_consolidated_identities(self.vault,self.stage)['historical_ids'],1)

    def test_reintroducing_retired_id_is_rejected(self):
        self.stage_doc(self.current['assets'] + [{'id':'old'}])
        with self.assertRaisesRegex(ValueError,'recreate or duplicate'):
            publish_assets.check_consolidated_identities(self.vault,self.stage)

    def test_dropping_or_changing_alias_is_rejected(self):
        for assets in ([{'id':'keep'}], [], [{'id':'keep','consolidated_variants':[{'retired_id':'old'}]}]):
            self.stage_doc(assets)
            with self.assertRaisesRegex(ValueError,'discard consolidated'):
                publish_assets.check_consolidated_identities(self.vault,self.stage)

    def test_stale_registry_is_rejected(self):
        self.stage_doc(self.current['assets'])
        (self.vault/'metadata/asset-import-registry.v1.json').write_text('{}')
        with self.assertRaisesRegex(ValueError,'Registry changed'):
            publish_assets.check_consolidated_identities(self.vault,self.stage)


if __name__ == '__main__':
    unittest.main()
