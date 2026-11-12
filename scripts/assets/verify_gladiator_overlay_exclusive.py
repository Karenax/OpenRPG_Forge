"""Compare real Gladiator source audits with duplicate source paths locked."""
import argparse
import contextlib
import json
import os
from pathlib import Path
import subprocess
import sys

from compare_gladiator_overlay_audit import normalized
from exclusive_source_delete import ExclusiveSource
from unity_shared_overlay import configured_resolver
import vault_storage as storage

REPO=Path(__file__).resolve().parents[2]
WORK=Path('E:/OpenRPG_Forge/.build/asset-conversion')
PACKAGE='originals/unity-purchased/hivemind/source-support-v1/modular-gladiator-arena-base170-v1'
TOOLS=(('case','audit_gladiator_mirror_case_v379.py','OPENRPG_GLADIATOR_AUDIT_OUTPUT'),
       ('lot','audit_gladiator_mirror_lot_v385.py','OPENRPG_GLADIATOR_LOT_OUTPUT'),
       ('closure','close_gladiator_lot_dependencies_v394.py','OPENRPG_GLADIATOR_CLOSURE_OUTPUT'))


def run(vault, prefix):
    vault=vault.resolve(strict=True);work=WORK.resolve(strict=True)
    if not prefix.isalnum():raise ValueError('alphanumeric prefix required')
    source=storage.confined(vault,vault/PACKAGE/'source')
    resolver=configured_resolver(vault,source)
    if resolver is None:raise ValueError('published overlay unavailable')
    manifest=json.loads(resolver.manifest_raw);root=manifest['source_root']+'/';candidates=[]
    for row in manifest['files']:
        if row['vault_path'].casefold().startswith(root.casefold()):continue
        path=storage.confined(vault,resolver.source_root/row['logical'])
        candidates.append((path,row))
    if len(candidates)!=60:raise ValueError('unexpected duplicate set')
    reg=storage.confined(vault,vault/'metadata/unity-shared-overlay-registry.v1.json')
    support=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json')
    models=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json')
    before=[x.read_bytes() for x in (reg,support,models)];results={}
    def execute(label):
        output={}
        for key,script,variable in TOOLS:
            destination=work/(prefix+label+key)
            if destination.exists():raise ValueError('output already exists: '+str(destination))
            env=os.environ.copy();env.pop('OPENRPG_UNITY_OVERLAY_MANIFEST',None);env.pop('OPENRPG_UNITY_OVERLAY_SHA256',None)
            env[variable]=str(destination)
            proc=subprocess.run([sys.executable,'-B',str(REPO/'scripts/hivemind'/script)],env=env,
                                capture_output=True,text=True,encoding='utf8',errors='replace')
            if proc.returncode:raise ValueError(script+' failed: '+proc.stderr[-2500:])
            data=(destination/'RESULT.v1.json').read_bytes();output[key]=(storage.sha(data),json.loads(data))
        return output
    baseline=execute('baseline')
    with contextlib.ExitStack() as stack:
        for path,row in candidates:
            handle=stack.enter_context(ExclusiveSource(path))
            if handle.sha256()!=row['sha256']:raise ValueError('original SHA changed')
        locked=execute('locked')
        for key,_,_ in TOOLS:
            if normalized(baseline[key][1])!=normalized(locked[key][1]):raise ValueError('audit results differ: '+key)
            results[key]={'baseline_sha256':baseline[key][0],'locked_sha256':locked[key][0]}
        if [x.read_bytes() for x in (reg,support,models)]!=before or resolver.manifest_path.read_bytes()!=resolver.manifest_raw:
            raise ValueError('registry changed')
    return {'state':'passed','locked_original_files':len(candidates),'audits':results,
            'overlay_registry_sha256':storage.sha(before[0]),'limitations':'Three source audits only; geometry capture, Unity import and other consumers are not qualified.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--vault',type=Path,required=True)
    p.add_argument('--prefix',required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=run(a.vault,a.prefix)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps(result))
