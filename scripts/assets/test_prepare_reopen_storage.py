import json
from pathlib import Path
import tempfile
import unittest

import prepare_reopen_storage as prepare
import vault_storage as storage


class ReopenPreparationTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name);self.run=self.root/'job/r1';self.run.mkdir(parents=True)
        self.old=self.root/'old.py';self.old.write_text('# legacy cache implementation')
        self.context=self.run/'context.json';self.context.write_text(json.dumps(dict(reopen_payload_store=dict(path=str(self.old),sha256=storage.sha(self.old.read_bytes())))))
        self.manifest=self.run/'manifest.json';self.manifest.write_text(json.dumps(dict(run_root=str(self.run),stages=[dict(id='reopen',inputs={str(self.context):storage.sha(self.context.read_bytes()),str(self.old):storage.sha(self.old.read_bytes())})])))
    def tearDown(self):self.temp.cleanup()
    def test_new_plan_selects_transient_store_and_updates_exact_input_pins(self):
        self.assertEqual(prepare.prepare(self.manifest,self.root)['state'],'prepared')
        c=json.loads(self.context.read_bytes());m=json.loads(self.manifest.read_bytes())
        self.assertEqual(c['reopen_payload_store']['retention'],'until_last_consumer_or_worker_exit')
        self.assertNotIn(str(self.old),m['stages'][0]['inputs'])
        self.assertEqual(m['stages'][0]['inputs'][str(self.context)],storage.sha(self.context.read_bytes()))
        self.assertEqual(prepare.prepare(self.manifest,self.root)['state'],'unchanged')
    def test_executed_or_sealed_plans_are_never_rewritten(self):
        before=self.manifest.read_bytes()
        for name in ('STATE.json','events.jsonl','PLANNING-INPUTS.json'):
            marker=self.run/name;marker.write_text('{}')
            with self.assertRaises(ValueError):prepare.prepare(self.manifest,self.root)
            self.assertEqual(self.manifest.read_bytes(),before);marker.unlink()
    def test_unexpected_input_change_refused_before_any_write(self):
        before=self.manifest.read_bytes();self.old.write_text('# changed')
        with self.assertRaises(ValueError):prepare.prepare(self.manifest,self.root)
        self.assertEqual(self.manifest.read_bytes(),before)


if __name__=='__main__':unittest.main()
