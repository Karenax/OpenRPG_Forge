import json
import tempfile
import unittest
from pathlib import Path
import vault_storage as storage
from unity_shared_overlay import prepare,materialize,cleanup,expand_source_root,UnitySourceResolver,configured_resolver,changed_overlay_rows


class UnityOverlayTests(unittest.TestCase):
    def test_changed_overlay_rows_requires_original_and_preserves_pins(self):
        a={'logical':'a.png','vault_path':'root/a.png','sha256':'x','bytes':1}
        b=dict(a,vault_path='shared/a.png')
        self.assertEqual(changed_overlay_rows({'source_root':'root','files':[b]}, {'source_root':'root','files':[a]},1),[b])
        with self.assertRaises(ValueError):changed_overlay_rows({'source_root':'root','files':[dict(b,bytes=2)]}, {'source_root':'root','files':[a]},1)
        with self.assertRaises(ValueError):changed_overlay_rows({'source_root':'root','files':[b]}, {'source_root':'root','files':[dict(a,vault_path='other')]},1)

    def test_published_registry_discovers_only_pinned_matching_source(self):
        with tempfile.TemporaryDirectory() as temp:
            vault=Path(temp).resolve()/'vault';(vault/'pack/source/HDRP').mkdir(parents=True)
            (vault/'metadata/overlays').mkdir(parents=True);(vault/'other').mkdir()
            doc={'schema':'openrpg.unity-shared-overlay.v1','source_root':'pack/source/HDRP','files':[]}
            manifest=vault/'metadata/overlays/one.json';manifest.write_text(json.dumps(doc),encoding='utf8')
            self.assertIsNone(configured_resolver(vault,vault/'pack/source'))
            item={'source_root':'pack/source/HDRP','manifest':'metadata/overlays/one.json','sha256':storage.sha(manifest.read_bytes())}
            registry=vault/'metadata/unity-shared-overlay-registry.v1.json'
            registry.write_text(json.dumps({'schema':'openrpg.unity-shared-overlay-registry.v1','overlays':[item]}))
            self.assertIsNotNone(configured_resolver(vault,vault/'pack/source'))
            self.assertIsNone(configured_resolver(vault,vault/'other'))
            manifest.write_text('{}')
            with self.assertRaisesRegex(ValueError,'manifest SHA'):configured_resolver(vault,vault/'pack/source')
            item['sha256']=storage.sha(manifest.read_bytes());registry.write_text(json.dumps({'schema':'openrpg.unity-shared-overlay-registry.v1','overlays':[item,item]}))
            with self.assertRaisesRegex(ValueError,'ambiguous'):configured_resolver(vault,vault/'pack/source')

    def test_resolver_reads_shared_file_and_rejects_changes_or_escapes(self):
        with tempfile.TemporaryDirectory() as temp:
            base=Path(temp).resolve();vault=base/'vault';(vault/'hdr').mkdir(parents=True)
            (vault/'retained').mkdir();target=vault/'retained/a.png';target.write_bytes(b'pixels')
            doc={'schema':'openrpg.unity-shared-overlay.v1','source_root':'hdr',
                 'files':[{'logical':'Art/a.png','vault_path':'retained/a.png','sha256':storage.sha(b'pixels'),'bytes':6}]}
            manifest=base/'manifest.json';manifest.write_text(json.dumps(doc),encoding='utf8')
            pin=storage.sha(manifest.read_bytes());resolver=UnitySourceResolver(vault,manifest,pin)
            self.assertEqual(resolver.resolve('Art/a.png').read_bytes(),b'pixels')
            self.assertEqual(resolver.resolve_absolute(vault/'hdr/Art/a.png'),target)
            self.assertTrue(resolver.contains_absolute(vault/'hdr/Art/a.png'))
            self.assertFalse(resolver.contains_absolute(vault/'hdr/Art/missing.png'))
            self.assertEqual(resolver.resolve_absolute(target),target)
            with resolver.open_verified('Art/a.png') as stream:
                self.assertEqual(stream.read(),b'pixels')
            with self.assertRaises(ValueError): resolver.resolve('../retained/a.png')
            with self.assertRaises(ValueError): resolver.resolve('Art/missing.png')
            target.write_bytes(b'change')
            with self.assertRaisesRegex(ValueError,'payload changed'): resolver.resolve('Art/a.png')
            with self.assertRaisesRegex(ValueError,'payload changed'):
                with resolver.open_verified('Art/a.png'): pass
            target.write_bytes(b'pixels');manifest.write_text('{}',encoding='utf8')
            with self.assertRaisesRegex(ValueError,'manifest changed'): resolver.resolve('Art/a.png')
            with self.assertRaisesRegex(ValueError,'manifest SHA'): UnitySourceResolver(vault,manifest,pin)

    def test_resolver_rejects_duplicate_logical_names_and_unsafe_targets(self):
        with tempfile.TemporaryDirectory() as temp:
            base=Path(temp).resolve();vault=base/'vault';(vault/'hdr').mkdir(parents=True)
            row={'logical':'Art/a.png','vault_path':'../escape','sha256':storage.sha(b'a'),'bytes':1}
            doc={'schema':'openrpg.unity-shared-overlay.v1','source_root':'hdr','files':[row]}
            manifest=base/'manifest.json';manifest.write_text(json.dumps(doc),encoding='utf8')
            with self.assertRaises(ValueError): UnitySourceResolver(vault,manifest,storage.sha(manifest.read_bytes()))
            row['vault_path']='retained/a.png';doc['files']=[row,dict(row,logical='art/A.png')]
            manifest.write_text(json.dumps(doc),encoding='utf8')
            with self.assertRaisesRegex(ValueError,'duplicate'): UnitySourceResolver(vault,manifest,storage.sha(manifest.read_bytes()))

    def test_full_source_root_keeps_non_overlay_files_after_duplicate_retirement(self):
        with tempfile.TemporaryDirectory() as temp:
            base=Path(temp).resolve();vault=base/'vault';work=base/'work';work.mkdir()
            hdr=vault/'pack/source/Assets/Pack/HDRP';hdr.mkdir(parents=True)
            urp=vault/'retained';urp.mkdir();(urp/'a.png').write_bytes(b'pixels')
            (hdr/'material.mat').write_bytes(b'material');(vault/'pack/source/ProjectSettings.asset').write_bytes(b'settings')
            overlay={'schema':'openrpg.unity-shared-overlay.v1','source_root':'pack/source/Assets/Pack/HDRP',
                     'files':[{'logical':'a.png','vault_path':'retained/a.png','sha256':storage.sha(b'pixels'),'bytes':6},
                              {'logical':'material.mat','vault_path':'pack/source/Assets/Pack/HDRP/material.mat',
                               'sha256':storage.sha(b'material'),'bytes':8}],
                     'directories':[],'shared_files':1}
            mp=work/'overlay.json';mp.write_text(json.dumps(overlay),encoding='utf8')
            full=expand_source_root(vault,mp,'pack/source')
            expanded=work/'expanded.json';expanded.write_text(json.dumps(full),encoding='utf8')
            materialize(vault,expanded,work/'export',work)
            self.assertEqual((work/'export/Assets/Pack/HDRP/a.png').read_bytes(),b'pixels')
            self.assertEqual((work/'export/ProjectSettings.asset').read_bytes(),b'settings')
            (hdr/'a.png').write_bytes(b'different')
            with self.assertRaisesRegex(ValueError,'existing logical source changed'):
                expand_source_root(vault,mp,'pack/source')

    def test_restores_exact_unity_tree_from_one_shared_copy(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();vault=root/'vault';work=root/'work'
            (vault/'metadata').mkdir(parents=True);work.mkdir()
            ptr=b'{}';(vault/'metadata/asset-source-support-registry.v1.json').write_bytes(ptr)
            for side in ('hdr','urp'):
                (vault/side/'Art').mkdir(parents=True)
                (vault/side/'Art/a.png').write_bytes(b'pixels')
                (vault/side/'Art/a.png.meta').write_bytes(b'guid: 0123456789abcdef0123456789abcdef\n')
            (vault/'hdr/Art/a.mat').write_bytes(b'guid: 0123456789abcdef0123456789abcdef\n')
            (vault/'hdr/empty').mkdir()
            item={'hdrp':'hdr/Art/a.png','urp':'urp/Art/a.png',
                  'hdrp_info':{'sha256':storage.sha(b'pixels'),
                    'meta_sha256':storage.sha((vault/'hdr/Art/a.png.meta').read_bytes())}}
            comp={'source_registry_sha256':storage.sha(ptr),
                  'results':[{'state':'identical_dependencies','dependencies':[item]}]}
            comparison=work/'comparison.json';comparison.write_text(json.dumps(comp),encoding='utf8')
            manifest=prepare(vault,'hdr',comparison);self.assertEqual(manifest['shared_files'],2)
            path=work/'manifest.json';path.write_text(json.dumps(manifest),encoding='utf8')
            (vault/'hdr/Art/a.png').unlink();(vault/'hdr/Art/a.png.meta').unlink()
            result=materialize(vault,path,work/'export',work)
            self.assertEqual(result['files'],3)
            self.assertEqual((work/'export/Art/a.png').read_bytes(),b'pixels')
            self.assertEqual((work/'export/Art/a.png.meta').read_bytes(),(vault/'urp/Art/a.png.meta').read_bytes())
            self.assertTrue((work/'export/empty').is_dir())
            proof=work/'proof.json';proof.write_text(json.dumps(result),encoding='utf8')
            (work/'export/Art/a.png').write_bytes(b'edited')
            with self.assertRaisesRegex(ValueError,'export file changed'):
                cleanup(path,proof,work/'export',work)
            self.assertTrue((work/'export/Art/a.mat').exists())
            (work/'export/Art/a.png').write_bytes(b'pixels')
            self.assertEqual(cleanup(path,proof,work/'export',work)['files'],3)
            self.assertFalse((work/'export').exists())
            (vault/'urp/Art/a.png').write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError,'differs'):
                materialize(vault,path,work/'bad-export',work)


if __name__=='__main__':unittest.main()
