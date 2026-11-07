import json
import os
from pathlib import Path
import subprocess
import unittest

import build_delivery
import game_kit
import stage_studio_game_kit as stage
import test_game_kit


class StudioGameKitStageTests(unittest.TestCase):
    def setUp(self):
        self.fixture = test_game_kit.GameKitTests(
            methodName='test_assembles_private_kit_with_pinned_notices_and_no_qualification_claim'
        )
        self.fixture.setUp()
        self.addCleanup(self.fixture.doCleanups)
        self.fixture.assemble()
        self.kit = self.fixture.args.output
        self.frontend = self.fixture.root / 'studio-frontend'
        self.frontend.mkdir()
        (self.frontend / 'forge-kit').mkdir()
        (self.frontend / 'forge-kit/build.json').write_bytes(b'unchanged historic kit')
        (self.frontend / 'forge-imports').mkdir()
        (self.frontend / 'forge-imports/asset.glb').write_bytes(b'unchanged historic asset')
        (self.frontend / 'forge-studio-local-assets-manifest.json').write_bytes(b'historic receipt')
        self.receipt_sha = build_delivery.digest(self.kit / 'game-kit-assembly-receipt.json')
        self.source_sha = build_delivery.digest(self.fixture.source_path)

    def run_stage(self, **kwargs):
        return stage.stage(self.kit, self.frontend, self.receipt_sha, self.source_sha, **kwargs)

    def historical_bytes(self):
        return {
            'forge-kit/build.json': (self.frontend / 'forge-kit/build.json').read_bytes(),
            'forge-imports/asset.glb': (self.frontend / 'forge-imports/asset.glb').read_bytes(),
            'forge-studio-local-assets-manifest.json':
                (self.frontend / 'forge-studio-local-assets-manifest.json').read_bytes(),
        }

    def test_copies_only_verified_manifest_and_preserves_historical_assets(self):
        before = self.historical_bytes()
        manifest = (self.kit / 'forge-kit/game-export-v1.json').read_bytes()
        receipt = self.run_stage()
        self.assertEqual((self.frontend / stage.MANIFEST).read_bytes(), manifest)
        self.assertEqual(self.historical_bytes(), before)
        self.assertEqual(receipt['kit_assembly_receipt_sha256'], self.receipt_sha)
        self.assertEqual(receipt['source_manifest_sha256'], self.source_sha)
        self.assertEqual(receipt['manifest_sha256'], build_delivery.digest(self.frontend / stage.MANIFEST))
        self.assertTrue(receipt['build_only'])
        self.assertFalse(receipt['runtime_verified'] or receipt['installation_verified']
                         or receipt['distribution_qualified'])
        reread = stage.verify_staged(self.kit, self.frontend, self.receipt_sha, self.source_sha)
        self.assertEqual(reread, receipt)
        self.assertEqual(json.loads((self.frontend / stage.RECEIPT).read_bytes()), receipt)
        self.assertEqual({item.name for item in self.frontend.iterdir()},
                         {'forge-kit', 'forge-imports', 'forge-studio-local-assets-manifest.json',
                          stage.MANIFEST, stage.RECEIPT})

    def test_wrong_pins_or_corrupt_kit_are_refused_before_output(self):
        before = self.historical_bytes()
        with self.assertRaises(ValueError):
            stage.stage(self.kit, self.frontend, '0' * 64, self.source_sha)
        with self.assertRaises(ValueError):
            stage.stage(self.kit, self.frontend, self.receipt_sha, '1' * 64)
        (self.kit / 'game-export-kit' / game_kit.RUNTIME).write_bytes(b'changed runtime')
        with self.assertRaises(ValueError):
            self.run_stage()
        self.assertFalse((self.frontend / stage.MANIFEST).exists())
        self.assertFalse((self.frontend / stage.RECEIPT).exists())
        self.assertEqual(self.historical_bytes(), before)

    def test_kit_change_during_copy_is_refused_and_only_owned_overlay_is_removed(self):
        before = self.historical_bytes()
        calls = []

        def reader(*args):
            verified = game_kit.read_assembled(*args)
            calls.append(1)
            if len(calls) == 1:
                (self.kit / 'game-export-kit' / game_kit.RUNTIME).write_bytes(b'changed runtime')
            return verified

        with self.assertRaises(ValueError):
            self.run_stage(reader=reader)
        self.assertEqual(len(calls), 1)
        self.assertFalse((self.frontend / stage.MANIFEST).exists())
        self.assertFalse((self.frontend / stage.RECEIPT).exists())
        self.assertEqual(self.historical_bytes(), before)

    def test_preexisting_output_is_never_overwritten(self):
        manifest = self.frontend / stage.MANIFEST
        manifest.write_bytes(b'previous user file')
        with self.assertRaises(ValueError):
            self.run_stage()
        self.assertEqual(manifest.read_bytes(), b'previous user file')
        self.assertFalse((self.frontend / stage.RECEIPT).exists())

    def test_preexisting_receipt_is_never_overwritten(self):
        receipt = self.frontend / stage.RECEIPT
        receipt.write_bytes(b'previous user receipt')
        with self.assertRaises(ValueError):
            self.run_stage()
        self.assertEqual(receipt.read_bytes(), b'previous user receipt')
        self.assertFalse((self.frontend / stage.MANIFEST).exists())

    def test_changed_overlay_is_preserved_when_rollback_would_be_unsafe(self):
        calls = []

        def reader(*args):
            calls.append(1)
            if len(calls) == 2:
                (self.frontend / stage.MANIFEST).write_bytes(b'changed by another actor')
                raise ValueError('kit was changed')
            return game_kit.read_assembled(*args)

        with self.assertRaisesRegex(ValueError, 'cleanup was unsafe'):
            self.run_stage(reader=reader)
        self.assertEqual((self.frontend / stage.MANIFEST).read_bytes(), b'changed by another actor')
        self.assertFalse((self.frontend / stage.RECEIPT).exists())

    def test_changed_manifest_or_receipt_cannot_be_admitted_later(self):
        self.run_stage()
        manifest = self.frontend / stage.MANIFEST
        original = manifest.read_bytes()
        manifest.write_bytes(b'wrong manifest')
        with self.assertRaises(ValueError):
            stage.verify_staged(self.kit, self.frontend, self.receipt_sha, self.source_sha)
        manifest.write_bytes(original)
        (self.frontend / stage.RECEIPT).write_bytes(b'{}')
        with self.assertRaises(ValueError):
            stage.verify_staged(self.kit, self.frontend, self.receipt_sha, self.source_sha)

    def test_nested_or_relative_frontend_is_refused(self):
        with self.assertRaises(ValueError):
            stage.stage(self.kit, self.kit / 'game-export-kit', self.receipt_sha, self.source_sha)
        with self.assertRaises(ValueError):
            stage.stage(self.kit, Path('relative'), self.receipt_sha, self.source_sha)

    def test_linked_frontend_is_refused(self):
        link = self.fixture.root / 'frontend-link'
        try:
            os.symlink(self.frontend, link, target_is_directory=True)
        except (OSError, NotImplementedError):
            self.skipTest('Windows does not allow a symlink in this session')
        with self.assertRaises(ValueError):
            stage.stage(self.kit, link, self.receipt_sha, self.source_sha)
        self.assertFalse((self.frontend / stage.MANIFEST).exists())

    @unittest.skipUnless(os.name == 'nt', 'Windows junction test')
    def test_frontend_junction_is_refused_without_touching_its_target(self):
        before = self.historical_bytes()
        junction = self.fixture.root / 'frontend-junction'
        creation = subprocess.run(['cmd', '/c', 'mklink', '/J', str(junction), str(self.frontend)],
                                  capture_output=True, text=True, check=False)
        if creation.returncode != 0:
            self.skipTest(f'Cannot create a test junction: {creation.stderr}')
        try:
            with self.assertRaisesRegex(ValueError, 'reparse'):
                stage.stage(self.kit, junction, self.receipt_sha, self.source_sha)
            self.assertEqual(self.historical_bytes(), before)
            self.assertFalse((self.frontend / stage.MANIFEST).exists())
        finally:
            os.rmdir(junction)


