import importlib.util
import sys
import unittest
from pathlib import Path

sys.path.insert(0,r'E:\CodexData\skills\openrpg-import-assets\scripts')
path=Path(r'E:\OpenRPG_Forge\.build\asset-conversion\source-support-import-distinct-preview-20261112.py')
spec=importlib.util.spec_from_file_location('distinct_source_preview_guard',path)
api=importlib.util.module_from_spec(spec);spec.loader.exec_module(api)


class DistinctSourcePreviewTests(unittest.TestCase):
    def test_requires_explicit_flag_and_preserves_identity_checks(self):
        old={'id':'p.old','availability':'source_support','source_sha256':'s',
             'source_meta_sha256':'m','preview_sha256':'old'}
        alias={'retired_id':'p.old','original_detail':old}
        card={'id':'p.new','source_sha256':'s','source_meta_sha256':'m',
              'preview_sha256':'new','consolidated_sources':[alias]}
        with self.assertRaises(ValueError):api.source_alias_ids({'assets':[card]},set())
        alias['preserve_distinct_preview']=True
        self.assertEqual(api.source_alias_ids({'assets':[card]},set()),{'p.old'})
        old['source_sha256']='different'
        with self.assertRaises(ValueError):api.source_alias_ids({'assets':[card]},set())


if __name__=='__main__':unittest.main()
