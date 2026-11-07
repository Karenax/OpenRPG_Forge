import contextlib
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
from types import SimpleNamespace
import tempfile
import unittest
from urllib.parse import urlsplit
from unittest import mock
import zipfile

import application_sources
import build_delivery
import complete_rebuild
import game_kit as kit
import runtime_notices
import test_runtime_notices
import test_linux_build_evidence

REAL_NOTICE_PREPARATION = runtime_notices.prepare_for_assembly


def write(path, value):
    Path(path).write_text(json.dumps(value, sort_keys=True), encoding='utf-8')


def pinned(data):
    return {'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()}


def inert_windows_x64_pe(label):
    data = bytearray(160)
    data[:2] = b'MZ'
    data[60:64] = (128).to_bytes(4, 'little')
    data[128:132] = b'PE\0\0'
    data[132:134] = b'\x64\x86'
    data[152:154] = b'\x0b\x02'
    return bytes(data) + label


class GameKitTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name).resolve()
        self.repo=self.root/'repo';self.repo.mkdir()
        frontend=self.root/'.build/tmp/standalone-frontend';frontend.mkdir(parents=True)
        self.frontend = frontend
        for name in ('index.html','styles.css','desktop.js'):(frontend/name).write_text('generic '+name)
        self.launcher_bytes=inert_windows_x64_pe(b'inert standalone launcher')
        self.runtime_bytes=inert_windows_x64_pe(b'inert module runtime')
        self.launcher=self.root/'standalone.exe';self.launcher.write_bytes(self.launcher_bytes)
        self.runtime=self.root/'module.exe';self.runtime.write_bytes(self.runtime_bytes)
        self.project_notice=self.repo/'THIRD_PARTY_NOTICES.md'
        self.project_notice.write_bytes(b'Notice [licence](third_party/example/LICENSE).')
        self.source={'schema':'forge.delivery.shared-source-inputs.v1','source_root':str(self.repo),'scope':['apps'],
                     'files':{'apps/forge_launcher/Cargo.toml':{'sha256':'a'*64,'bytes':1},
                              'apps/forge_runtime/Cargo.toml':{'sha256':'b'*64,'bytes':1},
                              'scripts/delivery/complete_rebuild.py':{'sha256':'c'*64,'bytes':1}},'deleted':[],
                     'working_tree_files':{'THIRD_PARTY_NOTICES.md':build_delivery.digest(self.project_notice)}}
        self.source_path=self.root/'source-inputs.json';write(self.source_path,self.source)
        mhash=build_delivery.digest(self.source_path)
        common={'schema':'forge.delivery.native-build.v1','status':'BUILD_ONLY','shared_working_tree':True,
                'source_root':str(self.repo),'source_manifest_sha256':mhash,'source_stable_during_build':True,
                'orchestrator_sha256':'c'*64}
        config=json.dumps(kit.standalone_tauri_config(frontend, self.repo))
        self.launcher_proof={**common,'application_role':'standalone-launcher','profile':'release',
            'frontend_profile':'standalone','cargo_features':['standalone'],'frontend':str(frontend),
            'frontend_pins':complete_rebuild.frontend_inputs(frontend),'frontend_builder_marker':'STANDALONE_FRONTEND_READY',
            'frontend_builder_output':str(frontend),
            'launcher_verification':{'executable_sha256':build_delivery.digest(self.launcher),'command':'--verify-distributable',
                                     'exit_code':0,'marker':kit.STANDALONE},
            'tauri_config_sha256':hashlib.sha256(config.encode()).hexdigest(),
            'build_environment':{'FORGE_LAUNCHER_PROFILE':'standalone','TAURI_CONFIG':config,
                'FORGE_LAUNCHER_DIST':str(frontend),'FORGE_LAUNCHER_TEMP_ROOT':str(self.root/'.build/tmp'),
                'FORGE_AUBE_SOURCE':str(self.repo/'apps/forge_launcher/canonical-ui')},
            'commands':[{'cwd':str(self.repo/'apps/forge_launcher'),'arguments':['node',
                 str(self.repo/'apps/forge_launcher/scripts/build-aube.mjs')]},
                {'cwd':str(self.repo/'apps/forge_launcher'),'arguments':['cargo','build','--manifest-path',
                 str(self.repo/'apps/forge_launcher/Cargo.toml'),'--release','--locked','--offline','--features','standalone']}],
            'executable':str(self.launcher),'executable_sha256':build_delivery.digest(self.launcher)}
        self.runtime_proof={**common,'application_role':'module-game','profile':'release',
            'commands':[{'cwd':str(self.repo),'arguments':['cargo','build','-p','forge_runtime','--no-default-features',
                 '--features','module-game','--bin','forge-module','--release','--locked','--offline']}],
            'executable':str(self.runtime),'executable_sha256':build_delivery.digest(self.runtime)}
        self.launcher_proof_path=self.root/'launcher-proof.json';self.runtime_proof_path=self.root/'runtime-proof.json'
        self.save_proofs()
        self.graph=self.root/'graph.json';write(self.graph,{'scopes':[{'name':'launcher-standalone','target':kit.TARGET,
            'graphs':{'all_nondev':{'exit_code':0}}}]})
        self.ids={'path+file:///repo#forge_launcher_prototype','registry+https://example#dependency'}
        self.notices=self.make_notices()
        src_name='licenses/native-media/NOTICE.txt';src_bytes=b'fixture native dependency sources'
        self.application=application_sources.PreparedSources(((src_name,src_bytes),),
            {'schema':'forge.delivery.application-sources-receipt.v1','files':{src_name:pinned(src_bytes)}})
        self.args=SimpleNamespace(platform=kit.PLATFORM,version='candidate-1',output=self.root/'output',
            source_manifest=self.source_path,launcher=self.launcher,launcher_proof=self.launcher_proof_path,
            module_runtime=self.runtime,module_runtime_proof=self.runtime_proof_path,
            runtime_notice_bundle=self.root/'bundle',runtime_notice_receipt=self.root/'notice.json',
            runtime_notice_graph=self.graph,cargo_cache=self.root/'cargo/registry')
        self.patches=contextlib.ExitStack();self.addCleanup(self.patches.close)
        self.shared=self.patches.enter_context(mock.patch.object(complete_rebuild,'shared_source_inputs',return_value=self.source))
        self.notice_prepare=self.patches.enter_context(mock.patch.object(runtime_notices,'prepare_for_assembly',return_value=self.notices))
        self.sources_prepare=self.patches.enter_context(mock.patch.object(application_sources,'prepare_for_assembly',return_value=self.application))
        self.launcher_deps=self.patches.enter_context(mock.patch.object(kit,'current_launcher_dependencies',return_value=self.ids))

    def make_notices(self):
        text=b'fixture notice';t=hashlib.sha256(text).hexdigest()
        base={'schema':'forge.rust-dependency-notices.v2','requests':[{'package':'forge_launcher_prototype',
              'target':kit.TARGET,'features':['standalone'],'no_default_features':False,'include_root_dev':False,
              'package_ids':sorted(self.ids)}],
              'packages':[{'notices':['texts/'+t+'.txt']}]}
        base_raw=json.dumps(base,sort_keys=True).encode();base_hash=hashlib.sha256(base_raw).hexdigest()
        sup_raw=json.dumps({'schema':'forge.rust-notice-supplements.v1','base_index_sha256':base_hash}).encode()
        sup_hash=hashlib.sha256(sup_raw).hexdigest()
        forge_raw=json.dumps({'schema':'forge.original-code-notices.v1','base_index_sha256':base_hash,
                              'supplement_index_sha256':sup_hash}).encode()
        asset_raw=json.dumps({'schema':'forge.asset-notice-supplements.v1','base_index_sha256':base_hash}).encode()
        prefix='licenses/rust-runtime-candidate/'
        files=((prefix+'INDEX.json',base_raw),(prefix+'SUPPLEMENT-NOTICES.json',sup_raw),
               (prefix+'FORGE-ORIGINAL-NOTICES.json',forge_raw),(prefix+'ASSET-NOTICES.json',asset_raw),
               (prefix+'texts/'+t+'.txt',text))
        return runtime_notices.PreparedNotices(files,b'Notice [licence](third_party/example/LICENSE).',
                (('third_party/example/LICENSE',b'fixture terms'),),{'distribution_qualified':False})

    def save_proofs(self):
        write(self.launcher_proof_path,self.launcher_proof);write(self.runtime_proof_path,self.runtime_proof)

    def verify_fake(self,path):
        return {'executable_sha256':build_delivery.digest(path),'command':'--verify-distributable',
                'exit_code':0,'marker':kit.STANDALONE}

    def assemble(self):
        return kit.assemble(self.args,repo=self.repo,launcher_verifier=self.verify_fake)

    def prepare_studio_package(self):
        import stage_studio_game_kit
        self.assemble()
        root = self.args.output
        assembly_pin = build_delivery.digest(root/'game-kit-assembly-receipt.json')
        source_pin = build_delivery.digest(self.source_path)
        frontend = self.repo/'apps/forge_studio/dist'; frontend.mkdir(parents=True)
        (frontend/'index.html').write_text('inert Studio frontend', encoding='utf-8')
        staged = stage_studio_game_kit.stage(root, frontend, assembly_pin, source_pin)
        studio = self.root/'studio.exe'; studio.write_bytes(inert_windows_x64_pe(b'inert Studio'))
        bins = self.root/'packaging/bin'; bins.mkdir(parents=True)
        (bins/'forge-studio.exe').write_bytes(studio.read_bytes())
        proof = {'schema':'forge.delivery.native-build.v1','status':'BUILD_ONLY','shared_working_tree':True,
            'source_root':str(self.repo),'source_manifest_sha256':source_pin,'source_stable_during_build':True,
            'profile':'release','authoring':True,'orchestrator_sha256':'c'*64,
            'executable':str(studio),'executable_sha256':build_delivery.digest(studio),
            'frontend':str(frontend),'frontend_pins':complete_rebuild.frontend_inputs(frontend),
            'game_kit':{'assembly_root':str(root),'assembly_receipt_sha256':assembly_pin,
                        'source_manifest_sha256':source_pin,'manifest_sha256':staged['manifest_sha256'],
                        'stage_receipt_sha256':build_delivery.digest(frontend/'forge-game-export-staging-receipt.json'),
                        'stage_receipt':staged}}
        proof_path = self.root/'studio-proof.json'; write(proof_path, proof)
        return root, studio, proof_path, assembly_pin, build_delivery.digest(proof_path), bins

    def stage_studio_package(self, fixture):
        root, studio, proof_path, assembly_pin, studio_pin, bins = fixture
        return kit.stage_studio_adjacent(root, studio, proof_path, self.source_path, assembly_pin,
                                         studio_pin, bins, repo=self.repo)

    def test_assembles_private_kit_with_pinned_notices_and_no_qualification_claim(self):
        proof=self.assemble();output=self.args.output
        manifest=json.loads((output/'forge-kit/game-export-v1.json').read_text(encoding='utf-8'))
        self.assertEqual(manifest['schema'],'forge.delivery.game-kit.v1')
        self.assertEqual(manifest['source_revision'],build_delivery.digest(self.source_path))
        self.assertEqual(manifest['launcher_contract'],'forge.game-launcher.v2')
        self.assertEqual(manifest['rights'],'private_local_only')
        self.assertEqual(manifest['platform'],'windows-x64')
        self.assertEqual((output/'game-export-kit'/manifest['launcher']).read_bytes(),self.launcher.read_bytes())
        self.assertEqual((output/'game-export-kit'/manifest['module_runtime']).read_bytes(),self.runtime.read_bytes())
        self.assertIn('licenses/THIRD_PARTY_NOTICES.md',manifest['license_paths'])
        self.assertIn('licenses/rust-runtime-candidate/INDEX.json',manifest['license_paths'])
        self.assertIn('licenses/native-media/NOTICE.txt',manifest['license_paths'])
        self.assertEqual(kit.inventory(output/'game-export-kit'),manifest['files'])
        self.assertFalse(proof['runtime_verified'] or proof['installation_verified'] or proof['distribution_qualified'])
        self.assertTrue((output/'game-kit-assembly-receipt.json').is_file())

    def test_reader_revalidates_the_assembled_kit(self):
        self.assemble()
        receipt = self.args.output/'game-kit-assembly-receipt.json'
        source_pin = build_delivery.digest(self.source_path)
        verified = kit.read_assembled(self.args.output, build_delivery.digest(receipt), source_pin)
        self.assertEqual(verified.manifest_bytes, (self.args.output/'forge-kit/game-export-v1.json').read_bytes())
        self.assertEqual(verified.manifest_sha256, verified.receipt['kit_manifest_sha256'])
        self.assertEqual(verified.kit_root, self.args.output/'game-export-kit')
        self.assertEqual(verified, kit.read_assembled(self.args.output, build_delivery.digest(receipt), source_pin))

    def test_adjacent_staging_copies_only_the_verified_kit_next_to_studio(self):
        fixture = self.prepare_studio_package()
        receipt = self.stage_studio_package(fixture)
        root, studio, _, _, _, bins = fixture
        expected = json.loads((root/'forge-kit/game-export-v1.json').read_bytes())['files']
        self.assertEqual(kit.inventory(bins/'game-export-kit'), expected)
        self.assertEqual((bins/'forge-studio.exe').read_bytes(), studio.read_bytes())
        self.assertEqual({path.name for path in bins.iterdir()}, {'forge-studio.exe', 'game-export-kit'})
        self.assertFalse(receipt['runtime_verified'] or receipt['installation_verified'] or receipt['distribution_qualified'])

    def test_adjacent_staging_refuses_stale_studio_or_sources(self):
        fixture = self.prepare_studio_package()
        bins = fixture[-1]
        (bins/'forge-studio.exe').write_bytes(inert_windows_x64_pe(b'different Studio'))
        with self.assertRaisesRegex(ValueError, 'Packaged Studio differs'):
            self.stage_studio_package(fixture)
        (bins/'forge-studio.exe').write_bytes(fixture[1].read_bytes())
        changed = {**self.source, 'files':{**self.source['files'],
                   'apps/forge_runtime/Cargo.toml':{'sha256':'f'*64,'bytes':1}}}
        self.shared.return_value = changed
        with self.assertRaisesRegex(ValueError, 'sources are stale'):
            self.stage_studio_package(fixture)
        self.assertFalse((bins/'game-export-kit').exists())

    def test_adjacent_staging_preserves_existing_destination(self):
        fixture = self.prepare_studio_package()
        target = fixture[-1]/'game-export-kit'; target.mkdir()
        (target/'existing').write_text('preserve', encoding='utf-8')
        with self.assertRaisesRegex(ValueError, 'already exists'):
            self.stage_studio_package(fixture)
        self.assertEqual((target/'existing').read_text(encoding='utf-8'), 'preserve')

    def test_adjacent_staging_refuses_source_kit_change_during_copy(self):
        fixture = self.prepare_studio_package()
        original_copy = shutil.copyfileobj
        changed = []
        def mutate(source, destination, length):
            original_copy(source, destination, length)
            if not changed:
                (fixture[0]/'game-export-kit'/kit.RUNTIME).write_bytes(inert_windows_x64_pe(b'changed runtime'))
                changed.append(True)
        with mock.patch.object(kit.shutil, 'copyfileobj', side_effect=mutate):
            with self.assertRaises(ValueError):
                self.stage_studio_package(fixture)
        self.assertTrue(changed)
        self.assertFalse((fixture[-1]/'game-export-kit').exists())
        self.assertEqual(list(fixture[-1].glob('.forge-game-kit-*')), [])

    def test_reader_rejects_stale_receipt_source_or_content(self):
        self.assemble()
        root = self.args.output
        receipt_pin = build_delivery.digest(root/'game-kit-assembly-receipt.json')
        source_pin = build_delivery.digest(self.source_path)
        with self.assertRaisesRegex(ValueError, 'receipt pin differs'):
            kit.read_assembled(root, 'f'*64, source_pin)
        with self.assertRaisesRegex(ValueError, 'current private'):
            kit.read_assembled(root, receipt_pin, 'f'*64)
        (root/'game-export-kit'/kit.RUNTIME).write_bytes(b'changed')
        with self.assertRaisesRegex(ValueError, 'files differ'):
            kit.read_assembled(root, receipt_pin, source_pin)

    def test_reader_rejects_uninventoried_files_and_unexpected_root_entries(self):
        self.assemble()
        root = self.args.output
        receipt_pin = build_delivery.digest(root/'game-kit-assembly-receipt.json')
        source_pin = build_delivery.digest(self.source_path)
        (root/'game-export-kit/extra.txt').write_text('uncontrolled')
        with self.assertRaisesRegex(ValueError, 'files differ'):
            kit.read_assembled(root, receipt_pin, source_pin)
        (root/'game-export-kit/extra.txt').unlink()
        (root/'unexpected.txt').write_text('uncontrolled')
        with self.assertRaisesRegex(ValueError, 'unexpected entries'):
            kit.read_assembled(root, receipt_pin, source_pin)

    def test_reader_rejects_public_rights_even_when_rehashed(self):
        self.assemble()
        root = self.args.output
        manifest_path = root/'forge-kit/game-export-v1.json'
        manifest = json.loads(manifest_path.read_bytes()); manifest['rights'] = 'public'
        write(manifest_path, manifest)
        receipt_path = root/'game-kit-assembly-receipt.json'
        receipt = json.loads(receipt_path.read_bytes()); receipt['kit_manifest_sha256'] = build_delivery.digest(manifest_path)
        write(receipt_path, receipt)
        with self.assertRaisesRegex(ValueError, 'private candidate'):
            kit.read_assembled(root, build_delivery.digest(receipt_path), build_delivery.digest(self.source_path))

    def test_reader_rejects_unsupported_notice_schema_even_when_rehashed(self):
        self.assemble()
        root = self.args.output
        notice_path = root/'game-export-kit/licenses/rust-runtime-candidate/INDEX.json'
        notice = json.loads(notice_path.read_bytes()); notice['schema'] = 'unsupported'
        write(notice_path, notice)
        manifest_path = root/'forge-kit/game-export-v1.json'
        manifest = json.loads(manifest_path.read_bytes()); manifest['files'] = kit.inventory(root/'game-export-kit')
        write(manifest_path, manifest)
        receipt_path = root/'game-kit-assembly-receipt.json'
        receipt = json.loads(receipt_path.read_bytes())
        receipt['kit_manifest_sha256'] = build_delivery.digest(manifest_path)
        receipt['kit_file_bytes'] = sum(item['bytes'] for item in manifest['files'])
        write(receipt_path, receipt)
        with self.assertRaisesRegex(ValueError, 'Unsupported staged notice index'):
            kit.read_assembled(root, build_delivery.digest(receipt_path), build_delivery.digest(self.source_path))

    def test_unpinned_launcher_or_runtime_refused_before_output(self):
        self.launcher.write_bytes(b'changed')
        with self.assertRaises(ValueError):self.assemble()
        self.assertFalse(self.args.output.exists())
        self.launcher.write_bytes(self.launcher_bytes);self.runtime.write_bytes(b'changed')
        with self.assertRaises(ValueError):self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_stale_source_or_proof_refused(self):
        self.launcher_proof['source_manifest_sha256']='f'*64;self.save_proofs()
        with self.assertRaises(ValueError):self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_project_notice_must_match_the_pinned_checkout(self):
        self.project_notice.write_bytes(b'Changed notice')
        with self.assertRaisesRegex(ValueError, 'Project notice differs'):
            self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_aube_marker_refused_even_if_binary_hash_is_current(self):
        self.launcher.write_bytes(inert_windows_x64_pe(b'inert '+kit.AUBE))
        self.launcher_proof['executable_sha256']=build_delivery.digest(self.launcher)
        self.launcher_proof['launcher_verification']['executable_sha256']=build_delivery.digest(self.launcher)
        self.save_proofs()
        with self.assertRaisesRegex(ValueError,'Aube marker'):self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_wrong_frontend_profile_feature_and_tauri_title_refused(self):
        self.launcher_proof['frontend_profile']='aube';self.save_proofs()
        with self.assertRaises(ValueError):self.assemble()
        self.launcher_proof['frontend_profile']='standalone';self.launcher_proof['cargo_features']=['production'];self.save_proofs()
        with self.assertRaises(ValueError):self.assemble()
        self.launcher_proof['cargo_features']=['standalone']
        self.launcher_proof['build_environment']['TAURI_CONFIG']=json.dumps({'productName':'OpenRPG Forge'})
        self.save_proofs()
        with self.assertRaises(ValueError):self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_changed_embedded_frontend_refused(self):
        (self.frontend/'index.html').write_text('other')
        with self.assertRaisesRegex(ValueError,'frontend differs'):self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_frontend_path_must_be_isolated_and_match_tauri_config(self):
        config = kit.standalone_tauri_config(self.frontend, self.repo)
        relative = config['build']['frontendDist']
        self.assertFalse(Path(relative).is_absolute())
        self.assertFalse(urlsplit(relative).scheme)
        self.assertEqual((self.repo/'apps/forge_launcher'/relative).resolve(), self.frontend.resolve())
        config['build']['frontendDist'] = str(self.frontend)
        encoded = json.dumps(config)
        self.launcher_proof['build_environment']['TAURI_CONFIG'] = encoded
        self.launcher_proof['tauri_config_sha256'] = hashlib.sha256(encoded.encode()).hexdigest()
        self.save_proofs()
        with self.assertRaisesRegex(ValueError, 'Tauri config .* differs'):
            self.assemble()

        config = kit.standalone_tauri_config(self.frontend, self.repo)
        config['build']['frontendDist'] = str(self.root/'other')
        encoded = json.dumps(config)
        self.launcher_proof['build_environment']['TAURI_CONFIG'] = encoded
        self.launcher_proof['tauri_config_sha256'] = hashlib.sha256(encoded.encode()).hexdigest()
        self.save_proofs()
        with self.assertRaisesRegex(ValueError, 'Tauri config .* differs'):
            self.assemble()
        self.launcher_proof['frontend'] = str(self.repo/'apps/forge_launcher/dist')
        self.launcher_proof['build_environment']['TAURI_CONFIG'] = json.dumps(
            kit.standalone_tauri_config(self.repo/'apps/forge_launcher/dist', self.repo))
        self.save_proofs()
        with self.assertRaisesRegex(ValueError, 'direct child'):
            self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_wrong_runtime_feature_or_profile_refused(self):
        self.runtime_proof['application_role']='game';self.save_proofs()
        with self.assertRaises(ValueError):self.assemble()
        self.runtime_proof['application_role']='module-game'
        self.runtime_proof['commands'][0]['arguments'].remove('--no-default-features');self.save_proofs()
        with self.assertRaises(ValueError):self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_non_x64_windows_binary_refused_even_when_hash_is_current(self):
        raw = bytearray(self.runtime_bytes)
        raw[132:134] = b'\x4c\x01'
        self.runtime.write_bytes(raw)
        self.runtime_proof['executable_sha256'] = build_delivery.digest(self.runtime)
        self.save_proofs()
        with self.assertRaisesRegex(ValueError, 'Windows x64 PE'):
            self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_missing_standalone_notice_graph_refused(self):
        write(self.graph,{'scopes':[{'name':'launcher-production','target':kit.TARGET,'graphs':{'all_nondev':{'exit_code':0}}}]})
        with self.assertRaisesRegex(ValueError,'notice graph'):self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_launcher_dependency_closure_drift_refused(self):
        self.launcher_deps.return_value={'another-package'}
        with self.assertRaisesRegex(ValueError,'dependency closure'):self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_notice_scope_with_root_dev_dependencies_refused(self):
        files = dict(self.notices.files)
        index_path = 'licenses/rust-runtime-candidate/INDEX.json'
        index = json.loads(files[index_path])
        index['requests'][0]['include_root_dev'] = True
        files[index_path] = json.dumps(index).encode()
        self.notice_prepare.return_value = runtime_notices.PreparedNotices(
            tuple(files.items()), self.notices.project_notice, self.notices.project_links, self.notices.receipt)
        with self.assertRaisesRegex(ValueError, 'notice request'):
            self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_launcher_verification_must_name_the_required_command(self):
        def wrong_command(path):
            return {**self.verify_fake(path), 'command': '--other'}
        with self.assertRaisesRegex(ValueError, 'verification does not match'):
            kit.assemble(self.args, repo=self.repo, launcher_verifier=wrong_command)
        self.assertFalse(self.args.output.exists())

    def test_build_receipt_requires_actual_launcher_verification_and_orchestrator(self):
        self.launcher_proof['launcher_verification']['marker'] = 'wrong'
        self.save_proofs()
        with self.assertRaisesRegex(ValueError, 'build verification'):
            self.assemble()
        self.launcher_proof['launcher_verification']['marker'] = kit.STANDALONE
        self.runtime_proof['orchestrator_sha256'] = 'f'*64
        self.save_proofs()
        with self.assertRaisesRegex(ValueError, 'orchestrator differs'):
            self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_version_limit_counts_utf8_bytes(self):
        self.args.version = 'é' * 65
        with self.assertRaisesRegex(ValueError, 'version is invalid'):
            self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_inventory_refuses_reserved_names_and_reader_limits(self):
        folder = self.root/'inventory'; (folder/'game').mkdir(parents=True)
        reserved = folder/'game/launcher.json'; reserved.write_bytes(b'x')
        with self.assertRaisesRegex(ValueError, 'reserved'):
            kit.inventory(folder)
        reserved.unlink()
        (folder/'game/allowed').write_bytes(b'xx')
        with mock.patch.object(kit, 'MAX_FILE_BYTES', 1):
            with self.assertRaisesRegex(ValueError, 'reader limits'):
                kit.inventory(folder)

    @unittest.skipUnless(os.name == 'nt', 'Windows junction test')
    def test_reader_refuses_a_real_junction_without_visiting_its_target(self):
        self.assemble()
        external = self.root/'external'; external.mkdir()
        sentinel = external/'unique.txt'; sentinel.write_text('preserve', encoding='utf-8')
        junction = self.args.output/'game-export-kit/junction'
        creation = subprocess.run(['cmd', '/c', 'mklink', '/J', str(junction), str(external)],
                                  capture_output=True, text=True, check=False)
        if creation.returncode != 0:
            self.skipTest(f'Cannot create a test junction: {creation.stderr}')
        try:
            with self.assertRaisesRegex(ValueError, 'reparse entry'):
                kit.read_assembled(self.args.output,
                    build_delivery.digest(self.args.output/'game-kit-assembly-receipt.json'),
                    build_delivery.digest(self.source_path))
            self.assertEqual(sentinel.read_text(encoding='utf-8'), 'preserve')
        finally:
            os.rmdir(junction)

    def test_existing_output_is_never_overwritten(self):
        self.args.output.mkdir();sentinel=self.args.output/'other';sentinel.write_text('keep')
        with self.assertRaises(ValueError):self.assemble()
        self.assertEqual(sentinel.read_text(),'keep')

    def test_notice_failure_refuses_before_creating_output(self):
        self.notice_prepare.side_effect=ValueError('stale notices')
        with self.assertRaisesRegex(ValueError,'stale notices'):self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_real_notice_validator_is_wired_into_staging(self):
        fixture = test_runtime_notices.RuntimeNoticeCandidateTests('test_refuses_missing_inputs_and_unpinned_entries')
        fixture.setUp()
        self.addCleanup(fixture.doCleanups)
        shutil.copyfile(fixture.project, self.repo/'THIRD_PARTY_NOTICES.md')
        self.source['files'].update(fixture.source['files'])
        self.source['working_tree_files']['THIRD_PARTY_NOTICES.md'] = build_delivery.digest(self.project_notice)
        write(self.source_path, self.source)
        for proof in (self.launcher_proof, self.runtime_proof):
            proof['source_manifest_sha256'] = build_delivery.digest(self.source_path)
        self.save_proofs()
        project_link = self.repo/'third_party/example/LICENSE'
        project_link.parent.mkdir(parents=True)
        shutil.copyfile(fixture.root/'third_party/example/LICENSE', project_link)
        collector = self.repo/'scripts/tidewater/collect_rust_notices.py'
        collector.parent.mkdir(parents=True)
        shutil.copyfile(Path(runtime_notices.__file__).resolve().parents[1]/'tidewater/collect_rust_notices.py', collector)
        index_path = fixture.bundle/'INDEX.json'
        index = json.loads(index_path.read_bytes())
        index['requests'].append({'package':'forge_launcher_prototype','target':kit.TARGET,
            'features':['standalone'],'no_default_features':False,'include_root_dev':False,'package_ids':sorted(self.ids)})
        write(index_path, index)
        base_hash = runtime_notices.sha(index_path.read_bytes())
        supplement_path = fixture.bundle/'SUPPLEMENT-NOTICES.json'
        supplement = json.loads(supplement_path.read_bytes())
        supplement.update(schema='forge.rust-notice-supplements.v1', base_index_sha256=base_hash)
        write(supplement_path, supplement)
        forge_path = fixture.bundle/'FORGE-ORIGINAL-NOTICES.json'
        forge = json.loads(forge_path.read_bytes())
        forge.update(schema='forge.original-code-notices.v1', base_index_sha256=base_hash,
                     supplement_index_sha256=runtime_notices.sha(supplement_path.read_bytes()))
        write(forge_path, forge)
        asset_path = fixture.bundle/'ASSET-NOTICES.json'
        asset = json.loads(asset_path.read_bytes())
        asset['base_index_sha256'] = base_hash
        write(asset_path, asset)
        graph = json.loads(fixture.graph.read_bytes())
        graph['scopes'].append({'name':'launcher-standalone','target':kit.TARGET,
                                'graphs':{'all_nondev':{'exit_code':0}}})
        write(fixture.graph, graph)
        candidate = json.loads(fixture.candidate.read_bytes())
        candidate.update(graph_evidence_sha256=runtime_notices.sha(fixture.graph.read_bytes()),
                         base_index_sha256=base_hash,
                         supplement_index_sha256=runtime_notices.sha(supplement_path.read_bytes()),
                         forge_index_sha256=runtime_notices.sha(forge_path.read_bytes()),
                         asset_index_sha256=runtime_notices.sha(asset_path.read_bytes()),
                         bundle_bytes=sum(path.stat().st_size for path in [index_path,supplement_path,
                                          forge_path,asset_path,fixture.text_path]))
        write(fixture.candidate, candidate)
        self.args.runtime_notice_bundle = fixture.bundle
        self.args.runtime_notice_receipt = fixture.candidate
        self.args.runtime_notice_graph = fixture.graph
        self.args.cargo_cache.mkdir(parents=True)
        metadata = {'packages':[{'id':'local:forge_runtime','name':'forge_runtime'},
                                {'id':'registry:dependency','name':'dependency'}],
                    'resolve':{'nodes':[{'id':'local:forge_runtime','deps':[{'pkg':'registry:dependency',
                                        'dep_kinds':[{'kind':None}]}]},
                                        {'id':'registry:dependency','deps':[]}]}}
        self.notice_prepare.side_effect = REAL_NOTICE_PREPARATION
        output = SimpleNamespace(returncode=0, stdout=json.dumps(metadata).encode())
        with mock.patch.object(runtime_notices, 'shared_source_inputs', return_value=self.source), \
             mock.patch.object(runtime_notices.subprocess, 'run', return_value=output):
            proof = self.assemble()
        self.assertEqual(proof['runtime_notices']['current_runtime_package_count'], 2)
        self.assertFalse(proof['runtime_notices']['distribution_qualified'])
        self.assertEqual((self.args.output/'game-export-kit/licenses/THIRD_PARTY_NOTICES.md').read_bytes(),
                         fixture.project.read_bytes())

    def test_source_change_during_staging_is_refused_and_cleaned(self):
        changed={**self.source,'files':{**self.source['files'],'apps/forge_runtime/Cargo.toml':{'sha256':'c'*64,'bytes':1}}}
        self.shared.side_effect=[self.source,changed]
        with self.assertRaises(ValueError):self.assemble()
        self.assertFalse(self.args.output.exists())
        self.assertEqual(list(self.root.glob('.forge-game-kit-*')),[])

    def test_copy_failure_cleans_only_owned_staging(self):
        self.sources_prepare.return_value=self.application
        with mock.patch.object(application_sources,'stage_sources',side_effect=ValueError('simulated failure')):
            with self.assertRaisesRegex(ValueError,'simulated failure'):self.assemble()
        self.assertFalse(self.args.output.exists())
        self.assertEqual(list(self.root.glob('.forge-game-kit-*')),[])

    def test_verify_launcher_checks_exact_marker_without_running_fixture(self):
        response=SimpleNamespace(returncode=0,stdout=kit.STANDALONE+'\n',stderr='')
        with mock.patch.object(kit.subprocess,'run',return_value=response) as run:
            result=kit.verify_launcher(self.launcher)
            self.assertEqual(result['marker'],kit.STANDALONE)
            self.assertEqual(run.call_args.args[0],[str(self.launcher),'--verify-distributable'])
        response.stdout='FORGE_LAUNCHER_AUBE_FRONTEND_READY\n'
        with mock.patch.object(kit.subprocess,'run',return_value=response):
            with self.assertRaises(ValueError):kit.verify_launcher(self.launcher)
        response.stdout=' '+kit.STANDALONE+'\n'
        with mock.patch.object(kit.subprocess,'run',return_value=response):
            with self.assertRaises(ValueError):kit.verify_launcher(self.launcher)

    def test_linux_target_refuses_windows_build_receipts(self):
        self.args.platform='linux-x64'
        with self.assertRaises(ValueError):self.assemble()
        self.assertFalse(self.args.output.exists())


