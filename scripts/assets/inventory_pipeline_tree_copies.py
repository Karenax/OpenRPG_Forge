"""Find exact same-relative-path files across two extracted Unity pipelines.

Inventory only. It never follows junctions, alters the Vault or approves
removal. Equal content does not prove that an extracted Unity tree can lose a
file: active references and GUID consumers must be examined separately.
"""
import argparse
import collections
import json
import os
from pathlib import Path
import stat
import vault_storage as storage
from stage_source_support_relinks import digest_file


def enumerate_tree(root):
    files={};skipped=[];stack=[root]
    while stack:
        directory=stack.pop()
        with os.scandir(directory) as entries:
            for entry in entries:
                st=entry.stat(follow_symlinks=False)
                if (st.st_file_attributes & stat.FILE_ATTRIBUTE_REPARSE_POINT) if hasattr(st,'st_file_attributes') else entry.is_symlink():
                    skipped.append(str(Path(entry.path).relative_to(root)));continue
                if entry.is_dir(follow_symlinks=False):stack.append(Path(entry.path))
                elif entry.is_file(follow_symlinks=False):
                    path=Path(entry.path);relative=path.relative_to(root).as_posix();key=relative.casefold()
                    if key in files:raise ValueError('ambiguous case-insensitive path')
                    files[key]=(path,st.st_size)
    return files,skipped


def inventory(vault,roots):
    vault=vault.resolve(strict=True)
    ptrs=[storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json'),
          storage.confined(vault,vault/'metadata/asset-import-registry.v1.json')]
    pins=[p.read_bytes() for p in ptrs]
    pairs=[];counts=collections.Counter();skipped=[]
    for group,hrel,urel in roots:
        hdr=storage.confined(vault,vault/hrel);urp=storage.confined(vault,vault/urel)
        if not hdr.is_dir() or not urp.is_dir():raise ValueError('pipeline root missing')
        left,skip1=enumerate_tree(hdr);right,skip2=enumerate_tree(urp)
        skipped.extend({'group':group,'side':side,'path':x} for side,entries in (('hdrp',skip1),('urp',skip2)) for x in entries)
        counts['hdrp_files']+=len(left);counts['urp_files']+=len(right)
        counts['unmatched_hdrp']+=len(left.keys()-right.keys());counts['unmatched_urp']+=len(right.keys()-left.keys())
        for key in sorted(left.keys()&right.keys()):
            a,asize=left[key];b,bsize=right[key]
            if asize!=bsize:counts['different_size']+=1;continue
            ah,bh=digest_file(a),digest_file(b)
            if ah!=bh:counts['different_sha']+=1;continue
            counts['identical']+=1;counts['identical_bytes']+=asize
            pairs.append({'group':group,'old':a.relative_to(vault).as_posix(),
                          'retained':b.relative_to(vault).as_posix(),'sha256':ah,'bytes':asize})
    if any(p.read_bytes()!=b for p,b in zip(ptrs,pins)):raise ValueError('registry changed')
    return {'state':'inventory_only','source_registry_sha256':storage.sha(pins[0]),
            'model_registry_sha256':storage.sha(pins[1]),'counts':dict(counts),'skipped_reparse':skipped,
            'pairs':pairs,'limitations':'Exact bytes and relative paths only; references, Unity GUIDs and running jobs not qualified.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault',type=Path,required=True);p.add_argument('--roots',type=Path,required=True)
    p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    roots=json.loads(a.roots.read_bytes());result=inventory(a.vault,roots)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'counts':result['counts'],'skipped_reparse':len(result['skipped_reparse'])}))
