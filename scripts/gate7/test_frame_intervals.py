import copy
import json
import tempfile
import unittest
from pathlib import Path
from frame_intervals import MAX_METADATA, decode, load


def fixture():
    return {'schema': 'forge.native-main-frame-intervals.v2', 'truncated': False,
        'maximum_samples': 20_000, 'maximum_metadata_snapshots_per_domain': 512,
        'graphics_observations': [{'frame': 10, 'observation': {'ready': True, 'completed': 8}}],
        'imported_observations': [],
        'samples': [{'frame': 20, 'tick': 15, 'frame_ms': 16.7, 'projection_ready': False, 'graphics': 0}]}


class Intervals(unittest.TestCase):
    def test_snapshot_fence_is_not_relabelled_or_promoted_by_a_later_sample(self):
        document = fixture()
        row = decode(document)[0]
        self.assertEqual(row['frame'], 20)
        self.assertEqual(row['graphics_observed_frame'], 10)
        self.assertEqual(row['graphics']['completed'], 8)
        self.assertFalse(row['projection_ready'])
        self.assertEqual(document['samples'][0]['graphics'], 0)

    def test_legacy_rows_keep_their_own_observation_frame(self):
        document = fixture()
        document['schema'] = 'forge.native-main-frame-intervals.v1'
        document['samples'][0]['graphics'] = {'ready': True, 'completed': 18}
        row = decode(document)[0]
        self.assertEqual(row['graphics_observed_frame'], 20)
        self.assertEqual(row['graphics']['completed'], 18)

    def test_unknown_truncated_out_of_range_boolean_or_future_references_are_refused(self):
        for failure in range(8):
            document = copy.deepcopy(fixture())
            if failure == 0:
                document['schema'] = 'unknown'
            elif failure == 1:
                document['truncated'] = True
            elif failure == 2:
                document['samples'][0]['graphics'] = -1
            elif failure == 3:
                document['samples'][0]['graphics'] = 1
            elif failure == 4:
                document['samples'][0]['graphics'] = True
            elif failure == 5:
                document['graphics_observations'][0]['frame'] = 21
            elif failure == 6:
                document['maximum_metadata_snapshots_per_domain'] = MAX_METADATA + 1
            else:
                document['samples'][0]['frame_ms'] = float('nan')
            with self.subTest(failure=failure), self.assertRaises(ValueError):
                decode(document)

    def test_larger_bounded_trace_keeps_each_observations_original_date(self):
        document = fixture()
        document['maximum_metadata_snapshots_per_domain'] = MAX_METADATA
        document['graphics_observations'] = [
            {'frame': index, 'observation': {'ready': True, 'completed': index - 1}}
            for index in range(1, 2049)
        ]
        document['samples'][0].update(frame=2100, graphics=2047)
        row = decode(document)[0]
        self.assertEqual(row['graphics_observed_frame'], 2048)
        self.assertEqual(row['graphics']['completed'], 2047)
        self.assertEqual(row['frame'], 2100)

    def test_v3_preserves_absent_hlod_and_rejects_partial_or_invalid_observations(self):
        document = fixture()
        document['schema'] = 'forge.native-main-frame-intervals.v3'
        row = decode(document)[0]
        self.assertNotIn('selected_triangles', row)
        complete = {'selected_triangles': 2, 'rendered_batches': 1,
                    'generation': 4, 'pending': False}
        document['samples'][0].update(complete)
        self.assertEqual(decode(document)[0]['selected_triangles'], 2)
        document['samples'][0].pop('generation')
        with self.assertRaises(ValueError):
            decode(document)
        document['samples'][0]['generation'] = 4
        document['samples'][0]['pending'] = 0
        with self.assertRaises(ValueError):
            decode(document)

    def test_reader_accepts_bounded_trace_larger_than_the_old_limit(self):
        document = fixture()
        document['padding'] = 'x' * (8 * 1024 * 1024)
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / 'intervals.json'
            path.write_text(json.dumps(document), encoding='utf-8')
            self.assertGreater(path.stat().st_size, 8 * 1024 * 1024)
            self.assertEqual(load(path)[0]['graphics_observed_frame'], 10)


if __name__ == '__main__':
    unittest.main()
