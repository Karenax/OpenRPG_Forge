import tempfile
import unittest
from pathlib import Path
from inventory_unity_pipeline_roots import discover


class UnityPipelineRootsTests(unittest.TestCase):
    def test_discovers_pipelines_without_entering_partial_work(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();(root/'metadata').mkdir()
            for name in ('asset-source-support-registry.v1.json','asset-import-registry.v1.json'):
                (root/'metadata'/name).write_text('{}',encoding='utf8')
            for work,side in (('base','HDRP(Default)'),('variant','URP'),('work.partial-id','HDRP(Default)')):
                (root/'sources'/work/'source'/'Assets'/'Pack'/side/'Art').mkdir(parents=True)
            result=discover(root,'sources')
            self.assertEqual(len(result['groups']),1)
            self.assertEqual(len(result['groups'][0]['hdrp']),1)
            self.assertEqual(len(result['groups'][0]['urp']),1)

    def test_discovers_source_base_and_variant_layouts(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();(root/'metadata').mkdir()
            for name in ('asset-source-support-registry.v1.json','asset-import-registry.v1.json'):
                (root/'metadata'/name).write_text('{}',encoding='utf8')
            (root/'sources'/'p1'/'source'/'base'/'Assets'/'Pack'/'HDRP(Default)'/'Art').mkdir(parents=True)
            (root/'sources'/'p2'/'source'/'variant'/'Assets'/'Pack'/'URP'/'Art').mkdir(parents=True)
            result=discover(root,'sources')
            self.assertEqual(len(result['groups']),1)
            self.assertEqual(len(result['groups'][0]['hdrp']),1)
            self.assertEqual(len(result['groups'][0]['urp']),1)


if __name__=='__main__':unittest.main()
