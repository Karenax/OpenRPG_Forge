import json
import tempfile
import unittest
from pathlib import Path
import vault_storage as storage
from verify_unity_overlay_guids import verify
from unity_shared_overlay import UnitySourceResolver


class UnityOverlayGuidTests(unittest.TestCase):
    def test_guid_audit_reads_shared_sources_without_original_copy(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();vault=root/'vault';(vault/'old/HDRP/Art').mkdir(parents=True)
            (vault/'shared').mkdir();guid='0123456789abcdef0123456789abcdef'
            meta=('guid: '+guid+'\n').encode();material=('texture: {guid: '+guid+'}\n').encode()
            (vault/'shared/a.png.meta').write_bytes(meta);(vault/'old/HDRP/Art/a.mat').write_bytes(material)
            manifest={'schema':'openrpg.unity-shared-overlay.v1','comparison_sha256':'a'*64,'source_root':'old/HDRP',
                'files':[{'logical':'Art/a.png.meta','vault_path':'shared/a.png.meta','sha256':storage.sha(meta),'bytes':len(meta)},
                         {'logical':'Art/a.mat','vault_path':'old/HDRP/Art/a.mat','sha256':storage.sha(material),'bytes':len(material)}]}
            audit={'comparison_sha256':'a'*64,'images':[{'hdrp':'old/HDRP/Art/a.png','guid':guid,
                'references':[{'side':'hdrp','path':'old/HDRP/Art/a.mat'}]}]}
            mp=root/'manifest.json';ap=root/'audit.json';mp.write_text(json.dumps(manifest));ap.write_text(json.dumps(audit))
            resolver=UnitySourceResolver(vault,mp,storage.sha(mp.read_bytes()))
            self.assertEqual(verify(mp,ap,resolver=resolver)['guid_file_references'],1)
            (vault/'shared/a.png.meta').write_bytes(b'bad')
            with self.assertRaisesRegex(ValueError,'payload changed'):verify(mp,ap,resolver=resolver)

    def test_detects_modified_guid_reference(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);tree=root/'export';(tree/'Art').mkdir(parents=True)
            guid='0123456789abcdef0123456789abcdef';meta=('guid: '+guid+'\n').encode()
            material=('texture: {guid: '+guid+'}\n').encode()
            (tree/'Art/a.png.meta').write_bytes(meta);(tree/'Art/a.mat').write_bytes(material)
            comparison_sha='a'*64
            manifest={'comparison_sha256':comparison_sha,'source_root':'old/HDRP',
                      'files':[{'logical':'Art/a.png.meta','sha256':storage.sha(meta)},
                               {'logical':'Art/a.mat','sha256':storage.sha(material)}]}
            audit={'comparison_sha256':comparison_sha,'images':[{'hdrp':'old/HDRP/Art/a.png',
                'guid':guid,'references':[{'side':'hdrp','path':'old/HDRP/Art/a.mat'}]}]}
            mp=root/'manifest.json';ap=root/'audit.json'
            mp.write_text(json.dumps(manifest),encoding='utf8');ap.write_text(json.dumps(audit),encoding='utf8')
            self.assertEqual(verify(mp,ap,tree)['guid_file_references'],1)
            (tree/'Art/a.mat').write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError,'text differs'):verify(mp,ap,tree)


if __name__=='__main__':unittest.main()
