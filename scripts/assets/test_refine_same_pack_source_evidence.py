import hashlib
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import refine_same_pack_source_evidence as audit


class SourceEvidenceTests(unittest.TestCase):
    def test_nested_source_pin_and_lod_change_are_checked(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp); vault = root/'vault'; work = root/'work'
            (vault/'metadata').mkdir(parents=True); (vault/'pack').mkdir(); (work/'demo/source/Assets').mkdir(parents=True)
            prefab = work/'demo/source/Assets/fern.prefab'
            prefab.write_text('--- !u!205 &42\nLODGroup:\n  m_Size: 1.5\n  m_LocalReferencePoint: {x: 0, y: 1, z: 0}\n  - screenRelativeHeight: 0.25\n  - screenRelativeHeight: 0.01\n', encoding='utf8')
            source_pin = hashlib.sha256(prefab.read_bytes()).hexdigest()
            entry = {'id':'hivemind.demo.fern', 'sha256':'a'*64,
                     'source_support_provenance':{'source_sha256':source_pin}}
            catalog = vault/'pack/catalog.json'; catalog.write_text(json.dumps({'assets':[entry]}), encoding='utf8')
            card = {'id':entry['id'], 'source_catalog':str(catalog), 'source_prefab':'Assets/fern.prefab',
                    'model_sha256':'a'*64, 'readiness':{'metadata_ready':False}}
            details = json.dumps({'assets':[card]}).encode(); (vault/'pack/details.v1.json').write_bytes(details)
            registry = json.dumps({'packs':{'hivemind.demo':{'path':'pack','files':{'details.v1.json':hashlib.sha256(details).hexdigest()}}}}).encode()
            (vault/'metadata/asset-import-registry.v1.json').write_bytes(registry)
            inventory = root/'inventory.json'; inventory.write_text(json.dumps({'registry_sha256':hashlib.sha256(registry).hexdigest()}),encoding='utf8')
            header = {'registry_sha256':hashlib.sha256(registry).hexdigest(), 'source_sha256':hashlib.sha256(inventory.read_bytes()).hexdigest()}
            group = {'pack':'hivemind.demo','source_filename':'fern','lod':0,'comparisons':[],
                     'cards':[{'id':entry['id'],'source_sha256':source_pin,'catalog_sha256':hashlib.sha256(catalog.read_bytes()).hexdigest(),'component_counts':{'1':1}}]}
            previous = root/'audit.jsonl'; previous.write_text('\n'.join(json.dumps(x) for x in (header,group,{'state':'complete'}))+'\n',encoding='utf8')
            with patch.object(audit,'WORK',work):
                result = audit.refine(vault,previous,inventory)
                self.assertEqual(result['groups'][0]['cards'][0]['lod_thresholds'],['0.25','0.01'])
                self.assertEqual(result['groups'][0]['cards'][0]['lod_group_bounds'],
                                 [{'size':'1.5', 'center':'{x: 0, y: 1, z: 0}'}])
                self.assertEqual(result['groups'][0]['cards'][0]['state'],'verified')
                prefab.write_text('  - screenRelativeHeight: 0.5\n',encoding='utf8')
                result = audit.refine(vault,previous,inventory)
                self.assertEqual(result['groups'][0]['cards'][0]['state'],'review_required')


if __name__ == '__main__': unittest.main()
