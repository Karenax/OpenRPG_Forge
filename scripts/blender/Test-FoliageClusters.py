"""Small, CPU-only fixtures for reusable complete-leaf clustering."""
import math
import unittest
from foliage_clusters import partition_faces


class Clusters(unittest.TestCase):
    def test_export_split_vertices_still_keep_whole_card(self):
        points = [(0,0,0),(2,0,0),(2,2,0),(0,0,0),(2,2,0),(0,2,0)]
        faces = [(0,1,2),(3,4,5)]
        self.assertEqual(partition_faces(points, faces, 1), [{'cell':(1,1,0),'faces':[0,1]}])

    def test_nearby_cards_are_not_welded_and_every_face_is_preserved(self):
        points = [(0,0,0),(.2,0,0),(0,.2,0),(2,0,0),(2.2,0,0),(2,.2,0)]
        faces = [(0,1,2),(3,4,5)]
        groups = partition_faces(points, faces, 1)
        self.assertEqual([g['cell'] for g in groups], [(0,0,0),(2,0,0)])
        self.assertEqual(sorted(i for g in groups for i in g['faces']), [0,1])
        self.assertEqual(groups, partition_faces(points, faces, 1))
        self.assertEqual(faces, [(0,1,2),(3,4,5)])

    def test_negative_coordinates_use_floor(self):
        groups = partition_faces([(-.1,0,0),(-.2,0,0),(-.1,.1,0)], [(0,1,2)], 1)
        self.assertEqual(groups[0]['cell'], (-1,0,0))

    def test_invalid_inputs_rejected(self):
        points = [(0,0,0),(1,0,0),(0,1,0)]
        for size in (0,-1,math.inf,math.nan):
            with self.assertRaises(ValueError): partition_faces(points, [(0,1,2)], size)
        for faces in ([],[(0,1,8)],[(0,0,2)],[(0,1)]):
            with self.assertRaises(ValueError): partition_faces(points, faces, 1)
        with self.assertRaises(ValueError):
            partition_faces([(math.nan,0,0),*points[1:]], [(0,1,2)], 1)


if __name__ == '__main__':
    unittest.main()
