import unittest
from stage_cross_root_preview_relinks import select


class CrossRootTests(unittest.TestCase):
    def test_selects_one_retained_path_without_duplicate_model_ids(self):
        inv={'groups':[{'sha256':'a'*64,'paths':[
            {'path':r'F:\Vault\a\one.png','models':[{'id':'a'}]},
            {'path':r'F:\Vault\b\two.png','models':[{'id':'b'}]},
            {'path':r'F:\Vault\c\three.png','models':[{'id':'c'}]}]}]}
        paths,ids=select(inv)
        self.assertEqual(len(paths),2)
        self.assertEqual(set(ids),{'b','c'})
        self.assertTrue(all(x['retained'].endswith('one.png') for x in paths.values()))

    def test_rejects_model_identity_reused_across_candidates(self):
        inv={'groups':[{'sha256':'a'*64,'paths':[
            {'path':r'F:\Vault\a\one.png','models':[{'id':'a'}]},
            {'path':r'F:\Vault\b\two.png','models':[{'id':'b'}]},
            {'path':r'F:\Vault\c\three.png','models':[{'id':'b'}]}]}]}
        with self.assertRaisesRegex(ValueError,'duplicate model identity'):
            select(inv)


if __name__=='__main__':
    unittest.main()
