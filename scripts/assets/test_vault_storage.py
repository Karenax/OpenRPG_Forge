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
        storage.atomic_json(self.root/'.forge-storage/deployed-readers.v1.json',dict(state='ready',vault=str(self.root),reader_cli_sha256=reader_sha,test_fixture=True))
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
