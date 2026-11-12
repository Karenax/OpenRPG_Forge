"""Reconstruct SHA-pinned retired Unity source files into an isolated work folder.

Accepts only transaction recovery rows produced by retire_archived_effect_sources.
Never writes to the Vault and never extracts archive-provided paths directly.
"""
import argparse
import gzip
import hashlib
import json
import os
from pathlib import Path
import re
import tarfile

import vault_storage as storage
from stage_source_support_relinks import norm


def reconstruct(vault, recovery, destination, work_root):
    vault = vault.resolve(strict=True)
    work_root = work_root.resolve(strict=True)
    destination = destination.absolute()
    if destination.parent.resolve(strict=True) != work_root or destination.exists():
        raise ValueError('destination must be a new direct child of work root')
    raw = recovery.read_bytes()
    doc = json.loads(raw)
    rows = doc.get('rows')
    if not isinstance(rows, list) or not rows:
        raise ValueError('recovery rows missing')
    groups = {}
    seen = set()
    for row in rows:
        old, archive = row.get('old'), row.get('archive')
        norm(old); norm(archive)
        if ':' in old or ':' in archive or '\x00' in old or '\x00' in archive:
            raise ValueError('invalid recovery path')
        guid, member = row.get('guid'), row.get('archive_member')
        if (not isinstance(guid,str) or not re.fullmatch('[0-9a-f]{32}',guid)
                or member not in ('asset','asset.meta') or not storage.valid_hex(row.get('sha256'),64)
                or not storage.valid_hex(row.get('archive_sha256'),64)
                or type(row.get('bytes')) is not int or not 0 <= row['bytes'] <= 512*1024*1024):
            raise ValueError('invalid recovery row')
        key = old.casefold()
        if key in seen:raise ValueError('duplicate destination')
        seen.add(key)
        groups.setdefault((archive,row['archive_sha256']),[]).append(row)
    destination.mkdir()
    recovered = []
    for (archive,pin), items in groups.items():
        wanted = {}
        for row in items:
            key=(row['guid'],row['archive_member'])
            if key in wanted:raise ValueError('duplicate archive member request')
            wanted[key]=row
        found={}; pathnames={}
        path = storage.confined(vault, vault/archive)
        with storage.locked_source(path,share_delete=False) as source:
            if hashlib.file_digest(source,'sha256').hexdigest()!=pin:
                raise ValueError('archive SHA differs')
            source.seek(0)
            with gzip.GzipFile(fileobj=source,mode='rb') as unc, tarfile.open(fileobj=unc,mode='r|') as tar:
                for member in tar:
                    parts=member.name.replace('\\','/').split('/')
                    if len(parts)!=2 or parts[0] not in {x[0] for x in wanted}:
                        continue
                    key=(parts[0],parts[1])
                    if parts[1]=='pathname':
                        if parts[0] in pathnames or not member.isfile() or member.size>8192:
                            raise ValueError('invalid Unity pathname')
                        f=tar.extractfile(member)
                        if f is None:raise ValueError('unreadable pathname')
                        lines=f.read().decode('utf8').splitlines()
                        if len(lines)==2 and lines[1]=='00':lines.pop()
                        if len(lines)!=1:raise ValueError('invalid Unity pathname')
                        pathnames[parts[0]]=lines[0]
                    elif key in wanted:
                        if key in found or not member.isfile() or member.size!=wanted[key]['bytes']:
                            raise ValueError('duplicate, invalid or changed archive member')
                        row=wanted[key]; target=destination/row['old']
                        target.parent.mkdir(parents=True,exist_ok=True)
                        h=hashlib.sha256();f=tar.extractfile(member)
                        if f is None:raise ValueError('unreadable archive member')
                        with target.open('xb') as out:
                            while chunk:=f.read(1024*1024):out.write(chunk);h.update(chunk)
                            out.flush();os.fsync(out.fileno())
                        if h.hexdigest()!=row['sha256']:
                            raise ValueError('reconstructed bytes differ')
                        found[key]=target
            source.seek(0)
            if hashlib.file_digest(source,'sha256').hexdigest()!=pin:
                raise ValueError('archive changed during reconstruction')
        if set(found)!=set(wanted):raise ValueError('archive member missing')
        for key,row in wanted.items():
            if pathnames.get(row['guid'],'').replace('\\','/').casefold()!=row['source_asset_path'].replace('\\','/').casefold():
                raise ValueError('archive Unity pathname differs')
            with found[key].open('rb') as stream:
                if hashlib.file_digest(stream,'sha256').hexdigest()!=row['sha256']:
                    raise ValueError('reconstructed output readback differs')
            recovered.append({'old':row['old'],'output':str(found[key]),'sha256':row['sha256'],'bytes':row['bytes']})
    if recovery.read_bytes()!=raw:raise ValueError('recovery manifest changed')
    return {'state':'passed','recovery_sha256':storage.sha(raw),'files':len(recovered),
            'logical_bytes':sum(x['bytes'] for x in recovered),'outputs':recovered}


if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','recovery','destination','work-root','output'):
        parser.add_argument('--'+name,type=Path,required=True)
    a=parser.parse_args()
    if a.output.exists():parser.error('output exists')
    result=reconstruct(a.vault,a.recovery,a.destination,a.work_root)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps({k:v for k,v in result.items() if k!='outputs'}))
