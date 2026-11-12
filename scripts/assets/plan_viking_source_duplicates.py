"""Revalidate Viking FBX duplicates and their package references; never delete."""
import argparse
import collections
import hashlib
import json
from pathlib import Path

from retire_relinked_source_copies import contains_reference
import vault_storage as storage


def plan(vault, candidate_file, duplicate_file, references_file):
    vault=vault.resolve(strict=True)
    inputs=[p.read_bytes() for p in (candidate_file,duplicate_file,references_file)]
    candidates=json.loads(inputs[0]);duplicates=json.loads(inputs[1]);references=json.loads(inputs[2])
    if candidates.get('state')!='candidates_only' or references.get('state')!='audit_only':
        raise ValueError('unexpected input evidence')
    if references['candidate_sha256']!=storage.sha(inputs[0]) or references.get('parse_errors'):
        raise ValueError('candidate or package audit changed')
    pointers=[storage.confined(vault,vault/'metadata/asset-import-registry.v1.json'),
              storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json')]
    before=[p.read_bytes() for p in pointers]
    def canonical(value):return str(value).replace('\\','/').casefold()
    old={canonical(x['path']):x for x in candidates['rows']}
    prior={canonical(x['path']):x for x in references['results']}
    if len(old)!=len(candidates['rows']) or set(old)!=set(prior):raise ValueError('candidate coverage differs')
    groups={}
    for group in duplicates['groups']:
        for item in group['files']:
            key=canonical(item['path'])
            if key in groups:raise ValueError('ambiguous duplicate inventory')
            groups[key]=group
    document_cache={};file_cache={};rows=[]
    def pin(path):
        path=storage.confined(vault,Path(path))
        if path not in file_cache:
            with storage.locked_source(path,share_delete=False) as stream:
                size=path.stat().st_size;digest=hashlib.file_digest(stream,'sha256').hexdigest()
            file_cache[path]=(digest,size)
        return file_cache[path]
    for key,source in old.items():
        group=groups.get(key)
        if not group:raise ValueError('candidate missing from duplicate inventory')
        choices=[x for x in group['files'] if canonical(x['path'])!=key and canonical(x['path']) not in old
                 and '.partial' not in canonical(x['path'])]
        choices.sort(key=lambda x:canonical(x['path']))
        if not choices:
            rows.append({'old':source['path'],'state':'review_required','reason':'no independent retained path'});continue
        retained=choices[0]
        try:
            if pin(source['path'])!=(source['sha256'],source['bytes']) or pin(retained['path'])!=(source['sha256'],source['bytes']):
                raise ValueError('source bytes differ')
            relative=Path(source['path']).relative_to(vault).as_posix().casefold()
            docs=[]
            for item in prior[key]['package_references']:
                path=storage.confined(vault,Path(item['document']))
                if path not in document_cache:
                    raw=path.read_bytes();document_cache[path]=(storage.sha(raw),json.loads(raw))
                dp,doc=document_cache[path]
                docs.append({'document':str(path),'sha256':dp,'field':item['field'],
                             'resolved_exact_reference':contains_reference(doc,path,vault,{relative})})
            rows.append({'old':source['path'],'retained':retained['path'],'sha256':source['sha256'],
                         'bytes':source['bytes'],'state':'verified_bytes', 'package_references':docs})
        except (OSError,ValueError,KeyError) as exc:
            rows.append({'old':source['path'],'state':'review_required','reason':str(exc)})
    if [p.read_bytes() for p in pointers]!=before or [p.read_bytes() for p in (candidate_file,duplicate_file,references_file)]!=inputs:
        raise ValueError('registry or inputs changed')
    if any(storage.sha(path.read_bytes())!=pin for path,(pin,_) in document_cache.items()):
        raise ValueError('package document changed during audit')
    return {'state':'planning_only','model_registry_sha256':storage.sha(before[0]),
            'source_registry_sha256':storage.sha(before[1]),'input_sha256':[storage.sha(x) for x in inputs],
            'rows':rows,'limitations':'Byte comparison and recorded package documents only; active nested catalog migration, GUID consumers and removal are not qualified.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','candidates','duplicates','references','output'):p.add_argument('--'+n,type=Path,required=True)
    a=p.parse_args();result=plan(a.vault,a.candidates,a.duplicates,a.references)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    states=collections.Counter(x['state'] for x in result['rows'])
    print(json.dumps({'states':dict(states),'bytes':sum(x.get('bytes',0) for x in result['rows'])}))
