"""Run six private diagnostics sequentially, never concurrently using Cargo."""
from pathlib import Path
from datetime import datetime, timezone
import json, os, subprocess, sys, time

s=Path(__file__).parent
w=Path('F:/OpenRPG_ImportWork')
root=w/'townsmith-native-uv-batch-v26'
def save(data):
    tmp=root/'BATCH.tmp'
    tmp.write_text(json.dumps(data,indent=2))
    tmp.replace(root/'BATCH.v1.json')
if len(sys.argv)==1:
    assert not root.exists()
    root.mkdir()
    with (root/'controller.log').open('xb') as log:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (root/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=child.pid)))
    print(json.dumps(dict(root=str(root),controller_pid=child.pid)))
else:
    state=dict(status='sequential_uv_controls_running',completed_channels=[],full_import=False)
    try:
        for channel in range(6):
            state['active_channel']=channel
            save(state)
            env=os.environ.copy()
            env['OPENRPG_PRIVATE_UV_CHANNEL']=str(channel)
            subprocess.run([sys.executable,'-B',str(s/'queue_source_glb_uv_control_v26.py')],env=env,check=True)
            receipt=w/f'townsmith-native-uv-channel-{channel}-v26/RUN-RESULT.v1.json'
            while True:
                result=json.loads(receipt.read_text())
                if 'finished_utc' in result: break
                time.sleep(1)
            assert result.get('actual_exit_code')==0 and result.get('gpu_draw_executed'),result
            state['completed_channels'].append(channel)
            save(state)
        state['status']='six_native_uv_channels_drawn_with_stable_readbacks'
    except Exception as exc:
        state.update(status='native_uv_batch_refusal_retained',error=repr(exc))
    state['finished_utc']=datetime.now(timezone.utc).isoformat()
    save(state)
