"""Reject a release receipt that omits or misidentifies the actual game."""
import importlib.util
import os
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location(
    'gate3_release_driver', Path(__file__).resolve().parents[2]/'Validate-Gate3WorkingTree.py')
DRIVER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(DRIVER)


class NativeReleaseTests(unittest.TestCase):
    def fixture(self, root):
        paths = DRIVER.release_artifact_paths(root)
        for index, path in enumerate(paths.values()):
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(bytes([index + 1]))
        pins = [DRIVER.release_artifact_pin(path, root) for path in paths.values()]
        return paths, pins

    def test_socle_and_studio_alone_do_not_qualify_the_separate_game(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            paths, pins = self.fixture(root)
            with self.assertRaisesRegex(ValueError, 'inventory is incomplete'):
                DRIVER.verify_release_artifacts(pins[:2], list(paths.values()), root)

    def test_changed_game_binary_is_rejected_even_when_studio_is_unchanged(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            paths, pins = self.fixture(root)
            DRIVER.verify_release_artifacts(pins, list(paths.values()), root)
            paths['native-game-release'].write_bytes(b'changed game')
            with self.assertRaisesRegex(ValueError, 'Release artifact changed'):
                DRIVER.verify_release_artifacts(pins, list(paths.values()), root)

    def test_game_path_cannot_be_replaced_by_another_identical_file(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            paths, pins = self.fixture(root)
            other = root/'release'/'other-game'
            other.write_bytes(paths['native-game-release'].read_bytes())
            pins[-1] = DRIVER.release_artifact_pin(other, root)
            with self.assertRaisesRegex(ValueError, 'Release artifact changed'):
                DRIVER.verify_release_artifacts(pins, list(paths.values()), root)

    def test_command_and_artifact_routes_match_the_game_configured_by_studio(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory).resolve()
            paths = DRIVER.release_artifact_paths(root)
            suffix = '.exe' if os.name == 'nt' else ''
            self.assertEqual(paths['native-game-release'], root/'release'/'examples'/f'tidewater_plant_probe{suffix}')
            stages = {name: command for name, command, _ in DRIVER.STAGES['release']}
            command = stages['native-game-release']
            self.assertEqual(command[command.index('--example') + 1], 'tidewater_plant_probe')
            self.assertEqual(list(paths)[1], 'studio-release')


if __name__ == '__main__':
    unittest.main()
