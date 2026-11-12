import hashlib
import sys
import tempfile
import unittest
from pathlib import Path
from test_compare_unity_light_prefabs import prefab

sys.path.insert(0, 'E:/CodexData/skills/openrpg-import-assets/scripts')
import source_support_import as api


class PipelineLightImporterTests(unittest.TestCase):
    def test_known_lights_are_validated_and_geometry_changes_are_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            vault=Path(temp).resolve();(vault/'originals').mkdir()
            def put(name,data):
                (vault/'originals'/name).write_bytes(data);return hashlib.sha256(data).hexdigest()
            first=prefab('11','7a68c43fe1f2a47cfa234b5eeaa98012','10').encode()
            second=prefab('12','474bcb49853aa07438625e644c072ee6','1').encode()
            hp=put('a.prefab',first);up=put('b.prefab',second);meta=put('a.meta',b'meta');archive=put('pack.unitypackage',b'archive')
            def card(ident,source,pin):
                return {'id':ident,'pack_id':'p','availability':'source_support','private_only':True,
                        'license':'LicenseRef-Unity-Asset-Store-EULA','names':{'fr':'Source'},'descriptions':{'fr':'Prefab'},
                        'taxonomy':{'type':'unity-prefab','family':'source-support','subtype':'item'},
                        'readiness':{'source_preserved':True,'metadata_ready':True,'converted':False,'studio_visible':True},
                        'tags':['source-support','not-placeable'],'source_file':'originals/'+source,'source_sha256':pin,
                        'source_meta_file':'originals/a.meta','source_meta_sha256':meta,
                        'source_archive':'originals/pack.unitypackage','source_archive_sha256':archive}
            original=card('p.old','a.prefab',hp);owner=card('p.keep','b.prefab',up)
            alias={'retired_id':'p.old','original_detail':original,'source_variation':{'kind':'pipeline_light_only'}}
            owner['consolidated_sources']=[alias];doc={'schema_version':1,'assets':[owner]}
            self.assertEqual(api.validate_catalog(doc,vault,set(),set())['validated'],1)
            owner['source_sha256']=put('b.prefab',second.replace(b'mesh: mesh',b'mesh: other'))
            with self.assertRaisesRegex(ValueError,'beyond permitted'):api.validate_catalog(doc,vault,set(),set())


if __name__=='__main__':unittest.main()
