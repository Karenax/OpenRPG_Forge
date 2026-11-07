import json
from pathlib import Path
import shutil
import subprocess
from types import SimpleNamespace
import unittest
from unittest import mock

import build_classic_installer as installer
import build_delivery
import test_game_kit
import test_studio_delivery


class StudioInstallerModeTests(unittest.TestCase):
    def setUp(self):
        preparation = test_studio_delivery.StudioDeliveryTests(
            'test_builds_private_editor_only_package_with_kit_and_reservations')
        preparation.setUp()
        self.addCleanup(preparation.doCleanups)
        self.preparation = preparation
        result = preparation.assemble()
        fixture = preparation.fixture
        self.root = fixture.root
        web = self.root/'webview2.exe'
        web.write_bytes(test_game_kit.inert_windows_x64_pe(b'inert webview'))
        nsis = self.root/'makensis.exe'; nsis.write_bytes(b'inert NSIS runner')
        self.args = SimpleNamespace(delivery_root=preparation.args.output,
            studio_receipt_sha256=result['receipt_sha256'], source_manifest=fixture.source_path,
            webview2=web, webview2_sha256=build_delivery.digest(web), nsis=nsis,
            nsis_sha256=build_delivery.digest(nsis), output=self.root/'candidate.exe',
            evidence=self.root/'installer-evidence', version='test-1')
        self.old_root = self.root/'legacy'; self.old_root.mkdir()
        (self.old_root/'Forge-ContentManager.exe').write_bytes(b'preserve existing hub')
        source_patcher = mock.patch.object(installer, 'shared_source_inputs', return_value=fixture.source)
        source_patcher.start(); self.addCleanup(source_patcher.stop)
        self.real_signature = installer.require_microsoft_signature
        signature_patcher = mock.patch.object(installer, 'require_microsoft_signature', autospec=True)
        self.signature = signature_patcher.start(); self.addCleanup(signature_patcher.stop)
        self.commands = []

    def fake_nsis(self, command, log, env, cwd):
        self.commands.append(command)
        pending = next(arg.split('=', 1)[1] for arg in command if str(arg).startswith('/DOUTPUT_FILE='))
        Path(pending).write_bytes(b'inert installer output')

    def build(self, runner=None):
        return installer.build_studio_installer(self.args, repo=self.preparation.fixture.repo,
                                                runner=runner or self.fake_nsis)

    def test_prepares_studio_only_without_copying_or_touching_hub(self):
        result = self.build()
        self.assertEqual(result['sha256'], build_delivery.digest(self.args.output))
        self.assertFalse(result['distribution_qualified'])
        self.assertEqual((self.old_root/'Forge-ContentManager.exe').read_bytes(), b'preserve existing hub')
        command = list(map(str, self.commands[0]))
        self.assertIn('/DSTUDIO_ONLY', command)
        self.assertTrue(any(arg.startswith('/DSTUDIO_PACKAGE=') for arg in command))
        self.assertFalse(any('Forge-ContentManager' in arg or 'Forge.exe' in arg for arg in command))
        self.assertEqual({path.name for path in self.args.evidence.iterdir()}, {'studio-installer-result.json'})
        receipt = json.loads((self.args.evidence/'studio-installer-result.json').read_bytes())
        self.assertFalse(receipt['installation_verified'])
        self.assertEqual(receipt['webview2_stub_architecture'], 'x64')
        self.assertTrue(receipt['webview2_microsoft_signature_verified'])
        self.assertFalse(receipt['webview2_payload_architecture_verified'])
        self.signature.assert_called_once_with(self.args.webview2)

    def test_accepts_signed_x86_stub_without_claiming_payload_architecture(self):
        data = bytearray(test_game_kit.inert_windows_x64_pe(b'inert webview'))
        data[132:134] = (0x014c).to_bytes(2, 'little')
        data[152:154] = (0x010b).to_bytes(2, 'little')
        self.args.webview2.write_bytes(data)
        self.args.webview2_sha256 = build_delivery.digest(self.args.webview2)
        self.build()
        receipt = json.loads((self.args.evidence/'studio-installer-result.json').read_bytes())
        self.assertEqual(receipt['webview2_stub_architecture'], 'x86')
        self.assertFalse(receipt['webview2_payload_architecture_verified'])
        self.signature.assert_called_once_with(self.args.webview2)

    def test_refuses_untrusted_or_invalid_webview_before_packaging(self):
        self.signature.side_effect = ValueError('WebView2 installer requires a valid Microsoft signature')
        with self.assertRaisesRegex(ValueError, 'valid Microsoft signature'):
            self.build()
        self.assertFalse(self.args.output.exists())
        self.assertFalse(self.args.evidence.exists())
        self.signature.reset_mock()
        self.signature.side_effect = None
        self.args.webview2.write_bytes(b'not a PE executable')
        self.args.webview2_sha256 = build_delivery.digest(self.args.webview2)
        with self.assertRaisesRegex(ValueError, 'Windows PE executable'):
            self.build()
        self.signature.assert_not_called()

    def test_signature_checker_uses_a_separate_environment_path(self):
        path = self.root/'installer name with spaces.exe'
        with mock.patch.dict(installer.os.environ, {'SystemRoot':r'C:\Windows'}), \
             mock.patch.object(installer.subprocess, 'run', return_value=SimpleNamespace(returncode=1)) as run:
            with self.assertRaisesRegex(ValueError, 'valid Microsoft signature'):
                self.real_signature(path)
        self.assertEqual(run.call_args.kwargs['env']['FORGE_WEBVIEW2_INSTALLER_VERIFY'], str(path))
        self.assertEqual(run.call_args.kwargs['env']['PSMODULEPATH'],
                         str(Path(r'C:\Windows')/'System32/WindowsPowerShell/v1.0/Modules'))
        self.assertNotIn(str(path), ' '.join(run.call_args.args[0]))

    def test_refuses_a_stale_assembly_receipt(self):
        self.args.studio_receipt_sha256 = '0'*64
        with self.assertRaisesRegex(ValueError, 'receipt pin'):
            self.build()
        self.assertFalse(self.args.output.exists())
        self.assertFalse(self.args.evidence.exists())

    def test_refuses_payload_mutation_during_installer_build(self):
        def mutate(command, log, env, cwd):
            self.fake_nsis(command, log, env, cwd)
            with (self.args.delivery_root/'forge-studio.forgepack').open('ab') as file:
                file.write(b'changed')
        with self.assertRaises(ValueError):
            self.build(mutate)
        self.assertFalse(self.args.output.exists())
        receipt = json.loads((self.args.evidence/'studio-installer-result.json').read_bytes())
        self.assertEqual(receipt['status'], 'FAIL')

    def test_hub_installer_is_retired(self):
        argv = ['build_classic_installer.py', 'legacy', '--evidence','e', '--output','o', '--cargo','c', '--node','n',
                '--target','t', '--nsis','i', '--shared-working-tree']
        with mock.patch.object(installer, 'build_studio_installer') as new, \
             mock.patch('sys.argv', argv), mock.patch('sys.stderr'), self.assertRaises(SystemExit) as refused:
            installer.main()
        self.assertEqual(refused.exception.code, 2)
        new.assert_not_called()

    def test_nsis_compiles_studio_and_refuses_the_retired_profile(self):
        compiler = shutil.which('makensis')
        if not compiler:
            candidate = Path(r'C:\Program Files (x86)\NSIS\makensis.exe')
            if not candidate.is_file():
                self.skipTest('NSIS compiler unavailable')
            compiler = str(candidate)
        folder = self.root/'nsis-fixture'; folder.mkdir()
        for name in ('forge-studio.forgepack', 'forge-delivery.exe', 'delivery.json',
                     'MicrosoftEdgeWebView2RuntimeInstallerX64.exe'):
            (folder/name).write_bytes(b'inert fixture')
        source = Path(installer.__file__).with_name('classic_installer.nsi')
        common = [compiler, '/NOCD', '/INPUTCHARSET', 'UTF8', '/OUTPUTCHARSET', 'UTF8',
                  f'/DPAYLOAD_DIR={folder}', '/DVERSION=fixture-1']
        studio = ['/DSTUDIO_ONLY', f'/DSTUDIO_PACKAGE={folder / "forge-studio.forgepack"}',
                  f'/DSTUDIO_DELIVERY_CLI={folder / "forge-delivery.exe"}',
                  f'/DSTUDIO_DELIVERY_INDEX={folder / "delivery.json"}',
                  f'/DSTUDIO_WEBVIEW2={folder / "MicrosoftEdgeWebView2RuntimeInstallerX64.exe"}']
        ambiguous = subprocess.run(
            common + ['/PPO', f'/DOUTPUT_FILE={folder / "ambiguous.exe"}', str(source)],
            capture_output=True, text=True, encoding='utf-8', errors='replace', check=False,
        )
        self.assertNotEqual(ambiguous.returncode, 0)
        self.assertIn('Select the STUDIO_ONLY profile', ambiguous.stderr)
        output = folder / 'studio.exe'
        args = common + studio + [f'/DOUTPUT_FILE={output}', str(source)]
        legacy = subprocess.run(
            args[:-1] + ['/DLEGACY_WITH_HUB', '/PPO', args[-1]],
            capture_output=True, text=True, encoding='utf-8', errors='replace', check=False,
        )
        self.assertNotEqual(legacy.returncode, 0)
        self.assertIn('Historical installer retired', legacy.stderr)
        preprocessed = subprocess.run(args[:-1] + ['/PPO', args[-1]], text=True, encoding='utf-8',
                                      errors='replace', capture_output=True, check=True).stdout
        self.assertNotIn('Forge-ContentManager.exe', preprocessed)
        self.assertNotIn('aube-installer.bmp', preprocessed)
        for command in ('uninstall-studio', 'apply-studio-installer', 'plan-studio-installer',
                        'recover-legacy-modules', 'StudioInstallation'):
            self.assertIn(command, preprocessed)
        subprocess.run(args, text=True, encoding='utf-8', errors='replace', capture_output=True, check=True)
        self.assertTrue(output.is_file())


if __name__ == '__main__':
    unittest.main()
