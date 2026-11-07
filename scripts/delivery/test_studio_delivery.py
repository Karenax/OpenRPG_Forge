import contextlib
import hashlib
import json
import ntpath
import os
from pathlib import Path
import shutil
import tempfile
from types import SimpleNamespace
import unittest
from unittest import mock

import build_delivery as delivery
import game_kit
import runtime_notices
import stage_studio_game_kit
import test_game_kit


def blob(value):
    return json.dumps(value, sort_keys=True).encode()


class StudioDeliveryTests(unittest.TestCase):
    def setUp(self):
        fixture = test_game_kit.GameKitTests('test_assembles_private_kit_with_pinned_notices_and_no_qualification_claim')
        fixture.setUp()
        self.addCleanup(fixture.doCleanups)
        self.fixture = fixture
        repo = fixture.repo
        source = fixture.source
        for name in ('.cargo/config.toml', 'Cargo.toml', 'Cargo.lock', 'apps/forge_studio/Cargo.toml',
                     'scripts/tidewater/collect_rust_notices.py'):
            path = repo / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text('fixture ' + name, encoding='utf-8')
            source['files'][name] = test_game_kit.pinned(path.read_bytes())
        test_game_kit.write(fixture.source_path, source)
        source_hash = delivery.digest(fixture.source_path)
        fixture.launcher_proof['source_manifest_sha256'] = source_hash
        fixture.runtime_proof['source_manifest_sha256'] = source_hash
        fixture.save_proofs()

        prefix = 'licenses/rust-runtime-candidate/'
        files = dict(fixture.notices.files)
        index = json.loads(files[prefix+'INDEX.json'])
        base_notice = index['packages'][0]['notices'][0]
        index['packages'] = [{'package_id':name, 'notices':[base_notice]} for name in sorted(fixture.ids)]
        index['packages'].extend({'package_id':name, 'notices':[]} for name in ('native-crate', 'web-crate'))
        self.scopes = []
        for target, features, request_features, no_default, ids in (
                ('x86_64-pc-windows-msvc', ['tauri/custom-protocol'], [], False, ['native-crate']),
                ('wasm32-unknown-unknown', ['web'], ['forge_studio/web'], True, ['web-crate'])):
            index['requests'].append({'manifest':str(repo/'Cargo.toml'), 'package':'forge_studio',
                'target':target, 'features':request_features, 'no_default_features':no_default,
                'include_root_dev':False, 'package_ids':ids})
            ids_sha = hashlib.sha256(json.dumps(sorted(ids)).encode()).hexdigest()
            self.scopes.append({'target':target, 'manifest':str(repo/'apps/forge_studio/Cargo.toml'),
                'features':features, 'index_request_features':request_features, 'no_default_features':no_default,
                'include_root_dev':False, 'resolved_package_count':len(ids), 'index_package_count':len(ids),
                'resolved_ids_sha256':ids_sha, 'index_ids_sha256':ids_sha, 'ids_equal':True,
                'resolved_only':[], 'index_only':[], 'resolved_ids_missing_from_bundle':[],
                'unresolved_identity_cases':['web-crate'] if no_default else []})
        files[prefix+'INDEX.json'] = blob(index)
        index_sha = hashlib.sha256(files[prefix+'INDEX.json']).hexdigest()
        supplement = json.loads(files[prefix+'SUPPLEMENT-NOTICES.json'])
        supplement['base_index_sha256'] = index_sha
        supplement['packages'] = [{'package_id':'native-crate',
                                   'notices':[{'path':base_notice, 'sha256':Path(base_notice).stem}]}]
        files[prefix+'SUPPLEMENT-NOTICES.json'] = blob(supplement)
        supplement_sha = hashlib.sha256(files[prefix+'SUPPLEMENT-NOTICES.json']).hexdigest()
        for name in ('FORGE-ORIGINAL-NOTICES.json', 'ASSET-NOTICES.json'):
            value = json.loads(files[prefix+name])
            value['base_index_sha256'] = index_sha
            if name.startswith('FORGE-'):
                value['supplement_index_sha256'] = supplement_sha
            files[prefix+name] = blob(value)
        notice = fixture.notices
        fixture.notices = runtime_notices.PreparedNotices(tuple(sorted(files.items())), notice.project_notice,
            notice.project_links, {**notice.receipt, 'graph_receipt_sha256':delivery.digest(fixture.graph)})
        fixture.notice_prepare.return_value = fixture.notices
        root, studio, studio_proof, assembly_pin, studio_pin, _ = fixture.prepare_studio_package()

        cli = fixture.root/'delivery.exe'
        cli.write_bytes(test_game_kit.inert_windows_x64_pe(b'inert delivery CLI'))
        cli_proof = fixture.root/'delivery-proof.json'
        registry = fixture.root/'cargo/registry'
        registry.mkdir(parents=True)
        test_game_kit.write(cli_proof, {'schema':'forge.delivery.native-build.v1', 'status':'BUILD_ONLY',
            'shared_working_tree':True, 'source_root':str(repo), 'source_manifest_sha256':source_hash,
            'source_stable_during_build':True, 'application_role':'delivery-cli', 'profile':'release',
            'executable':str(cli), 'executable_sha256':delivery.digest(cli),
            'built_executable_sha256':delivery.digest(cli), 'orchestrator_sha256':'c'*64,
            'build_environment':{'CARGO_HOME':str(registry.parent)},
            'commands':[{'cwd':str(repo/'tools/forge_delivery'), 'arguments':['cargo','build','--manifest-path',
                str(repo/'tools/forge_delivery/Cargo.toml'),'--bin','forge_delivery','--release','--locked',
                '--offline','-j','2']}]})
        pins = {name:source['files'][name]['sha256'] for name in ('.cargo/config.toml', 'Cargo.toml', 'Cargo.lock',
                                                                  'apps/forge_studio/Cargo.toml')}
        pins.update(collector=source['files']['scripts/tidewater/collect_rust_notices.py']['sha256'],
                    index=index_sha, graph_receipt=delivery.digest(fixture.graph), recalculation_receipt='0'*64)
        audit = {'schema':'forge.gate7.studio-notice-scope-audit.v1', 'inputs_sha256_before':pins,
                 'inputs_sha256_after':pins.copy(), 'changed_inputs':[], 'index_sha256_matches_recalculation':True,
                 'results':self.scopes, 'distribution_qualified':False, 'gate_closed':False}
        audit_path = fixture.root/'notice-audit.json'; test_game_kit.write(audit_path, audit)
        self.args = SimpleNamespace(delivery_cli=cli, delivery_cli_proof=cli_proof,
            delivery_cli_proof_sha256=delivery.digest(cli_proof), studio=studio, studio_proof=studio_proof,
            studio_proof_sha256=studio_pin, source_manifest=fixture.source_path, kit_root=root,
            kit_receipt_sha256=assembly_pin, studio_notice_audit=audit_path,
            studio_notice_audit_sha256=delivery.digest(audit_path), cargo_cache=registry,
            output=fixture.root/'studio-output',
            version='test-1')
        self.audit = audit
        self.patcher = mock.patch.object(delivery, 'run', side_effect=self.pack_inert)
        self.patcher.start(); self.addCleanup(self.patcher.stop)
        self.source_patcher = mock.patch.object(delivery, 'shared_source_inputs', return_value=source)
        self.source_patcher.start(); self.addCleanup(self.source_patcher.stop)
        self.cli_scope = {'target':runtime_notices.WINDOWS_TARGET, 'package':'forge_delivery',
                          'cargo_home':str(registry.parent), 'package_count':2, 'package_ids_sha256':'d'*64}
        self.cli_scope_patcher = mock.patch.object(runtime_notices, 'windows_delivery_cli_scope',
                                                   return_value=self.cli_scope)
        self.cli_scope_mock = self.cli_scope_patcher.start(); self.addCleanup(self.cli_scope_patcher.stop)

    def pack_inert(self, cli, action, root, descriptor_path, output):
        self.assertEqual(action, 'pack')
        descriptor = json.loads(Path(descriptor_path).read_bytes())
        descriptor['files'] = game_kit.inventory(root)
        header = json.dumps(descriptor, separators=(',', ':'), ensure_ascii=False).encode()
        with Path(output).open('xb') as stream:
            stream.write(b'FORGEPK1' + len(header).to_bytes(8, 'little') + header)
            for entry in descriptor['files']:
                stream.write((Path(root)/entry['path']).read_bytes())
        return {'descriptor':descriptor, 'manifest_sha256':hashlib.sha256(header).hexdigest(),
                'archive_sha256':delivery.digest(output)}

    def assemble(self):
        return delivery.assemble_studio_only(self.args, repo=self.fixture.repo)

    def test_builds_private_editor_only_package_with_kit_and_reservations(self):
        result = self.assemble()
        output = self.args.output
        receipt = json.loads((output/'studio-assembly-receipt.json').read_bytes())
        descriptor = json.loads((output/'forge-studio.manifest.json').read_bytes())
        self.assertEqual(result['receipt_sha256'], delivery.digest(output/'studio-assembly-receipt.json'))
        self.assertFalse(receipt['distribution_qualified'])
        self.assertEqual(receipt['notice_audit']['unresolved_identity_cases'], ['web-crate'])
        self.assertEqual(receipt['notice_audit']['windows_delivery_cli_offline_replay'], self.cli_scope)
        self.assertEqual(self.cli_scope_mock.call_count, 2)
        self.assertEqual(set(descriptor['product']['apps']), {'editor'})
        names = {entry['path'] for entry in descriptor['files']}
        self.assertIn('bin/forge-studio.exe', names)
        self.assertIn('bin/game-export-kit/bin/forge-module.exe', names)
        self.assertIn('bin/game-export-kit/bin/forge-game-launcher.exe', names)
        self.assertFalse(any('hub' in name.lower() or 'aube' in name.lower() for name in names))
        delivery.verify_package_stream(output/'forge-studio.forgepack', descriptor,
            receipt['package']['manifest_sha256'], receipt['package']['archive_sha256'])

    def test_refuses_mismatched_build_proof_and_preserves_output(self):
        self.args.delivery_cli_proof_sha256 = '0'*64
        with self.assertRaisesRegex(ValueError, 'proof pin'):
            self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_refuses_stale_notice_audit_even_when_its_new_receipt_is_pinned(self):
        self.audit['results'][1]['index_ids_sha256'] = '0'*64
        test_game_kit.write(self.args.studio_notice_audit, self.audit)
        self.args.studio_notice_audit_sha256 = delivery.digest(self.args.studio_notice_audit)
        with self.assertRaisesRegex(ValueError, 'scope does not match'):
            self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_refuses_notice_audit_that_omits_a_package_without_a_notice(self):
        self.audit['results'][1]['unresolved_identity_cases'] = []
        test_game_kit.write(self.args.studio_notice_audit, self.audit)
        self.args.studio_notice_audit_sha256 = delivery.digest(self.args.studio_notice_audit)
        with self.assertRaisesRegex(ValueError, 'scope does not match'):
            self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_refuses_missing_cli_coverage_or_wrong_build_registry(self):
        self.cli_scope_mock.side_effect = ValueError('CLI not covered')
        with self.assertRaisesRegex(ValueError, 'not covered'):
            self.assemble()
        self.assertFalse(self.args.output.exists())
        self.cli_scope_mock.side_effect = None
        self.cli_scope_mock.return_value = {**self.cli_scope, 'cargo_home':str(self.fixture.root/'other')}
        with self.assertRaisesRegex(ValueError, 'differs from its build Cargo home'):
            self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_refuses_cli_notice_closure_changed_during_packaging(self):
        self.cli_scope_mock.side_effect = [self.cli_scope, {**self.cli_scope, 'package_ids_sha256':'e'*64}]
        with self.assertRaisesRegex(ValueError, 'Inputs changed'):
            self.assemble()
        self.assertFalse(self.args.output.exists())

    def test_requires_the_cargo_registry_for_windows_studio(self):
        self.args.cargo_cache = None
        with self.assertRaisesRegex(ValueError, 'requires the delivery build Cargo registry'):
            self.assemble()
        self.cli_scope_mock.assert_not_called()

    def test_refuses_corrupt_package_payload_even_with_forged_archive_hash(self):
        self.assemble()
        path = self.args.output/'forge-studio.forgepack'
        descriptor = json.loads((self.args.output/'forge-studio.manifest.json').read_bytes())
        receipt = json.loads((self.args.output/'studio-assembly-receipt.json').read_bytes())
        with path.open('r+b') as stream:
            stream.seek(-1, 2)
            byte = stream.read(1)
            stream.seek(-1, 2)
            stream.write(bytes([byte[0] ^ 1]))
        with self.assertRaisesRegex(ValueError, 'Package file hash differs'):
            delivery.verify_package_stream(path, descriptor, receipt['package']['manifest_sha256'], delivery.digest(path))

    def test_hub_assembly_is_retired(self):
        argv = ['build_delivery.py', 'legacy', '--delivery-cli','cli', '--hub','hub', '--launcher','launcher',
                '--aube-proof','proof', '--studio','studio', '--studio-proof','sp', '--playtest','playtest',
                '--webview2','web', '--output','out', '--source-revision','rev', '--frozen-archive']
        with mock.patch.object(delivery, 'assemble_studio_only') as new, \
             mock.patch('sys.argv', argv), mock.patch('sys.stderr'), self.assertRaises(SystemExit) as refused:
            delivery.main()
        self.assertEqual(refused.exception.code, 2)
        new.assert_not_called()


class LinuxStudioNoticeAuditTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name); self.repo=self.root/'snapshot'; self.repo.mkdir()
        self.original=r'E:\Forge'; self.source={'repository':self.original,'files':{}}
        for name,data in (('Cargo.toml',b'workspace'),('Cargo.lock',b'lock'),
                          ('scripts/delivery/runtime_notices.py',b'inert notice reader')):
            path=self.repo/name; path.parent.mkdir(parents=True,exist_ok=True); path.write_bytes(data)
            self.source['files'][name]=test_game_kit.pinned(data)
        kit_root=self.root/'kit'; index_path=kit_root/'licenses/rust-runtime-candidate/INDEX.json'
        index_path.parent.mkdir(parents=True)
        specs=(('studio-custom-protocol','forge_studio','x86_64-unknown-linux-gnu',['tauri/custom-protocol'],False,'Cargo.toml'),
               ('delivery-cli','forge_delivery','x86_64-unknown-linux-gnu',[],False,'tools/forge_delivery/Cargo.toml'),
               ('studio-wasm','forge_studio','wasm32-unknown-unknown',['forge_studio/web'],True,'Cargo.toml'),
               ('launcher-standalone','forge_launcher_prototype','x86_64-unknown-linux-gnu',['standalone'],False,
                'apps/forge_launcher/Cargo.toml'))
        scopes={}; requests=[]
        for role,package,target,features,no_default,cargo_manifest in specs:
            ids=['registry+fixture#'+package+'@1.0.0']
            manifest=ntpath.join(self.original,*cargo_manifest.split('/'))
            command=['cargo','metadata','--manifest-path',manifest,'--format-version','1','--locked','--offline',
                     '--filter-platform',target]
            if features: command+=['--features',','.join(features)]
            if no_default: command.append('--no-default-features')
            scopes[role]={'package':package,'target':target,'features':features,'no_default_features':no_default,
                'manifest':manifest,'metadata_command':command,'package_count':len(ids),
                'package_ids_sha256':hashlib.sha256(json.dumps(sorted(ids)).encode()).hexdigest(),'package_ids':ids}
            requests.append({'package':package,'target':target,'features':features,'no_default_features':no_default,
                             'include_root_dev':False,'package_ids':ids})
        test_game_kit.write(index_path,{'requests':requests})
        self.graph=self.root/'graph.json'; test_game_kit.write(self.graph,{'scopes':[]})
        self.verified=SimpleNamespace(kit_root=kit_root,receipt={'runtime_notices':{
            'candidate_receipt_sha256':'a'*64,'graph_receipt_sha256':delivery.digest(self.graph)}})
        pins={name:self.source['files'][name]['sha256'] for name in ('Cargo.toml','Cargo.lock')}
        self.audit={'schema':'forge.gate7.linux-studio-cli-notice-preflight.v1','passed':True,
            'distribution_qualified':False,'gate_closed':False,'linux_snapshot_or_cargo_replayed':False,
            'cargo_input_sha256_before':pins,'cargo_input_sha256_after':pins.copy(),
            'candidate_sha256':'a'*64,'graph_sha256':delivery.digest(self.graph),'index_sha256':delivery.digest(index_path),
            'runtime_notices_sha256':self.source['files']['scripts/delivery/runtime_notices.py']['sha256'],'scopes':scopes}
        self.audit_path=self.root/'audit.json'; test_game_kit.write(self.audit_path,self.audit)
        home=self.root/'cargo-home'; (home/'registry').mkdir(parents=True)
        target=self.root/'target'; target.mkdir()
        self.observed={'cargo_home':str(home),'cargo_target':str(target)}
        def replay(*args,**kwargs):
            return {'captured_package_ids_sha256':scopes[kwargs['name']]['package_ids_sha256'],'fixture':True}
        self.patch=mock.patch.object(runtime_notices,'_linux_notice_scope',side_effect=replay)
        self.replay=self.patch.start(); self.addCleanup(self.patch.stop)

    def verify(self):
        return delivery.verify_linux_studio_notice_audit(self.repo,self.source,self.verified,self.audit_path,
                                                         delivery.digest(self.audit_path),self.graph,self.observed)

    def test_replays_native_scopes_and_keeps_wasm_at_pinned_preflight(self):
        result=self.verify()
        self.assertEqual(self.replay.call_count,2)
        self.assertEqual(self.replay.call_args_list[1].kwargs['features'],[])
        self.assertEqual(result['wasm_scope'],'pinned-preflight')
        self.assertFalse(result['distribution_qualified'])

    def test_refuses_snapshot_pin_or_scope_drift(self):
        (self.repo/'Cargo.lock').write_bytes(b'changed')
        with self.assertRaisesRegex(ValueError,'snapshot input differs|source pin differs'): self.verify()
        (self.repo/'Cargo.lock').write_bytes(b'lock')
        self.audit['scopes']['delivery-cli']['features']=['invented']
        test_game_kit.write(self.audit_path,self.audit)
        with self.assertRaisesRegex(ValueError,'notice scope differs'): self.verify()


