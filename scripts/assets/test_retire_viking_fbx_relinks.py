import unittest
from pathlib import Path
from retire_viking_fbx_relinks import validate_plan


class VikingRetirementTests(unittest.TestCase):
    def test_accepts_only_viking_source_and_refuses_overlap(self):
        root=Path('F:/OpenRPG_AssetVault')
        row={'old':str(root/'normalized/hivemind/modular-viking-village-a/sources/files/a.fbx'),
             'retained':str(root/'normalized/hivemind/other/sources/a.fbx'),'sha256':'a'*64,'bytes':10}
        plan={'schema':'openrpg.viking-fbx-relink-stage.v1','state':'prepared_only','paths':[row]}
        self.assertEqual(len(validate_plan(root,plan)),1)
        with self.assertRaisesRegex(ValueError,'duplicate'):
            validate_plan(root,{**plan,'paths':[row,row]})
        with self.assertRaisesRegex(ValueError,'outside'):
            validate_plan(root,{**plan,'paths':[{**row,'old':str(root/'other/a.fbx')}]})
        with self.assertRaisesRegex(ValueError,'retirement target'):
            second={**row,'old':row['retained'],'retained':row['old']}
            second['old']=str(root/'normalized/hivemind/modular-viking-village-b/sources/b.fbx')
            first={**row,'retained':second['old']}
            validate_plan(root,{**plan,'paths':[first,second]})


if __name__=='__main__':unittest.main()
