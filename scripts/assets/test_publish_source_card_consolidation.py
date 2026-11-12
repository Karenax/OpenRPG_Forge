import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import vault_storage as storage
import publish_source_card_consolidation as publisher
from publish_source_support_relinks import IMPORTER, importer_module


class PublishAliasTests(unittest.TestCase):
    def test_prior_aliases_are_preserved_while_new_aliases_are_added(self):
        importer=importer_module(storage.sha(IMPORTER.read_bytes()))
        def card(keep,old):
            source={'id':old,'availability':'source_support','source_sha256':'a','source_meta_sha256':'b'}
            return {'id':keep,'source_sha256':'a','source_meta_sha256':'b',
                    'consolidated_sources':[{'retired_id':old,'original_detail':source}]}
        prior={'assets':[card('p.keep1','p.old1')]}
        future={'assets':[card('p.keep1','p.old1'),card('p.keep2','p.old2')]}
        self.assertEqual(publisher.verify_alias_transition(importer,prior,future,set(),[{'hdrp_id':'p.old2'}]),1)
        future['assets'][0]['consolidated_sources'][0]['original_detail']['name']='changed'
        with self.assertRaisesRegex(ValueError,'existing source alias changed'):
            publisher.verify_alias_transition(importer,prior,future,set(),[{'hdrp_id':'p.old2'}])

    def test_publication_requires_exact_reader_and_preserves_prior_revision(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            vault, stage = root/'vault', root/'stage'
            (vault/'metadata').mkdir(parents=True)
            stage.mkdir()
            old_catalog = {'schema_version':1, 'assets':[]}
            old_raw = json.dumps(old_catalog).encode()
            (vault/'metadata/catalog.json').write_bytes(old_raw)
            pointer = {'schema_version':1, 'path':'metadata/catalog.json',
                       'revision':storage.sha(old_raw), 'sha256':storage.sha(old_raw)}
            pointer_raw = json.dumps(pointer).encode()
            (vault/'metadata/asset-source-support-registry.v1.json').write_bytes(pointer_raw)
            models_raw = json.dumps({'packs':{}}).encode()
            (vault/'metadata/asset-import-registry.v1.json').write_bytes(models_raw)
            old = {'id':'p.old','availability':'source_support','source_sha256':'a','source_meta_sha256':'b'}
            kept = {'id':'p.keep','availability':'source_support','source_sha256':'a','source_meta_sha256':'b',
                    'consolidated_sources':[{'retired_id':'p.old','original_detail':old}]}
            staged = json.dumps({'schema_version':1,'assets':[kept]}).encode()
            (stage/'source-support.v1.json').write_bytes(staged)
            plan = {'source_registry_sha256':storage.sha(pointer_raw),'asset_registry_sha256':storage.sha(models_raw),
                    'new_catalog_sha256':storage.sha(staged),'pairs':[{'hdrp_id':'p.old'}]}
            (stage/'plan.json').write_text(json.dumps(plan), encoding='utf8')
            studio, test = root/'studio.exe', root/'test.exe'
            studio.write_bytes(b'studio')
            test.write_bytes(b'test')
            proof = {'state':'passed','stage_plan_sha256':storage.sha((stage/'plan.json').read_bytes()),
                     'stage_catalog_sha256':storage.sha(staged),'pairs':1,'studio_binary_sha256':storage.sha(b'studio'),
                     'native_test_sha256':storage.sha(b'test')}
            proof_path = root/'proof.json'
            proof_path.write_text(json.dumps(proof), encoding='utf8')
            source_sha = storage.sha(IMPORTER.read_bytes())
            model_sha = storage.sha(publisher.MODEL_IMPORTER.read_bytes())
            importer = importer_module(source_sha)
            with patch.object(publisher, 'prepare', return_value=(staged,plan)):
                studio.write_bytes(b'wrong')
                with self.assertRaisesRegex(ValueError, 'install qualified'):
                    publisher.publish(vault,stage,proof_path,studio,test,importer,source_sha,model_sha)
                self.assertEqual((vault/'metadata/asset-source-support-registry.v1.json').read_bytes(),pointer_raw)
                studio.write_bytes(b'studio')
                result = publisher.publish(vault,stage,proof_path,studio,test,importer,source_sha,model_sha)
            self.assertEqual(result['state'], 'committed')
            self.assertEqual(result['prior_pointer'], pointer)
            self.assertEqual((vault/'metadata/catalog.json').read_bytes(), old_raw)


if __name__ == '__main__':
    unittest.main()
