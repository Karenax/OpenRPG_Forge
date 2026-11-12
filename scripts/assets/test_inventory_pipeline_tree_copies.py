import json
import tempfile
import unittest
from pathlib import Path
from inventory_pipeline_tree_copies import inventory


class PipelineTreeTests(unittest.TestCase):
    def test_same_relative_path_requires_equal_bytes(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp).resolve();(root/'metadata').mkdir();(root/'h').mkdir();(root/'u').mkdir()
            for name in ('asset-source-support-registry.v1.json','asset-import-registry.v1.json'):
                (root/'metadata'/name).write_text('{}',encoding='utf8')
            for name,data in (('same',b'a'),('diff',b'a'),('hdr-only',b'z')):(root/'h'/name).write_bytes(data)
            for name,data in (('same',b'a'),('diff',b'b')):(root/'u'/name).write_bytes(data)
            result=inventory(root,[['test','h','u']])
            self.assertEqual(result['counts']['identical'],1)
            self.assertEqual(result['counts']['different_sha'],1)
            self.assertEqual(result['counts']['unmatched_hdrp'],1)
            self.assertEqual(result['pairs'][0]['old'],'h/same')


if __name__=='__main__':unittest.main()
