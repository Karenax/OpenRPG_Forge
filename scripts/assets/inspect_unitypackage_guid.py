"""Inspect one GUID in an unchanged Unity package, without extracting files."""
import argparse
import hashlib
import json
import re
import tarfile
from pathlib import Path


class HashReader:
    def __init__(self, stream):
        self.stream=stream
        self.digest=hashlib.sha256()
        self.bytes=0
    def read(self, count=-1):
        b=self.stream.read(count)
        self.digest.update(b); self.bytes+=len(b)
        return b


def main():
    p=argparse.ArgumentParser()
    p.add_argument('--archive',type=Path,required=True)
    p.add_argument('--sha256',required=True)
    p.add_argument('--guid',required=True)
    p.add_argument('--output',type=Path,required=True)
    a=p.parse_args()
    if not re.fullmatch('[0-9a-f]{32}',a.guid) or not re.fullmatch('[0-9a-f]{64}',a.sha256):
        raise ValueError('invalid GUID or digest')
    before=a.archive.stat()
    members=[]
    with a.archive.open('rb') as stream:
        hashed=HashReader(stream)
        with tarfile.open(fileobj=hashed,mode='r|gz') as archive:
            for member in archive:
                parts=member.name.replace('\\','/').split('/')
                if not parts or parts[0].casefold()!=a.guid or not member.isfile():continue
                if member.size>256*1024*1024:raise ValueError('selected member too large')
                source=archive.extractfile(member)
                digest=hashlib.sha256(); small=bytearray()
                for chunk in iter(lambda:source.read(1024*1024),b''):
                    digest.update(chunk)
                    if len(small)<65536:small.extend(chunk[:65536-len(small)])
                item={'name':member.name,'bytes':member.size,'sha256':digest.hexdigest()}
                if parts[-1] in ('pathname','asset.meta') and member.size<=65536:
                    item['text']=small.decode('utf-8',errors='replace')
                members.append(item)
        while hashed.read(4*1024*1024):pass
    after=a.archive.stat()
    if (before.st_size,before.st_mtime_ns)!=(after.st_size,after.st_mtime_ns):raise ValueError('archive changed')
    actual=hashed.digest.hexdigest()
    if actual!=a.sha256:raise ValueError('archive SHA mismatch: '+actual)
    result={'archive':str(a.archive),'archive_sha256':actual,'archive_bytes':hashed.bytes,'guid':a.guid,
            'members':members,'status':'found' if members else 'not_found_in_verified_archive'}
    with a.output.open('x',encoding='utf-8') as f:json.dump(result,f,ensure_ascii=False,indent=2)
    print(json.dumps({'status':result['status'],'members':len(members),'archive_bytes':hashed.bytes}),flush=True)


if __name__=='__main__':main()
