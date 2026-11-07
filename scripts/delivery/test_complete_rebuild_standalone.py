"""Test the standalone stage with inert files; no Node or Cargo process runs."""
import contextlib
import copy
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest import mock

import complete_rebuild as rebuild
import game_kit
from test_game_kit import inert_windows_x64_pe


class StandaloneStageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.repo = self.root / 'repo'
        self.reference_frontend = self.repo / 'apps/forge_launcher/dist'
        self.reference_frontend.mkdir(parents=True)
        (self.reference_frontend/'index.html').write_text('reference frontend must stay unchanged', encoding='utf-8')
        self.script = self.repo / 'scripts/delivery/complete_rebuild.py'
        self.script.parent.mkdir(parents=True)
        self.script.write_text('fixture orchestrator', encoding='utf-8')
        notice = self.repo / 'THIRD_PARTY_NOTICES.md'
        notice.write_text('pinned notice', encoding='utf-8')
        self.cache = self.root / 'cargo-target'
        self.release = self.cache / 'x86_64-pc-windows-msvc' / 'release'
        self.release.mkdir(parents=True)
        self.cargo_home = self.root / 'cargo-home'; self.cargo_home.mkdir()
        self.temp_dir = self.root / '.build/tmp'; self.temp_dir.mkdir(parents=True)
        self.frontend = self.temp_dir / 'standalone-frontend'
        self.evidence = self.root / 'evidence'; self.evidence.mkdir()
        self.source = {'schema':'forge.delivery.shared-source-inputs.v1','source_root':str(self.repo),
            'scope':['apps'], 'files':{'apps/forge_launcher/Cargo.toml':{'sha256':'a'*64,'bytes':1},
            'apps/forge_runtime/Cargo.toml':{'sha256':'b'*64,'bytes':1},
            'rust-toolchain.toml':{'sha256':'c'*64,'bytes':1},
            'scripts/delivery/complete_rebuild.py':{'sha256':game_kit.build_delivery.digest(self.script),'bytes':1},
            'apps/forge_launcher/dist/index.html':{'sha256':game_kit.build_delivery.digest(self.reference_frontend/'index.html'),
                                                 'bytes':(self.reference_frontend/'index.html').stat().st_size}}, 'deleted':[],
            'working_tree_files':{'THIRD_PARTY_NOTICES.md':game_kit.build_delivery.digest(notice)}}
        self.source_manifest = self.evidence / 'source-inputs.json'
        self.source_manifest.write_text(json.dumps(self.source), encoding='utf-8')
        self.args = SimpleNamespace(stage='standalone-launcher', evidence=self.evidence, cache=self.cache,
            frontend_assets=None, previous_build=None, validated_gate3_evidence=None, cargo_home=self.cargo_home,
            temp_dir=self.temp_dir, launcher_frontend=self.frontend, launcher_jobs=2, node='node')
        self.commands = []
        self.fake_validator = SimpleNamespace(working_tree_changes=lambda before, after: [])
        self.patches = contextlib.ExitStack(); self.addCleanup(self.patches.close)
        self.patches.enter_context(mock.patch.object(rebuild, '__file__', str(self.script)))
        self.inventories = self.patches.enter_context(mock.patch.object(rebuild, 'shared_source_inputs', return_value=self.source))
        self.patches.enter_context(mock.patch.object(rebuild, 'gate3_validator', return_value=self.fake_validator))
        self.patches.enter_context(mock.patch.object(rebuild.subprocess, 'check_output', side_effect=self.fake_check_output))
        self.patches.enter_context(mock.patch.object(rebuild.subprocess, 'run', side_effect=self.fake_run))
        self.verifier = self.patches.enter_context(mock.patch.object(game_kit, 'verify_launcher', side_effect=self.fake_verify))

    def fake_check_output(self, command, **kwargs):
        if command[0] == 'git':
            return str(self.repo) + '\n'
        if command[0] == 'rustc':
            return 'rustc fixture\n'
        raise AssertionError(command)

    def fake_run(self, command, cwd, env, stdout, stderr):
        self.commands.append((list(command), cwd, dict(env)))
        if command[0] == 'node':
            self.frontend.mkdir()
            (self.frontend/'index.html').write_text('<html data-forge-launcher="standalone"><title>Jeu</title>'
                                                   '<script src="desktop.js"></script></html>', encoding='utf-8')
            (self.frontend/'styles.css').write_text('body { color: white; }', encoding='utf-8')
            (self.frontend/'desktop.js').write_text('fixture();', encoding='utf-8')
            stdout.write(f'STANDALONE_FRONTEND_READY output={env["FORGE_LAUNCHER_DIST"]}\n')
        elif command[0] == 'cargo':
            name = 'forge_delivery.exe' if '--bin' in command and 'forge_delivery' in command else 'forge-launcher-prototype.exe'
            (self.release/name).write_bytes(inert_windows_x64_pe(b'inert native fixture'))
            stdout.write('fake cargo result\n')
        else:
            raise AssertionError(command)
        return SimpleNamespace(returncode=0)

    def fake_verify(self, path):
        return {'executable_sha256':game_kit.build_delivery.digest(path), 'command':'--verify-distributable',
                'exit_code':0, 'marker':game_kit.STANDALONE}

    def test_stage_receipt_is_accepted_by_the_game_kit_validator(self):
        manifest_hash = game_kit.build_delivery.digest(self.source_manifest)
        rebuild.shared_rebuild(self.args)
        proof = json.loads((self.evidence/'standalone-launcher-build-proof.json').read_bytes())
        self.assertEqual(proof['source_manifest_sha256'], manifest_hash)
        self.assertEqual(proof['application_role'], 'standalone-launcher')
        self.assertEqual(proof['frontend_builder_marker'], 'STANDALONE_FRONTEND_READY')
        self.assertEqual(proof['frontend_builder_output'], str(self.frontend))
        self.assertEqual(proof['launcher_verification']['marker'], game_kit.STANDALONE)
        self.assertTrue(proof['frontend_required_until_kit_assembly'])
        self.assertFalse(proof['runtime_verified'] or proof['gate_qualified'])
        self.assertEqual(json.loads(proof['build_environment']['TAURI_CONFIG']), game_kit.standalone_tauri_config(self.frontend, self.repo))
        self.assertEqual(len(self.commands), 2)
        self.assertEqual(self.commands[0][2]['FORGE_LAUNCHER_PROFILE'], 'standalone')
        self.assertEqual(self.commands[0][2]['FORGE_LAUNCHER_DIST'], str(self.frontend))
        self.assertEqual(self.commands[1][2]['CARGO_HOME'], str(self.cargo_home))
        self.assertIn('--locked', self.commands[1][0]); self.assertIn('--offline', self.commands[1][0])
        runtime = self.root/'runtime.exe'; runtime.write_bytes(inert_windows_x64_pe(b'inert runtime fixture'))
        runtime_proof = {'schema':'forge.delivery.native-build.v1','status':'BUILD_ONLY','shared_working_tree':True,
            'source_root':str(self.repo),'source_manifest_sha256':manifest_hash,'source_stable_during_build':True,
            'orchestrator_sha256':game_kit.build_delivery.digest(self.script),
            'application_role':'module-game','profile':'release','executable':str(runtime),
            'executable_sha256':game_kit.build_delivery.digest(runtime),
            'commands':[{'cwd':str(self.repo),'arguments':['cargo','build','-p','forge_runtime',
                '--no-default-features','--features','module-game','--bin','forge-module',
                '--release','--locked','--offline']}]}
        game_kit.verify_builds(self.repo, self.source_manifest, self.source, Path(proof['executable']), proof,
                               runtime, runtime_proof)
        self.assertEqual((self.reference_frontend/'index.html').read_text(), 'reference frontend must stay unchanged')
        (self.frontend/'index.html').write_text('tampered frontend', encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'frontend differs'):
            game_kit.verify_builds(self.repo, self.source_manifest, self.source, Path(proof['executable']), proof,
                                   runtime, runtime_proof)

    def test_preexisting_frontend_is_never_overwritten(self):
        self.frontend.mkdir(); (self.frontend/'unrecognized.txt').write_text('preserve')
        with self.assertRaisesRegex(ValueError, 'fresh direct child'):
            rebuild.shared_rebuild(self.args)
        self.assertEqual(self.commands, [])
        self.assertEqual((self.frontend/'unrecognized.txt').read_text(), 'preserve')

    def test_frontend_inside_checkout_is_refused_before_any_command(self):
        self.args.launcher_frontend = self.reference_frontend
        with self.assertRaisesRegex(ValueError, 'fresh direct child'):
            rebuild.shared_rebuild(self.args)
        self.assertEqual(self.commands, [])
        self.assertEqual((self.reference_frontend/'index.html').read_text(), 'reference frontend must stay unchanged')

    def test_stale_manifest_refused_before_any_command(self):
        changed = copy.deepcopy(self.source)
        changed['files']['apps/forge_launcher/Cargo.toml']['sha256'] = 'd'*64
        self.inventories.return_value = changed
        with self.assertRaisesRegex(ValueError, 'Shared inputs differ'):
            rebuild.shared_rebuild(self.args)
        self.assertEqual(self.commands, [])

    def test_wrong_frontend_marker_refuses_cargo_and_build_proof(self):
        def wrong_marker(command, **kwargs):
            result = self.fake_run(command, **kwargs)
            if command[0] == 'node':
                kwargs['stdout'].write(f'STANDALONE_FRONTEND_READY output={self.frontend}\n')
            return result
        with mock.patch.object(rebuild.subprocess, 'run', side_effect=wrong_marker):
            with self.assertRaisesRegex(ValueError, 'frontend marker or output path differs'):
                rebuild.shared_rebuild(self.args)
        self.assertEqual(len(self.commands), 1)
        self.assertFalse((self.evidence/'standalone-launcher-build-proof.json').exists())
        self.assertEqual(json.loads((self.evidence/'standalone-launcher-result.json').read_bytes())['status'], 'FAIL')

    def test_frontend_marker_with_wrong_output_path_refuses_cargo(self):
        def wrong_path(command, **kwargs):
            result = self.fake_run(command, **kwargs)
            if command[0] == 'node':
                kwargs['stdout'].seek(0)
                kwargs['stdout'].truncate()
                kwargs['stdout'].write(f'STANDALONE_FRONTEND_READY output={self.temp_dir / "other"}\n')
            return result
        with mock.patch.object(rebuild.subprocess, 'run', side_effect=wrong_path):
            with self.assertRaisesRegex(ValueError, 'frontend marker or output path differs'):
                rebuild.shared_rebuild(self.args)
        self.assertEqual(len(self.commands), 1)
        self.assertFalse((self.evidence/'standalone-launcher-build-proof.json').exists())

    def test_frontend_changed_during_cargo_refuses_build_proof(self):
        def changed_frontend(command, **kwargs):
            result = self.fake_run(command, **kwargs)
            if command[0] == 'cargo': (self.frontend/'desktop.js').write_text('changed')
            return result
        with mock.patch.object(rebuild.subprocess, 'run', side_effect=changed_frontend):
            with self.assertRaisesRegex(ValueError, 'changed during native embedding'):
                rebuild.shared_rebuild(self.args)
        self.assertFalse((self.evidence/'standalone-launcher-build-proof.json').exists())

    def test_wrong_verification_marker_refuses_build_proof(self):
        def wrong_marker(path):
            return {**self.fake_verify(path), 'marker':'wrong'}
        self.verifier.side_effect = wrong_marker
        with self.assertRaisesRegex(ValueError, 'verification differs'):
            rebuild.shared_rebuild(self.args)
        self.assertFalse((self.evidence/'standalone-launcher-build-proof.json').exists())

    def test_source_changed_during_frontend_refuses_cargo(self):
        changed = copy.deepcopy(self.source)
        changed['files']['apps/forge_runtime/Cargo.toml']['sha256'] = 'd'*64
        self.inventories.side_effect = [self.source, changed, changed]
        with self.assertRaisesRegex(ValueError, 'changed while building'):
            rebuild.shared_rebuild(self.args)
        self.assertEqual(len(self.commands), 1)
        self.assertFalse((self.evidence/'standalone-launcher-build-proof.json').exists())

    def test_invalid_jobs_refused_before_any_command(self):
        self.args.launcher_jobs = 0
        with self.assertRaisesRegex(ValueError, 'job count'):
            rebuild.shared_rebuild(self.args)
        self.assertEqual(self.commands, [])

    def test_delivery_cli_stage_builds_only_the_pinned_internal_tool(self):
        self.args.stage = 'delivery-cli'
        self.args.delivery_jobs = 2
        rebuild.shared_rebuild(self.args)
        proof = json.loads((self.evidence/'delivery-cli-build-proof.json').read_bytes())
        self.assertEqual(proof['application_role'], 'delivery-cli')
        self.assertEqual(proof['profile'], 'release')
        self.assertEqual(proof['executable'], str(self.release/'forge_delivery.exe'))
        self.assertEqual(proof['build_environment']['CARGO_BUILD_TARGET'], 'x86_64-pc-windows-msvc')
        self.assertIn('target-feature=+crt-static', proof['build_environment']['RUSTFLAGS'])
        self.assertEqual(proof['source_manifest_sha256'], game_kit.build_delivery.digest(self.source_manifest))
        self.assertEqual(len(self.commands), 1)
        self.assertEqual(self.commands[0][0][self.commands[0][0].index('--bin')+1], 'forge_delivery')
        self.assertEqual(self.commands[0][2]['CARGO_HOME'], str(self.cargo_home))
        self.assertFalse(self.frontend.exists())


if __name__ == '__main__':
    unittest.main(verbosity=2)
