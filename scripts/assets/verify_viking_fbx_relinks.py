"""Regenerate a Viking FBX relink stage and compare every output byte."""
import argparse
import json
from pathlib import Path
import stat

import vault_storage as storage
from stage_viking_fbx_relinks import prepare


def files_under(root):
    root=root.resolve(strict=True);files={};dirs=[];stack=[root]
    while stack:
        directory=stack.pop();dirs.append(directory)
        for p in directory.iterdir():
            info=p.lstat()
            if p.is_symlink() or getattr(info,'st_file_attributes',0)&0x400:
                raise ValueError('reparse point in stage')
            if stat.S_ISDIR(info.st_mode):stack.append(p)
            elif stat.S_ISREG(info.st_mode):files[p.relative_to(root)]=p
            else:raise ValueError('unexpected stage entry')
    return files,dirs


def verify(vault,candidates,audit,stage,rebuilt):
    stage=stage.resolve(strict=True);work=stage.parent.resolve(strict=True)
    rebuilt=rebuilt.absolute()
    if rebuilt.parent.resolve(strict=True)!=work or rebuilt.exists() or rebuilt==stage:
        raise ValueError('rebuild destination must be a new work-root child')
    raw=(stage/'plan.json').read_bytes();plan=json.loads(raw)
    if plan.get('schema')!='openrpg.viking-fbx-relink-stage.v1' or plan.get('state')!='prepared_only':
        raise ValueError('invalid stage')
    prepare(vault,candidates,audit,rebuilt)
    originals,_=files_under(stage);copies,dirs=files_under(rebuilt)
    if set(originals)!=set(copies):raise ValueError('rebuilt file coverage differs')
    for rel,path in originals.items():
        if path.read_bytes()!=copies[rel].read_bytes():raise ValueError('rebuilt file differs: '+str(rel))
    if (stage/'plan.json').read_bytes()!=raw:raise ValueError('stage changed')
    # Remove only the exact verified files and empty directories created here.
    if rebuilt.resolve(strict=True).parent!=work:raise ValueError('rebuild location changed')
    for rel,path in copies.items():
        if path.is_symlink() or path.resolve(strict=True).parent != path.parent.resolve(strict=True):
            raise ValueError('rebuild path changed')
        if path.read_bytes()!=originals[rel].read_bytes():raise ValueError('rebuild changed before cleanup')
        path.unlink()
    for directory in sorted(dirs,key=lambda p:len(p.parts),reverse=True):directory.rmdir()
    return {'state':'stage_verified','plan_sha256':storage.sha(raw),'files':len(plan['paths']),
            'catalogs':len(plan['catalogs']),'changed_pointers':len(plan['changed']),
            'changed_cards':len(plan['changed_ids']),'temporary_rebuild_removed':not rebuilt.exists(),
            'limitations':'Deterministic regeneration only; importer, native reader, external consumers and retirement remain unqualified.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','candidates','audit','stage','rebuilt','output'):p.add_argument('--'+n,type=Path,required=True)
    a=p.parse_args();result=verify(a.vault,a.candidates,a.audit,a.stage,a.rebuilt)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps(result))
