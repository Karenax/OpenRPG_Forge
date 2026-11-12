import unittest
from stage_nested_archive_catalog_relinks import replace_archive_paths, replace_catalog_paths


class NestedArchiveTests(unittest.TestCase):
    def test_archive_relink_preserves_metadata_and_rejects_wrong_sha(self):
        doc={'assets':[{'source_archive':'old/a.zip','source_archive_sha256':'pin','source_archive_bytes':7,'license':'private'}]}
        mapping={'old/a.zip':{'sha256':'pin','bytes':7,'new_absolute':'F:/Vault/new/a.zip','new_relative':'new/a.zip'}}
        hits={};replace_archive_paths(doc,mapping,hits)
        self.assertEqual(doc['assets'][0]['source_archive'],'new/a.zip');self.assertEqual(doc['assets'][0]['license'],'private')
        self.assertEqual(hits,{'pin':1})
        doc['assets'][0]['source_archive']='old/a.zip';doc['assets'][0]['source_archive_sha256']='wrong'
        with self.assertRaisesRegex(ValueError,'SHA'):replace_archive_paths(doc,mapping,{})

    def test_relinks_nested_catalog_without_changing_variant_identity(self):
        doc={'assets':[{'id':'main','consolidated_variants':[{'retired_id':'old','original_detail':{'source_catalog':'F:/old.json','license':'private'}}]}]}
        changed=[];replace_catalog_paths(doc,{'f:/old.json':{'new':'F:/new.json','old':'F:/old.json','old_sha256':'pin'}},changed)
        variant=doc['assets'][0]['consolidated_variants'][0]
        self.assertEqual(variant['original_detail']['source_catalog'],'F:/new.json');self.assertEqual(variant['retired_id'],'old')
        self.assertEqual(variant['original_detail']['license'],'private');self.assertEqual(changed,['F:/old.json'])
        self.assertEqual(variant['original_detail']['source_catalog_archive_origin'],{'path':'F:/old.json','sha256':'pin'})


if __name__=='__main__':unittest.main()
