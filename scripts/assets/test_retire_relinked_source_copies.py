import contextlib
import json
import tempfile
import unittest
from pathlib import Path

import vault_storage as storage
from retire_relinked_source_copies import run, validate_scope, contains_reference, active_references


class FakeImporter:
    @staticmethod
    @contextlib.contextmanager
    def shared_import_lock(_vault):
        yield


class RetireTests(unittest.TestCase):
    def test_refuses_meta_retirement_while_unity_companion_remains(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve(); vault=root/'vault'; meta=vault/'metadata'; meta.mkdir(parents=True)
            (vault/'.forge-storage').mkdir(); base=vault/'originals/unity-purchased/hivemind/test';base.mkdir(parents=True)
            (base/'old.prefab').write_bytes(b'prefab');(base/'old.prefab.meta').write_bytes(b'meta');(base/'kept.meta').write_bytes(b'meta')
            catalog=b'{"assets":[]}';(meta/'catalog.json').write_bytes(catalog)
            source=json.dumps({'path':'metadata/catalog.json','sha256':storage.sha(catalog)}).encode();(meta/'asset-source-support-registry.v1.json').write_bytes(source)
            asset=b'{"packs":{}}';(meta/'asset-import-registry.v1.json').write_bytes(asset)
            binary=root/'binary';binary.write_bytes(b'binary')
            plan={'schema':'openrpg.source-support-relinks-stage.v1','state':'prepared_only','asset_registry_sha256':storage.sha(asset),
                  'staged_catalog_sha256':storage.sha(catalog),'paths':[{'old':'originals/unity-purchased/hivemind/test/old.prefab.meta',
                  'retained':'originals/unity-purchased/hivemind/test/kept.meta','sha256':storage.sha(b'meta')}]}
            plan_path=root/'plan.json';plan_path.write_text(json.dumps(plan),encoding='utf8')
            proof={'state':'passed','old_paths_locked':1,'binary_sha256':storage.sha(b'binary'),
                   'plan_sha256':storage.sha(plan_path.read_bytes()),'source_registry_sha256':storage.sha(source)}
            proof_path=root/'proof.json';proof_path.write_text(json.dumps(proof),encoding='utf8')
            with self.assertRaisesRegex(ValueError,'companion remains'):
                run(vault,plan_path,proof_path,binary,FakeImporter(),root/'journal',False)
            self.assertTrue((base/'old.prefab.meta').exists())

    def test_retirement_refuses_reference_in_nested_variant_catalog(self):
        with tempfile.TemporaryDirectory() as temp:
            vault=Path(temp).resolve();meta=vault/'metadata';meta.mkdir();snap=meta/'snapshot';snap.mkdir()
            olddir=vault/'normalized/pack';olddir.mkdir(parents=True)
            active=olddir/'active.json';nested=olddir/'legacy.json'
            active.write_text(json.dumps({'assets':[]}),encoding='utf8')
            nested.write_text(json.dumps({'archive':'sources/old.fbx'}),encoding='utf8')
            details={'assets':[{'source_catalog':str(active),'consolidated_variants':[
                {'original_detail':{'source_catalog':str(nested)}}]}]}
            dr=json.dumps(details).encode();pr=b'{}';(snap/'details.v1.json').write_bytes(dr);(snap/'profiles.v1.json').write_bytes(pr)
            source=b'{"assets":[]}';(meta/'source.json').write_bytes(source)
            (meta/'asset-source-support-registry.v1.json').write_text(json.dumps({'path':'metadata/source.json','sha256':storage.sha(source)}),encoding='utf8')
            (meta/'asset-import-registry.v1.json').write_text(json.dumps({'packs':{'p':{'path':'metadata/snapshot',
                'files':{'details.v1.json':storage.sha(dr),'profiles.v1.json':storage.sha(pr)}}}}),encoding='utf8')
            with self.assertRaisesRegex(ValueError,'promotion catalog references'):
                active_references(vault,{'normalized/pack/sources/old.fbx'})

    def test_refuses_catalog_relative_reference(self):
        vault=Path('F:/Vault');catalog=vault/'normalized/pack/catalog.json'
        targets={'normalized/pack/sources/example.fbx'}
        self.assertTrue(contains_reference({'source_fbx':'sources/example.fbx'},catalog,vault,targets))
        self.assertTrue(contains_reference({'path':'../pack/sources/example.fbx'},catalog,vault,targets))
        self.assertFalse(contains_reference({'source_fbx':'sources/other.fbx'},catalog,vault,targets))

    def test_refuses_package_root_relative_reference_from_nested_manifest(self):
        vault=Path('F:/OpenRPG_AssetVault')
        manifest=vault/'normalized/hivemind/example/sources/source-closure.v1.json'
        targets={'normalized/hivemind/example/sources/files/a/model.fbx'}
        self.assertTrue(contains_reference({'path':'sources/files/a/model.fbx'},manifest,vault,targets))
        self.assertFalse(contains_reference({'path':'sources/files/a/other.fbx'},manifest,vault,targets))

    def test_nested_record_paths_resolve_against_its_source_catalog(self):
        vault=Path('F:/OpenRPG_AssetVault')
        details=vault/'metadata/asset-import-revisions/pack/rev/details.v1.json'
        catalog=vault/'normalized/hivemind/example/catalog.json'
        targets={'normalized/hivemind/example/sources/files/a/model.fbx'}
        record={'consolidated_variants':[{'original_detail':{'source_catalog':str(catalog),
                 'source_fbx':[{'path':'sources/files/a/model.fbx'}]}}]}
        self.assertTrue(contains_reference(record,details,vault,targets))

    def test_extracted_unity_tree_is_refused_even_if_paths_are_identical(self):
        plan={'schema':'openrpg.source-support-relinks-stage.v1','state':'prepared_only',
              'paths':[{'old':'originals/unity-purchased/hivemind/test/source/Assets/A/x.fbx',
                        'retained':'originals/unity-purchased/hivemind/test/source/Assets/B/x.fbx',
                        'sha256':storage.sha(b'bytes')}]}
        with self.assertRaisesRegex(ValueError,'GUID-preserving'):
            validate_scope(Path('.'),plan,{},Path('unused'))
        plan['paths'][0]['old']='originals/unity-purchased/hivemind/test/source/base/Assets/A/x.fbx'
        with self.assertRaisesRegex(ValueError,'GUID-preserving'):
            validate_scope(Path('.'),plan,{},Path('unused'))

    def test_removal_keeps_identical_source_and_recovery_plan(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp).resolve()
            vault = root/'vault'
            (vault/'metadata/asset-source-support-transactions').mkdir(parents=True)
            (vault/'.forge-storage').mkdir()
            base = vault/'originals/unity-purchased/hivemind/test'
            base.mkdir(parents=True)
            (base/'old.prefab').write_bytes(b'original')
            (base/'keep.prefab').write_bytes(b'original')
            catalog_raw = json.dumps({'assets': []}).encode()
            (vault/'metadata/catalog.json').write_bytes(catalog_raw)
            source_raw = json.dumps({'path':'metadata/catalog.json','sha256':storage.sha(catalog_raw)}).encode()
            (vault/'metadata/asset-source-support-registry.v1.json').write_bytes(source_raw)
            asset_raw = json.dumps({'packs': {}}).encode()
            (vault/'metadata/asset-import-registry.v1.json').write_bytes(asset_raw)
            binary = root/'binary'
            binary.write_bytes(b'binary')
            plan = {'schema':'openrpg.source-support-relinks-stage.v1','state':'prepared_only',
                    'asset_registry_sha256':storage.sha(asset_raw),'staged_catalog_sha256':storage.sha(catalog_raw),
                    'paths':[{'old':'originals/unity-purchased/hivemind/test/old.prefab',
                              'retained':'originals/unity-purchased/hivemind/test/keep.prefab',
                              'sha256':storage.sha(b'original')}]}
            plan_path = root/'plan.json'
            plan_path.write_text(json.dumps(plan), encoding='utf8')
            proof = {'state':'passed','old_paths_locked':1,'binary_sha256':storage.sha(b'binary'),
                     'plan_sha256':storage.sha(plan_path.read_bytes()), 'source_registry_sha256':storage.sha(source_raw)}
            proof_path = root/'proof.json'
            proof_path.write_text(json.dumps(proof), encoding='utf8')
            result = run(vault,plan_path,proof_path,binary,FakeImporter(),root/'journal.jsonl',True)
            self.assertEqual(result['removed'], 1)
            self.assertFalse((base/'old.prefab').exists())
            self.assertEqual((base/'keep.prefab').read_bytes(), b'original')
            self.assertEqual(Path(result['recovery']).read_bytes(), plan_path.read_bytes())


if __name__ == '__main__':
    unittest.main()
