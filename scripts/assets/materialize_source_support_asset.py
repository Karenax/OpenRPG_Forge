"""Reconstruct a source asset and its Unity GUID sidecar from a catalog card.

This exports only the requested source and metadata, not its dependencies or a
complete Unity project. The destination must be a fresh caller-owned workspace.
"""
import argparse
import contextlib
import hashlib
import json
import os
import stat
from pathlib import Path

import vault_storage as storage
from stage_source_support_relinks import norm


def select_card(document, ident):
    found=[]
    for card in document['assets']:
        if card.get('id')==ident: found.append(card)
        for alias in card.get('consolidated_sources',[]):
            if alias.get('retired_id')==ident:
                detail=alias.get('original_detail')
                if not isinstance(detail,dict) or detail.get('id')!=ident: raise ValueError('invalid source alias')
                found.append(detail)
    if len(found)!=1: raise ValueError('missing or ambiguous source identity')
    return found[0]


def materialize(vault, card, destination):
    vault=vault.resolve(strict=True); destination=destination.resolve(strict=True)
    logical=card.get('source_asset_path')
    if not isinstance(logical,str) or '\\' in logical or ':' in logical:
        raise ValueError('invalid Unity asset path')
    parts=Path(logical).parts
    if not parts or parts[0].casefold()!='assets' or any(x in ('.','..') for x in parts):
        raise ValueError('asset path escapes project Assets')
    rows=[]
    for path_key, hash_key in (('source_file','source_sha256'),('source_meta_file','source_meta_sha256')):
        rel=card.get(path_key); sha=card.get(hash_key)
        norm(rel)
        if not storage.valid_hex(sha,64): raise ValueError('missing source SHA')
        rows.append((storage.confined(vault,vault/rel),sha))
    folder=destination
    for name in parts[:-1]:
        folder=folder/name
        if folder.exists():
            if folder.is_symlink() or not folder.is_dir() or getattr(folder.stat(), 'st_file_attributes', 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT:
                raise ValueError('destination contains a link or non-directory')
        else: folder.mkdir()
    asset=folder/parts[-1]; targets=[asset,asset.with_name(asset.name+'.meta')]
    if any(p.exists() for p in targets): raise ValueError('destination already exists')
    created=[]
    try:
        with contextlib.ExitStack() as stack:
            sources=[stack.enter_context(storage.locked_source(p,share_delete=False)) for p,_ in rows]
            for stream,(_,sha),target in zip(sources,rows,targets):
                if hashlib.file_digest(stream,'sha256').hexdigest()!=sha: raise ValueError('source SHA differs')
                stream.seek(0)
                with target.open('xb') as out:
                    created.append(target)
                    while True:
                        data=stream.read(1024*1024)
                        if not data: break
                        out.write(data)
                    out.flush();os.fsync(out.fileno())
                with target.open('rb') as check:
                    if hashlib.file_digest(check,'sha256').hexdigest()!=sha: raise ValueError('destination SHA differs')
    except Exception:
        for path in reversed(created): path.unlink(missing_ok=True)
        raise
    return {'state':'materialized','asset':str(targets[0]),'meta':str(targets[1]),
            'limitations':'Single source and sidecar only; Unity dependencies and runtime are not included.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault',type=Path,required=True);p.add_argument('--id',required=True)
    p.add_argument('--destination',type=Path,required=True);a=p.parse_args()
    vault=a.vault.resolve(strict=True); dest=a.destination.resolve(strict=True)
    work=Path('E:/OpenRPG_Forge/.build/asset-conversion').resolve(strict=True)
    if not dest.is_relative_to(work) or dest==work: p.error('destination must be inside asset conversion work root')
    pointer=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json'); before=pointer.read_bytes();entry=json.loads(before)
    raw=storage.confined(vault,vault/entry['path']).read_bytes()
    if storage.sha(raw)!=entry['sha256']:raise ValueError('source catalog SHA differs')
    card=select_card(json.loads(raw),a.id)
    if pointer.read_bytes()!=before:raise ValueError('catalog changed')
    print(json.dumps(materialize(vault,card,dest)))