class LinuxStudioGameKitStageTests(unittest.TestCase):
    def setUp(self):
        self.fixture=test_game_kit.LinuxGameKitTests('test_assembles_and_reads_linux_kit_from_the_real_core_reader')
        self.fixture.setUp(); self.addCleanup(self.fixture.doCleanups)
        self.fixture.assemble()
        self.kit=self.fixture.args.output
        self.frontend=self.fixture.core.root/'studio-frontend'; self.frontend.mkdir()
        (self.frontend/'existing.js').write_bytes(b'preserve')
        self.receipt_sha=build_delivery.digest(self.kit/'game-kit-assembly-receipt.json')
        self.source_sha=build_delivery.digest(self.fixture.args.source_manifest)

    def test_linux_is_explicit_and_preserves_existing_frontend(self):
        with self.assertRaises(ValueError):
            stage.stage(self.kit,self.frontend,self.receipt_sha,self.source_sha)
        receipt=stage.stage(self.kit,self.frontend,self.receipt_sha,self.source_sha,
                            expected_platform=game_kit.LINUX_PLATFORM)
        self.assertEqual((self.frontend/'existing.js').read_bytes(),b'preserve')
        self.assertEqual((self.frontend/stage.MANIFEST).read_bytes(),
                         (self.kit/'forge-kit/game-export-v1.json').read_bytes())
        self.assertEqual(stage.verify_staged(self.kit,self.frontend,self.receipt_sha,self.source_sha,
                         expected_platform=game_kit.LINUX_PLATFORM),receipt)
        self.assertFalse(receipt['distribution_qualified'])
        with self.assertRaises(ValueError):
            stage.verify_staged(self.kit,self.frontend,self.receipt_sha,self.source_sha)

    def test_changed_linux_kit_is_refused_before_frontend_write(self):
        with (self.kit/'game-export-kit'/game_kit.LINUX_RUNTIME).open('ab') as stream: stream.write(b'changed')
        with self.assertRaises(ValueError):
            stage.stage(self.kit,self.frontend,self.receipt_sha,self.source_sha,
                        expected_platform=game_kit.LINUX_PLATFORM)
        self.assertEqual((self.frontend/'existing.js').read_bytes(),b'preserve')
        self.assertFalse((self.frontend/stage.MANIFEST).exists())


if __name__ == '__main__':
    unittest.main()
