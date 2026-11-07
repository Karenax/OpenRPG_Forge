import importlib.util
import hashlib
import json
import os
from pathlib import Path
import tempfile
import time
import sqlite3
import contextlib
import unittest
from unittest.mock import patch
import finish_work_retirement as finish

spec=importlib.util.spec_from_file_location('retire',Path(__file__).with_name('retire_work_duplicates.py'))
retire=importlib.util.module_from_spec(spec);spec.loader.exec_module(retire)


class RetirementTests(unittest.TestCase):
    def setUp(self):
        self.work=tempfile.TemporaryDirectory();self.root=Path(self.work.name)/'jobs';self.root.mkdir()
        self.run=self.root/'converted-example/r1';self.run.mkdir(parents=True)
        self.state=self.run/'STATE.json';self.state.write_text('{"stages":{"export":{"status":"complete"}}}')
        self.source=self.run/'copy.glb';self.source.write_bytes(b'EXACT derived model')
        old=time.time()-3*86400;os.utime(self.source,(old,old))
        s=self.source.stat();self.row=dict(path=str(self.source),bytes=s.st_size,mtime_ns=s.st_mtime_ns,sha256=hashlib.sha256(self.source.read_bytes()).hexdigest(),job='converted-example',state='hashed')
        self.keeper=self.root/'retained.glb';self.keeper.write_bytes(self.source.read_bytes())
        _,state=retire.source_ready(self.root,self.row,set(),'')
        self.item=dict(source=self.row,target=dict(kind='work',file=dict(path=str(self.keeper))),completed_state=state)
    def tearDown(self):self.work.cleanup()
    def apply(self):
        plan=dict(work_root=str(self.root),repository=str(self.root.parent),files=[self.item])
        with patch.object(retire,'references',return_value=(set(),[])):
            return retire.apply_plan(plan,self.root.parent/'vault','')
    def test_exact_duplicate_removed_and_retained_copy_preserved(self):
        result=self.apply();self.assertEqual(len(result['removed']),1)
        self.assertFalse(self.source.exists());self.assertTrue(self.keeper.exists())
    def test_same_size_different_contents_never_removed(self):
        self.keeper.write_bytes(b'x'*self.row['bytes'])
        self.assertEqual(len(self.apply()['removed']),0);self.assertTrue(self.source.exists())
    @unittest.skipUnless(os.name=='nt','Windows sharing semantics')
    def test_retained_copy_cannot_be_written_or_replaced_while_held(self):
        replacement=self.root/'replacement';replacement.write_bytes(b'new data')
        with contextlib.ExitStack() as held:
            retire.verify_target(self.item,self.root,self.root.parent/'vault',held)
            with self.assertRaises(PermissionError):self.keeper.write_bytes(b'new data')
            with self.assertRaises(PermissionError):os.replace(replacement,self.keeper)
        self.assertEqual(self.keeper.read_bytes(),self.source.read_bytes())
    def test_source_changed_after_plan_never_removed(self):
        self.source.write_bytes(b'x'*self.row['bytes'])
        self.assertEqual(len(self.apply()['removed']),0);self.assertTrue(self.source.exists())
    def test_current_reference_or_process_blocks_retirement(self):
        for protected,commands in [({'converted-example'},''),(set(),'python converted-example')]:
            with self.assertRaises(ValueError):retire.source_ready(self.root,self.row,protected,commands)
    def test_incomplete_and_state_changed_runs_preserved(self):
        self.state.write_text('{"stages":{"export":{"status":"running"}}}')
        self.assertEqual(len(self.apply()['removed']),0);self.assertTrue(self.source.exists())
    def test_outside_work_root_refused(self):
        self.row['path']=str(self.keeper.parent.parent/'outside.glb')
        with self.assertRaises(ValueError):retire.source_ready(self.root,self.row,set(),'')
    def test_ad_hoc_worker_requires_exact_delivery_pin(self):
        self.state.unlink()
        receipt=self.run/'DELIVERY-READBACK.v1.json'
        receipt.write_text(json.dumps(dict(status='CANDIDATES_ALL_PAYLOAD_SHA256_READBACK_PASS',destination='F:/Vault/candidates/example',files=[dict(path='models/a.glb',bytes=self.row['bytes'],sha256=self.row['sha256'])])))
        _,proof=retire.source_ready(self.root,self.row,set(),'')
        self.assertEqual(len(proof['delivered_paths']),1)
        receipt.write_text(json.dumps(dict(status='CANDIDATES_ALL_PAYLOAD_SHA256_READBACK_PASS',files=[])))
        with self.assertRaises(ValueError):retire.source_ready(self.root,self.row,set(),'')
    def test_missing_state_can_use_exact_independent_replica_not_filename_heuristic(self):
        self.state.unlink()
        replica=dict(state='complete',path='F:/Vault/candidates/model.glb',sha256=self.row['sha256'],bytes=self.row['bytes'],descriptor_sha256='1'*64)
        _,proof=retire.source_ready(self.root,self.row,set(),'',replica)
        self.assertEqual(proof['kind'],'verified-vault-replica')
        with self.assertRaises(ValueError):retire.source_ready(self.root,self.row,set(),'',dict(replica,sha256='0'*64))
        with self.assertRaises(ValueError):retire.source_ready(self.root,self.row,{'converted-example'},'',replica)
        self.state.write_text('{"stages":{"export":{"status":"running"}}}')
        with self.assertRaises(ValueError):retire.source_ready(self.root,self.row,set(),'',replica)
    def test_same_replica_does_not_authorize_removal_of_original_asset_tree(self):
        self.state.unlink();folder=self.run/'Assets';folder.mkdir();source=folder/'source.glb';source.write_bytes(self.source.read_bytes())
        s=source.stat();row=dict(self.row,path=str(source),mtime_ns=s.st_mtime_ns)
        replica=dict(state='complete',path='F:/Vault/candidates/model.glb',sha256=row['sha256'],bytes=row['bytes'],descriptor_sha256='1'*64)
        with self.assertRaises(ValueError):retire.source_ready(self.root,row,set(),'',replica)
    def test_verified_vault_descriptor_must_match_exact_receipt(self):
        vault=self.root.parent/'vault';vault.mkdir();target=vault/'model.glb'
        writer=retire.storage.Packer(vault)
        target.write_bytes(self.source.read_bytes())
        try:
            prepared=writer.prepare(target);writer.publish(prepared)
        finally:writer.close()
        raw=target.read_bytes()
        self.item['target']=dict(kind='vault-receipt',receipt=dict(path=str(target),descriptor_sha256=retire.storage.sha(raw)))
        retire.verify_target(self.item,self.root,vault)
        target.write_bytes(raw+b' ')
        with self.assertRaises(ValueError):retire.verify_target(self.item,self.root,vault)
        self.assertTrue(self.source.exists())
    def test_raw_vault_replica_is_rehashed_under_lock_and_changed_bytes_refused(self):
        vault=self.root.parent/'vault';vault.mkdir();target=vault/'model.glb'
        raw=b'glTF raw verified replica';self.source.write_bytes(raw);target.write_bytes(raw)
        stat=self.source.stat();digest=hashlib.sha256(raw).hexdigest()
        self.row.update(bytes=stat.st_size,mtime_ns=stat.st_mtime_ns,sha256=digest)
        self.item['target']=dict(kind='vault-receipt',receipt=dict(path=str(target),representation='raw-glb',descriptor_sha256=digest))
        retire.verify_target(self.item,self.root,vault)
        target.write_bytes(b'glTF changed but same size')
        with self.assertRaises(ValueError):retire.verify_target(self.item,self.root,vault)
        self.assertTrue(self.source.exists())
    def test_absolute_model_readback_pins_delivered_path(self):
        self.state.unlink();target=(self.root.parent/'vault'/'model.glb').resolve()
        receipt=self.run/'DELIVERY-READBACK.v1.json'
        receipt.write_text(json.dumps(dict(status='MODELS_SHA256_READBACK_PASS',models=[dict(path=str(target),bytes=self.row['bytes'],sha256=self.row['sha256'])])))
        _,proof=retire.source_ready(self.root,self.row,set(),'')
        self.assertEqual(proof['delivered_paths'],[str(target)])
    def test_hash_inventory_reuses_existing_records_and_rejects_changed_files(self):
        database=self.root.parent/'inventory.sqlite';output=self.root.parent/'hashes.jsonl'
        with contextlib.closing(sqlite3.connect(database)) as db:
            db.execute('CREATE TABLE files(path,bytes,mtime_ns,job,extension,git)')
            db.execute('INSERT INTO files VALUES (?,?,?,?,?,?)',(str(self.source),self.row['bytes'],self.row['mtime_ns'],'converted-example','.glb',''))
            db.commit()
        retire.hash_inventory(database,self.root,output)
        records=output.read_text().splitlines();self.assertEqual(json.loads(records[0])['sha256'],self.row['sha256'])
        retire.hash_inventory(database,self.root,output);self.assertEqual(output.read_text().splitlines(),records)
        self.source.write_bytes(b'changed')
        output.unlink();retire.hash_inventory(database,self.root,output)
        self.assertEqual(json.loads(output.read_text())['state'],'excluded')
    def test_finalizer_requires_same_completed_final_migration(self):
        self.assertFalse(finish.dependency_state(dict(state='running',plan_sha256='pin'),'pin'))
        self.assertTrue(finish.dependency_state(dict(state='complete',phase='final',plan_sha256='pin'),'pin'))
        for value in [dict(state='interrupted',plan_sha256='pin'),dict(state='complete',phase='preparing',plan_sha256='pin'),dict(state='complete',phase='final',plan_sha256='different')]:
            with self.assertRaises(ValueError):finish.dependency_state(value,'pin')
    def test_incremental_reader_uses_only_finished_verified_records(self):
        journal=self.root.parent/'migration.jsonl'
        journal.write_bytes(b'{"state":"complete","path":"verified"}\n{"state":"prepared"}\n{"state":"complete"')
        self.assertEqual(finish.completed_receipts(journal),[dict(state='complete',path='verified')])


if __name__=='__main__':unittest.main()
