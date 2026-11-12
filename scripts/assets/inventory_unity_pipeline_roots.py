"""Find extracted HDRP and URP source roots without following junctions.

Directory inventory only: a paired root is not an equivalence or cleanup
decision. Rejected and partial extraction directories are not traversed.
"""
import argparse
import collections
import json
import os
from pathlib import Path
import re
import stat
import vault_storage as storage

PIPELINE=re.compile(r'^(hdrp|urp)(?:\s*\(default\))?$',re.I)


def discover(vault,relative,max_depth=18):
    vault=vault.resolve(strict=True);root=storage.confined(vault,vault/relative)
    ptrs=[storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json'),
          storage.confined(vault,vault/'metadata/asset-import-registry.v1.json')]
    before=[p.read_bytes() for p in ptrs];groups=collections.defaultdict(lambda:collections.defaultdict(set))
    stack=[(root,0)];skips=[];visited=0
    while stack:
        directory,depth=stack.pop();visited+=1
        with os.scandir(directory) as entries:
            for entry in entries:
                st=entry.stat(follow_symlinks=False)
                reparse=(bool(st.st_file_attributes & stat.FILE_ATTRIBUTE_REPARSE_POINT)
                         if hasattr(st,'st_file_attributes') else entry.is_symlink())
                if reparse:skips.append(str(Path(entry.path).relative_to(vault)));continue
                if not entry.is_dir(follow_symlinks=False):continue
                name=entry.name.casefold()
                if any(x in name for x in ('.failed-partial-','.partial-','.rejected-')):continue
                path=Path(entry.path);parts=path.relative_to(vault).parts
                match=PIPELINE.fullmatch(entry.name)
                # Some imports insert `base` or `variant` between source and
                # Assets; treating only source/Assets omitted those trees.
                asset_indices=[i for i in range(len(parts)-1) if parts[i].casefold()=='assets'
                               and any(parts[j].casefold()=='source' for j in range(max(0,i-3),i))]
                if match and asset_indices:
                    idx=asset_indices[-1]
                    asset_prefix='/'.join(parts[idx+1:-1]).casefold()
                    if asset_prefix:
                        side=match.group(1).lower()
                        groups[asset_prefix][side].add(path.relative_to(vault).as_posix())
                    continue
                if depth+1>max_depth:skips.append(path.relative_to(vault).as_posix()+' [depth]')
                else:stack.append((path,depth+1))
    if any(p.read_bytes()!=b for p,b in zip(ptrs,before)):raise ValueError('registry changed')
    result=[{'assets_prefix':k,'hdrp':sorted(v['hdrp']),'urp':sorted(v['urp'])}
            for k,v in sorted(groups.items())]
    return {'state':'inventory_only','source_registry_sha256':storage.sha(before[0]),
            'model_registry_sha256':storage.sha(before[1]),'directories_visited':visited,
            'groups':result,'skipped':skips,'limitations':'Directory names only; payloads, references and asset equivalence are unverified.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault',type=Path,required=True);p.add_argument('--root',required=True)
    p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    result=discover(a.vault,a.root)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'directories_visited':result['directories_visited'],'groups':len(result['groups']),
        'paired':sum(bool(x['hdrp'] and x['urp']) for x in result['groups']),'skipped':len(result['skipped'])}))
