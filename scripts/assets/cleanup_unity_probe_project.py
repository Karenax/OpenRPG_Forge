"""Remove one verified temporary Unity probe and retain its compact receipt."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

from unity_shared_overlay import inventory_tree
import vault_storage as storage

WORK=Path('E:/OpenRPG_Forge/.build/asset-conversion')


def cleanup(project,prepared,log,output,cases,dependencies):
    work=WORK.resolve(strict=True);project=storage.confined(work,project.absolute())
    if project.parent!=work or output.exists():raise ValueError('invalid cleanup destination')
    running=subprocess.run(['powershell','-NoProfile','-Command',
        '@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],capture_output=True,text=True,check=True)
    if running.stdout.strip()!='0':raise ValueError('Unity is still running')
    prepared_raw=prepared.read_bytes();p=json.loads(prepared_raw)
    if Path(p['project']).resolve(strict=True)!=project or len(p['cases'])!=cases or len(p['dependencies'])!=dependencies:
        raise ValueError('prepared probe differs')
    result_raw=(project/'guid-probe-result.json').read_bytes();r=json.loads(result_raw)
    if r.get('checked_count')!=cases or r.get('dependency_count')!=dependencies or r.get('failures')!=0 or r.get('errors'):
        raise ValueError('Unity probe did not pass')
    log_raw=log.read_bytes()
    if b'SHARED_GUID_PROBE' not in log_raw:raise ValueError('Unity log has no probe result')
    files,dirs=inventory_tree(project);total=sum(path.stat().st_size for _,path in files.values())
    result={'state':'passed','checked_count':cases,'dependency_count':dependencies,'failures':0,
            'prepared_sha256':storage.sha(prepared_raw),'result_sha256':storage.sha(result_raw),
            'log_sha256':storage.sha(log_raw),'files_to_remove':len(files),'logical_bytes':total,
            'limitations':'Isolated Unity import and direct dependency check; rendering and other consumers not qualified.'}
    with output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n');f.flush();os.fsync(f.fileno())
    before=shutil.disk_usage(work).free
    for _,path in files.values():
        storage.confined(project,path).unlink()
    for rel in sorted(dirs,key=lambda x:len(Path(x).parts),reverse=True):
        storage.confined(project,project/rel).rmdir()
    project.rmdir()
    result['state']='passed_temporary_project_removed';result['free_delta_observed']=shutil.disk_usage(work).free-before
    output.write_text(json.dumps(result,indent=2)+'\n',encoding='utf8')
    return result


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('project','prepared','log','output'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--cases',type=int,required=True);p.add_argument('--dependencies',type=int,required=True);a=p.parse_args()
    print(json.dumps(cleanup(a.project,a.prepared,a.log,a.output,a.cases,a.dependencies)))
