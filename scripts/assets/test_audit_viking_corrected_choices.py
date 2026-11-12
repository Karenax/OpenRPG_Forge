import copy
import hashlib
from pathlib import Path
import tempfile
import unittest
from audit_viking_corrected_choices import check,PINS


class VikingChoicesTests(unittest.TestCase):
    def test_pinned_model_and_source_and_dimensions(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);cat=root/'catalog.json';cat.write_text('{}');source=root/'source.prefab';source.write_bytes(b'prefab')
            entry={'path':'source.prefab','sha256':hashlib.sha256(b'prefab').hexdigest()}
            main={'id':'hivemind.modular-viking-village.prefab-sm-housebuilding-001-a-x',
                  'model_sha256':PINS['001'],'license':'private','source_prefab':[entry],
                  'measurement':{'dimensions':[1,2,3],'triangles':4}}
            old={'license':'private','source_prefab':[entry],'source_catalog':str(cat),
                 'measurement':{'dimensions':[1,2,3],'triangles':4}}
            self.assertEqual(check(main,old,root)['keep_side'],'main')
            other=copy.deepcopy(main);other['model_sha256']='0'*64
            with self.assertRaises(ValueError):check(other,old,root)
            other=copy.deepcopy(old);other['measurement']['dimensions'][0]=5
            with self.assertRaises(ValueError):check(main,other,root)


if __name__=='__main__':unittest.main()
