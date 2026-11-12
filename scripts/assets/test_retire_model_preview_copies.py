import contextlib
import json
import tempfile
import unittest
from pathlib import Path

import vault_storage as storage
from retire_model_preview_copies import run


class FakeImporter:
    def __init__(self, file, target):
        self.__file__=str(file)
        self.target=target

    @contextlib.contextmanager
    def locked(self, _):
        yield

    def redirected_preview_catalog(self, _vault, _old, _pin):
        return self.target


class RetirePreviewTests(unittest.TestCase):
    def test_verified_retirement_preserves_image_and_recovery_mapping(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();vault=root/'vault';stage=root/'stage'
            (vault/'metadata/rev').mkdir(parents=True)
            (vault/'metadata/asset-source-support-transactions').mkdir()
            (vault/'.forge-storage').mkdir()
            pack=vault/'normalized/hivemind/test';(pack/'previews').mkdir(parents=True)
            stage.mkdir()
            old,keep=pack/'previews/old.png',pack/'previews/keep.png'
            old.write_bytes(b'png');keep.write_bytes(b'png')
            old_cat,new_cat=pack/'old.json',pack/'new.json'
            old_cat.write_bytes(b'old catalog')
            new_cat.write_text(json.dumps({'assets':[{'id':'p.a','sha256':'model',
                'model':'models/a.glb','preview':'previews/keep.png'}]}),encoding='utf8')
            details={'assets':[{'id':'p.a','model_path':str(pack/'models/a.glb'),
                     'model_sha256':'model','source_catalog':str(new_cat)}]}
            dr=json.dumps(details).encode();pr=b'{"profiles":[]}'
            (vault/'metadata/rev/details.v1.json').write_bytes(dr)
            (vault/'metadata/rev/profiles.v1.json').write_bytes(pr)
            reg={'packs':{'p':{'path':'metadata/rev','revision':'a'*64,
                  'files':{'details.v1.json':storage.sha(dr),'profiles.v1.json':storage.sha(pr)}}}}
            rb=json.dumps(reg).encode();(vault/'metadata/asset-import-registry.v1.json').write_bytes(rb)
            sr=b'{"assets":[]}';(vault/'metadata/source.json').write_bytes(sr)
            sb=json.dumps({'path':'metadata/source.json','sha256':storage.sha(sr)}).encode()
            (vault/'metadata/asset-source-support-registry.v1.json').write_bytes(sb)
            plan={'schema':'openrpg.model-preview-relink-stage.v1','catalogs':[{'old':str(old_cat),'new':str(new_cat)}],
                  'candidate_paths':[{'old':str(old),'retained':str(keep),'sha256':storage.sha(b'png'),'ids':['p.a']}]}
            plan_path=root/'plan.json';plan_path.write_text(json.dumps(plan),encoding='utf8')
            redirect={'schema':'openrpg.model-catalog-redirect-stage.v1','prior_preview_plan_sha256':storage.sha(plan_path.read_bytes()),
                      'candidate_paths':plan['candidate_paths'],'packs':{'p':{'revision':'a'*64}}}
            (stage/'plan.json').write_text(json.dumps(redirect),encoding='utf8')
            binary=root/'binary';binary.write_bytes(b'binary')
            fake_file=root/'importer.py';fake_file.write_bytes(b'importer')
            proof={'state':'passed','plan_sha256':storage.sha(plan_path.read_bytes()),
                   'redirect_plan_sha256':storage.sha((stage/'plan.json').read_bytes()),'old_paths_locked':1,
                   'models':1,'native_test_sha256':storage.sha(b'binary'),'registry_sha256':storage.sha(rb),
                   'source_registry_sha256':storage.sha(sb)}
            proof_path=root/'proof.json';proof_path.write_text(json.dumps(proof),encoding='utf8')
            result=run(vault,plan_path,stage,proof_path,binary,FakeImporter(fake_file,new_cat),
                       storage.sha(b'importer'),root/'journal.jsonl',True)
            self.assertEqual(result['removed'],1)
            self.assertFalse(old.exists())
            self.assertEqual(keep.read_bytes(),b'png')
            self.assertEqual(json.loads(Path(result['recovery']).read_bytes())['paths'][0]['sha256'],storage.sha(b'png'))


if __name__=='__main__':
    unittest.main()
