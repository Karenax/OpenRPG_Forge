import json
import tempfile
import unittest
from pathlib import Path
import vault_storage as storage
from plan_active_raw_variant_storage import prepare


class ActiveModelPlanTests(unittest.TestCase):
    def test_family_filter_and_primary_scope_exclude_other_assets(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp).resolve();(root/'metadata/revision').mkdir(parents=True);(root/'normalized').mkdir()
            a=root/'normalized/a.glb';b=root/'normalized/b.glb';c=root/'normalized/c.glb'
            for path in (a,b,c):path.write_bytes(b'glTF'+bytes(20))
            cards=[{'id':'hivemind.witch-house.model-a','model_path':str(a),'model_sha256':'a'*64},
                   {'id':'hivemind.witch-house.variant-abc.model-b','model_path':str(b),'model_sha256':'b'*64},
                   {'id':'hivemind.other.model-c','model_path':str(c),'model_sha256':'c'*64}]
            raw=json.dumps({'assets':cards}).encode();(root/'metadata/revision/details.v1.json').write_bytes(raw)
            (root/'metadata/asset-import-registry.v1.json').write_text(json.dumps({'packs':{'p':{'path':'metadata/revision',
                'files':{'details.v1.json':storage.sha(raw)}}}}),encoding='utf8')
            variant=prepare(root);self.assertEqual([x['asset_id'] for x in variant['files']],[cards[1]['id']])
            all_witch=prepare(root,families={'witch-house'},include_primary=True)
            self.assertEqual({x['asset_id'] for x in all_witch['files']},{cards[0]['id'],cards[1]['id']})


if __name__=='__main__':unittest.main()
