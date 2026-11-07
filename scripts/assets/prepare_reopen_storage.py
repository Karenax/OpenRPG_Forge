"""Use transient decoded payloads in a new, not-yet-sealed reopening plan.

Never amend an executed manifest or its historical results. This changes only
the lifetime of regenerable copies; the decoder API and asset bytes are intact.
"""
import argparse
import json
from pathlib import Path

import retire_work_duplicates as retirement
import vault_storage as storage


def prepare(manifest,work):
    manifest=retirement.confined(work,Path(manifest));raw=manifest.read_bytes();doc=json.loads(raw)
    run=retirement.confined(work,Path(doc['run_root']))
    if manifest.parent!=run:raise ValueError('Manifest/run mismatch')
    if any((run/name).exists() for name in ('STATE.json','events.jsonl','PLANNING-INPUTS.json')):raise ValueError('Run already executed or sealed; preserve historical plan')
    if (run/'receipts').exists() and any((run/'receipts').iterdir()):raise ValueError('Historical receipts exist')
    context=retirement.confined(work,run/'context.json');old=context.read_bytes();value=json.loads(old)
    source=Path(__file__).with_name('verified_payload_store.py');pin=storage.sha(source.read_bytes())
    before=value['reopen_payload_store'];old_path=before['path']
    if old_path==str(source) and before['sha256']==pin:return dict(state='unchanged',manifest=str(manifest))
    if storage.sha(Path(old_path).read_bytes())!=before['sha256']:raise ValueError('Previous decoder pin differs')
    for stage in doc['stages']:
        inputs=stage.get('inputs',{})
        if str(context) in inputs and inputs[str(context)]!=storage.sha(old):raise ValueError('Context pin differs before preparation')
    value['reopen_payload_store']=dict(path=str(source),sha256=pin,retention='until_last_consumer_or_worker_exit',source='retained_GLB')
    encoded=json.dumps(value,ensure_ascii=True,sort_keys=True).encode()
    for stage in doc['stages']:
        inputs=stage.setdefault('inputs',{})
        if old_path in inputs:del inputs[old_path]
        inputs[str(source)]=pin;inputs[str(context)]=storage.sha(encoded)
    for tool in doc.get('owned_tools',{}).values():
        if tool['path']==old_path:tool.update(path=str(source),sha256=pin)
    # A mid-preparation interruption leaves a hash mismatch, never an accepted
    # executed plan. Normal errors restore the original context immediately.
    context.write_bytes(encoded)
    try:storage.atomic_json(manifest,doc)
    except BaseException:context.write_bytes(old);raise
    return dict(state='prepared',manifest=str(manifest),payload_retention='transient',decoder_sha256=pin)


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--manifest',type=Path,required=True);a=p.parse_args()
    work=Path(__file__).resolve().parents[2].parent/'.build/asset-conversion'
    print(json.dumps(prepare(a.manifest,work)))


if __name__=='__main__':main()
