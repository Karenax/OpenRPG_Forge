import importlib.util
import json
import os
import struct
import sys
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

spec=importlib.util.spec_from_file_location('vault_storage',Path(__file__).with_name('vault_storage.py'))
storage=importlib.util.module_from_spec(spec);spec.loader.exec_module(storage)

class StorageTests(unittest.TestCase):
    def test_only_explicit_witch_model_directory_is_eligible(self):
        root=Path('F:/Vault')
        self.assertTrue(storage.eligible_model_storage_path(root,root/'witch-house-urp-20261108-r2/models/a.glb'))
        self.assertFalse(storage.eligible_model_storage_path(root,root/'witch-house-urp-20261108-r2/sources/a.glb'))
        self.assertFalse(storage.eligible_model_storage_path(root,root/'witch-house-urp-20261108-r2/models/a.fbx'))
        self.assertFalse(storage.eligible_model_storage_path(root,root/'witch-house-urp-20261108-r20/models/a.glb'))

    def test_applied_stage_replay_refuses_changed_revision_and_snapshot(self):
        publish_spec=importlib.util.spec_from_file_location('project_publisher_replay',Path(__file__).with_name('publish_assets.py'))
        publisher=importlib.util.module_from_spec(publish_spec);publish_spec.loader.exec_module(publisher)
        with tempfile.TemporaryDirectory() as work:
            vault=Path(work).resolve(); stage=vault/'stage';stage.mkdir();rev=vault/'metadata/revision';rev.mkdir(parents=True)
            details=b'{"pack_id":"p","assets":[]}'; profiles=b'{"profiles":[]}'
            for folder in (stage,rev):
                (folder/'details.v1.json').write_bytes(details);(folder/'profiles.v1.json').write_bytes(profiles)
            files={'details.v1.json':storage.sha(details),'profiles.v1.json':storage.sha(profiles)}
            plan={'pack_id':'p','revision':'r1','files':files,'expected_registry_sha256':storage.sha(b'old registry')}
            (stage/'plan.json').write_text(json.dumps(plan),encoding='utf8')
            registry={'packs':{'p':{'revision':'r1','path':'metadata/revision','files':files}}}
            index=vault/'metadata/asset-import-registry.v1.json';index.write_text(json.dumps(registry),encoding='utf8')
            self.assertEqual(publisher.check_consolidated_identities(vault,stage)['state'],'already_applied')
            (rev/'profiles.v1.json').write_bytes(b'tampered')
            with self.assertRaisesRegex(ValueError,'snapshot differs'):
                publisher.check_consolidated_identities(vault,stage)
            (rev/'profiles.v1.json').write_bytes(profiles)
            registry['packs']['p']['revision']='r2';index.write_text(json.dumps(registry),encoding='utf8')
            with self.assertRaisesRegex(ValueError,'Registry changed'):
                publisher.check_consolidated_identities(vault,stage)

    def test_positive_savings_without_arbitrary_minimum(self):
        self.assertTrue(storage.batch_saves_allocation(1024, 1023))
        self.assertFalse(storage.batch_saves_allocation(1024, 1024))
        self.assertFalse(storage.batch_saves_allocation(1024, 1025))

    def test_windows_readonly_attribute_is_detected_without_changing_file(self):
        from types import SimpleNamespace
        self.assertTrue(storage.windows_readonly(SimpleNamespace(st_file_attributes=1)))
        self.assertTrue(storage.windows_readonly(SimpleNamespace(st_file_attributes=33)))
        self.assertFalse(storage.windows_readonly(SimpleNamespace(st_file_attributes=32)))
        self.assertFalse(storage.windows_readonly(SimpleNamespace()))

    def test_reader_receipt_rejects_changed_studio_and_reader(self):
        with tempfile.TemporaryDirectory(prefix='forge-receipt-test-') as work:
            root=Path(work); studio=root/'studio.exe'; reader=root/'reader.exe'; importer=root/'importer.py'
            studio.write_bytes(b'qualified studio');reader.write_bytes(b'qualified reader');importer.write_bytes(b'qualified importer')
            receipt=dict(state='ready',vault=str(root),studio=str(studio),
                         studio_sha256=storage.sha(studio.read_bytes()),reader_cli_sha256=storage.sha(reader.read_bytes()),
                         python_import_reader=str(importer),python_import_reader_sha256=storage.sha(importer.read_bytes()),
                         storage_writer_sha256=storage.sha(Path(storage.__file__).read_bytes()))
            storage.verify_deployed_readers(receipt,root,reader)
            studio.write_bytes(b'new unqualified studio')
            with self.assertRaisesRegex(ValueError,'Studio differs'):
                storage.verify_deployed_readers(receipt,root,reader)
            studio.write_bytes(b'qualified studio');reader.write_bytes(b'new reader')
            with self.assertRaisesRegex(ValueError,'Native verifier differs'):
                storage.verify_deployed_readers(receipt,root,reader)
            reader.write_bytes(b'qualified reader');importer.write_bytes(b'new importer')
            with self.assertRaisesRegex(ValueError,'Python reader differs'):
                storage.verify_deployed_readers(receipt,root,reader)
            importer.write_bytes(b'qualified importer');receipt['storage_writer_sha256']='0'*64
            with self.assertRaisesRegex(ValueError,'Storage writer differs'):
                storage.verify_deployed_readers(receipt,root,reader)
            receipt.pop('studio')
            with self.assertRaisesRegex(ValueError,'Studio path or SHA missing'):
                storage.verify_deployed_readers(receipt,root,reader)

    def test_publication_optimization_rejects_stale_studio_before_queue(self):
        publish_spec=importlib.util.spec_from_file_location('project_publisher_guard',Path(__file__).with_name('publish_assets.py'))
        publisher=importlib.util.module_from_spec(publish_spec);publish_spec.loader.exec_module(publisher)
        studio=self.root/'studio.exe';reader=self.root/'reader.exe'
        studio.write_bytes(b'new studio');reader.write_bytes(b'reader')
        directory=self.root/'.forge-storage';directory.mkdir()
        receipt=dict(state='ready',vault=str(self.root),studio=str(studio),
                     studio_sha256=storage.sha(b'old studio'),reader_cli_sha256=storage.sha(b'reader'))
        (directory/'deployed-readers.v1.json').write_text(json.dumps(receipt),encoding='utf-8')
        with patch.object(publisher.storage,'queue_models') as queue:
            with self.assertRaisesRegex(ValueError,'Studio differs'):
                publisher.optimize_published(self.root,self.root/'unused-stage',reader,self.root,{})
            queue.assert_not_called()

    def test_atomic_json_retries_transient_windows_sharing_denial(self):
        with tempfile.TemporaryDirectory() as work:
            path=Path(work)/'status.json'
            path.write_text('{"old":true}')
            real_replace=os.replace
            calls=[]
            def replace(source,target):
                calls.append(source)
                if len(calls)<3:raise PermissionError('sharing violation')
                return real_replace(source,target)
            with patch.object(storage.os,'replace',side_effect=replace),patch.object(storage.time,'sleep'):
                storage.atomic_json(path,{'new':True})
            self.assertEqual(json.loads(path.read_bytes()),{'new':True})
            self.assertEqual(len(set(calls)),1)
            self.assertEqual(list(Path(work).glob('*.tmp-*')),[])

    def setUp(self):
        self.work=tempfile.TemporaryDirectory(prefix='forge-vault-tests-')
        self.root=Path(self.work.name)
    def tearDown(self):
        self.work.cleanup()
    def model(self,name='model.glb',body=None):
        path=self.root/name;data=body or b'original model contents\0'*10000
        path.write_bytes(data)
        return path,data
    def test_reconstruction_transaction_and_shared_storage(self):
        a,data=self.model();b,_=self.model('variant.glb',data)
        writer=storage.Packer(self.root)
        try:
            first=writer.prepare(a);second=writer.prepare(b)
            writer.close_pack()
            self.assertEqual(len(writer.created_packs),1)
            self.assertEqual(first['descriptor']['segments'],second['descriptor']['segments'])
            self.assertEqual(a.read_bytes(),data)
            writer.publish(first);writer.publish(second)
            self.assertEqual(storage.read_bytes(self.root,a),data)
            self.assertEqual(storage.digest(self.root,b),storage.sha(data))
            self.assertEqual(list((self.root/'.forge-storage/transactions').iterdir()),[])
            self.assertEqual(list(self.root.glob('*.orpv-original-*')),[])
            self.assertEqual(os.stat(a).st_mtime_ns,first['mtime_ns'])
        finally:writer.close()
    def test_interruption_before_replace_preserves_original(self):
        path,data=self.model();writer=storage.Packer(self.root)
        try:
            item=writer.prepare(path)
            with patch.object(storage,'replace_with_backup',side_effect=OSError('simulated interruption')):
                with self.assertRaises(OSError):writer.publish(item)
            self.assertEqual(path.read_bytes(),data)
            self.assertEqual(len(list((self.root/'.forge-storage/transactions').iterdir())),1)
        finally:writer.close()
        record=next((self.root/'.forge-storage/transactions').glob('*.json'))
        self.assertEqual(storage.recover_transaction(self.root,record)['state'],'rolled-back')
        self.assertEqual(path.read_bytes(),data)
    def test_long_model_name_uses_short_transaction_paths(self):
        parent=self.root/('nested-'+'x'*55)
        parent.mkdir()
        path=parent/('model-'+'y'*120+'.glb')
        data=b'long named original model'*1000
        path.write_bytes(data)
        writer=storage.Packer(self.root)
        try:
            item=writer.prepare(path)
            with patch.object(storage,'replace_with_backup',side_effect=OSError('interruption')):
                with self.assertRaises(OSError):writer.publish(item)
        finally:writer.close()
        record=next((self.root/'.forge-storage/transactions').glob('*.json'))
        transaction=json.loads(record.read_bytes())
        self.assertLess(len(Path(transaction['temporary']).name),50)
        self.assertLess(len(Path(transaction['backup']).name),50)
        self.assertEqual(storage.recover_transaction(self.root,record)['state'],'rolled-back')
        self.assertEqual(path.read_bytes(),data)
    def test_replaced_original_is_retained_after_interruption(self):
        path,data=self.model();writer=storage.Packer(self.root);original=storage.replace_with_backup
        def interrupt(*args):
            original(*args)
            raise OSError('interrupted after replacement')
        try:
            item=writer.prepare(path)
            with patch.object(storage,'replace_with_backup',side_effect=interrupt):
                with self.assertRaises(OSError):writer.publish(item)
            backup=list(self.root.glob('*.orpv-original-*'))
            self.assertEqual(len(backup),1)
            self.assertEqual(backup[0].read_bytes(),data)
            self.assertEqual(storage.read_bytes(self.root,path),data)
        finally:writer.close()
        record=next((self.root/'.forge-storage/transactions').glob('*.json'))
        self.assertEqual(storage.recover_transaction(self.root,record)['state'],'completed')
        self.assertEqual(list(self.root.glob('*.orpv-original-*')),[])
    def test_source_modified_before_publication_is_never_removed(self):
        path,_=self.model();writer=storage.Packer(self.root)
        try:
            item=writer.prepare(path)
            replacement=self.root/'new.glb';replacement.write_bytes(b'new source')
            storage.replace_with_backup(path,replacement,self.root/'concurrent-source-backup')
            with self.assertRaises(ValueError):writer.publish(item)
            self.assertEqual(path.read_bytes(),b'new source')
        finally:writer.close()

    @unittest.skipUnless(os.name=='nt','Windows sharing contract')
    def test_live_writers_are_excluded_and_prepared_sources_cannot_be_written(self):
        path,data=self.model();writer=storage.Packer(self.root)
        try:
            with path.open('r+b'):
                with self.assertRaises(OSError):writer.prepare(path)
            item=writer.prepare(path)
            with self.assertRaises(OSError):
                with path.open('r+b'):pass
            self.assertEqual(path.read_bytes(),data)
            writer.publish(item)
            self.assertEqual(storage.read_bytes(self.root,path),data)
        finally:writer.close()
    def test_corrupt_pack_budget_and_escape(self):
        path,data=self.model();writer=storage.Packer(self.root)
        try:
            item=writer.prepare(path);writer.publish(item)
            with self.assertRaises(ValueError):storage.read_bytes(self.root,path,len(data)-1)
            value=storage.read_descriptor(self.root,path);value['segments'][0]['pack']='../../bad'
            with self.assertRaises(ValueError):storage.validate_descriptor(value,storage.MAX_MODEL)
            pack=next((self.root/'.forge-storage/packs').glob('*.pack'));pack.write_bytes(b'bad')
            with self.assertRaises(ValueError):storage.read_bytes(self.root,path)
        finally:writer.close()
    def test_exclusive_writer_and_discard_unprofitable_batch(self):
        path,data=self.model();writer=storage.Packer(self.root)
        try:
            with self.assertRaises(FileExistsError):storage.Packer(self.root)
            writer.prepare(path);writer.discard_unpublished_batch(0)
            self.assertEqual(list((self.root/'.forge-storage/packs').iterdir()),[])
            self.assertEqual(writer.db.execute('SELECT count(*) FROM blobs').fetchone()[0],0)
            self.assertEqual(path.read_bytes(),data)
        finally:writer.close()

    @unittest.skipUnless(os.environ.get('OPENRPG_IMPORTER_SCRIPTS'),'Requires explicit canonical local importer path')
    def test_canonical_importer_reads_the_exact_model_after_compaction(self):
        importer=Path(os.environ['OPENRPG_IMPORTER_SCRIPTS'])/'glb_inspect.py'
        import_spec=importlib.util.spec_from_file_location('canonical_glb_inspect',importer)
        inspector=importlib.util.module_from_spec(import_spec);import_spec.loader.exec_module(inspector)
        binary=struct.pack('<9f',0,0,0,1,0,0,0,2,0)
        document={'asset':{'version':'2.0'},'buffers':[{'byteLength':len(binary)}],
            'bufferViews':[{'buffer':0,'byteLength':len(binary)}],
            'accessors':[{'bufferView':0,'componentType':5126,'count':3,'type':'VEC3'}],
            'meshes':[{'primitives':[{'attributes':{'POSITION':0}}]}],
            'nodes':[{'mesh':0}],'scenes':[{'nodes':[0]}],'scene':0}
        text=json.dumps(document).encode();text+=b' '*((-len(text))%4)
        raw=struct.pack('<III',0x46546c67,2,28+len(text)+len(binary))+struct.pack('<II',len(text),0x4e4f534a)+text+struct.pack('<II',len(binary),0x004e4942)+binary
        path,_=self.model(body=raw);before=inspector.inspect(path)
        writer=storage.Packer(self.root)
        try:writer.publish(writer.prepare(path))
        finally:writer.close()
        with patch.dict(os.environ,{'OPENRPG_ASSET_VAULT':str(self.root)}):
            self.assertEqual(inspector.inspect(path),before)
            self.assertEqual(inspector.digest(path),storage.sha(raw))

    @unittest.skipUnless(os.environ.get('OPENRPG_IMPORTER_SCRIPTS') and os.environ.get('OPENRPG_STORAGE_VERIFY_CLI'),'Requires explicit local importer and native verifier')
    def test_new_publication_is_queued_during_migration_then_compacted(self):
        importer=Path(os.environ['OPENRPG_IMPORTER_SCRIPTS'])
        sys.path.insert(0,str(importer))
        try:import asset_import as api
        finally:sys.path.remove(str(importer))
        publish_spec=importlib.util.spec_from_file_location('project_publisher',Path(__file__).with_name('publish_assets.py'))
        publisher=importlib.util.module_from_spec(publish_spec);publish_spec.loader.exec_module(publisher)
        binary=struct.pack('<9f',0,0,0,1,0,0,0,2,0)+bytes(3*1024**2)
        document={'asset':{'version':'2.0'},'buffers':[{'byteLength':len(binary)}],
            'bufferViews':[{'buffer':0,'byteLength':36}],
            'accessors':[{'bufferView':0,'componentType':5126,'count':3,'type':'VEC3'}],
            'meshes':[{'primitives':[{'attributes':{'POSITION':0}}]}],
            'nodes':[{'mesh':0}],'scenes':[{'nodes':[0]}],'scene':0}
        text=json.dumps(document).encode();text+=b' '*((-len(text))%4)
        raw=struct.pack('<III',0x46546c67,2,28+len(text)+len(binary))+struct.pack('<II',len(text),0x4e4f534a)+text+struct.pack('<II',len(binary),0x004e4942)+binary
        (self.root/'normalized').mkdir();model=self.root/'normalized/model.glb';model.write_bytes(raw)
        api.atomic(self.root/'catalog.json',{'id':'test.storage','license':'LicenseRef-Test','assets':[{'id':'test.model','name':'Storage fixture','model':'normalized/model.glb','sha256':storage.sha(raw)}]})
        api.atomic(self.root/'annotations.json',{'schema_version':1,'pack_id':'test.storage','assets':{}})
        api.atomic(self.root/'validation.khronos.json',{'reports':[{'asset_id':'test.model','sha256':storage.sha(raw),'report':{'issues':{'numErrors':0}}}]})
        stage=self.root/'metadata/stage'
        with patch.dict(os.environ,{'OPENRPG_ASSET_VAULT':str(self.root)}):
            api.prepare(self.root,self.root/'catalog.json',self.root/'annotations.json',stage)
        verifier=Path(os.environ['OPENRPG_STORAGE_VERIFY_CLI'])
        import hashlib
        with verifier.open('rb') as stream:reader_sha=hashlib.file_digest(stream,'sha256').hexdigest()
        writer=storage.Packer(self.root)
        studio=self.root/'test-studio.exe';studio.write_bytes(b'qualified studio fixture')
        importer_reader=importer/'glb_inspect.py'
        storage.atomic_json(self.root/'.forge-storage/deployed-readers.v1.json',dict(state='ready',vault=str(self.root),reader_cli_sha256=reader_sha,studio=str(studio),studio_sha256=storage.sha(studio.read_bytes()),python_import_reader=str(importer_reader),python_import_reader_sha256=storage.sha(importer_reader.read_bytes()),storage_writer_sha256=storage.sha(Path(storage.__file__).read_bytes()),test_fixture=True))
        try:
            result=publisher.publish(self.root,stage,importer,verifier,self.root)
            self.assertEqual(result['publication']['status'],'applied')
            self.assertEqual(result['storage'],'queued',result)
            self.assertEqual(result['work_retirement'],'required')
            self.assertEqual(result['closure'],'open')
            self.assertEqual(model.read_bytes(),raw)
        finally:writer.close()
        result=publisher.publish(self.root,stage,importer,verifier,self.root)
        self.assertEqual(result['publication']['status'],'unchanged')
        self.assertEqual(result['storage'],'processed',result)
        self.assertEqual(result['compacted_models'],1)
        self.assertEqual(storage.read_bytes(self.root,model),raw)
        model.unlink()
        restored=storage.restore_descriptor(self.root,model,storage.sha(raw),verifier)
        self.assertEqual(restored['state'],'restored')
        self.assertEqual(storage.read_bytes(self.root,model),raw)

if __name__=='__main__':unittest.main()
