import unittest
from pathlib import Path
from stage_viking_fbx_relinks import replace_source_paths,replace_catalog_paths


class VikingRelinkTests(unittest.TestCase):
    def test_relinks_only_matching_pinned_asset_and_retains_metadata(self):
        vault=Path('F:/OpenRPG_AssetVault');doc=vault/'normalized/hivemind/pack/catalog.json'
        old=str(vault/'normalized/hivemind/pack/sources/a.fbx').replace('\\','/').casefold()
        mapping={old:{'retained':str(vault/'normalized/hivemind/other/a.fbx'),'sha256':'a'*64}}
        obj={'source_fbx':[{'path':'sources/a.fbx','sha256':'a'*64,'source_kind':'asset','fileID':42}]}
        hits={};replace_source_paths(obj,doc,vault,mapping,hits)
        self.assertEqual(obj['source_fbx'][0]['path'],mapping[old]['retained'])
        self.assertEqual(obj['source_fbx'][0]['fileID'],42)
        self.assertEqual(hits,{old:1})
        with self.assertRaisesRegex(ValueError,'SHA differs'):
            replace_source_paths({'path':'sources/a.fbx','sha256':'wrong','source_kind':'asset'},doc,vault,mapping,{})

    def test_nested_catalog_redirect_preserves_prior_record(self):
        old='F:\\vault\\catalog.json';new='F:\\vault\\openrpg-prefab-test.json'
        catalogs={old.casefold():{'old':old,'new':new,'old_sha256':'a'*64}}
        doc={'consolidated_variants':[{'original_detail':{'source_catalog':old}}]};changed=[]
        replace_catalog_paths(doc,catalogs,changed)
        record=doc['consolidated_variants'][0]['original_detail']
        self.assertEqual(record['source_catalog'],new)
        self.assertEqual(record['source_catalog_prefab_origin'],{'path':old,'sha256':'a'*64})
        self.assertEqual(changed,[old])

    def test_nested_source_fbx_follows_its_catalog_root(self):
        vault=Path('F:/OpenRPG_AssetVault');old=str(vault/'normalized/pack/catalog.json')
        candidate=str(vault/'normalized/pack/sources/a.fbx').replace('\\','/').casefold()
        mapping={candidate:{'retained':str(vault/'normalized/other/a.fbx'),'sha256':'a'*64}}
        catalogs={old.casefold():{'old':old,'new':str(vault/'normalized/pack/new.json'),'old_sha256':'b'*64}}
        doc={'original_detail':{'source_catalog':old,'source_fbx':[{'path':'sources/a.fbx',
             'sha256':'a'*64,'source_kind':'asset'}]}}
        changed=[];hits={};replace_catalog_paths(doc,catalogs,changed,vault,mapping,hits)
        self.assertEqual(doc['original_detail']['source_fbx'][0]['path'],mapping[candidate]['retained'])
        self.assertEqual(hits,{candidate:1})


if __name__=='__main__':unittest.main()
