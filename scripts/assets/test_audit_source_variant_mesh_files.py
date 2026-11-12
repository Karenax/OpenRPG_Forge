import json
import tempfile
import unittest
from pathlib import Path
import vault_storage as storage
from audit_source_variant_mesh_files import audit
from audit_mesh_candidate_active_references import scan


class MeshVariantAuditTests(unittest.TestCase):
    def test_pinned_mesh_and_meta_comparison(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();(root/'metadata').mkdir()
            for name in ('a','b'):
                (root/(name+'.fbx')).write_bytes(b'mesh')
                (root/(name+'.fbx.meta')).write_bytes(b'guid')
            catalog={'assets':[{'id':'h','source_mesh_files':['a.fbx']},
                               {'id':'u','source_mesh_files':['b.fbx']}]}
            raw=json.dumps(catalog).encode();(root/'metadata/source.json').write_bytes(raw)
            pointer={'path':'metadata/source.json','sha256':storage.sha(raw)}
            pointer_raw=json.dumps(pointer).encode();(root/'metadata/asset-source-support-registry.v1.json').write_bytes(pointer_raw)
            pair={'hdrp_id':'h','urp_id':'u','declared_source_sha_equal':True,'declared_meta_sha_equal':True,
                  'different_fields':['preview_sha256','source_mesh_files']}
            inv=root/'inventory.json';inv.write_text(json.dumps({'source_registry_sha256':storage.sha(pointer_raw),'pairs':[pair]}),encoding='utf8')
            self.assertEqual(audit(root,inv)['results'][0]['state'],'identical_dependencies')
            (root/'b.fbx.meta').write_bytes(b'other-guid')
            self.assertEqual(audit(root,inv)['results'][0]['state'],'different_dependencies')
            (root/'b.fbx.meta').unlink()
            self.assertEqual(audit(root,inv)['results'][0]['state'],'review_required')

    def test_single_mesh_dependency_is_compared(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();(root/'metadata').mkdir()
            for name in ('a','b'):
                (root/(name+'.fbx')).write_bytes(b'mesh');(root/(name+'.fbx.meta')).write_bytes(b'guid')
            raw=json.dumps({'assets':[{'id':'h','source_mesh_file':'a.fbx'},
                                      {'id':'u','source_mesh_file':'b.fbx'}]}).encode()
            (root/'metadata/source.json').write_bytes(raw)
            pointer_raw=json.dumps({'path':'metadata/source.json','sha256':storage.sha(raw)}).encode()
            (root/'metadata/asset-source-support-registry.v1.json').write_bytes(pointer_raw)
            pair={'hdrp_id':'h','urp_id':'u','declared_source_sha_equal':True,'declared_meta_sha_equal':True,
                  'different_fields':['preview_sha256','source_mesh_file','taxonomy']}
            inv=root/'inventory.json';inv.write_text(json.dumps({'source_registry_sha256':storage.sha(pointer_raw),'pairs':[pair]}),encoding='utf8')
            self.assertEqual(audit(root,inv,singular=True)['results'][0]['state'],'identical_dependencies')

    def test_all_meshes_compares_only_corresponding_unity_paths(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();(root/'metadata').mkdir()
            names=['base/source/Assets/Pack/HDRP(Default)/Art/a.fbx','variant/source/Assets/Pack/URP/Art/a.fbx']
            for name in names:
                path=root/name;path.parent.mkdir(parents=True);path.write_bytes(b'mesh')
                path.with_name(path.name+'.meta').write_bytes(b'guid')
            raw=json.dumps({'assets':[{'id':'h','source_mesh_file':names[0]},
                                      {'id':'u','source_mesh_file':names[1]}]}).encode()
            (root/'metadata/source.json').write_bytes(raw)
            ptr=json.dumps({'path':'metadata/source.json','sha256':storage.sha(raw)}).encode()
            (root/'metadata/asset-source-support-registry.v1.json').write_bytes(ptr)
            inv=root/'inventory.json';inv.write_text(json.dumps({'source_registry_sha256':storage.sha(ptr),
                'pairs':[{'hdrp_id':'h','urp_id':'u'}]}),encoding='utf8')
            self.assertEqual(audit(root,inv,all_meshes=True)['results'][0]['state'],'identical_dependencies')

    def test_reference_scan_matches_nested_strings_without_substrings(self):
        found={'a.fbx':[]};scan({'x':['a.fbx','a.fbx.meta']},{'a.fbx':'a.fbx'},'doc','',found)
        self.assertEqual(found['a.fbx'],[{'document':'doc','field':'/x/0'}])


if __name__=='__main__':unittest.main()
