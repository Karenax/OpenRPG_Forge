import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, r'E:\CodexData\skills\openrpg-import-assets\scripts')
candidate = Path(r'E:\OpenRPG_Forge\.build\asset-conversion\asset-import-guard-candidate-v65.py')
spec = importlib.util.spec_from_file_location('canonical_guard_candidate', candidate)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class CanonicalGuardTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.vault = Path(self.tmp.name).resolve()
        (self.vault/'metadata').mkdir()
        (self.vault/'current').mkdir()
        self.stage = self.vault/'stage'
        self.stage.mkdir()
        self.owner = {'id':'keep', 'consolidated_variants':[{'retired_id':'old', 'retired_model_sha256':'a'*64}]}
        data = module.encoded({'pack_id':'pack', 'assets':[self.owner]})
        (self.vault/'current/details.v1.json').write_bytes(data)
        (self.vault/'current/profiles.v1.json').write_bytes(module.encoded({'profiles':[]}))
        registry = {'schema_version':1,'packs':{'pack':{'path':'current','revision':'b'*64,
                    'files':{name:module.file_hash(self.vault/'current'/name) for name in ('details.v1.json','profiles.v1.json')}}}}
        (self.vault/'metadata/asset-import-registry.v1.json').write_bytes(module.encoded(registry))

    def prepare_stage(self, assets):
        details={'pack_id':'pack','assets':assets}
        profiles={'schema_version':1,'profiles':[]}
        (self.stage/'details.v1.json').write_bytes(module.encoded(details))
        (self.stage/'profiles.v1.json').write_bytes(module.encoded(profiles))
        plan={'schema_version':1,'pack_id':'pack','vault':str(self.vault),
              'revision':module.content_hash({'profiles':profiles,'details':details}),
              'expected_registry_sha256':module.file_hash(self.vault/'metadata/asset-import-registry.v1.json'),
              'inputs':{}, 'files':{name:module.file_hash(self.stage/name) for name in ('details.v1.json','profiles.v1.json')}}
        (self.stage/'plan.json').write_bytes(module.encoded(plan))

    def test_direct_apply_retains_aliases(self):
        self.prepare_stage([self.owner, {'id':'fresh'}])
        self.assertEqual(module.apply(self.vault,self.stage)['status'], 'applied')

    def test_direct_apply_rejects_reintroduced_historical_id(self):
        self.prepare_stage([self.owner, {'id':'old'}])
        with self.assertRaisesRegex(ValueError,'recreate a consolidated identity'):
            module.apply(self.vault,self.stage)

    def test_direct_apply_rejects_discarded_alias(self):
        self.prepare_stage([{'id':'keep'}])
        with self.assertRaisesRegex(ValueError,'discard consolidated'):
            module.apply(self.vault,self.stage)


if __name__ == '__main__':
    unittest.main()
