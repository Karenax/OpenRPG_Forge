import json
import tempfile
import unittest
from pathlib import Path
import vault_storage as storage
from prepare_mesh_relink_candidates import prepare


class CandidatePreparationTests(unittest.TestCase):
    def test_rejects_unsupported_consumer_and_meta_reference(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);comp=root/'comparison.json';refs=root/'refs.json'
            pair={'hdrp':'h/a.fbx','urp':'u/a.fbx','same_mesh':True,'same_meta':True,
                  'hdrp_info':{'sha256':'a'*64,'bytes':10}}
            result={'source_registry_sha256':'p','results':[{'state':'identical_dependencies','dependencies':[pair]}]}
            comp.write_text(json.dumps(result),encoding='utf8')
            doc={'source_sha256':storage.sha(comp.read_bytes()),'source_registry_sha256':'p',
                 'paths':{'h/a.fbx':[{'field':'/assets/0/source_mesh_files/0',
                                       'document':'metadata/asset-source-support-revisions/a/catalog.json'}],
                          'h/a.fbx.meta':[]}}
            refs.write_text(json.dumps(doc),encoding='utf8')
            raw,count,size=prepare(comp,refs)
            self.assertEqual((count,size),(1,10));self.assertIn('h/a.fbx',raw)
            doc['paths']['h/a.fbx'][0]['field']='/assets/0/other/0'
            refs.write_text(json.dumps(doc),encoding='utf8')
            with self.assertRaisesRegex(ValueError,'unsupported active reference'):prepare(comp,refs)
            doc['paths']['h/a.fbx'][0]['field']='/assets/0/source_mesh_files/0'
            doc['paths']['h/a.fbx.meta']=[{'field':'/unexpected'}]
            refs.write_text(json.dumps(doc),encoding='utf8')
            with self.assertRaisesRegex(ValueError,'meta has active reference'):prepare(comp,refs)


if __name__=='__main__':unittest.main()
