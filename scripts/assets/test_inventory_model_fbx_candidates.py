import unittest
from inventory_model_fbx_candidates import mesh_paths


class MeshPathsTests(unittest.TestCase):
    def test_original_names_and_metadata_are_retained(self):
        value=[{'path':'files/hash.fbx','original_source_path':'Assets/HDRP/Door.fbx',
                'meta_guid':'a'*32,'fileID':42}, {'path':'Assets/URP/Wall.fbx'}]
        self.assertEqual(mesh_paths(value), [('Assets/HDRP/Door.fbx','a'*32,42),
                                             ('Assets/URP/Wall.fbx',None,None)])


if __name__=='__main__':unittest.main()
