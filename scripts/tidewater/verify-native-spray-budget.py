"""Verify causal GPU feedback and reconstruct the native adaptive budget independently."""
from pathlib import Path
import json,math,struct,sys
import numpy as np
assert len(sys.argv)==4
native,repeat,output=map(Path,sys.argv[1:]);assert not output.exists()
probe=json.loads((native/'probe.json').read_text());report=json.loads((native/'native-budget.json').read_text())
assert probe['native_budget_feedback'] is True and probe['source_budget_schedule'] is False
assert probe['source_particle_results_uploaded'] is False and probe['source_fft_textures_uploaded'] is False
assert report['source_schedule_loaded'] is False and report['steps']==120
assert report==json.loads((repeat/'native-budget.json').read_text()),'budget repeat differs'
head=np.fromfile(native/'history-head.u32.bin','<u4');assert head.size==120
assert (native/'history-head.u32.bin').read_bytes()==(repeat/'history-head.u32.bin').read_bytes()
rows=report['schedule'];assert len(rows)==120
budget=.3;history=[];time=0.;previous_time=0.;max_error=0.
for i,row in enumerate(rows):
 time+=1/60
 assert row['frame']==i+1 and abs(row['time']-time)<1e-12
 used=budget
 if i==0:
  assert row['feedback_frame'] is row['feedback_head'] is row['feedback_time'] is None
 else:
  assert row['feedback_frame']==i and row['feedback_head']==int(head[i-1])
  assert abs(row['feedback_time']-previous_time)<1e-12
  history.append((previous_time,int(head[i-1])))
  while len(history)>2 and previous_time-history[0][0]>2.5:history.pop(0)
  dt=history[-1][0]-history[0][0]
  if dt>1:
   rate=((history[-1][1]-history[0][1])%(2**32))/dt
   want=budget*math.sqrt(16384/rate) if rate>1 else 1
   budget=min(1,max(.05,budget+(want-budget)*.04))
 for name,value in [('emitter_budget',used),('budget',budget)]:
  actual=row[name];assert math.isfinite(actual) and .05<=actual<=1
  max_error=max(max_error,abs(actual-value))
  assert abs(actual-value)<1e-12 and struct.pack('<f',actual)==struct.pack('<f',value)
 previous_time=time
out={'steps':120,'causal_feedback_samples_verified':119,'budget_repeat_exact':True,'native_head_repeat_exact':True,'gpu_f32_budgets_match_independent_reconstruction':True,'max_f64_reconstruction_error':max_error,'first_adjustment_after_emission':next(r['frame'] for r in rows if r['budget']!=.3),'final_budget':rows[-1]['budget'],'native_emissions':int(head[-1]),'limits':['simulation waits for head readbacks, not a real-time performance test','source/native visual comparison remains separate']}
output.write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2))
