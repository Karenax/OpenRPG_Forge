"""Receipt checks on inert files; no Cargo or real worker execution."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location(
    'gate3_worker_reader', Path(__file__).resolve().parent / 'Validate-Gate3WorkingTree.py')
DRIVER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(DRIVER)


class NavigationWorkerProfileTests(unittest.TestCase):
    def receipt(self, version):
        result = dict(schema='forge.navigation-worker-build', version=version, build_only=True,
                      runtime_verified=False, source_unchanged_during_build=True, configuration='release')
        if version == 2:
            result.update(cargo_profile='forge-navigation-worker', profile_inherits='release',
                          cargo_target='wasm32-unknown-unknown',
                          cargo_arguments=list(DRIVER.NAVIGATION_WORKER_PROFILE_ARGS),
                          effective_profile_overrides_verified=False)
        return result

    def test_legacy_v1_remains_valid(self):
        DRIVER.validate_navigation_worker_header(self.receipt(1))
        self.assertNotIn('.cargo/config.toml', DRIVER.navigation_worker_source_paths(self.receipt(1), {}))

    def test_v2_requires_explicit_config_pin(self):
        DRIVER.validate_navigation_worker_header(self.receipt(2))
        self.assertIn('.cargo/config.toml', DRIVER.navigation_worker_source_paths(self.receipt(2), {}))

    def test_v2_rejects_wrong_profile_inheritance_target_or_command(self):
        for key, value in [('cargo_profile', 'release'), ('profile_inherits', 'dev'),
                           ('cargo_target', 'other'), ('cargo_arguments', []),
                           ('effective_profile_overrides_verified', True)]:
            with self.subTest(key=key):
                receipt = self.receipt(2)
                receipt[key] = value
                with self.assertRaisesRegex(ValueError, 'profile receipt differs'):
                    DRIVER.validate_navigation_worker_header(receipt)

    def test_v2_rejects_missing_profile_and_mutated_features(self):
        receipt = self.receipt(2)
        del receipt['cargo_profile']
        with self.assertRaisesRegex(ValueError, 'profile receipt differs'):
            DRIVER.validate_navigation_worker_header(receipt)
        receipt = self.receipt(2)
        receipt['cargo_arguments'][receipt['cargo_arguments'].index('web')] = 'default'
        with self.assertRaisesRegex(ValueError, 'profile receipt differs'):
            DRIVER.validate_navigation_worker_header(receipt)

    def test_v1_and_v2_preserve_closed_frontend_pins(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            names = ('index.html', 'styles.css', 'tidewater-webgpu-compat.js',
                     'forge-navigation-worker.js', 'forge_navigation.js', 'forge_navigation_bg.wasm',
                     'forge_studio.js', 'forge_studio_bg.wasm')
            for name in names:
                (root/name).write_bytes(name.encode())
            sources = {name: 'a' * 64 for name in (
                'Cargo.toml', 'Cargo.lock', 'crates/forge_navigation/Cargo.toml',
                'crates/forge_navigation/build.rs', 'third_party/rerecast-0.4.0/Cargo.toml',
                'apps/forge_studio/forge-navigation-worker.js', '.cargo/config.toml',
                'crates/forge_navigation/src/lib.rs', 'third_party/rerecast-0.4.0/src/lib.rs',
                'scripts/Stage-StudioLocalAssets.ps1')}
            assets = dict(schema='forge.studio.local-assets.v1', build_only=True, runtime_verified=False,
                          gate_qualified=False, orchestrator_sha256='a'*64, assets_present=False,
                          status='LOCAL_ASSETS_ABSENT', files={}, import_files=0, kit_files=0,
                          source_manifest_sha256=None)
            (root/'forge-studio-local-assets-manifest.json').write_text(json.dumps(assets))
            artifact_names = ('forge-navigation-worker.js', 'forge_navigation.js', 'forge_navigation_bg.wasm')
            for version in (1, 2):
                with self.subTest(version=version):
                    receipt = self.receipt(version)
                    receipt['artifacts'] = [dict(path=name, sha256=DRIVER.sha(root/name)) for name in artifact_names]
                    receipt['source_pins'] = [dict(path=name, sha256=sources[name])
                                              for name in sorted(DRIVER.navigation_worker_source_paths(receipt, sources))]
                    path = root/'forge-navigation-worker-manifest.json'
                    path.write_text(json.dumps(receipt))
                    DRIVER.validate_frontend(root, sources)
                    receipt['artifacts'][0]['sha256'] = 'b'*64
                    path.write_text(json.dumps(receipt))
                    with self.assertRaisesRegex(ValueError, 'artifact changed'):
                        DRIVER.validate_frontend(root, sources)
                    if version == 2:
                        receipt['artifacts'][0]['sha256'] = DRIVER.sha(root/artifact_names[0])
                        receipt['source_pins'] = [pin for pin in receipt['source_pins'] if pin['path'] != '.cargo/config.toml']
                        path.write_text(json.dumps(receipt))
                        with self.assertRaisesRegex(ValueError, 'source inventory is not closed'):
                            DRIVER.validate_frontend(root, sources)


if __name__ == '__main__':
    unittest.main()
