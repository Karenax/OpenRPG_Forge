import hashlib
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, 'E:/CodexData/skills/openrpg-import-assets/scripts')
import source_support_import as api


class LodImporterTests(unittest.TestCase):
    def test_requires_evidence_and_rejects_non_lod_changes(self):
        with tempfile.TemporaryDirectory() as temp:
            vault=Path(temp).resolve();(vault/'originals').mkdir()
            def put(name,data):
                (vault/'originals'/name).write_bytes(data)
                return hashlib.sha256(data).hexdigest()
            first=put('a.prefab',b'm_LODs:\n  - screenRelativeHeight: 0.6\n    renderer: {fileID: 1}\n')
            second=put('b.prefab',b'm_LODs:\n  - screenRelativeHeight: 0.3\n    renderer: {fileID: 1}\n')
            meta=put('a.meta',b'meta');archive=put('pack.unitypackage',b'archive')
            def card(ident,source,pin):
                return {'id':ident,'pack_id':'p','availability':'source_support','private_only':True,
                        'license':'LicenseRef-Unity-Asset-Store-EULA','names':{'fr':'Source'},
                        'descriptions':{'fr':'Prefab'},'taxonomy':{'type':'unity-prefab','family':'source-support','subtype':'item'},
                        'readiness':{'source_preserved':True,'metadata_ready':True,'converted':False,'studio_visible':True},
                        'tags':['source-support','not-placeable'],'source_file':'originals/'+source,'source_sha256':pin,
                        'source_meta_file':'originals/a.meta','source_meta_sha256':meta,
                        'source_archive':'originals/pack.unitypackage','source_archive_sha256':archive}
            old=card('p.old','a.prefab',first);keep=card('p.keep','b.prefab',second)
            alias={'retired_id':'p.old','original_detail':old,'source_variation':{'kind':'lod_thresholds_only'}}
            keep['consolidated_sources']=[alias];doc={'schema_version':1,'assets':[keep]}
            self.assertEqual(api.validate_catalog(doc,vault,set(),set())['validated'],1)
            alias.pop('source_variation')
            with self.assertRaisesRegex(ValueError,'conflicting'):api.validate_catalog(doc,vault,set(),set())
            alias['source_variation']={'kind':'lod_thresholds_only'}
            keep['source_sha256']=put('b.prefab',b'm_LODs:\n  - screenRelativeHeight: 0.3\n    renderer: {fileID: 2}\n')
            with self.assertRaisesRegex(ValueError,'beyond thresholds'):api.validate_catalog(doc,vault,set(),set())


if __name__=='__main__':unittest.main()
