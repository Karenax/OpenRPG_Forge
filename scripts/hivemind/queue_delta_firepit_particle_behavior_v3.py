"""Reopen terminal v2 refusal and capture with the normal installed-package resolver."""
from pathlib import Path
import hashlib
import json
import subprocess
import sys

W=Path('F:/OpenRPG_ImportWork')
BASE=Path(__file__).with_name('queue_delta_firepit_particle_behavior_v1.py')
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest()


def read(path):
    return json.loads(Path(path).read_text('utf-8-sig'))


if len(sys.argv)==1:
    prior_root=W/'unity-delta-firepit-particle-behavior-v2'
    prior=read(prior_root/'RUN-RESULT.v1.json')
    assert prior['status']=='refused_or_unverified' and prior['unity_exit_code']==1
    assert 'error CS0234' in (prior_root/'unity.log').read_text('utf-8-sig')
    prior_job=read(prior_root/'JOB.v1.json')
    output=subprocess.run([PS,'-NoProfile','-Command',
        f'Get-Process -Id {prior["unity_pid"]},{prior_job["controller_pid"]} -ErrorAction SilentlyContinue | ForEach-Object {{$_.Id}}'],
        capture_output=True,text=True,creationflags=subprocess.CREATE_NO_WINDOW).stdout
    assert not output.strip(),'Exact previous native processes remain live; preserve them'

SCRIPT=BASE.read_text('utf-8-sig')


def replace(original,replacement):
    global SCRIPT
    assert SCRIPT.count(original)==1,original
    SCRIPT=SCRIPT.replace(original,replacement)


replace("ROOT = W / 'unity-delta-firepit-particle-behavior-v1'",
        "ROOT = W / 'unity-delta-firepit-particle-behavior-v3'")
replace("PROJECT = W / 'hivemind-prefab-instance-source-closure-v1/projects/native-american-village'",
        "PROJECT = W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'")
replace("assert not destination.exists() and not Path(str(destination)+'.meta').exists()",
        "assert destination.is_file() and sha(destination)==sha(helper), 'Retain identical installed helper; refuse a changed helper'")
replace('shutil.copyfile(helper,destination)',
        "assert sha(helper)==sha(destination), 'No helper rewrite or source change'")
replace("'-nographics','-noUpm','-quit'","'-nographics','-quit'")
needle="pins.update(source['input_pins']);pins.update(source['output_pins'])"
replace(needle,needle+"""
    repair_root=W/'delta-firepit-hdrp-closure-repair-v2'
    repair=read(repair_root/'RESULT.v1.json');check(repair['input_pins'])
    pins.update(repair['input_pins']);pins[str(repair_root/'RESULT.v1.json')]=sha(repair_root/'RESULT.v1.json')
    prior_root=W/'unity-delta-firepit-particle-behavior-v2'
    for p in [prior_root/'RUN-RESULT.v1.json',prior_root/'unity.log',prior_root/'PREPARED.v1.json',Path(__file__).with_name('queue_delta_firepit_particle_behavior_v1.py')]:
        pins[str(p)]=sha(p)
""")
exec(compile(SCRIPT,str(BASE),'exec'),{'__name__':'__main__','__file__':str(Path(__file__).resolve())})
