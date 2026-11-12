import unittest
from audit_legacy_block_attribution import ACTIVE,HISTORICAL,OTHER,SNAPSHOT,add_descriptor,index_mask,summarize


class AttributionTests(unittest.TestCase):
    def test_current_model_also_in_snapshot_remains_active(self):
        self.assertEqual(index_mask('model',{'model'},set(),{'model'}),ACTIVE|SNAPSHOT)
        self.assertEqual(index_mask('historical',set(),{'historical'},set()),0)
        self.assertEqual(index_mask('other',set(),set(),set()),OTHER)

    def test_shared_and_exclusive_blocks_are_counted_once(self):
        block={'pack':'a'*32,'offset':0,'stored_bytes':3,'bytes':3,'sha256':'b'*64,'codec':'raw'}
        doc={'schema':1,'vault_id':'c'*32,'bytes':3,'sha256':'d'*64,'segments':[block]}
        blocks={};add_descriptor(blocks,doc,ACTIVE);add_descriptor(blocks,doc,HISTORICAL)
        self.assertEqual(summarize(blocks)['3']['stored_bytes'],3)
        add_descriptor(blocks,doc,OTHER)
        self.assertEqual(summarize(blocks)['7']['blocks'],1)
        add_descriptor(blocks,doc,SNAPSHOT)
        self.assertEqual(summarize(blocks)['15']['stored_bytes'],3)

    def test_conflicting_offsets_are_rejected(self):
        block={'pack':'a'*32,'offset':0,'stored_bytes':3,'bytes':3,'sha256':'b'*64,'codec':'raw'}
        doc={'schema':1,'vault_id':'c'*32,'bytes':3,'sha256':'d'*64,'segments':[block]}
        blocks={};add_descriptor(blocks,doc,ACTIVE)
        altered={**doc,'segments':[{**block,'sha256':'e'*64}]}
        with self.assertRaisesRegex(ValueError,'conflicting'):add_descriptor(blocks,altered,HISTORICAL)


if __name__=='__main__':unittest.main()
