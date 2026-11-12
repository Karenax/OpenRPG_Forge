import copy
import unittest
from unittest.mock import patch

from audit_dungeon_collision_choices import classify, classify_single_node


def samples():
    root = {'mesh': 0, 'extras': {'asset_id': 'main', 'source_prefab_guid': 'same'}}
    collision = {'mesh': 1, 'name': 'UCX_box', 'extras': {'source_role': 'collision'}}
    materials = {'materials': [{'name': 'wall', 'extras': {'source_unity_sha256': 'a'}}], 'textures': []}
    main = {'geometry': {'nodes': [root, collision], 'scenes': [{'nodes': [0, 1]}], 'scene': 0},
            'mesh_payloads': [['visible'], ['collider']], 'images': ['texture'],
            'materials': materials, 'private_extensions': []}
    variant = copy.deepcopy(main)
    variant['geometry']['nodes'] = [copy.deepcopy(root)]
    variant['geometry']['nodes'][0]['extras']['asset_id'] = 'variant'
    variant['geometry']['scenes'] = [{'nodes': [0]}]
    variant['mesh_payloads'] = [['visible']]
    variant['materials']['materials'][0]['extras']['source_unity_sha256'] = 'b'
    return main, variant


class CollisionCandidateTests(unittest.TestCase):
    def test_single_node_requires_source_material_and_triangle_match(self):
        main, variant = samples()
        main['geometry']['nodes'] = main['geometry']['nodes'][:1]
        main['geometry']['scenes'] = [{'nodes':[0]}]
        main['mesh_payloads'] = main['mesh_payloads'][:1]
        with patch('audit_dungeon_collision_choices.triangle_positions', return_value=[1]):
            self.assertEqual(classify_single_node(main,variant,b'a',b'b')['keep_side'],'main')
            variant['images'] = ['different']
            with self.assertRaises(ValueError):classify_single_node(main,variant,b'a',b'b')
        variant['images'] = ['texture']
        with patch('audit_dungeon_collision_choices.triangle_positions', side_effect=[[1],[2]]):
            with self.assertRaises(ValueError):classify_single_node(main,variant,b'a',b'b')

    def test_additional_collision_is_selected(self):
        self.assertEqual(classify(*samples())['keep_side'], 'main')

    def test_triangle_collider_requires_explicit_schema_and_role(self):
        main, variant = samples()
        node = main['geometry']['nodes'][1]
        node['name'] = 'UTM_triangle'
        node['extras']['openrpg_collision'] = {'schema': 'source-collider.v1', 'kind': 'triangle_mesh'}
        self.assertEqual(classify(main, variant)['keep_side'], 'main')
        node['extras']['openrpg_collision']['kind'] = 'unknown'
        with self.assertRaises(ValueError):
            classify(main, variant)

    def test_source_revision_difference_is_opt_in(self):
        main, variant = samples()
        main['geometry']['nodes'][0]['extras']['source_sha256'] = 'old'
        variant['geometry']['nodes'][0]['extras']['source_sha256'] = 'new'
        with self.assertRaises(ValueError):
            classify(main, variant)
        self.assertEqual(classify(main, variant, True)['keep_side'], 'main')

    def test_visible_changes_are_rejected(self):
        for field in ('mesh', 'transform', 'texture', 'material', 'material_extra', 'noncollision', 'scene'):
            main, other = samples()
            if field == 'mesh': other['mesh_payloads'][0] = ['changed']
            if field == 'transform': other['geometry']['nodes'][0]['translation'] = [1, 0, 0]
            if field == 'texture': other['images'] = ['changed']
            if field == 'material': other['materials']['materials'][0]['alphaMode'] = 'BLEND'
            if field == 'material_extra': other['materials']['materials'][0]['extras']['meaning'] = 'changed'
            if field == 'noncollision': main['geometry']['nodes'][1]['extras']['source_role'] = 'render'
            if field == 'scene': main['geometry']['scenes'][0]['nodes'] = [0]
            with self.subTest(field=field), self.assertRaises(ValueError):
                classify(main, other)


if __name__ == '__main__':
    unittest.main()
