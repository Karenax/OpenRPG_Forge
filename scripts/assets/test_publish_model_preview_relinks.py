import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import vault_storage as storage
import publish_model_preview_relinks as module


class PublishPreviewTests(unittest.TestCase):
    def test_prefab_relink_requires_studio_pin_and_preserves_sources(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve(); vault=root/'vault'; stage=root/'stage'
            old_rev=vault/'metadata/asset-import-revisions/p'/('a'*64)
            old_rev.mkdir(parents=True); (vault/'.forge-storage').mkdir(); (vault/'data').mkdir(); (stage/'p').mkdir(parents=True)
            details=b'{"assets":[]}'; profiles=b'{"profiles":[]}'
            (old_rev/'details.v1.json').write_bytes(details); (old_rev/'profiles.v1.json').write_bytes(profiles)
            previous={'revision':'a'*64,'path':old_rev.relative_to(vault).as_posix(),'files':{'details.v1.json':storage.sha(details),'profiles.v1.json':storage.sha(profiles)}}
            registry=json.dumps({'schema_version':1,'packs':{'p':previous}}).encode()
            (vault/'metadata/asset-import-registry.v1.json').write_bytes(registry); (vault/'metadata/asset-source-support-registry.v1.json').write_bytes(b'{}')
            for name in ('old.prefab','keep.prefab'): (vault/'data'/name).write_bytes(b'prefab')
            catalog=vault/'data/new.json'; catalog.write_bytes(b'catalog')
            (stage/'p/details.v1.json').write_bytes(details); (stage/'p/profiles.v1.json').write_bytes(profiles)
            plan={'schema':'openrpg.model-prefab-relink-stage.v1','registry_sha256':storage.sha(registry),'source_registry_sha256':storage.sha(b'{}'),'changed_ids':['p.a'],
                  'catalogs':[{'new':str(catalog),'new_sha256':storage.sha(b'catalog')}],
                  'paths':[{'old':'data/old.prefab','retained':'data/keep.prefab','sha256':storage.sha(b'prefab')}],
                  'packs':{'p':{'revision':'b'*64,'details.v1.json':storage.sha(details),'profiles.v1.json':storage.sha(profiles)}}}
            (stage/'plan.json').write_text(json.dumps(plan),encoding='utf8')
            binary=root/'test.exe'; binary.write_bytes(b'test'); studio=root/'studio.exe'; studio.write_bytes(b'studio')
            proof={'state':'passed','plan_sha256':storage.sha((stage/'plan.json').read_bytes()),'native_test_sha256':storage.sha(b'test'),
                   'studio_binary_sha256':storage.sha(b'studio'),'registry_sha256':plan['registry_sha256'],
                   'source_registry_sha256':plan['source_registry_sha256'],'cards':1}
            proof_path=root/'proof.json'; proof_path.write_text(json.dumps(proof),encoding='utf8')
            sha=storage.sha(module.IMPORTER.read_bytes()); importer=module.load_importer(sha)
            with self.assertRaisesRegex(ValueError,'qualified Studio required'):
                module.publish(vault,stage,proof_path,binary,importer,sha,audit=root/'audit',candidates=root/'candidates')
            self.assertEqual((vault/'metadata/asset-import-registry.v1.json').read_bytes(),registry)
            with patch.object(module,'verify_prefab',return_value={'state':'stage_verified'}):
                result=module.publish(vault,stage,proof_path,binary,importer,sha,studio=studio,audit=root/'audit',candidates=root/'candidates')
            self.assertEqual(result['state'],'committed')
            self.assertTrue((vault/'data/old.prefab').exists()); self.assertTrue((vault/'data/keep.prefab').exists())

    def test_nested_archive_requires_pins_and_publishes_without_deleting(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();vault=root/'vault';stage=root/'stage';old_rev=vault/'metadata/asset-import-revisions/p'/('a'*64)
            old_rev.mkdir(parents=True);(vault/'.forge-storage').mkdir();(vault/'data').mkdir();(stage/'p').mkdir(parents=True)
            details=b'{"assets":[]}';profiles=b'{"profiles":[]}';(old_rev/'details.v1.json').write_bytes(details);(old_rev/'profiles.v1.json').write_bytes(profiles)
            previous={'revision':'a'*64,'path':old_rev.relative_to(vault).as_posix(),'files':{'details.v1.json':storage.sha(details),'profiles.v1.json':storage.sha(profiles)}}
            registry=json.dumps({'schema_version':1,'packs':{'p':previous}}).encode();(vault/'metadata/asset-import-registry.v1.json').write_bytes(registry);(vault/'metadata/asset-source-support-registry.v1.json').write_bytes(b'{}')
            for name in ('old.zip','keep.zip'):(vault/'data'/name).write_bytes(b'archive')
            catalog=vault/'data/new.json';catalog.write_bytes(b'catalog');(stage/'p/details.v1.json').write_bytes(details);(stage/'p/profiles.v1.json').write_bytes(profiles)
            plan={'schema':'openrpg.nested-archive-relink-stage.v1','registry_sha256':storage.sha(registry),'source_registry_sha256':storage.sha(b'{}'),'changed_ids':['p.a'],
                'catalogs':[{'new':str(catalog),'new_sha256':storage.sha(b'catalog')}],
                'archive_rows':[{'old':str(vault/'data/old.zip'),'retained':str(vault/'data/keep.zip'),'sha256':storage.sha(b'archive')}],
                'packs':{'p':{'revision':'b'*64,'details.v1.json':storage.sha(details),'profiles.v1.json':storage.sha(profiles)}}}
            (stage/'plan.json').write_text(json.dumps(plan),encoding='utf8');binary=root/'test.exe';binary.write_bytes(b'test')
            proof={'state':'passed','plan_sha256':storage.sha((stage/'plan.json').read_bytes()),'native_test_sha256':storage.sha(b'test'),
                   'registry_sha256':plan['registry_sha256'],'source_registry_sha256':plan['source_registry_sha256'],'cards':1}
            proof_path=root/'proof.json';proof_path.write_text(json.dumps(proof),encoding='utf8');sha=storage.sha(module.IMPORTER.read_bytes());importer=module.load_importer(sha)
            with self.assertRaisesRegex(ValueError,'audit and recovery required'):module.publish(vault,stage,proof_path,binary,importer,sha)
            with patch.object(module,'verify_nested_archive',return_value={'state':'stage_verified'}):
                result=module.publish(vault,stage,proof_path,binary,importer,sha,audit=root/'audit',recovery=root/'recovery')
            self.assertEqual(result['state'],'committed');self.assertTrue((vault/'data/old.zip').exists());self.assertTrue((vault/'data/keep.zip').exists())

    def test_commits_verified_snapshot_without_removing_images(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            vault, stage = root/'vault', root/'stage'
            old_rev = vault/'metadata/asset-import-revisions/p'/('a'*64)
            old_rev.mkdir(parents=True)
            (vault/'.forge-storage').mkdir()
            (vault/'data').mkdir()
            (stage/'p').mkdir(parents=True)
            old_details, old_profiles = b'{"assets":[]}', b'{"profiles":[]}'
            (old_rev/'details.v1.json').write_bytes(old_details)
            (old_rev/'profiles.v1.json').write_bytes(old_profiles)
            previous = {'revision':'a'*64,'path':old_rev.relative_to(vault).as_posix(),
                        'files':{'details.v1.json':storage.sha(old_details),'profiles.v1.json':storage.sha(old_profiles)}}
            registry_raw = json.dumps({'schema_version':1,'packs':{'p':previous}}).encode()
            (vault/'metadata/asset-import-registry.v1.json').write_bytes(registry_raw)
            (vault/'metadata/asset-source-support-registry.v1.json').write_bytes(b'{}')
            for name in ('old.png','keep.png'):
                (vault/'data'/name).write_bytes(b'image')
            (vault/'data/new-catalog.json').write_bytes(b'catalog')
            (stage/'p/details.v1.json').write_bytes(b'{"assets":[]}')
            (stage/'p/profiles.v1.json').write_bytes(b'{"profiles":[]}')
            plan = {'schema':'openrpg.model-preview-relink-stage.v1','registry_sha256':storage.sha(registry_raw),'source_registry_sha256':storage.sha(b'{}'),
                    'changed_ids':['p.a'], 'catalogs':[{'new':str(vault/'data/new-catalog.json'),
                    'new_sha256':storage.sha(b'catalog')}], 'candidate_paths':[{'old':str(vault/'data/old.png'),
                    'retained':str(vault/'data/keep.png'),'sha256':storage.sha(b'image')}],
                    'packs':{'p':{'revision':'b'*64,'details.v1.json':storage.sha(b'{"assets":[]}'),
                                   'profiles.v1.json':storage.sha(b'{"profiles":[]}')}}}
            (stage/'plan.json').write_text(json.dumps(plan),encoding='utf8')
            binary = root/'test.exe'
            binary.write_bytes(b'test')
            proof = {'state':'passed','stage_plan_sha256':storage.sha((stage/'plan.json').read_bytes()),
                     'native_test_sha256':storage.sha(b'test'),'registry_sha256':plan['registry_sha256'],
                     'source_registry_sha256':plan['source_registry_sha256'],'models':1}
            proof_path = root/'proof.json'
            proof_path.write_text(json.dumps(proof),encoding='utf8')
            sha = storage.sha(module.IMPORTER.read_bytes())
            importer = module.load_importer(sha)
            with patch.object(module,'verify',return_value={'state':'stage_verified'}):
                binary.write_bytes(b'changed')
                with self.assertRaisesRegex(ValueError,'proof or importer'):
                    module.publish(vault,stage,proof_path,binary,importer,sha)
                self.assertEqual((vault/'metadata/asset-import-registry.v1.json').read_bytes(),registry_raw)
                binary.write_bytes(b'test')
                result = module.publish(vault,stage,proof_path,binary,importer,sha)
            self.assertEqual(result['state'],'committed')
            new = json.loads((vault/'metadata/asset-import-registry.v1.json').read_bytes())
            self.assertEqual(new['packs']['p']['revision'],'b'*64)
            self.assertEqual(new['packs']['p']['history'][0]['revision'],'a'*64)
            self.assertEqual((vault/'data/old.png').read_bytes(),b'image')
            self.assertEqual((vault/'data/keep.png').read_bytes(),b'image')


if __name__ == '__main__':
    unittest.main()
