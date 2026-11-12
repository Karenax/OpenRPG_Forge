import hashlib
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0,'E:/CodexData/skills/openrpg-import-assets/scripts')
import source_support_import as api


class ImporterLodColliderTests(unittest.TestCase):
    def test_requires_validated_lod_and_known_collider_changes(self):
        with tempfile.TemporaryDirectory() as temp:
            vault=Path(temp).resolve();(vault/'originals').mkdir()
            def put(name,data):
                (vault/'originals'/name).write_bytes(data)
                return hashlib.sha256(data).hexdigest()
            old=b'--- !u!205 &1\nLODGroup:\n  m_Size: 1\n--- !u!1 &2\nGameObject:\n  m_Name: Collider\n  mesh: stable\n'
            new=old.replace(b'm_Size: 1',b'm_Size: 2').replace(b'm_Name: Collider',b'm_Name: BoxColliderObject')
            hp=put('a.prefab',old);up=put('b.prefab',new);meta=put('a.meta',b'meta');archive=put('pack.unitypackage',b'archive')
            def card(ident,source,pin):
                return {'id':ident,'pack_id':'p','availability':'source_support','private_only':True,
                        'license':'LicenseRef-Unity-Asset-Store-EULA','names':{'fr':'Source'},'descriptions':{'fr':'Prefab'},
                        'taxonomy':{'type':'unity-prefab','family':'source-support','subtype':'item'},
                        'readiness':{'source_preserved':True,'metadata_ready':True,'converted':False,'studio_visible':True},
                        'tags':['source-support','not-placeable'],'source_file':'originals/'+source,'source_sha256':pin,
                        'source_meta_file':'originals/a.meta','source_meta_sha256':meta,
                        'source_archive':'originals/pack.unitypackage','source_archive_sha256':archive}
            original=card('p.old','a.prefab',hp);owner=card('p.keep','b.prefab',up)
            alias={'retired_id':'p.old','original_detail':original,'source_variation':{'kind':'lod_collider_only'}}
            owner['consolidated_sources']=[alias];doc={'schema_version':1,'assets':[owner]}
            self.assertEqual(api.validate_catalog(doc,vault,set(),set())['validated'],1)
            alias['source_variation']={'kind':'unknown'}
            with self.assertRaisesRegex(ValueError,'conflicting'):api.validate_catalog(doc,vault,set(),set())
            alias['source_variation']={'kind':'lod_collider_only'}
            owner['source_sha256']=put('b.prefab',new.replace(b'mesh: stable',b'mesh: changed'))
            with self.assertRaisesRegex(ValueError,'beyond permitted'):api.validate_catalog(doc,vault,set(),set())


if __name__=='__main__':unittest.main()
