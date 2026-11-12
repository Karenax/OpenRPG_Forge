import hashlib
import tempfile
import unittest
from pathlib import Path

from materialize_source_support_asset import materialize,select_card


class MaterializeSourceTests(unittest.TestCase):
    def test_separately_stored_meta_reconstructed_beside_prefab(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();vault=root/'vault';vault.mkdir();out=root/'out';out.mkdir()
            (vault/'payload').write_bytes(b'prefab');(vault/'shared-meta').write_bytes(b'guid: 123')
            card={'id':'p.a','source_asset_path':'Assets/Pack/Prefab.prefab','source_file':'payload',
                  'source_sha256':hashlib.sha256(b'prefab').hexdigest(),'source_meta_file':'shared-meta',
                  'source_meta_sha256':hashlib.sha256(b'guid: 123').hexdigest()}
            result=materialize(vault,card,out)
            self.assertEqual(Path(result['asset']).read_bytes(),b'prefab')
            self.assertEqual(Path(result['meta']).read_bytes(),b'guid: 123')
            with self.assertRaisesRegex(ValueError,'already exists'):materialize(vault,card,out)

    def test_hash_mismatch_removes_partial_output_and_alias_is_preserved(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();vault=root/'vault';vault.mkdir();out=root/'out';out.mkdir()
            (vault/'payload').write_bytes(b'prefab');(vault/'meta').write_bytes(b'incorrect')
            card={'id':'old','source_asset_path':'Assets/Pack/Prefab.prefab','source_file':'payload',
                  'source_sha256':hashlib.sha256(b'prefab').hexdigest(),'source_meta_file':'meta',
                  'source_meta_sha256':hashlib.sha256(b'expected').hexdigest()}
            self.assertEqual(select_card({'assets':[{'id':'new','consolidated_sources':[{'retired_id':'old','original_detail':card}]}]},'old'),card)
            with self.assertRaisesRegex(ValueError,'SHA differs'):materialize(vault,card,out)
            self.assertFalse((out/'Assets/Pack/Prefab.prefab').exists())
            card['source_asset_path']='Assets/../../escape.prefab'
            with self.assertRaisesRegex(ValueError,'escapes'):materialize(vault,card,out)


if __name__=='__main__':unittest.main()
