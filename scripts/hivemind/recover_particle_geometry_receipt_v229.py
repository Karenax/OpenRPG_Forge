"""Restore a historical run receipt only if reconstructed bytes match its pinned SHA."""
from pathlib import Path
import hashlib,json,shutil
W=Path('F:/OpenRPG_ImportWork');O=W/'firepit-particle-transform-native-v228'
p=W/'unity-delta-firepit-particle-geometry-v1/RUN-RESULT.v1.json'
prep=json.loads((O/'PREPARED.v1.json').read_text('utf-8-sig'));expected=prep['input_pins'][str(p)]
data=dict(status='336_native_billboard_and_atlas_observations_reopened',native_geometry_verified=True,
gpu_rendered=False,studio_verified=False,full_import=False,upm_broker_pid=32816,upm_owner_pid=37972,
upm_ipc_label='Upm-OpenRPGFirePit-37972',upm_pipe_ready=True,upm_readiness_seconds=0.20053719996940345,
unity_pid=26396,unity_exit_code=0,geometry_observations_verified=336,vertex_observations=30144,
native_geometry_repeated_exactly=True,prior_112_particle_frames_unchanged=True,
independent_receipt_sha256='1533ff38c9f75fd4fc2a37f153c6087b500fd1b2ddd830985263173395330173',
owned_upm_broker_stopped=True,owned_upm_broker_exit_code=1,finished_utc='2026-10-08T03:22:54.164834+00:00')
text=json.dumps(data,indent=2)
candidates=[text.encode('utf-8'),text.replace('\n','\r\n').encode('utf-8')]
matches=[raw for raw in candidates if hashlib.sha256(raw).hexdigest()==expected]
assert len(matches)==1,([hashlib.sha256(raw).hexdigest() for raw in candidates],expected)
raw=matches[0]
backup=O/'ACCIDENTAL-HISTORICAL-RUN-REFUSAL.json';assert not backup.exists();shutil.copyfile(p,backup)
p.write_bytes(raw);assert hashlib.sha256(p.read_bytes()).hexdigest()==expected
(O/'HISTORICAL-RECEIPT-RESTORED.v1.json').write_text(json.dumps(dict(status='historical_run_receipt_restored_to_exact_preexisting_pinned_bytes',path=str(p),sha256=expected,accidental_refusal_preserved=str(backup),source_or_capture_data_changed=False,full_import=False),indent=2),'utf-8')
print('Historical receipt restored: exact preexisting SHA verified.')