class LinuxGameKitTests(unittest.TestCase):
    """Exercise real Linux core proof validation with inert binaries and mock notices."""
    def setUp(self):
        base=GameKitTests('test_assembles_private_kit_with_pinned_notices_and_no_qualification_claim')
        base.setUp(); self.addCleanup(base.doCleanups); self.base=base
        core=test_linux_build_evidence.LinuxCoreEvidenceTests('test_accepts_matching_snapshot_and_both_elf_builds')
        core.setUp(); self.addCleanup(core.doCleanups); self.core=core
        core.contents['scripts/delivery/game_kit.py']=Path(kit.__file__).read_bytes()
        core.manifest['repository']=r'E:\OpenRPG_Forge\OpenRPG_Forge-Rust'
        core.manifest['files']={name:{'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)}
                                for name,data in core.contents.items()}
        raw=json.dumps(core.manifest).encode()
        with zipfile.ZipFile(core.zip,'w') as archive:
            for name,data in core.contents.items(): archive.writestr(name,data)
            archive.writestr('__forge_vm_source_manifest__.json',raw)
        (core.output/'source-manifest.json').write_bytes(raw)
        source_sha=kit.linux_build_evidence.sha(core.zip); manifest_sha=hashlib.sha256(raw).hexdigest()
        owner={'schema':'forge.gate7.linux-isolated-target.v1','run_id':'fixture-001','source_sha256':source_sha,
               'target':'/qualification/isolated-targets/fixture-001','cargo_home':'/qualification/cargo-home'}
        owner_sha=hashlib.sha256((json.dumps(owner,sort_keys=True,separators=(',', ':'))+'\n').encode()).hexdigest()
        core.started.update(schema='forge.gate7.linux-editor-launcher-started.v1',suite='core',
            standalone_launcher_requested=True,gate_closed=False,launcher_qualified=False,
            source_sha256=source_sha,source_manifest_sha256=manifest_sha,isolated_owner_sha256=owner_sha)
        core.capture.update(sha256=source_sha,archive_bytes=core.zip.stat().st_size,files=len(core.contents))
        frontend='/qualification/tmp/standalone-launcher-fixture-001'
        config={**kit.STANDALONE_TAURI_CONFIG,'build':{'frontendDist':frontend}}
        core.launcher.update(source_sha256=source_sha,source_manifest_sha256=manifest_sha,
            isolated_owner_sha256=owner_sha,source_inputs={name:core.manifest['files'][name]
                                                          for name in kit.linux_build_evidence.LAUNCHER_SOURCE_INPUTS},
            tauri_config=config,tauri_config_sha256=hashlib.sha256(json.dumps(config,ensure_ascii=False,
                                                     separators=(',', ':')).encode()).hexdigest())
        core.core.update(source_sha256=source_sha,source_manifest_sha256=manifest_sha,isolated_owner_sha256=owner_sha)
        core.save_proofs()
        self.repo=core.root/'snapshot'; self.repo.mkdir()
        for name,data in core.contents.items():
            path=self.repo/name; path.parent.mkdir(parents=True,exist_ok=True); path.write_bytes(data)
        started=core.root/'started.json'; capture=core.root/'capture.json'
        write(started,core.started); write(capture,core.capture)
        notice=base.notices
        launcher_scope={'command':['cargo','metadata','--manifest-path',
            '/qualification/sources/fixture-001/apps/forge_launcher/Cargo.toml','--format-version','1',
            '--locked','--offline','--filter-platform',runtime_notices.LINUX_TARGET,'--features','standalone'],
            'package_count':2,'metadata_sha256':'a'*64,'current_package_ids_sha256':'b'*64,
            'captured_package_ids_sha256':'c'*64,'normalized_package_ids_sha256':'d'*64}
        self.notice=runtime_notices.PreparedNotices(notice.files,notice.project_notice,notice.project_links,
                     {**notice.receipt,'runtime_target':runtime_notices.LINUX_TARGET,
                      'launcher_standalone_scope':launcher_scope})
        self.args=SimpleNamespace(platform=kit.LINUX_PLATFORM,version='candidate-linux',output=core.root/'linux-kit',
            source_manifest=core.output/'source-manifest.json',launcher=core.output/'forge-launcher-prototype',
            launcher_proof=core.output/'launcher-build-proof.json',module_runtime=core.output/'forge-module',
            module_runtime_proof=core.output/'linux-core-artifacts.json',cargo_cache='/qualification/cargo-home/registry',
            cargo_target='/qualification/isolated-targets/fixture-001',
            linux_source_zip=core.zip,linux_capture_receipt=capture,linux_capture_sha256=build_delivery.digest(capture),
            linux_started=started,linux_started_sha256=build_delivery.digest(started),linux_core_proof_sha256=core.core_sha)
        base.patches.enter_context(mock.patch.object(application_sources,'prepare_sources',return_value=base.application))
        self.linux_notices=base.patches.enter_context(mock.patch.object(runtime_notices,'prepare_for_linux_snapshot',
                                                                       return_value=self.notice))

    def assemble(self):
        with mock.patch.object(kit.sys,'platform','linux'):
            return kit.assemble(self.args,repo=self.repo,
                                launcher_verifier=lambda _:self.fail('A Linux ELF must not use the Windows verifier'))

    def test_assembles_and_reads_linux_kit_from_the_real_core_reader(self):
        proof=self.assemble()
        root=self.args.output; receipt_pin=build_delivery.digest(root/'game-kit-assembly-receipt.json')
        source_pin=build_delivery.digest(self.args.source_manifest)
        verified=kit.read_assembled(root,receipt_pin,source_pin,expected_platform=kit.LINUX_PLATFORM)
        self.assertEqual(verified.manifest['platform'],'linux-x64')
        self.assertEqual(verified.manifest['launcher'],kit.LINUX_LAUNCHER)
        self.assertEqual(verified.manifest['module_runtime'],kit.LINUX_RUNTIME)
        self.assertEqual(proof['linux_provenance']['core_proof_sha256'],self.core.core_sha)
        self.assertEqual(proof['launcher_verification']['source'],'linux-sdk-observation')
        self.assertFalse(proof['distribution_qualified'])
        self.assertFalse(hasattr(self.args,'shared_working_tree'))
        self.assertEqual(self.linux_notices.call_count,2)
        self.assertTrue(self.linux_notices.call_args.kwargs['include_launcher'])
        self.assertEqual(self.linux_notices.call_args.args[1:7],
            (self.repo,self.core.zip,self.args.source_manifest,self.core.started['source_sha256'],
             source_pin,self.args.linux_capture_receipt))
        with self.assertRaises(ValueError):kit.read_assembled(root,receipt_pin,source_pin)
        if os.name!='nt':
            for item in verified.manifest['files']:
                path=root/'game-export-kit'/item['path']
                executable=bool(path.stat().st_mode & 0o111)
                self.assertEqual(executable,item['path'] in (kit.LINUX_LAUNCHER,kit.LINUX_RUNTIME))

    def test_refuses_changed_binary_or_wrong_pinned_receipt_without_output(self):
        binary=self.args.module_runtime; original=binary.read_bytes(); binary.write_bytes(original+b'tampered')
        with self.assertRaisesRegex(ValueError,'executable or dependency'):self.assemble()
        self.assertFalse(self.args.output.exists())
        binary.write_bytes(original)
        self.args.linux_started_sha256='0'*64
        with self.assertRaisesRegex(ValueError,'pins are required'):self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_refuses_missing_launcher_notice_closure(self):
        self.linux_notices.return_value=runtime_notices.PreparedNotices(
            self.notice.files,self.notice.project_notice,self.notice.project_links,{'distribution_qualified':False})
        with self.assertRaisesRegex(ValueError,'launcher notice closure'):self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_refuses_windows_or_default_launcher_notice_scope(self):
        scope=dict(self.notice.receipt['launcher_standalone_scope'])
        command=list(scope['command']); command[9]='x86_64-pc-windows-msvc'; scope['command']=command
        receipt={**self.notice.receipt,'launcher_standalone_scope':scope}
        self.linux_notices.return_value=runtime_notices.PreparedNotices(
            self.notice.files,self.notice.project_notice,self.notice.project_links,receipt)
        with self.assertRaisesRegex(ValueError,'notice command differs'):self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_refuses_cargo_paths_from_another_build(self):
        for field,value in (('cargo_cache','/qualification/other/registry'),
                            ('cargo_target','/qualification/isolated-targets/another')):
            old=getattr(self.args,field); setattr(self.args,field,value)
            with self.subTest(field=field), self.assertRaisesRegex(ValueError,'Cargo home and target'):
                self.assemble()
            setattr(self.args,field,old)
        self.assertFalse(self.args.output.exists())

    def test_refuses_changed_core_proof_during_staging_and_cleans_owned_files(self):
        sentinel=self.core.root/'untouched'; sentinel.write_bytes(b'keep')
        original=build_delivery.copy
        def changed(source,destination):
            original(source,destination)
            if Path(source)==self.args.module_runtime:
                with self.args.module_runtime_proof.open('ab') as stream: stream.write(b'changed')
        with mock.patch.object(build_delivery,'copy',side_effect=changed):
            with self.assertRaisesRegex(ValueError,'Build receipt changed'):self.assemble()
        self.assertFalse(self.args.output.exists())
        self.assertEqual(sentinel.read_bytes(),b'keep')
        self.assertEqual(list(self.core.root.glob('.forge-game-kit-*')),[])

    def test_linux_reader_refuses_a_modified_kit_file(self):
        self.assemble()
        root=self.args.output; receipt_pin=build_delivery.digest(root/'game-kit-assembly-receipt.json')
        source_pin=build_delivery.digest(self.args.source_manifest)
        runtime=root/'game-export-kit'/kit.LINUX_RUNTIME
        with runtime.open('ab') as stream: stream.write(b'changed')
        with self.assertRaisesRegex(ValueError,'files differ'):
            kit.read_assembled(root,receipt_pin,source_pin,expected_platform=kit.LINUX_PLATFORM)

    def test_linux_adjacent_stage_reuses_kit_and_requires_matching_studio_overlay(self):
        import stage_studio_game_kit
        self.assemble()
        root=self.args.output; receipt_pin=build_delivery.digest(root/'game-kit-assembly-receipt.json')
        source_pin=build_delivery.digest(self.args.source_manifest)
        frontend=self.repo/'apps/forge_studio/dist'; frontend.mkdir(parents=True)
        stage_studio_game_kit.stage(root,frontend,receipt_pin,source_pin,expected_platform=kit.LINUX_PLATFORM)
        studio=self.core.output/'forge-studio'; studio.write_bytes(test_linux_build_evidence.elf(b'inert studio'))
        proof=self.core.output/'receipt.json'; write(proof,{'fixture':'linux studio run'})
        dest=self.core.root/'delivery/bin'; dest.mkdir(parents=True)
        shutil.copyfile(studio,dest/'forge-studio')
        proof_pin=build_delivery.digest(proof)
        observed={'source_manifest_sha256':source_pin,'studio_sha256':build_delivery.digest(studio),
            'kit_manifest_sha256':build_delivery.digest(root/'forge-kit/game-export-v1.json'),
            'overlay_stage_sha256':build_delivery.digest(frontend/'forge-game-export-staging-receipt.json')}
        context={'source_zip':self.core.zip,'started_path':self.args.linux_started,
                 'capture_path':self.args.linux_capture_receipt,'started_sha256':self.args.linux_started_sha256,
                 'capture_sha256':self.args.linux_capture_sha256}
        read=kit.read_document
        def document(path):
            result=read(path)
            if Path(path) == self.args.source_manifest:
                result['files']['scripts/delivery/build_delivery.py']=pinned(Path(build_delivery.__file__).read_bytes())
            return result
        with mock.patch.object(kit.sys,'platform','linux'), mock.patch.object(kit,'read_document',side_effect=document), \
                mock.patch.object(runtime_notices,'_snapshot_module',return_value=object()), \
                mock.patch.object(runtime_notices,'_verify_extracted_snapshot'), \
                mock.patch.object(kit.linux_build_evidence,'verify_linux_studio',return_value=observed):
            record=kit.stage_studio_adjacent(root,studio,proof,self.args.source_manifest,receipt_pin,proof_pin,dest,
                                            repo=self.repo,expected_platform=kit.LINUX_PLATFORM,linux_context=context)
        self.assertEqual(record['kit_assembly_receipt_sha256'],receipt_pin)
        self.assertEqual(kit.inventory(dest/'game-export-kit'),
                         kit.read_assembled(root,receipt_pin,source_pin,expected_platform=kit.LINUX_PLATFORM).manifest['files'])
        self.assertFalse(record['installation_verified'])

    def test_linux_rejects_linked_core_input_before_staging(self):
        link=self.core.root/'linked-core'
        try:
            link.symlink_to(self.core.output,target_is_directory=True)
        except (OSError,NotImplementedError):
            self.skipTest('Symbolic links are unavailable')
        try:
            self.args.launcher=link/'forge-launcher-prototype'
            with self.assertRaisesRegex(ValueError,'paths must be physical'): self.assemble()
            self.assertFalse(self.args.output.exists())
        finally:
            link.unlink()


if __name__=='__main__':unittest.main(verbosity=2)
