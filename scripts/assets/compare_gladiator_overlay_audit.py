"""Compare direct and shared-path Gladiator audit outputs without mutating them."""
import argparse
import json
from pathlib import Path
import vault_storage as storage


def normalized(value):
    if isinstance(value,dict):
        return {k:normalized(v) for k,v in value.items() if k!='finished_utc'}
    if isinstance(value,list):
        return sorted((normalized(v) for v in value),key=lambda v:json.dumps(v,sort_keys=True))
    return value


def compare(direct,shared,manifest):
    ar,br,mr=direct.read_bytes(),shared.read_bytes(),manifest.read_bytes()
    a,b,m=json.loads(ar),json.loads(br),json.loads(mr)
    if normalized(a)!=normalized(b):raise ValueError('audits disagree')
    root=m['source_root']+'/';pins={x.replace('\\','/').casefold() for x in b['input_pins']}
    entries=[x for x in m['files'] if not x['vault_path'].casefold().startswith(root.casefold())]
    used=sum(any(k.endswith((root+x['logical']).casefold()) for k in pins) for x in entries)
    if not used:raise ValueError('audit did not access any shared entries')
    if (direct.read_bytes(),shared.read_bytes(),manifest.read_bytes())!=(ar,br,mr):raise ValueError('inputs changed')
    return {'state':'passed','direct_sha256':storage.sha(ar),'shared_sha256':storage.sha(br),
            'manifest_sha256':storage.sha(mr),'shared_inputs':used,
            'limitations':'One source dependency audit only; conversion, rendering and other consumers unqualified.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('direct','shared','manifest','output'):p.add_argument('--'+n,type=Path,required=True)
    a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=compare(a.direct,a.shared,a.manifest)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps(result))
