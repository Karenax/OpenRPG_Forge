"""Reconstruct a pinned Unity source manifest from its original archive on E:."""
import argparse
import hashlib
import io
import json
import os
from pathlib import Path, PurePosixPath
import re
import tarfile
import tempfile
import vault_storage as storage


class HashReader(io.RawIOBase):
    def __init__(self, stream):
        self.stream=stream;self.hash=hashlib.sha256()
    def readable(self):return True
    def readinto(self, buffer):
        n=self.stream.readinto(buffer)
        if n:self.hash.update(memoryview(buffer)[:n])
        return n


def safe_relative(value):
    if '\\' in value or ':' in value:raise ValueError('invalid source path')
    path=PurePosixPath(value)
    if path.is_absolute() or not path.parts or any(x in ('.','..') or x.endswith(('.', ' ')) for x in path.parts):
        raise ValueError('invalid source path')
    return path


def materialize(archive,manifest_path,destination,work_root):
    work_root=work_root.resolve(strict=True)
    if destination.parent.resolve(strict=True)!=work_root or destination.exists():raise ValueError('destination must be new work-root child')
    raw=manifest_path.read_bytes();manifest=json.loads(raw)
    expected_archive=manifest['archive_sha256']
    if not re.fullmatch('[0-9a-f]{64}',expected_archive):raise ValueError('invalid archive hash')
    records={};paths={};guids=manifest['guid_paths'];names=set()
    for guid,path in guids.items():
        if not re.fullmatch('[0-9a-f]{32}',guid):raise ValueError('invalid GUID')
        rel=safe_relative(path);key=rel.as_posix().casefold()
        if key in names:raise ValueError('ambiguous source path')
        names.add(key);paths[guid]=rel
    for row in manifest['files']:
        if row['kind'] not in ('asset','asset.meta'):continue
        guid=row['guid'];kind=row['kind']
        if guid not in paths or safe_relative(row['source_path'])!=paths[guid]:raise ValueError('manifest path mismatch')
        if not re.fullmatch('[0-9a-f]{64}',row['sha256']) or row['bytes']<0:raise ValueError('invalid file pin')
        key=(guid,kind)
        if key in records:raise ValueError('duplicate manifest entry')
        records[key]=row
    if not records or any((g,'asset.meta') not in records for g in paths):raise ValueError('missing metadata records')
    with tempfile.TemporaryDirectory(prefix='.unitypackage-stage-',dir=work_root) as temp:
        staged=Path(temp)/'source';staged.mkdir();seen=set();pathnames={}
        with storage.locked_source(archive,share_delete=False) as stream:
            before=os.fstat(stream.fileno());hashed=HashReader(stream);buffer=io.BufferedReader(hashed,1024*1024)
            with tarfile.open(fileobj=buffer,mode='r|gz') as tar:
                for member in tar:
                    parts=member.name.replace('\\','/').split('/')
                    if len(parts)!=2 or parts[0] not in paths or parts[1] not in ('asset','asset.meta','pathname'):continue
                    guid,kind=parts
                    if not member.isfile():raise ValueError('unsupported archive member')
                    if kind=='pathname':
                        if guid in pathnames or member.size>16384:raise ValueError('invalid pathname member')
                        pathnames[guid]=tar.extractfile(member).read();continue
                    key=(guid,kind)
                    if key not in records:raise ValueError('unlisted asset member')
                    if key in seen:raise ValueError('duplicate archive member')
                    row=records[key]
                    if member.size!=row['bytes']:raise ValueError('archive member size differs')
                    target=staged.joinpath(*paths[guid].parts)
                    if kind=='asset.meta':target=target.with_name(target.name+'.meta')
                    target.parent.mkdir(parents=True,exist_ok=True);digest=hashlib.sha256();size=0
                    with tar.extractfile(member) as src,target.open('xb') as dst:
                        while block:=src.read(1024*1024):digest.update(block);size+=len(block);dst.write(block)
                    if size!=row['bytes'] or digest.hexdigest()!=row['sha256']:raise ValueError('archive member hash differs')
                    seen.add(key)
            while buffer.read(4*1024*1024):pass
            after=os.fstat(stream.fileno())
            if (before.st_size,before.st_mtime_ns)!=(after.st_size,after.st_mtime_ns):raise ValueError('archive changed')
            if hashed.hash.hexdigest()!=expected_archive:raise ValueError('archive SHA differs')
        if seen!=set(records) or set(pathnames)!=set(paths):raise ValueError('archive is missing manifest members')
        for guid,content in pathnames.items():
            expected=paths[guid].as_posix().encode('utf8')
            if content not in (expected,expected+b'\n',expected+b'\x00',expected+b'\n00'):
                raise ValueError('archive pathname differs')
            if (guid,'asset') not in records:staged.joinpath(*paths[guid].parts).mkdir(parents=True,exist_ok=True)
        if manifest_path.read_bytes()!=raw:raise ValueError('manifest changed')
        os.replace(staged,destination)
    return {'state':'source_materialized','archive_sha256':expected_archive,
            'manifest_sha256':storage.sha(raw),'source_root':str(destination),
            'files':len(records),'guids':len(paths),'logical_bytes':sum(x['bytes'] for x in records.values()),
            'limitations':'Archive and file hashes checked; converter and runtime not yet qualified.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('archive','manifest','destination','output'):p.add_argument('--'+name,type=Path,required=True)
    a=p.parse_args();work=Path(r'E:\OpenRPG_Forge\.build\asset-conversion').resolve(strict=True)
    if a.output.parent.resolve(strict=True)!=work or a.output.exists():p.error('output must be new work-root child')
    result=materialize(a.archive,a.manifest,a.destination,work)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps(result))
