"""Regression: implicit Unity UV slots must not become literal zero vectors."""
import unittest
from source_graph_uv_inputs import implicit_uv_input, checked_channel


class SourceUvInputTests(unittest.TestCase):
    def test_all_original_channels_and_refusal_of_invalid_metadata(self):
        for channel in range(8):
            slot={'m_Type':'UnityEditor.ShaderGraph.UVMaterialSlot','m_Channel':channel}
            value={'literal':{'x':0.0,'y':0.0},'source_slot':slot}
            expected=('source_uv' if channel==0 else f'source_uv{channel}')+'.xy'
            self.assertEqual(implicit_uv_input(value),(expected,2,channel))
        for invalid in (-1,8,True,1.0,'1',None):
            with self.assertRaises(ValueError):checked_channel(invalid)
        with self.assertRaises(ValueError):
            implicit_uv_input({'source_slot':{'m_Type':'UnityEditor.ShaderGraph.UVMaterialSlot'}})

    def test_connected_and_authored_literal_inputs_are_not_rebound(self):
        self.assertIsNone(implicit_uv_input({'ref':'17','source_slot':{'m_Type':'UnityEditor.ShaderGraph.UVMaterialSlot','m_Channel':1}}))
        self.assertIsNone(implicit_uv_input({'literal':{'x':0.0,'y':0.0}}))
        self.assertIsNone(implicit_uv_input({'literal':{'x':.25,'y':.75},'source_slot':{'m_Type':'UnityEditor.ShaderGraph.Vector2MaterialSlot'}}))


if __name__=='__main__':unittest.main()
