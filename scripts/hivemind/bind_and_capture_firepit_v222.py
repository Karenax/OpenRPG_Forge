"""Complete the v3 carrier with measured native flags, then reuse the current GPU binary.

No defaults, no source changes, no shader/geometry changes, no runtime qualification.
"""
from pathlib import Path
import copy, datetime, hashlib, json, os, shutil, struct, subprocess, sys, traceback
import bind_native_scene_render_state_v1 as binding

W=Path('F:/OpenRPG_ImportWork')
O=W/'firepit-native-state-conversion-v222'
G=W/'firepit-native-state-lod-gpu-v223'
P=W/'firepit-current-lod-gpu-v220'
def read(p):return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def check(pins):
    for p,h in pins.items():assert sha(p)==h,p
def save(state):
    t=G/'RUN-RESULT.tmp';t.write_text(json.dumps(state,indent=2),'utf-8');t.replace(G/'RUN-RESULT.v1.json')

def prepare():
    assert not O.exists() and not G.exists()
    prior=read(P/'RUN-RESULT.v1.json');assert prior['cargo_exit_code']==0 and prior['capture_exit_code']==1
    assert (P/'stderr.log').read_text('utf-8').strip()=='Error: "Missing authored bool: force_rendering_off"'
    prep=read(P/'PREPARED.v1.json');check(prep['input_pins']);assert sha(P/'capture.exe')==prior['exe_sha256']
    review=read(binding.REVIEW);assert review['status']=='native_lod_renderer_and_hdrp_light_identity_reopened';check(review['input_pins'])
    candidate=Path(prep['candidate']['glb']);raw=candidate.read_bytes();n=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+n]);original=copy.deepcopy(doc)
    native=read(binding.NATIVE);counts=binding.bind(doc,native);assert counts==dict(nodes=9,renderers=6,lod_groups=1)
    negative=[]
    for case in ('renderer_identity','active_flag','shadow_mode'):
        bad=copy.deepcopy(original);renderer=bad['nodes'][1]['extras']['unity_source_node']['renderers'][0]
        if case=='renderer_identity':renderer['component']['guid']='0'*32
        elif case=='active_flag':bad['nodes'][0]['extras']['unity_source_node']['active_self']=not bad['nodes'][0]['extras']['unity_source_node']['active_self']
        else:renderer['shadow_casting_mode']=99
        try:binding.bind(bad,native)
        except (AssertionError,StopIteration):negative.append(case)
        else:raise AssertionError('Corrupted native binding accepted')
    encoded=json.dumps(doc,separators=(',',':'),ensure_ascii=False,allow_nan=False).encode('utf-8');encoded+=b' '*((-len(encoded))%4);tail=raw[20+n:]
    out=raw[:8]+struct.pack('<III',20+len(encoded)+len(tail),len(encoded),0x4e4f534a)+encoded+tail
    assert out[20+len(encoded):]==tail and json.loads(out[20:20+len(encoded)])==doc
    O.mkdir();target=O/'firepit-native-state.glb';target.write_bytes(out)
    pins={**prep['input_pins'],**review['input_pins'],**{str(p):sha(p) for p in [Path(__file__),Path(binding.__file__),binding.REVIEW,binding.NATIVE,P/'RUN-RESULT.v1.json',P/'stderr.log',P/'capture.exe']}}
    check(pins)
    receipt=dict(status='current_firepit_native_renderer_flags_and_lod_identity_bound',input_pins=pins,source=str(candidate),source_sha256=sha(candidate),target=str(target),target_sha256=sha(target),target_bytes=len(out),unchanged_binary_chunk_sha256=hashlib.sha256(tail).hexdigest(),negative_controls=negative,counts=counts,full_import=False,finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    (O/'RESULT.v1.json').write_text(json.dumps(receipt,indent=2),'utf-8')
    G.mkdir();(G/'temp').mkdir();shutil.copyfile(P/'CASE.v1.json',G/'CASE.v1.json')
    pins.update({str(p):sha(p) for p in [target,O/'RESULT.v1.json',G/'CASE.v1.json']})
    (G/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins,candidate=str(target),executable=str(P/'capture.exe'),compiler_repeated=False),indent=2),'utf-8')
    save(dict(status='native_state_lod_gpu_prepared',full_import=False,compiler_repeated=False))
    with (G/'controller.log').open('xb') as log:
        p=subprocess.Popen([sys.executable,'-B',str(Path(__file__)),'run'],stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    (G/'JOB.v1.json').write_text(json.dumps(dict(controller_pid=p.pid)),'utf-8');print(json.dumps(dict(root=str(G),controller_pid=p.pid,conversion=counts)))

def run():
    state=read(G/'RUN-RESULT.v1.json')
    try:
        prep=read(G/'PREPARED.v1.json');check(prep['input_pins']);env=os.environ.copy();env.update(TEMP=str(G/'temp'),TMP=str(G/'temp'))
        with (G/'stdout.log').open('xb') as out,(G/'stderr.log').open('xb') as err:
            p=subprocess.Popen([prep['executable'],prep['candidate'],str(G/'NATIVE-RESULT.v1.json')],env=env,cwd=G,stdout=out,stderr=err,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='native_state_lod_gpu_running',capture_pid=p.pid);save(state);state['capture_exit_code']=p.wait()
        assert state['capture_exit_code']==0,'See stderr.log';check(prep['input_pins'])
        native=read(G/'NATIVE-RESULT.v1.json');assert len(native['captures'])==8
        state.update(status='current_common_host_four_lod_eight_gpu_captures_retained',input_pins=prep['input_pins'],native_sha256=sha(G/'NATIVE-RESULT.v1.json'),exe_sha256=sha(Path(prep['executable'])),capture_pins={r['raw_path']:sha(r['raw_path']) for r in native['captures']},visual_review_pending=True,automatic_lod_selection_verified=False,effects_rendered=False,full_import=False)
    except Exception as exc:state.update(status='native_state_lod_gpu_refusal_retained',error=repr(exc),traceback=traceback.format_exc())
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save(state)

if __name__=='__main__':
    if len(sys.argv)==1:prepare()
    else:assert sys.argv[1:]==['run'];run()