class LinuxStudioDeliveryTests(unittest.TestCase):
    def setUp(self):
        fixture=test_game_kit.LinuxGameKitTests('test_assembles_and_reads_linux_kit_from_the_real_core_reader')
        fixture.setUp(); self.addCleanup(fixture.doCleanups); self.fixture=fixture
        fixture.assemble(); root=fixture.args.output; out=fixture.core.output
        self.kit_pin=delivery.digest(root/'game-kit-assembly-receipt.json')
        self.source_pin=delivery.digest(fixture.args.source_manifest)
        frontend=fixture.repo/'apps/forge_studio/dist'; frontend.mkdir(parents=True)
        stage_studio_game_kit.stage(root,frontend,self.kit_pin,self.source_pin,expected_platform=game_kit.LINUX_PLATFORM)
        studio=out/'forge-studio'; studio.write_bytes(test_game_kit.test_linux_build_evidence.elf(b'inert studio'))
        cli=out/'forge_delivery'; cli.write_bytes(test_game_kit.test_linux_build_evidence.elf(b'inert delivery'))
        proof=out/'receipt.json'; test_game_kit.write(proof,{'fixture':'inert linux run'})
        audit=fixture.core.root/'audit.json'; test_game_kit.write(audit,{'fixture':True})
        self.observed={'source_manifest_sha256':self.source_pin,'studio_sha256':delivery.digest(studio),
            'delivery_cli_sha256':delivery.digest(cli),
            'kit_manifest_sha256':delivery.digest(root/'forge-kit/game-export-v1.json'),
            'overlay_stage_sha256':delivery.digest(frontend/'forge-game-export-staging-receipt.json'),
            'cargo_home':'/qualification/cargo-home','cargo_target':'/qualification/isolated-targets/fixture-001'}
        self.args=SimpleNamespace(platform='linux-x64',delivery_cli=cli,delivery_cli_proof=proof,
            delivery_cli_proof_sha256=delivery.digest(proof),studio=studio,studio_proof=proof,
            studio_proof_sha256=delivery.digest(proof),source_manifest=fixture.args.source_manifest,kit_root=root,
            kit_receipt_sha256=self.kit_pin,studio_notice_audit=audit,studio_notice_audit_sha256=delivery.digest(audit),
            output=fixture.core.root/'studio-linux-output',version='fixture-1',linux_source_zip=fixture.core.zip,
            linux_started=fixture.args.linux_started,linux_started_sha256=fixture.args.linux_started_sha256,
            linux_capture_receipt=fixture.args.linux_capture_receipt,linux_capture_sha256=fixture.args.linux_capture_sha256)
        self.stack=contextlib.ExitStack(); self.addCleanup(self.stack.close)
        self.stack.enter_context(mock.patch.object(game_kit.sys,'platform','linux'))
        self.stack.enter_context(mock.patch.object(game_kit.linux_build_evidence,'verify_linux_studio',return_value=self.observed))
        self.stack.enter_context(mock.patch.object(game_kit.linux_build_evidence.os,'access',return_value=True))
        self.stack.enter_context(mock.patch.object(runtime_notices,'_snapshot_module',return_value=object()))
        self.stack.enter_context(mock.patch.object(runtime_notices,'_verify_extracted_snapshot'))
        self.stack.enter_context(mock.patch.object(delivery,'verify_linux_studio_notice_audit',
                return_value={'fixture':True,'distribution_qualified':False}))
        self.stack.enter_context(mock.patch.object(delivery,'run',side_effect=StudioDeliveryTests.pack_inert.__get__(self)))
        read=game_kit.read_document
        def source_document(path):
            result=read(path)
            if Path(path)==fixture.args.source_manifest:
                result['files']['scripts/delivery/build_delivery.py']=test_game_kit.pinned(Path(delivery.__file__).read_bytes())
            return result
        self.stack.enter_context(mock.patch.object(game_kit,'read_document',side_effect=source_document))

    def test_linux_editor_package_uses_existing_delivery_service_and_kit(self):
        result=delivery.assemble_studio_only(self.args,repo=self.fixture.repo)
        output=self.args.output
        descriptor=json.loads((output/'forge-studio.manifest.json').read_bytes())
        self.assertEqual(descriptor['platform'],'linux-x64')
        self.assertEqual(descriptor['product'],delivery.studio_product('linux-x64'))
        paths={item['path'] for item in descriptor['files']}
        self.assertTrue({'bin/forge-studio','bin/game-export-kit/bin/forge-module',
                         'bin/game-export-kit/bin/forge-game-launcher'}.issubset(paths))
        self.assertFalse(any('hub' in path.lower() or 'aube' in path.lower() for path in paths))
        self.assertTrue((output/'forge-delivery').is_file())
        self.assertFalse(result['distribution_qualified'])

    def test_linux_refuses_mixed_build_receipts(self):
        self.args.delivery_cli_proof_sha256='0'*64
        with self.assertRaisesRegex(ValueError,'same verified run'):
            delivery.assemble_studio_only(self.args,repo=self.fixture.repo)
        self.assertFalse(self.args.output.exists())


if __name__ == '__main__':
    unittest.main()
