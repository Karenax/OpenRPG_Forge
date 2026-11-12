import json
import tempfile
import unittest
from pathlib import Path
import vault_storage as storage
from prepare_unity_shared_guid_probe import prepare


class UnityGuidProbeTests(unittest.TestCase):
    def test_collects_model_and_prefab_dependency(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();vault=root/'vault';work=root/'work';(vault/'u').mkdir(parents=True);work.mkdir()
            rows=[]
            for name,data in [('a.fbx',b'model'),('a.fbx.meta',b'meta'),('p.prefab',b'prefab'),('p.prefab.meta',b'guid')]:
                (vault/'u'/name).write_bytes(data)
                rows.append({'logical':name,'vault_path':'u/'+name,'bytes':len(data),'sha256':storage.sha(data)})
            manifest={'schema':'openrpg.unity-shared-overlay.v1','comparison_sha256':'b'*64,'source_root':'old','files':rows}
            audit={'comparison_sha256':'b'*64,'images':[{'hdrp':'old/a.fbx','guid':'a'*32,
                'references':[{'side':'hdrp','path':'old/p.prefab'}]}]}
            m=work/'m.json';a=work/'audit.json';m.write_text(json.dumps(manifest));a.write_text(json.dumps(audit))
            result=prepare(vault,[('test',m)],a,work/'project',work,per_group=1,include_prefabs=True)
            self.assertEqual(result['cases'][0]['kind'],'model')
            self.assertEqual(result['dependencies'][0]['kind'],'prefab')
            self.assertTrue((work/'project/Assets/Probe/test/p.prefab').exists())

    def test_collects_graph_dependencies_and_local_packages(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();vault=root/'vault';work=root/'work';vault.mkdir();work.mkdir();(vault/'u').mkdir()
            rows=[]
            for name,data in [('a.png',b'image'),('a.png.meta',b'meta'),('s.shadergraph',b'graph'),('s.shadergraph.meta',b'guid')]:
                (vault/'u'/name).write_bytes(data)
                rows.append({'logical':name,'vault_path':'u/'+name,'bytes':len(data),'sha256':storage.sha(data)})
            manifest={'schema':'openrpg.unity-shared-overlay.v1','comparison_sha256':'b'*64,'source_root':'old','files':rows}
            audit={'comparison_sha256':'b'*64,'images':[{'hdrp':'old/a.png','guid':'a'*32,
                'references':[{'side':'hdrp','path':'old/s.shadergraph'}]}]}
            m=work/'m.json';a=work/'audit.json';m.write_text(json.dumps(manifest),encoding='utf8');a.write_text(json.dumps(audit),encoding='utf8')
            packages={}
            for i,name in enumerate(('com.unity.shadergraph','com.unity.render-pipelines.core','com.unity.searcher')):
                path=work/('pkg'+str(i));path.mkdir();(path/'package.json').write_text('{}',encoding='utf8');packages[name]=path
            result=prepare(vault,[('test',m)],a,work/'project',work,per_group=1,include_graphs=True,package_roots=packages)
            self.assertEqual(result['dependencies'][0]['kind'],'graph')
            self.assertTrue((work/'project/Assets/Probe/test/s.shadergraph').exists())
            self.assertTrue((work/'project/Packages/com.unity.shadergraph/package.json').exists())
            packages['com.unity.searcher']='4.9.3'
            prepare(vault,[('test',m)],a,work/'project6',work,per_group=1,include_graphs=True,
                    package_roots=packages,editor_version='6000.5.8f1')
            pm=json.loads((work/'project6/Packages/manifest.json').read_text(encoding='utf8'))
            self.assertEqual(pm['dependencies']['com.unity.searcher'],'4.9.3')
            packages['com.unity.render-pipelines.high-definition']='17.5.0'
            prepare(vault,[('test',m)],a,work/'project-hdrp',work,per_group=1,include_graphs=True,
                    package_roots=packages,editor_version='6000.5.8f1')
            pm=json.loads((work/'project-hdrp/Packages/manifest.json').read_text(encoding='utf8'))
            self.assertEqual(pm['dependencies']['com.unity.render-pipelines.high-definition'],'17.5.0')
            packages['com.unity.unexpected']='1.0.0'
            with self.assertRaisesRegex(ValueError,'invalid shader graph packages'):
                prepare(vault,[('test',m)],a,work/'project-invalid',work,per_group=1,
                        include_graphs=True,package_roots=packages)

    def test_collects_material_texture_dependencies_without_duplicate_material_copies(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();vault=root/'vault';work=root/'work';vault.mkdir();work.mkdir();(vault/'u').mkdir()
            rows=[]
            for name,data in [('a.png',b'image'),('a.png.meta',b'meta'),('m.mat',b'material'),('m.mat.meta',b'guid')]:
                (vault/'u'/name).write_bytes(data)
                rows.append({'logical':name,'vault_path':'u/'+name,'bytes':len(data),'sha256':storage.sha(data)})
            manifest={'schema':'openrpg.unity-shared-overlay.v1','comparison_sha256':'b'*64,'source_root':'old','files':rows}
            audit={'comparison_sha256':'b'*64,'images':[{'hdrp':'old/a.png','guid':'a'*32,
                'references':[{'side':'hdrp','path':'old/m.mat'}]}]}
            m=work/'m.json';a=work/'audit.json';m.write_text(json.dumps(manifest),encoding='utf8');a.write_text(json.dumps(audit),encoding='utf8')
            result=prepare(vault,[('test',m)],a,work/'project',work,per_group=1,include_materials=True)
            self.assertEqual(len(result['dependencies']),1)
            self.assertTrue((work/'project/Assets/Probe/test/m.mat').exists())

    def test_probe_uses_pinned_retained_bytes(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();vault=root/'vault';work=root/'work';vault.mkdir();work.mkdir()
            (vault/'u').mkdir();(vault/'u/a.png').write_bytes(b'image');(vault/'u/a.png.meta').write_bytes(b'meta')
            guid='a'*32;comp='b'*64
            manifest={'schema':'openrpg.unity-shared-overlay.v1','comparison_sha256':comp,'source_root':'old',
                'files':[{'logical':'a.png','vault_path':'u/a.png','bytes':5,'sha256':storage.sha(b'image')},
                         {'logical':'a.png.meta','vault_path':'u/a.png.meta','bytes':4,'sha256':storage.sha(b'meta')}]}
            audit={'comparison_sha256':comp,'images':[{'hdrp':'old/a.png','guid':guid}]}
            m=work/'m.json';a=work/'audit.json';m.write_text(json.dumps(manifest),encoding='utf8');a.write_text(json.dumps(audit),encoding='utf8')
            result=prepare(vault,[('test',m)],a,work/'project',work,per_group=1)
            self.assertEqual(len(result['cases']),1)
            self.assertEqual((work/'project/Assets/Probe/test/a.png').read_bytes(),b'image')
            self.assertEqual(len(prepare(vault,[('test',m)],a,work/'all-project',work,per_group=None)['cases']),1)
            (vault/'u/a.png').write_bytes(b'bad')
            with self.assertRaisesRegex(ValueError,'SHA changed'):
                prepare(vault,[('test',m)],a,work/'bad-project',work,per_group=1)


if __name__=='__main__':unittest.main()
