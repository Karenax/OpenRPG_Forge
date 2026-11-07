"""Verified decoded GLB payloads, scoped to their actual consumer lifetime.

Compatible materialize/stats API for new reopening workers. These are derived
scratch copies of a retained GLB, never the asset library or original sources.
"""
import atexit
import hashlib
import os
from pathlib import Path
import re


_stores=[]


class VerifiedPayloadStore:
    def __init__(self,root,work_root=None):
        work=Path(work_root) if work_root is not None else Path(__file__).resolve().parents[2].parent/'.build/asset-conversion'
        self.root=Path(os.path.abspath(root));self.root.relative_to(work)
        for p in [self.root,*self.root.parents]:
            if p.is_symlink() or (p.exists() and getattr(p.lstat(),'st_file_attributes',0)&1024):raise ValueError('Reparse point in decoding cache')
            if p==work:break
        # Reuse this same path after a completed verification; never silently
        # adopt an unknown/active directory or create a second cache on failure.
        self.root.mkdir(exist_ok=False);self.verified={};self.created={};self.closed=False;self.cleanup_notes=[]
        self.stats=dict(logical_payloads=0,logical_bytes=0,new_files=0,new_bytes=0,existing_files_verified=0,existing_file_bytes_verified=0,same_identity_cache_hits=0,verified_reuses=0)
        _stores.append(self)

    def identity(self,p):
        info=p.stat();return (str(p.absolute()),info.st_size,info.st_mtime_ns,info.st_ino)

    def materialize(self,data,expected):
        if self.closed:raise ValueError('Decoding cache is closed')
        if not data or not re.fullmatch('[0-9a-f]{64}',expected) or hashlib.sha256(data).hexdigest()!=expected:raise ValueError('Payload differs from GLB hash')
        p=self.root/(expected+'.bin');self.stats['logical_payloads']+=1;self.stats['logical_bytes']+=len(data)
        if p.exists():
            key=self.identity(p)
            if key[1]!=len(data):raise ValueError('Cached payload changed')
            if key not in self.verified:
                with p.open('rb') as f:actual=hashlib.file_digest(f,'sha256').hexdigest()
                if actual!=expected or self.identity(p)!=key:raise ValueError('Cached payload changed')
                self.verified[key]=expected;self.stats['existing_files_verified']+=1;self.stats['existing_file_bytes_verified']+=len(data)
            else:self.stats['same_identity_cache_hits']+=1
            self.stats['verified_reuses']+=1;return p
        with p.open('xb') as f:f.write(data)
        key=self.identity(p)
        with p.open('rb') as f:actual=hashlib.file_digest(f,'sha256').hexdigest()
        if actual!=expected or self.identity(p)!=key:raise ValueError('Written payload changed')
        self.verified[key]=expected;self.created[p]=(key,expected)
        self.stats['new_files']+=1;self.stats['new_bytes']+=len(data);return p

    def close(self):
        if self.closed:return
        self.closed=True
        for p,(identity,expected) in self.created.items():
            try:
                if p.is_symlink() or getattr(p.lstat(),'st_file_attributes',0)&1024 or self.identity(p)!=identity:raise ValueError('Changed temporary payload retained')
                with p.open('rb') as f:actual=hashlib.file_digest(f,'sha256').hexdigest()
                if actual!=expected:raise ValueError('Changed temporary payload retained')
                p.unlink()
            except FileNotFoundError:pass
            except (OSError,ValueError) as error:self.cleanup_notes.append(dict(path=str(p),reason=str(error)))
        try:self.root.rmdir()
        except OSError:pass # Unknown files are preserved; never recursively purge.
        if self in _stores:_stores.remove(self)

    def __enter__(self):return self
    def __exit__(self,*unused):self.close()


def close_all_stores():
    for store in list(_stores):store.close()


atexit.register(close_all_stores)
