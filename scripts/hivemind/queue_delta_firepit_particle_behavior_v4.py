"""Use an owned, readiness-checked UPM broker instead of failing automatic startup."""
from pathlib import Path
import ctypes
import hashlib
import json
import os
import subprocess
import sys
import time

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'unity-delta-firepit-particle-behavior-v4'
BASE=Path(__file__).with_name('queue_delta_firepit_particle_behavior_v1.py')
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
UPM=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/Server/UnityPackageManager.exe')
LOG_HANDLES=[]


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


def write_state(state):
    temporary=ROOT/'RUN-RESULT.tmp'
    temporary.write_text(json.dumps(state,indent=2),'utf-8')
    os.replace(temporary,ROOT/'RUN-RESULT.v1.json')


def stop_broker(broker,state):
    if broker.poll() is None:
        broker.terminate()
        try:broker.wait(timeout=15)
        except subprocess.TimeoutExpired:broker.kill();broker.wait(timeout=10)
    for handle in LOG_HANDLES:handle.close()
    state['owned_upm_broker_stopped']=True
    state['owned_upm_broker_exit_code']=broker.returncode


def start_broker(state):
    owner=os.getpid()
    label='Upm-OpenRPGFirePit-'+str(owner)
    server_path='Unity-'+label
    pipe='\\\\.\\pipe\\'+server_path
    version=read(W/'unity-upm-runtime-diagnostic-v1/RESULT.v1.json')
    assert version['exit_code']==0 and sha(UPM)==version['binary_sha256']
    stdout=(ROOT/'upm.stdout.log').open('xb');stderr=(ROOT/'upm.stderr.log').open('xb')
    LOG_HANDLES.extend([stdout,stderr])
    env=os.environ.copy();env.update(TEMP=str(ROOT/'temp'),TMP=str(ROOT/'temp'),UPM_CACHE_ROOT=str(W/'unity-package-cache'))
    broker=subprocess.Popen([str(UPM),'server','-s',str(owner),'--ipc-path',server_path,'-l','2'],
        cwd=UPM.parent,env=env,stdout=stdout,stderr=stderr,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    state.update(status='starting_owned_upm_broker_for_installed_hdrp_packages',upm_broker_pid=broker.pid,
        upm_owner_pid=owner,upm_ipc_label=label,upm_pipe_ready=False)
    write_state(state)
    kernel=ctypes.WinDLL('kernel32',use_last_error=True)
    kernel.WaitNamedPipeW.argtypes=[ctypes.c_wchar_p,ctypes.c_uint32]
    kernel.WaitNamedPipeW.restype=ctypes.c_int
    started=time.monotonic()
    try:
        while time.monotonic()-started<45:
            assert broker.poll() is None,'Owned UPM broker exited before IPC readiness: '+str(broker.returncode)
            if kernel.WaitNamedPipeW(pipe,0):
                state['upm_pipe_ready']=True
                state['upm_readiness_seconds']=time.monotonic()-started
                with (ROOT/'UPM-BROKER-READY.v1.json').open('x',encoding='utf-8') as stream:
                    json.dump(dict(owner_pid=owner,broker_pid=broker.pid,pipe=pipe,
                        installed_binary_sha256=version['binary_sha256'],named_pipe_available=True,
                        automatic_v3_refusal_preserved=True,full_import=False),stream,indent=2)
                write_state(state)
                return broker,label
            time.sleep(.2)
        raise AssertionError('Owned UPM broker IPC not ready; preserve diagnostic logs')
    except Exception:
        stop_broker(broker,state)
        raise


if len(sys.argv)==1:
    previous=W/'unity-delta-firepit-particle-behavior-v3'
    prior=read(previous/'RUN-RESULT.v1.json')
    assert prior['status']=='refused_or_unverified' and prior['unity_exit_code']==1
    assert 'Could not establish a connection with the Unity Package Manager local server process.' in (previous/'unity.log').read_text('utf-8-sig')
    pid=read(previous/'JOB.v1.json')['controller_pid']
    live=subprocess.run([PS,'-NoProfile','-Command',
        f'Get-Process -Id {prior["unity_pid"]},{pid} -ErrorAction SilentlyContinue | ForEach-Object {{$_.Id}}'],
        capture_output=True,text=True,creationflags=subprocess.CREATE_NO_WINDOW).stdout
    assert not live.strip(),'Previous exact native job preserved until actual completion'

SCRIPT=BASE.read_text('utf-8-sig')


def replace(original,replacement):
    global SCRIPT
    assert SCRIPT.count(original)==1,original
    SCRIPT=SCRIPT.replace(original,replacement)


replace("ROOT = W / 'unity-delta-firepit-particle-behavior-v1'","ROOT = W / 'unity-delta-firepit-particle-behavior-v4'")
replace("PROJECT = W / 'hivemind-prefab-instance-source-closure-v1/projects/native-american-village'",
        "PROJECT = W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'")
replace("assert not destination.exists() and not Path(str(destination)+'.meta').exists()",
        "assert destination.is_file() and sha(destination)==sha(helper), 'Retain exact installed helper'")
replace('shutil.copyfile(helper,destination)',"assert sha(helper)==sha(destination), 'No helper rewrite'")
replace("'-nographics','-noUpm','-quit'","'-nographics','-quit','-upmIpcPath',ipc_label")
needle="pins.update(source['input_pins']);pins.update(source['output_pins'])"
replace(needle,needle+"""
    repair_root=W/'delta-firepit-hdrp-closure-repair-v2'
    repair=read(repair_root/'RESULT.v1.json');check(repair['input_pins'])
    pins.update(repair['input_pins']);pins[str(repair_root/'RESULT.v1.json')]=sha(repair_root/'RESULT.v1.json')
    for p in [W/'unity-delta-firepit-particle-behavior-v3/RUN-RESULT.v1.json',W/'unity-delta-firepit-particle-behavior-v3/unity.log',W/'unity-upm-runtime-diagnostic-v1/RESULT.v1.json',Path(__file__).with_name('queue_delta_firepit_particle_behavior_v1.py')]:
        pins[str(p)]=sha(p)
""")
replace("save('RUN-RESULT.v1.json',state)\n    try:","save('RUN-RESULT.v1.json',state)\n    broker=None\n    try:")
replace("with (ROOT/'unity.stdout.log').open('xb') as out,(ROOT/'unity.stderr.log').open('xb') as err:",
        "broker,ipc_label=start_broker(state)\n        with (ROOT/'unity.stdout.log').open('xb') as out,(ROOT/'unity.stderr.log').open('xb') as err:")
replace("state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)",
        "if broker is not None:stop_broker(broker,state)\n    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)")
exec(compile(SCRIPT,str(BASE),'exec'),{'__name__':'__main__','__file__':str(Path(__file__).resolve()),
    'start_broker':start_broker,'stop_broker':stop_broker})
