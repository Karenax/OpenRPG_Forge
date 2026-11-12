import unittest
import hashlib
import json
import tempfile
from pathlib import Path
from inventory_declared_prefab_hashes import prefab_pin, inventory


class PrefabPinTests(unittest.TestCase):
    def test_absolute_source_read_is_confined_to_vault(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp); vault=root/'vault'; (vault/'metadata').mkdir(parents=True); (vault/'pack').mkdir()
            inside=vault/'pack/asset.prefab'; inside.write_bytes(b'prefab')
            outside=root/'outside.prefab'; outside.write_bytes(b'outside')
            cards=[{'id':'hivemind.demo.a','source_prefab':str(inside)},
                   {'id':'hivemind.demo.b','source_prefab':str(outside)}]
            raw=json.dumps({'assets':cards}).encode(); (vault/'pack/details.v1.json').write_bytes(raw)
            registry=json.dumps({'packs':{'hivemind.demo':{'path':'pack','files':{'details.v1.json':hashlib.sha256(raw).hexdigest()}}}}).encode()
            (vault/'metadata/asset-import-registry.v1.json').write_bytes(registry)
            result=inventory(vault,hashlib.sha256(registry).hexdigest(),set(),True)
            self.assertEqual(result['unique_sources_read'],1)
            self.assertEqual(result['missing']['source_unavailable'],1)
            self.assertEqual(result['missing']['no_unambiguous_prefab_pin'],1)
            self.assertEqual([card['id'] for card in result['missing_cards']], ['hivemind.demo.b'])
            self.assertEqual(result['missing_cards'][0]['source_prefab'], str(outside))

    def test_only_unambiguous_prefab_pins_are_used(self):
        pin = 'a' * 64
        self.assertEqual(prefab_pin({'source_prefab':{'sha256':pin}},{}),pin)
        self.assertEqual(prefab_pin({}, {'source_prefab_sha256':pin}),pin)
        self.assertEqual(prefab_pin({}, {'source_support_provenance':{'source_file':'asset.prefab','source_sha256':pin}}),pin)
        self.assertIsNone(prefab_pin({}, {'source_support_provenance':{'source_file':'asset.fbx','source_sha256':pin}}))
        sources=[{'source_kind':'asset','original_source_path':'Assets/a.prefab','sha256':pin}]
        self.assertEqual(prefab_pin({'source_prefab':sources},{}),pin)
        self.assertIsNone(prefab_pin({'source_prefab':sources*2},{}))


if __name__ == '__main__': unittest.main()
