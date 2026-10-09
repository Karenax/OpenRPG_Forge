"""Install passed mask files only after old-preimage consumers are terminal."""
from pathlib import Path
import datetime
import hashlib
import json
import subprocess
import sys
import time

W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'source-graph-production-color-mask-install-queue-v1'
HERE=Path(__file__).resolve().parent
INSTALLER=HERE/'install_production_color_mask_v1.py'
CONSUMERS=[W/'source-unlit-forward-native-check-v2/RUN-RESULT.v1.json',
           W/'source-unlit-program-native-check-v1/RUN-RESULT.v1.json']


def read(path):return json.loads(path.read_text('utf-8-sig'))
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def save(name,data):
    temporary=ROOT/(name+'.tmp');temporary.write_text(json.dumps(data,indent=2),'utf-8');temporary.replace(ROOT/name)


def run():
    queued=read(ROOT/'QUEUED.v1.json')
    state=dict(status='waiting_for_old_preimage_consumers_to_finish',canonical_installed=False,full_import=False)
    save('RUN-RESULT.v1.json',state)
    try:
        while not all(path.is_file() and read(path).get('finished_utc') for path in CONSUMERS):time.sleep(15)
        assert sha(INSTALLER)==queued['installer_sha256'],'Installer changed after queueing'
        mask=W/'source-graph-production-color-mask-check-v4/RUN-RESULT.v1.json'
        proof=read(mask)
        assert proof.get('finished_utc') and proof['actual_renderer_tests_passed']==68 and proof['host_tests_passed']==17
        state.update(status='applying_passed_mask_sources',consumer_terminal_receipts={str(p):sha(p) for p in CONSUMERS})
        save('RUN-RESULT.v1.json',state)
        with (ROOT/'installer.stdout.log').open('xb') as out,(ROOT/'installer.stderr.log').open('xb') as err:
            child=subprocess.Popen([sys.executable,'-B',str(INSTALLER),'apply'],cwd=ROOT,
                stdin=subprocess.DEVNULL,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
            state['installer_pid']=child.pid;save('RUN-RESULT.v1.json',state)
            code=child.wait()
        assert code==0,'Installation refused; preserve diagnostics and preimages'
        result=read(W/'source-graph-production-color-mask-install-v1/INSTALL.v1.json')
        assert result['installed_files']==7 and not result['current_product_build_verified']
        state.update(status='seven_passed_mask_source_files_installed_product_build_pending',canonical_installed=True,
            installed_files=7,current_product_build_verified=False,install_receipt_sha256=sha(W/'source-graph-production-color-mask-install-v1/INSTALL.v1.json'))
    except Exception as error:
        state.update(status='refused_or_unverified',error=repr(error))
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)


if __name__=='__main__':
    if len(sys.argv)>1 and sys.argv[1]=='run':run()
    else:
        assert not ROOT.exists();ROOT.mkdir()
        save('QUEUED.v1.json',dict(installer_sha256=sha(INSTALLER),consumer_results=[str(p) for p in CONSUMERS],
             full_import=False,current_product_build_verified=False))
        with (ROOT/'controller.log').open('xb') as out:
            child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,
                stdin=subprocess.DEVNULL,stdout=out,stderr=out,creationflags=subprocess.CREATE_NO_WINDOW)
        save('JOB.v1.json',dict(controller_pid=child.pid));print(json.dumps(dict(controller_pid=child.pid)))
