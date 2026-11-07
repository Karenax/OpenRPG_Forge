"""Portable Linux source/artifact proof fixtures; no compiled program is run."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
import zipfile

import linux_build_evidence as evidence


def digest(data):
    return hashlib.sha256(data).hexdigest()


def save(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf-8', newline='\n')


def elf(label):
    header = bytearray(64)
    header[:7] = b'\x7fELF\x02\x01\x01'
    header[16:18] = (3).to_bytes(2, 'little')
    header[18:20] = (62).to_bytes(2, 'little')
    return bytes(header)+label


class LinuxCoreEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.output = self.root/'output'; self.output.mkdir()
        self.zip = self.root/'source.zip'
        contract = ("STANDALONE = 'FORGE_LAUNCHER_STANDALONE_FRONTEND_READY'\n"
                    "AUBE = b'FORGE_LAUNCHER_AUBE_FRONTEND_READY'\n"
                    "STANDALONE_TAURI_CONFIG = {'productName':'OpenRPG Forge',"
                    "'app':{'windows':[{'label':'main','title':'Jeu'}]}}\n").encode()
        contents = {name:('inert '+name).encode() for name in evidence.LAUNCHER_SOURCE_INPUTS}
        # Historical v1 stays valid even when its complete snapshot also contains
        # a later input that was not listed separately in that receipt version.
        contents['apps/forge_launcher/src/installer.rs'] = b'inert installed-game bootstrap'
        contents['rust-toolchain.toml'] = b'[toolchain]\nchannel="1.98.0"\n'
        contents['scripts/delivery/game_kit.py'] = contract
        contents[evidence.SOURCE_READER] = Path(evidence.__file__).read_bytes()
        for root in evidence.SOURCE_ROOTS:
            if not any(name.startswith(root+'/') for name in contents):
                contents[root+'/fixture.txt'] = root.encode()
        self.contents = contents
        self.manifest = {'schema':'forge.gate7.vm-source-snapshot.v1', 'roots':list(evidence.SOURCE_ROOTS),
                         'development_checkout':False,
                         'files':{name:{'sha256':digest(data),'bytes':len(data)} for name,data in contents.items()}}
        raw = json.dumps(self.manifest).encode()
        with zipfile.ZipFile(self.zip, 'x') as archive:
            for name,data in contents.items(): archive.writestr(name,data)
            archive.writestr('__forge_vm_source_manifest__.json',raw)
        (self.output/'source-manifest.json').write_bytes(raw)
        source_sha=evidence.sha(self.zip)
        owner={'schema':'forge.gate7.linux-isolated-target.v1', 'run_id':'fixture-001',
               'source_sha256':source_sha, 'target':'/qualification/isolated-targets/fixture-001',
               'cargo_home':'/qualification/cargo-home'}
        owner_sha=digest((json.dumps(owner,sort_keys=True,separators=(',', ':'))+'\n').encode())
        self.started={'run_id':'fixture-001','source_sha256':source_sha,'source_manifest_sha256':digest(raw),
            'runner_sha256':'c'*64,'image':'sha256:'+'d'*64,'network':'none','cargo_target_mode':'isolated-final',
            'cargo_home':owner['cargo_home'],'cargo_target':owner['target'],'isolated_owner_sha256':owner_sha,
            'cargo_jobs':2}
        self.capture={'full_read_verified':True,'sha256':source_sha,'archive_bytes':self.zip.stat().st_size,
                      'files':len(contents)}
        (self.output/'forge-module').write_bytes(elf(b'inert runtime'))
        (self.output/'forge-launcher-prototype').write_bytes(elf(b'inert launcher'))
        for name in ('forge-module','forge-launcher-prototype'):
            (self.output/(name+'.ldd.log')).write_bytes(b'fixture dependency list\n')
        (self.output/'09-module-runtime-build.log').write_bytes(b'inert runtime build\n')
        frontend={'index.html':b'fixture html','styles.css':b'fixture css','desktop.js':b'fixture js'}
        for name,data in frontend.items(): (self.output/('launcher-frontend-'+name)).write_bytes(data)
        frontend_pins={name:{'sha256':digest(data),'bytes':len(data)} for name,data in sorted(frontend.items())}
        frontend_rows=[{'path':name,**pin} for name,pin in frontend_pins.items()]
        frontend_path='/qualification/tmp/standalone-launcher-fixture-001'
        tauri={'productName':'OpenRPG Forge','app':{'windows':[{'label':'main','title':'Jeu'}]},
               'build':{'frontendDist':frontend_path}}
        launcher_root='/qualification/sources/fixture-001/apps/forge_launcher'
        commands=[('frontend',['node',launcher_root+'/scripts/build-aube.mjs'],'launcher-frontend-build.log'),
                  ('cargo',['cargo','build','--manifest-path',launcher_root+'/Cargo.toml','--features','standalone',
                            '--release','--locked','--offline','-j','2'],'launcher-native-build.log'),
                  ('verify',[owner['target']+'/release/forge-launcher-prototype','--verify-distributable'],
                   'launcher-distributable-check.log')]
        (self.output/commands[0][2]).write_bytes(('STANDALONE_FRONTEND_READY output='+frontend_path+'\n').encode())
        (self.output/commands[1][2]).write_bytes(b'inert launcher build\n')
        (self.output/commands[2][2]).write_bytes((evidence.STANDALONE_MARKER+'\n').encode())
        (self.output/'launcher-distributable-stderr.log').write_bytes(b'')
        steps=[{'name':name,'command':command,'cwd':launcher_root,'log':log,'sha256':evidence.sha(self.output/log),
                'exit_code':0} for name,command,log in commands]
        steps[-1].update(stderr_log='launcher-distributable-stderr.log',
                         stderr_sha256=evidence.sha(self.output/'launcher-distributable-stderr.log'))
        tools={name:{'path':'/usr/bin/'+name,'sha256':'a'*64,
                     'version':('v20.0.0' if name=='node' else name+' 1.98.0 (fixture)')}
               for name in ('node','cargo','rustc')}
        for name in ('cargo','rustc'):
            tools[name].update(toolchain_path='/toolchain/'+name,toolchain_sha256='b'*64)
        launcher=self.output/'forge-launcher-prototype'
        self.launcher={'schema':'forge.gate7.linux-standalone-launcher-build.v1','status':'BUILD_ONLY',
            'run_id':'fixture-001','platform':'linux-x64','target_triple':'x86_64-unknown-linux-gnu',
            'source_sha256':source_sha,'source_manifest_sha256':digest(raw),'runner_sha256':'c'*64,
            'source_inputs':{name:self.manifest['files'][name] for name in evidence.LAUNCHER_SOURCE_INPUTS},
            'source_unchanged':True,'source_transport_unchanged':True,'sdk_image_sha256':self.started['image'],
            'cargo_home':owner['cargo_home'],'cargo_target':owner['target'],'isolated_owner_sha256':owner_sha,
            'cargo_build_jobs':'2','network':'none','frontend_profile':'standalone','frontend_path':frontend_path,
            'frontend_files':frontend_pins,'frontend_state_sha256':digest(json.dumps(frontend_rows,ensure_ascii=False,
                    sort_keys=True,separators=(',', ':')).encode()),'frontend_temporary_removed':True,
            'tauri_config':tauri,'tauri_config_sha256':digest(json.dumps(tauri,ensure_ascii=False,
                    separators=(',', ':')).encode()),'tools':tools,'tools_unchanged':True,
            'binary_name':launcher.name,'binary_sha256':evidence.sha(launcher),'binary_bytes':launcher.stat().st_size,
            'elf_class':'x86_64','aube_marker_absent':True,'verification_marker':evidence.STANDALONE_MARKER,
            'steps':steps,'runtime_verified':False,'installation_verified':False,'distribution_qualified':False}
        self.core={'schema':'forge.gate7.linux-core-artifacts.v1','status':'BUILD_ONLY','run_id':'fixture-001',
            'platform':'linux-x64','source_sha256':source_sha,'source_manifest_sha256':digest(raw),'runner_sha256':'c'*64,
            'sdk_image_sha256':self.started['image'],'cargo_home':owner['cargo_home'],'cargo_target':owner['target'],
            'isolated_owner_sha256':owner_sha,'target_initially_empty':True,'cargo_build_jobs':'2','network':'none',
            'source_unchanged':True,'source_transport_unchanged':True,
            'evidence_reader_sha256':self.manifest['files'][evidence.SOURCE_READER]['sha256'],
            'runtime_check':{'name':'module-runtime-build','command':evidence.MODULE_COMMAND,'cwd':'.','exit_code':0,
                'log':'09-module-runtime-build.log','sha256':evidence.sha(self.output/'09-module-runtime-build.log')},
            'binaries':[self.binary_pin(name) for name in ('forge-module','forge-launcher-prototype')],
            'runtime_verified':False,'installation_verified':False,'distribution_qualified':False,'gate_closed':False}
        self.save_proofs()

    def binary_pin(self, name):
        binary=self.output/name; log=self.output/(name+'.ldd.log')
        return {'name':name,'sha256':evidence.sha(binary),'bytes':binary.stat().st_size,
                'ldd_log':log.name,'ldd_sha256':evidence.sha(log),'ldd_exit_code':0}

    def save_proofs(self):
        save(self.output/'launcher-build-proof.json',self.launcher)
        self.core['launcher_proof_sha256']=evidence.sha(self.output/'launcher-build-proof.json')
        save(self.output/'linux-core-artifacts.json',self.core)
        self.core_sha=evidence.sha(self.output/'linux-core-artifacts.json')

    def verify(self):
        return evidence.verify_linux_core(self.output,self.zip,self.started,self.core_sha,capture_receipt=self.capture)

    def test_accepts_matching_snapshot_and_both_elf_builds(self):
        proof=self.verify()
        self.assertEqual(proof['status'],'BUILD_ONLY')
        self.assertFalse(proof['installation_verified'])
        self.assertEqual([item['name'] for item in proof['binaries']],['forge-module','forge-launcher-prototype'])

    def test_v2_requires_the_explicit_pinned_installer_and_preserves_v1(self):
        self.assertEqual(self.launcher['schema'],'forge.gate7.linux-standalone-launcher-build.v1')
        self.verify()
        name='apps/forge_launcher/src/installer.rs'
        self.launcher['schema']='forge.gate7.linux-standalone-launcher-build.v2'
        self.launcher['source_inputs']={key:self.manifest['files'][key] for key in evidence.LAUNCHER_SOURCE_INPUTS_V2}
        self.save_proofs()
        self.verify()

        self.launcher['source_inputs'][name]={'sha256':'0'*64,'bytes':self.manifest['files'][name]['bytes']}
        self.save_proofs()
        with self.assertRaisesRegex(ValueError,'identity or scope'): self.verify()
        self.launcher['source_inputs'].pop(name)
        self.save_proofs()
        with self.assertRaisesRegex(ValueError,'identity or scope'): self.verify()
        self.launcher['source_inputs'][name]=self.manifest['files'][name]
        self.launcher['schema']='forge.gate7.linux-standalone-launcher-build.v3'
        self.save_proofs()
        with self.assertRaisesRegex(ValueError,'proof schema'): self.verify()

    def test_v2_refuses_a_snapshot_manifest_missing_the_installer(self):
        self.launcher['schema']='forge.gate7.linux-standalone-launcher-build.v2'
        self.launcher['source_inputs']={key:self.manifest['files'][key] for key in evidence.LAUNCHER_SOURCE_INPUTS_V2}
        self.save_proofs()
        missing={**self.manifest,'files':dict(self.manifest['files'])}
        del missing['files']['apps/forge_launcher/src/installer.rs']
        manifest=self.output/'source-manifest.json'
        save(manifest,missing)
        launcher_path=self.output/'launcher-build-proof.json'
        result={'standalone_launcher_requested':True,
                'standalone_launcher':{'proof_sha256':evidence.sha(launcher_path),
                                       'binary_sha256':self.launcher['binary_sha256']}}
        with self.assertRaisesRegex(ValueError,'source manifest is incomplete'):
            evidence.verify_linux_launcher(result,self.started,self.output,evidence.sha(manifest),
                                           self.contents['rust-toolchain.toml'],
                                           self.contents['scripts/delivery/game_kit.py'])

    def test_rejects_changed_source_zip_manifest_and_capture(self):
        self.capture['full_read_verified']=False
        with self.assertRaisesRegex(ValueError,'capture receipt'): self.verify()
        self.capture['full_read_verified']=True
        source_manifest=self.output/'source-manifest.json'; raw=source_manifest.read_bytes()
        source_manifest.write_bytes(raw+b' ')
        with self.assertRaisesRegex(ValueError,'manifest differs'): self.verify()
        source_manifest.write_bytes(raw)
        with self.zip.open('ab') as stream: stream.write(b'changed')
        with self.assertRaisesRegex(ValueError,'ZIP differs'): self.verify()

    def test_rejects_changed_core_proof_and_dependency_log(self):
        proof=self.output/'linux-core-artifacts.json'; original=proof.read_bytes()
        proof.write_bytes(original+b' ')
        with self.assertRaisesRegex(ValueError,'artifacts proof'): self.verify()
        proof.write_bytes(original)
        (self.output/'forge-module.ldd.log').write_bytes(b'libmissing => not found\n')
        with self.assertRaisesRegex(ValueError,'executable or dependency'): self.verify()

    def test_rejects_symbolic_linked_binary(self):
        binary=self.output/'forge-module'; data=binary.read_bytes()
        other=self.root/'target'; other.write_bytes(data)
        binary.unlink()
        try:
            binary.symlink_to(other)
        except OSError as error:
            self.skipTest(f'Symbolic links unavailable: {error}')
        with self.assertRaisesRegex(ValueError,'executable or dependency'): self.verify()

    @unittest.skipUnless(os.name == 'nt', 'Windows junction fixture')
    def test_rejects_windows_junction_before_entering_it(self):
        target=self.root/'outside'; target.mkdir()
        marker=target/'keep'; marker.write_bytes(b'unchanged')
        junction=self.root/'junction'
        subprocess.run([os.environ.get('COMSPEC','cmd.exe'),'/d','/c','mklink','/J',str(junction),str(target)],
                       capture_output=True,check=True)
        try:
            self.assertFalse(evidence.physical(junction/'keep'))
            self.assertEqual(marker.read_bytes(),b'unchanged')
        finally:
            os.rmdir(junction)
        self.assertEqual(marker.read_bytes(),b'unchanged')

    def test_rejects_binary_or_log_mutation_and_wrong_architecture(self):
        runtime=self.output/'forge-module'; before=runtime.read_bytes()
        runtime.write_bytes(before+b'changed')
        with self.assertRaisesRegex(ValueError,'executable or dependency'): self.verify()
        wrong=bytearray(before); wrong[18:20]=(3).to_bytes(2,'little'); runtime.write_bytes(wrong)
        self.core['binaries'][0]=self.binary_pin('forge-module'); self.save_proofs()
        with self.assertRaisesRegex(ValueError,'x86-64 ELF'): self.verify()
        runtime.write_bytes(before); self.core['binaries'][0]=self.binary_pin('forge-module')
        self.core['runtime_check']['command']=['cargo','build','--offline']; self.save_proofs()
        with self.assertRaisesRegex(ValueError,'build command'): self.verify()

    def test_rejects_aube_even_when_binary_and_proofs_are_rehashed(self):
        binary=self.output/'forge-launcher-prototype'; binary.write_bytes(binary.read_bytes()+evidence.AUBE_MARKER)
        self.launcher['binary_sha256']=evidence.sha(binary); self.launcher['binary_bytes']=binary.stat().st_size
        self.core['binaries'][1]=self.binary_pin(binary.name); self.save_proofs()
        with self.assertRaisesRegex(ValueError,'executable or marker'): self.verify()

    def test_rejects_wrong_marker_and_runner_image_or_target(self):
        marker=self.output/'launcher-distributable-check.log'; marker.write_bytes(b'WRONG\n')
        self.launcher['steps'][-1]['sha256']=evidence.sha(marker); self.save_proofs()
        with self.assertRaisesRegex(ValueError,'executable or marker'): self.verify()
        marker.write_bytes((evidence.STANDALONE_MARKER+'\n').encode())
        self.launcher['steps'][-1]['sha256']=evidence.sha(marker); self.save_proofs()
        for field,bad in [('runner_sha256','0'*64),('image','sha256:'+'0'*64),
                          ('cargo_target','/qualification/another-target')]:
            old=self.started[field]; self.started[field]=bad
            with self.subTest(field=field), self.assertRaises(ValueError): self.verify()
            self.started[field]=old


class LinuxStudioEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.core=LinuxCoreEvidenceTests('test_accepts_matching_snapshot_and_both_elf_builds')
        self.core.setUp(); self.addCleanup(self.core.doCleanups)
        core=self.core; out=core.output
        core.started.update(schema='forge.gate7.linux-editor-launcher-started.v1',suite='all',
            deferred_frontend=True,standalone_launcher_requested=True,linux_game_kit_requested=True,
            gate_closed=False,launcher_qualified=False)
        self.started=core.root/'started.json'; save(self.started,core.started)
        self.capture=core.root/'capture.json'; save(self.capture,core.capture)
        for name in ('forge-studio','forge_delivery'):
            (out/name).write_bytes(elf(('inert '+name).encode()))
            (out/(name+'.ldd.log')).write_bytes(b'inert SDK links\n')
        (out/'15-studio-build.log').write_bytes(b'inert studio build\n')
        (out/'delivery-release-build.log').write_bytes(b'inert delivery build\n')
        (out/'linux-game-kit-assembly-receipt.json').write_bytes(b'inert kit assembly')
        (out/'linux-game-kit-overlay-manifest.json').write_bytes(b'inert kit manifest')
        assembly_sha=evidence.sha(out/'linux-game-kit-assembly-receipt.json')
        manifest_sha=evidence.sha(out/'linux-game-kit-overlay-manifest.json')
        save(out/'linux-game-kit-overlay-stage.json',{'schema':'forge.studio.game-kit-stage.v1',
            'kit_assembly_receipt_sha256':assembly_sha,'source_manifest_sha256':core.started['source_manifest_sha256'],
            'manifest_sha256':manifest_sha,'build_only':True,'runtime_verified':False,
            'installation_verified':False,'distribution_qualified':False})
        kit={'status':'PRIVATE_STAGED_ONLY','assembly_receipt_sha256':assembly_sha,'manifest_sha256':manifest_sha,
             'overlay_stage_sha256':evidence.sha(out/'linux-game-kit-overlay-stage.json'),
             'runtime_verified':False,'installation_verified':False,'distribution_qualified':False}
        for phase in ('before','after'):
            path=out/('linux-game-kit-'+phase+'-studio.log'); path.write_bytes(b'inert revalidation\n')
            kit[phase+'_studio_verification']={'exit_code':0,'log_sha256':evidence.sha(path)}
        studio_command=['cargo','build','--release','--locked','--offline','-p','forge_studio','--bin',
                        'forge-studio','--features','tauri/custom-protocol']
        delivery_command=['cargo','build','--locked','--offline','--release','--manifest-path',
                          'tools/forge_delivery/Cargo.toml','--bin','forge_delivery']
        def binary(name):
            path=out/name
            return {'name':name,'sha256':evidence.sha(path),'bytes':path.stat().st_size}
        studio=binary('forge-studio'); studio['ldd_sha256']=evidence.sha(out/'forge-studio.ldd.log')
        cli=binary('forge_delivery')
        delivery={'status':'BUILD_ONLY','command':delivery_command,'cwd':'.','exit_code':0,
                  'log':'delivery-release-build.log','log_sha256':evidence.sha(out/'delivery-release-build.log'),
                  'binary_sha256':cli['sha256'],'binary_bytes':cli['bytes'],'ldd_log':'forge_delivery.ldd.log',
                  'ldd_sha256':evidence.sha(out/'forge_delivery.ldd.log'),'ldd_exit_code':0}
        self.result={'schema':'forge.gate7.linux-editor-launcher-build.v1','suite':'all','succeeded':True,
            'run_id':core.started['run_id'],'source_sha256':core.started['source_sha256'],
            'source_manifest_sha256':core.started['source_manifest_sha256'],'runner_sha256':core.started['runner_sha256'],
            'cargo_target':core.started['cargo_target'],'source_unchanged':True,'changed_sources':[],
            'source_transport_unchanged':True,'frontend_unchanged':True,'linux_game_kit_requested':True,
            'gate_closed':False,'linux_core_artifacts_sha256':core.core_sha,
            'checks':[{'name':'studio-build','command':studio_command,'cwd':'.','exit_code':0,
                       'log':'15-studio-build.log','sha256':evidence.sha(out/'15-studio-build.log')}],
            'binaries':[binary('forge-module'),studio,binary('forge-launcher-prototype'),cli],
            'delivery_cli':delivery,'linux_game_kit':kit}
        self.save_result()

    def save_result(self):
        save(self.core.output/'receipt.json',self.result)

    def verify(self):
        return evidence.verify_linux_studio(self.core.output,self.core.zip,self.started,self.capture,
            expected_result_sha256=evidence.sha(self.core.output/'receipt.json'),
            expected_started_sha256=evidence.sha(self.started),expected_capture_sha256=evidence.sha(self.capture),
            expected_kit_receipt_sha256=self.result['linux_game_kit']['assembly_receipt_sha256'])

    def test_accepts_pinned_studio_cli_and_private_overlay(self):
        proof=self.verify()
        self.assertEqual(proof['studio_sha256'],evidence.sha(self.core.output/'forge-studio'))
        self.assertEqual(proof['delivery_cli_sha256'],evidence.sha(self.core.output/'forge_delivery'))

    def test_refuses_changed_binary_or_build_command(self):
        binary=self.core.output/'forge_delivery'; old=binary.read_bytes(); binary.write_bytes(old+b'changed')
        with self.assertRaisesRegex(ValueError,'executable or SDK dependency'): self.verify()
        binary.write_bytes(old)
        self.result['delivery_cli']['command'].remove('--offline'); self.save_result()
        with self.assertRaisesRegex(ValueError,'delivery service build proof'): self.verify()

    def test_refuses_changed_overlay_or_missing_revalidation(self):
        manifest=self.core.output/'linux-game-kit-overlay-manifest.json'; old=manifest.read_bytes()
        manifest.write_bytes(b'changed')
        with self.assertRaisesRegex(ValueError,'kit overlay proof'): self.verify()
        manifest.write_bytes(old)
        self.result['linux_game_kit'].pop('after_studio_verification'); self.save_result()
        with self.assertRaisesRegex(ValueError,'revalidation is incomplete'): self.verify()


if __name__ == '__main__':
    unittest.main(verbosity=2)
