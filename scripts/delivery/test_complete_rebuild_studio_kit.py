"""Studio kit-overlay orchestration tests with inert files and no build process."""
import contextlib
import json
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest import mock

import complete_rebuild as rebuild
import stage_studio_game_kit as real_stage
import test_game_kit


class StudioKitStageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.repo = self.root/'repo'
        self.frontend = self.repo/'apps/forge_studio/dist'; self.frontend.mkdir(parents=True)
        (self.frontend/'index.html').write_text('base frontend', encoding='utf-8')
        (self.frontend/'studio.wasm').write_bytes(b'inert wasm')
        base_pins = rebuild.frontend_inputs(self.frontend)
        config = self.repo/'apps/forge_studio/tauri.conf.json'
        config.write_text(json.dumps({'build':{'frontendDist':'dist'},
                                      'app':{'windows':[{'label':'main','url':'index.html?authoring=1'}]}}), encoding='utf-8')
        self.script = self.repo/'scripts/delivery/complete_rebuild.py'
        self.script.parent.mkdir(parents=True)
        self.script.write_text('fixture orchestrator', encoding='utf-8')
        self.cache = self.root/'cargo-target'
        self.evidence = self.root/'evidence'; self.evidence.mkdir()
        self.source = {'schema':'forge.delivery.shared-source-inputs.v1','source_root':str(self.repo),
            'scope':['apps'], 'files':{'rust-toolchain.toml':{'sha256':'a'*64,'bytes':1}},
            'deleted':[], 'working_tree_files':{}}
        self.source_manifest = self.evidence/'source-inputs.json'
        self.source_manifest.write_text(json.dumps(self.source), encoding='utf-8')
        self.source_sha = rebuild.sha(self.source_manifest)
        prepared = {'schema':'forge.delivery.native-build.v1','status':'BUILD_ONLY','shared_working_tree':True,
                    'source_root':str(self.repo),'source_manifest_sha256':self.source_sha,
                    'source_stable_during_build':True,'frontend_pins':base_pins}
        (self.evidence/'prepare-frontend-build-proof.json').write_text(json.dumps(prepared), encoding='utf-8')
        self.assembly = self.root/'assembled-kit'; self.assembly.mkdir()
        self.assembly_sha = 'b'*64
        manifest = self.frontend/'forge-game-export-v1.json'; manifest.write_text('inert kit manifest', encoding='utf-8')
        self.stage_receipt = self.frontend/'forge-game-export-staging-receipt.json'
        staged = {'schema':'forge.studio.game-kit-stage.v1','status':'PRIVATE_STAGED_ONLY',
                  'manifest_file':manifest.name,'manifest_sha256':rebuild.sha(manifest),
                  'kit_assembly_receipt_sha256':self.assembly_sha,'source_manifest_sha256':self.source_sha}
        self.stage_receipt.write_text(json.dumps(staged), encoding='utf-8')
        self.overlay_content = {path.name:path.read_bytes() for path in (manifest, self.stage_receipt)}
        self.args = SimpleNamespace(stage='studio', evidence=self.evidence, cache=self.cache,
            frontend_assets=None, previous_build=None, validated_gate3_evidence=None,
            studio_game_kit_root=self.assembly, studio_game_kit_receipt_sha256=self.assembly_sha)
        self.commands = []
        self.environments = []
        self.mutate_on_build = None
        validator = SimpleNamespace(working_tree_changes=lambda before, after: [])
        self.patches = contextlib.ExitStack(); self.addCleanup(self.patches.close)
        self.patches.enter_context(mock.patch.object(rebuild, '__file__', str(self.script)))
        self.patches.enter_context(mock.patch.object(rebuild, 'shared_source_inputs', return_value=self.source))
        self.patches.enter_context(mock.patch.object(rebuild, 'gate3_validator', return_value=validator))
        self.patches.enter_context(mock.patch.object(rebuild, 'shared_frontend', side_effect=lambda path, *args: rebuild.frontend_inputs(path)))
        self.patches.enter_context(mock.patch.object(rebuild, 'release_stage_command',
            return_value=(['cargo','build','--release'], self.repo/'apps/forge_studio')))
        self.patches.enter_context(mock.patch.object(rebuild.subprocess, 'check_output', side_effect=self.fake_check_output))
        self.patches.enter_context(mock.patch.object(rebuild.subprocess, 'run', side_effect=self.fake_run))
        self.verify = mock.Mock(side_effect=self.fake_verify)
        self.stage = mock.Mock(side_effect=self.fake_stage)
        self.patches.enter_context(mock.patch.dict(sys.modules, {'stage_studio_game_kit':SimpleNamespace(verify_staged=self.verify,
                                                                                                       stage=self.stage)}))

    def fake_check_output(self, command, **kwargs):
        if command[0] == 'git': return str(self.repo)+'\n'
        if command[0] == 'rustc': return 'rustc fixture\n'
        raise AssertionError(command)

    def fake_run(self, command, cwd, env, stdout, stderr):
        self.commands.append(list(command))
        self.environments.append(dict(env))
        binary = 'forge-module.exe' if 'forge-module' in command else 'forge-studio.exe'
        contents = b'inert module executable' if binary == 'forge-module.exe' else b'inert Studio executable'
        release = self.cache / env.get('CARGO_BUILD_TARGET', '') / 'release'
        release.mkdir(parents=True, exist_ok=True)
        (release/binary).write_bytes(contents)
        stdout.write('simulated build\n')
        if self.mutate_on_build: self.mutate_on_build()
        return SimpleNamespace(returncode=0)

    def fake_verify(self, root, frontend, receipt_sha, source_sha):
        if (Path(root) != self.assembly or Path(frontend) != self.frontend or
                receipt_sha != self.assembly_sha or source_sha != self.source_sha):
            raise ValueError('kit pin differs')
        value = json.loads(self.stage_receipt.read_bytes())
        if value['manifest_sha256'] != rebuild.sha(self.frontend/'forge-game-export-v1.json'):
            raise ValueError('staged manifest differs')
        return value

    def fake_stage(self, root, frontend, receipt_sha, source_sha):
        for name, data in self.overlay_content.items():
            with (Path(frontend)/name).open('xb') as stream:
                stream.write(data)
        return self.fake_verify(root, frontend, receipt_sha, source_sha)

    def test_verified_overlay_is_pinned_in_studio_build_receipt(self):
        rebuild.shared_rebuild(self.args)
        proof = json.loads((self.evidence/'studio-build-proof.json').read_bytes())
        self.assertEqual(proof['game_kit']['assembly_receipt_sha256'], self.assembly_sha)
        self.assertEqual(proof['game_kit']['stage_receipt_sha256'], rebuild.sha(self.stage_receipt))
        self.assertEqual(proof['game_kit']['manifest_sha256'], rebuild.sha(self.frontend/'forge-game-export-v1.json'))
        self.assertEqual(self.verify.call_count, 3)
        self.assertEqual(len(self.commands), 1)
        self.assertEqual(proof['cargo_build_jobs'], '1')
        self.assertEqual(proof['build_environment']['CARGO_BUILD_JOBS'], '1')

    def test_studio_job_count_is_forwarded_and_recorded(self):
        self.args.shared_jobs = 8
        environment = {'CARGO_HOME':str(self.root/'shared-cargo-home'),
                       'TEMP':str(self.root/'shared-temp'), 'TMP':str(self.root/'shared-temp')}
        with mock.patch.dict(rebuild.os.environ, environment):
            rebuild.shared_rebuild(self.args)
        proof = json.loads((self.evidence/'studio-build-proof.json').read_bytes())
        self.assertEqual(self.environments[0]['CARGO_BUILD_JOBS'], '8')
        self.assertEqual(proof['cargo_build_jobs'], '8')
        self.assertEqual(proof['build_environment']['CARGO_BUILD_JOBS'], '8')
        for key, value in environment.items():
            self.assertEqual(self.environments[0][key], value)
            self.assertEqual(proof['build_environment'][key], value)

    def test_frontend_job_count_is_forwarded_and_recorded(self):
        self.args.stage = 'prepare-frontend'
        self.args.studio_game_kit_root = None
        self.args.studio_game_kit_receipt_sha256 = None
        self.args.shared_jobs = 6
        (self.evidence/'prepare-frontend-build-proof.json').unlink()
        environment = {'CARGO_HOME':str(self.root/'shared-cargo-home'),
                       'TEMP':str(self.root/'shared-temp'), 'TMP':str(self.root/'shared-temp')}
        with mock.patch.dict(rebuild.os.environ, environment):
            rebuild.shared_rebuild(self.args)
        proof = json.loads((self.evidence/'prepare-frontend-build-proof.json').read_bytes())
        self.assertEqual(self.environments[0]['CARGO_BUILD_JOBS'], '6')
        self.assertEqual(proof['cargo_build_jobs'], '6')
        self.assertEqual(proof['build_environment']['CARGO_BUILD_JOBS'], '6')
        self.assertEqual(self.commands[0][0], 'pwsh')
        self.assertNotIn('frontend_offline', proof)
        self.assertNotIn('TRUNK_OFFLINE', proof['build_environment'])
        for key, value in environment.items():
            self.assertEqual(self.environments[0][key], value)
            self.assertEqual(proof['build_environment'][key], value)

    def setup_module_game(self):
        self.args.stage = 'module-game'
        self.args.studio_game_kit_root = None
        self.args.studio_game_kit_receipt_sha256 = None

    def test_module_game_default_keeps_four_jobs(self):
        self.setup_module_game()
        rebuild.shared_rebuild(self.args)
        proof = json.loads((self.evidence/'module-game-build-proof.json').read_bytes())
        self.assertEqual(self.commands[0][-2:], ['-j', '4'])
        self.assertEqual(proof['cargo_build_jobs'], '4')
        self.assertEqual(proof['build_environment']['CARGO_BUILD_JOBS'], '4')

    def test_module_game_job_count_is_forwarded_and_recorded(self):
        self.setup_module_game()
        self.args.shared_jobs = 2
        rebuild.shared_rebuild(self.args)
        proof = json.loads((self.evidence/'module-game-build-proof.json').read_bytes())
        self.assertEqual(self.commands[0][-2:], ['-j', '2'])
        self.assertEqual(self.environments[0]['CARGO_BUILD_JOBS'], '2')
        self.assertEqual(proof['cargo_build_jobs'], '2')
        self.assertEqual(proof['build_environment']['CARGO_BUILD_JOBS'], '2')

    def setup_offline_frontend(self):
        self.args.stage = 'prepare-frontend'
        self.args.studio_game_kit_root = None
        self.args.studio_game_kit_receipt_sha256 = None
        self.args.frontend_offline = True
        self.args.frontend_trunk_version = '0.21.14'
        self.args.frontend_wasm_opt = self.root/'tools/wasm-opt.exe'
        self.args.frontend_wasm_opt_sha256 = 'a'*64
        self.args.frontend_wasm_opt_version = '131'
        (self.evidence/'prepare-frontend-build-proof.json').unlink()

    def test_offline_frontend_records_tools_and_forwards_environment(self):
        self.setup_offline_frontend()
        tools = {'pwsh':{'path':str(self.root/'tools/pwsh.exe'),'sha256':'b'*64,'version':'7.6.5'},
                 'wasm-opt':{'path':str(self.args.frontend_wasm_opt),'sha256':'a'*64,'version':'131'}}
        with mock.patch.object(rebuild, 'offline_frontend_tools', return_value=tools) as inspected:
            rebuild.shared_rebuild(self.args)
        proof = json.loads((self.evidence/'prepare-frontend-build-proof.json').read_bytes())
        self.assertEqual(inspected.call_count, 2)
        self.assertEqual(proof['offline_frontend_tools'], tools)
        self.assertTrue(proof['frontend_offline'])
        self.assertEqual(proof['frontend_build_log_sha256'], rebuild.sha(self.evidence/'shared-frontend-build.log'))
        self.assertEqual(proof['frontend_result_sha256'], rebuild.sha(self.evidence/'prepare-frontend-result.json'))
        self.assertEqual(self.commands[0][0], tools['pwsh']['path'])
        self.assertEqual(self.environments[0]['TRUNK_OFFLINE'], 'true')
        self.assertEqual(proof['build_environment']['TRUNK_SKIP_VERSION_CHECK'], 'true')
        self.assertTrue(self.environments[0]['PATH'].startswith(str(self.root/'tools')))

    def test_offline_frontend_refuses_changed_tool_before_receipt(self):
        self.setup_offline_frontend()
        first = {'pwsh':{'path':str(self.root/'pwsh.exe'),'sha256':'a'*64}}
        second = {'pwsh':{'path':str(self.root/'pwsh.exe'),'sha256':'b'*64}}
        with mock.patch.object(rebuild, 'offline_frontend_tools', side_effect=[first, second]):
            with self.assertRaisesRegex(ValueError, 'tools or environment changed'):
                rebuild.shared_rebuild(self.args)
        self.assertFalse((self.evidence/'prepare-frontend-build-proof.json').exists())
        self.assertEqual(json.loads((self.evidence/'prepare-frontend-result.json').read_bytes())['status'], 'FAIL')

    def test_offline_frontend_rejects_incomplete_or_unrequested_pins(self):
        self.setup_offline_frontend()
        self.args.frontend_wasm_opt_sha256 = None
        with self.assertRaisesRegex(ValueError, 'all explicit tool pins'):
            rebuild.shared_rebuild(self.args)
        self.args.frontend_offline = False
        with self.assertRaisesRegex(ValueError, 'all explicit tool pins'):
            rebuild.shared_rebuild(self.args)
        self.assertEqual(self.commands, [])

    def test_offline_frontend_refuses_inherited_compiler_overrides(self):
        self.setup_offline_frontend()
        with mock.patch.dict(rebuild.os.environ, {'RUSTC_WRAPPER':'unexpected-wrapper'}):
            with self.assertRaisesRegex(ValueError, 'compiler or flags overrides'):
                rebuild.shared_rebuild(self.args)
        self.assertEqual(self.commands, [])

    def test_offline_tool_preflight_checks_versions_and_pinned_binary(self):
        tool_dir = self.root/'tools'; tool_dir.mkdir()
        sysroot = self.root/'toolchain'; (sysroot/'bin').mkdir(parents=True)
        paths = {}
        for name in ('trunk', 'wasm-bindgen', 'wasm-opt', 'pwsh', 'cargo', 'rustc'):
            path = tool_dir/(name+'.exe'); path.write_bytes(('inert '+name).encode()); paths[name] = path
        for name in ('cargo', 'rustc'):
            (sysroot/'bin'/(name+'.exe')).write_bytes(('inert real '+name).encode())
        (self.repo/'Cargo.lock').write_text('[[package]]\nname="wasm-bindgen"\nversion="0.2.127"\n', encoding='utf-8')
        (self.repo/'rust-toolchain.toml').write_text('[toolchain]\nchannel="1.98.0"\n', encoding='utf-8')
        outputs = {'trunk':'trunk 0.21.14', 'wasm-bindgen':'wasm-bindgen 0.2.127',
                   'wasm-opt':'wasm-opt version 131 (version_131)', 'pwsh':'7.6.5',
                   'cargo':'cargo 1.98.0 (inert)', 'rustc':'rustc 1.98.0 (inert)'}
        def fake_version(command, **kwargs):
            name = Path(command[0]).stem
            return str(sysroot) if command[1:] == ['--print','sysroot'] else outputs[name]
        def fake_which(name, path):
            return str(paths[name]) if name in paths else None
        args = (self.repo, {'PATH':str(tool_dir)}, '0.21.14', paths['wasm-opt'],
                rebuild.sha(paths['wasm-opt']), '131')
        with mock.patch.object(rebuild.shutil, 'which', side_effect=fake_which), \
             mock.patch.object(rebuild.subprocess, 'check_output', side_effect=fake_version):
            found = rebuild.offline_frontend_tools(*args)
            self.assertEqual(found['wasm-bindgen']['version'], outputs['wasm-bindgen'])
            self.assertEqual(found['rustc']['toolchain_sha256'], rebuild.sha(sysroot/'bin/rustc.exe'))
            outputs['wasm-bindgen'] = 'wasm-bindgen 0.2.126'
            with self.assertRaisesRegex(ValueError, 'tool version'):
                rebuild.offline_frontend_tools(*args)
            outputs['wasm-bindgen'] = 'wasm-bindgen 0.2.127'
            with self.assertRaisesRegex(ValueError, 'SHA-256 differs'):
                rebuild.offline_frontend_tools(*args[:4], '0'*64, *args[5:])
            del paths['wasm-bindgen']
            with self.assertRaisesRegex(ValueError, 'missing from PATH'):
                rebuild.offline_frontend_tools(*args)

    def test_adoption_keeps_old_environment_receipts_compatible_without_weakening_pins(self):
        current = rebuild.shared_build_environment(self.cache)
        current.update(CARGO_HOME='shared-cargo-home', TEMP='shared-temp', TMP='shared-temp')
        old_keys = ('CARGO_TARGET_DIR', 'CARGO_BUILD_JOBS', 'CARGO_INCREMENTAL',
                    'CARGO_NET_OFFLINE', 'RUSTFLAGS', 'CARGO_TERM_COLOR', 'NO_COLOR')
        old = {key:current[key] for key in old_keys}
        (self.evidence/'wasm-source-manifest.json').write_text('{}', encoding='utf-8')
        (self.evidence/'wasm-result.json').write_text('{}', encoding='utf-8')
        frontend = {'assets_present':True, 'local_asset_manifest_sha256':'pin',
                    'files':{'index.html':{'sha256':'a'*64, 'bytes':1}}}
        receipt = {'build_environment':old, 'frontend':frontend, 'driver_sha256':'b'*64}
        validator = SimpleNamespace(LOCAL_ASSET_MANIFEST_SHA256='pin',
                                    completed_receipt=lambda *args: ({'build_environment':old}, receipt))
        result = rebuild.validated_gate3_inputs(validator, self.evidence, 'prepare-frontend', {}, self.cache, current)
        self.assertEqual(result[1], [{'path':'index.html', 'sha256':'a'*64, 'bytes':1}])
        current['CARGO_TARGET_DIR'] = 'different-target'
        with self.assertRaisesRegex(ValueError, 'environment differs'):
            rebuild.validated_gate3_inputs(validator, self.evidence, 'prepare-frontend', {}, self.cache, current)

    def test_invalid_job_counts_are_refused_before_build(self):
        for value in (0, -1, 65, True, '8'):
            self.args.shared_jobs = value
            with self.subTest(value=value), self.assertRaisesRegex(ValueError, 'job count'):
                rebuild.shared_rebuild(self.args)
        self.assertEqual(self.commands, [])

    def test_job_count_is_not_applied_to_an_adopted_or_unrelated_build(self):
        self.args.shared_jobs = 6
        self.args.stage = 'game'
        self.args.studio_game_kit_root = None
        self.args.studio_game_kit_receipt_sha256 = None
        with self.assertRaisesRegex(ValueError, 'only to a new frontend, Studio or module-game build'):
            rebuild.shared_rebuild(self.args)
        self.args.stage = 'studio'
        self.args.validated_gate3_evidence = self.root/'previous'
        with self.assertRaisesRegex(ValueError, 'only to a new frontend, Studio or module-game build'):
            rebuild.shared_rebuild(self.args)
        self.assertEqual(self.commands, [])

    def test_real_staging_and_reader_feed_the_studio_build_receipt(self):
        fixture = test_game_kit.GameKitTests('test_reader_revalidates_the_assembled_kit')
        fixture.setUp()
        self.addCleanup(fixture.doCleanups)
        fixture.assemble()
        fixture.patches.close()
        root = fixture.args.output
        kit_manifest = root/'forge-kit/game-export-v1.json'
        manifest = json.loads(kit_manifest.read_bytes()); manifest['source_revision'] = self.source_sha
        kit_manifest.write_text(json.dumps(manifest), encoding='utf-8')
        kit_receipt = root/'game-kit-assembly-receipt.json'
        receipt = json.loads(kit_receipt.read_bytes())
        receipt.update(source_revision=self.source_sha, source_manifest_sha256=self.source_sha,
                       kit_manifest_sha256=rebuild.sha(kit_manifest))
        kit_receipt.write_text(json.dumps(receipt), encoding='utf-8')
        (self.frontend/real_stage.MANIFEST).unlink()
        (self.frontend/real_stage.RECEIPT).unlink()
        self.args.studio_game_kit_root = root
        self.args.studio_game_kit_receipt_sha256 = rebuild.sha(kit_receipt)
        with mock.patch.dict(sys.modules, {'stage_studio_game_kit':real_stage}):
            rebuild.shared_rebuild(self.args)
        proof = json.loads((self.evidence/'studio-build-proof.json').read_bytes())
        self.assertEqual(proof['game_kit']['manifest_sha256'], rebuild.sha(kit_manifest))
        self.assertEqual(proof['game_kit']['assembly_receipt_sha256'], rebuild.sha(kit_receipt))

    def test_absent_overlay_is_staged_automatically_after_base_verification(self):
        for name in self.overlay_content:
            (self.frontend/name).unlink()
        rebuild.shared_rebuild(self.args)
        self.stage.assert_called_once()
        self.assertTrue((self.evidence/'studio-build-proof.json').is_file())

    def test_partial_overlay_is_preserved_and_refused(self):
        self.stage_receipt.unlink()
        with self.assertRaisesRegex(ValueError, 'overlay is partial'):
            rebuild.shared_rebuild(self.args)
        self.stage.assert_not_called()
        self.assertEqual((self.frontend/'forge-game-export-v1.json').read_bytes(),
                         self.overlay_content['forge-game-export-v1.json'])
        self.assertEqual(self.commands, [])

    def test_base_change_prevents_automatic_staging(self):
        for name in self.overlay_content:
            (self.frontend/name).unlink()
        (self.frontend/'index.html').write_text('changed base', encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'Shared frontend differs'):
            rebuild.shared_rebuild(self.args)
        self.stage.assert_not_called()
        self.assertEqual(self.commands, [])

    def test_missing_or_unrequested_overlay_is_refused_before_build(self):
        self.args.studio_game_kit_root = None
        self.args.studio_game_kit_receipt_sha256 = None
        with self.assertRaisesRegex(ValueError, 'Shared frontend differs'):
            rebuild.shared_rebuild(self.args)
        self.assertEqual(self.commands, [])

    def test_altered_base_frontend_is_refused_before_build(self):
        (self.frontend/'index.html').write_text('changed base', encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'Shared frontend differs'):
            rebuild.shared_rebuild(self.args)
        self.assertEqual(self.commands, [])

    def test_changed_overlay_during_build_is_refused(self):
        self.mutate_on_build = lambda: (self.frontend/'forge-game-export-v1.json').write_text('changed')
        with self.assertRaisesRegex(ValueError, 'frontend changed'):
            rebuild.shared_rebuild(self.args)
        self.assertFalse((self.evidence/'studio-build-proof.json').exists())
        self.assertEqual(json.loads((self.evidence/'studio-result.json').read_bytes())['status'], 'FAIL')

    def test_kit_changes_during_build_are_refused(self):
        self.verify.side_effect = [self.fake_verify(self.assembly,self.frontend,self.assembly_sha,self.source_sha),
                                   ValueError('kit changed')]
        with self.assertRaisesRegex(ValueError, 'kit changed'):
            rebuild.shared_rebuild(self.args)
        self.assertFalse((self.evidence/'studio-build-proof.json').exists())

    def test_root_and_receipt_pin_must_be_supplied_together(self):
        self.args.studio_game_kit_receipt_sha256 = None
        with self.assertRaisesRegex(ValueError, 'supplied together'):
            rebuild.shared_rebuild(self.args)
        self.assertEqual(self.commands, [])

    def test_old_studio_binary_cannot_be_adopted_with_a_new_overlay(self):
        self.args.validated_gate3_evidence = self.root/'old-evidence'
        with self.assertRaisesRegex(ValueError, 'must be built'):
            rebuild.shared_rebuild(self.args)
        self.assertEqual(self.commands, [])


if __name__ == '__main__':
    unittest.main(verbosity=2)
