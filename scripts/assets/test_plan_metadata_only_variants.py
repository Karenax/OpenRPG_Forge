import unittest
from plan_metadata_only_variants import metadata_only, verify_differences
from test_variant_legacy_recipe import glb


class MetadataOnlyTests(unittest.TestCase):
    def test_accepts_only_asset_identity_and_source_provenance(self):
        candidate = {'binary_chunk_exact': True, 'truncated': False,
                     'differences': ['$/nodes/3/extras/asset_id',
                                     '$/nodes/3/extras/source_sha256',
                                     '$/materials/2/extras/source_unity_sha256',
                                     '$/materials/2/extras/material_program_sha256',
                                     '$/materials/2/extras/source_graph_sha256',
                                     '$/materials/2/extras/source_material_sha256',
                                     '$/extras/source_asset_id',
                                     '$/images/4/name']}
        self.assertTrue(metadata_only(candidate))
        for field in ('$/materials/2/alphaMode', '$/nodes/3/translation', '$/materials/2/extras/other', '$/images/4/bufferView'):
            self.assertFalse(metadata_only(dict(candidate, differences=[field])))

    def test_rejects_truncated_or_different_binary(self):
        candidate = {'binary_chunk_exact': True, 'truncated': False,
                     'differences': ['$/nodes/0/extras/asset_id']}
        self.assertFalse(metadata_only(dict(candidate, truncated=True)))
        self.assertFalse(metadata_only(dict(candidate, binary_chunk_exact=False)))
        self.assertFalse(metadata_only(dict(candidate, differences=[])))

    def test_rechecks_current_json_against_audit(self):
        a = glb(b'{"images":[{"name":"old"}]}')
        b = glb(b'{"images":[{"name":"new"}]}')
        verify_differences(a, b, ['$/images/0/name'])
        with self.assertRaises(ValueError):
            verify_differences(a, b, ['$/nodes/0/extras/asset_id'])
        with self.assertRaises(ValueError):
            verify_differences(glb(b'{"alphaMode":"OPAQUE"}'),
                               glb(b'{"alphaMode":"MASK"}'), ['$/alphaMode'])

    def test_missing_double_sided_only_equals_explicit_false(self):
        missing = glb(b'{"materials":[{}]}')
        explicit_false = glb(b'{"materials":[{"doubleSided":false}]}')
        explicit_true = glb(b'{"materials":[{"doubleSided":true}]}')
        differences = ['$/materials/0/doubleSided (missing)']
        verify_differences(missing, explicit_false, differences)
        with self.assertRaises(ValueError):
            verify_differences(missing, explicit_true, differences)

    def test_source_graph_hash_must_be_well_formed_and_only_metadata(self):
        def model(value):
            return glb(('{"materials":[{"extras":{"source_graph_sha256":"' + value + '"}}]}').encode())
        differences = ['$/materials/0/extras/source_graph_sha256']
        verify_differences(model('a' * 64), model('b' * 64), differences)
        with self.assertRaises(ValueError):
            verify_differences(model('a' * 64), model('invalid'), differences)

    def test_pending_pipeline_metadata_is_checked(self):
        missing = glb(b'{"extras":{}}')
        valid = glb(b'{"extras":{"source_pipeline":"URP"}}')
        invalid = glb(b'{"extras":{"source_pipeline":"invented"}}')
        difference = ['$/extras/source_pipeline (missing)']
        verify_differences(missing, valid, difference)
        with self.assertRaisesRegex(ValueError, 'pipeline'):
            verify_differences(missing, invalid, difference)
        missing = glb(b'{"materials":[{"extras":{}}]}')
        valid = glb(b'{"materials":[{"extras":{"render_parity_unqualified":true}}]}')
        invalid = glb(b'{"materials":[{"extras":{"render_parity_unqualified":false}}]}')
        difference = ['$/materials/0/extras/render_parity_unqualified (missing)']
        verify_differences(missing, valid, difference)
        with self.assertRaisesRegex(ValueError, 'parity'):
            verify_differences(missing, invalid, difference)

    def test_missing_asset_extras_requires_known_provenance_hashes(self):
        missing = glb(b'{"asset":{"version":"2.0"}}')
        valid = glb(('{"asset":{"version":"2.0","extras":{"source_base_glb_sha256":"' + 'a' * 64 + '"}}}').encode())
        invalid = glb(b'{"asset":{"version":"2.0","extras":{"gameplay":"value"}}}')
        differences = ['$/asset/extras (missing)']
        verify_differences(missing, valid, differences)
        with self.assertRaises(ValueError):
            verify_differences(missing, invalid, differences)


if __name__ == '__main__':
    unittest.main()
