import unittest
from pathlib import Path

from stage_model_preview_relinks import change_preview, mapping_from_audit


class ModelPreviewStageTests(unittest.TestCase):
    def test_selects_only_verified_duplicates_in_same_directory(self):
        sha = 'a'*64
        rows = [{'path':r'F:\Vault\x\a.png','state':'verified','ids':['a']},
                {'path':r'F:\Vault\x\b.png','state':'verified','ids':['b']},
                {'path':r'F:\Vault\y\c.png','state':'verified','ids':['c']},
                {'path':r'F:\Vault\x\d.png','state':'unverified','ids':['d']}]
        result = mapping_from_audit({'groups':[{'sha256':sha,'paths':rows}]})
        self.assertEqual(len(result),1)
        self.assertEqual(next(iter(result.values()))['ids'],['b'])

    def test_rewrites_only_same_directory_and_matching_hash(self):
        root = Path(r'F:\Vault\pack')
        item = {'preview':'previews/b.png','preview_sha256':'pin'}
        change_preview(item, root/'previews/b.png', root/'previews/a.png', root, 'pin')
        self.assertEqual(item['preview'],'previews/a.png')
        with self.assertRaisesRegex(ValueError,'share a directory'):
            change_preview(item,root/'previews/a.png',root/'else/a.png',root,'pin')


if __name__ == '__main__':
    unittest.main()
