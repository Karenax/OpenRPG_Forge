"""Lossless Vault storage, shared with forge_delivery::vault_storage.

The writer uses immutable packs and keeps each replaced original as a transaction
backup until both native reconstruction and backup SHA-256 have been verified.
"""
from __future__ import annotations
import argparse
import contextlib
import ctypes
import datetime
import hashlib
import json
import mmap
import os
from pathlib import Path
import shutil
import sqlite3
import stat
import struct
import subprocess
import time
import uuid
import zlib

MAGIC = b'ORPVLT1\n'
MAX_DESCRIPTOR = 2 * 1024**2
MAX_BLOCK = 16 * 1024**2
BLOCK = 4 * 1024**2
MAX_SEGMENTS = 16384
# Storage-only streaming limit: GLB's u32 envelope. Product admission budgets stay unchanged.
MAX_MODEL = 2**32-1

def utc():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()

def sha(data):
    return hashlib.sha256(data).hexdigest()

def valid_hex(value, length):
    return isinstance(value,str) and len(value)==length and all(c in '0123456789abcdef' for c in value)

def windows_readonly(info):
    """Do not replace a file whose Windows read-only attribute is set."""
    return bool(getattr(info, 'st_file_attributes', 0) & getattr(stat, 'FILE_ATTRIBUTE_READONLY', 1))

def batch_saves_allocation(before, after):
    """A batch must have a positive conservative net allocation saving."""
    return before > after

def verify_deployed_readers(receipt, root, reader):
    """Require deployed readers and this writer to match their qualification."""
    if receipt.get('state')!='ready' or receipt.get('vault')!=str(root):
        raise ValueError('Compatible deployed readers required before replacement')
    studio_name=receipt.get('studio')
    studio_sha=receipt.get('studio_sha256')
    if not isinstance(studio_name,str) or not Path(studio_name).is_absolute() or not valid_hex(studio_sha,64):
        raise ValueError('Qualified Studio path or SHA missing')
    studio=Path(studio_name)
    if not studio.is_file():raise ValueError('Qualified Studio missing')
    with studio.open('rb') as stream:
        if hashlib.file_digest(stream,'sha256').hexdigest()!=studio_sha:
            raise ValueError('Studio differs from qualified reader receipt')
    if not Path(reader).is_file():raise ValueError('Native verifier missing')
    with Path(reader).open('rb') as stream:
        if hashlib.file_digest(stream,'sha256').hexdigest()!=receipt.get('reader_cli_sha256'):
            raise ValueError('Native verifier differs from qualified reader receipt')
    importer_name=receipt.get('python_import_reader')
    importer_sha=receipt.get('python_import_reader_sha256')
    if not isinstance(importer_name,str) or not Path(importer_name).is_absolute() or not valid_hex(importer_sha,64):
        raise ValueError('Qualified Python reader path or SHA missing')
    importer=Path(importer_name)
    if not importer.is_file():raise ValueError('Qualified Python reader missing')
    with importer.open('rb') as stream:
        if hashlib.file_digest(stream,'sha256').hexdigest()!=importer_sha:
            raise ValueError('Python reader differs from qualified reader receipt')
    with Path(__file__).open('rb') as stream:
        if hashlib.file_digest(stream,'sha256').hexdigest()!=receipt.get('storage_writer_sha256'):
            raise ValueError('Storage writer differs from qualified reader receipt')

def decode_json(raw):
    def object_pairs(pairs):
        result={}
        for key,value in pairs:
            if key in result:raise ValueError('Duplicate JSON field')
            result[key]=value
        return result
    def invalid(value):raise ValueError('Nonfinite JSON value: '+value)
    return json.loads(raw,object_pairs_hook=object_pairs,parse_constant=invalid)

def confined(root, path):
    root, path = Path(root), Path(path)
    if not root.is_absolute() or not path.is_absolute() or '..' in path.parts or not path.is_relative_to(root):
        raise ValueError('Path outside configured Vault')
    for parent in [*reversed(path.parents), path]:
        info = os.lstat(parent)
        if stat.S_ISLNK(info.st_mode) or getattr(info,'st_file_attributes',0)&0x400:
            raise ValueError('Link/reparse point refused: '+str(parent))
    return path

def read_descriptor(root, path, limit=MAX_MODEL):
    path = confined(root,path)
    with path.open('rb') as stream:
        info=os.fstat(stream.fileno())
        if not stat.S_ISREG(info.st_mode):
            raise ValueError('Not a regular payload')
        if stream.read(8)!=MAGIC:
            if info.st_size>limit: raise ValueError('Logical byte budget')
            return None
        if info.st_size>MAX_DESCRIPTOR: raise ValueError('Descriptor byte budget')
        raw=stream.read(MAX_DESCRIPTOR+1)
    if len(raw)>MAX_DESCRIPTOR-8: raise ValueError('Descriptor grew')
    value=decode_json(raw)
    validate_descriptor(value,limit)
    marker=confined(root,Path(root)/'.forge-storage/root.v1.json')
    with marker.open('rb') as stream: raw=stream.read(4097)
    if len(raw)>4096: raise ValueError('Root marker byte budget')
    marker=decode_json(raw)
    if not isinstance(marker,dict) or type(marker.get('schema')) is not int or marker!={'schema':1,'vault_id':value['vault_id']}:
        raise ValueError('Storage root identity mismatch')
    return value

def validate_descriptor(value,limit):
    if not isinstance(value,dict) or set(value)!={'schema','vault_id','bytes','sha256','segments'} or type(value['schema']) is not int or value['schema']!=1:
        raise ValueError('Unknown descriptor')
    if not valid_hex(value['vault_id'],32) or not valid_hex(value['sha256'],64): raise ValueError('Invalid digest/identity')
    if type(value['bytes']) is not int or not 0<=value['bytes']<=limit: raise ValueError('Logical byte budget')
    if not isinstance(value['segments'],list) or not 0<len(value['segments'])<=MAX_SEGMENTS: raise ValueError('Segment count')
    total=0
    for item in value['segments']:
        if set(item)!={'pack','offset','stored_bytes','bytes','sha256','codec'}: raise ValueError('Unknown segment')
        if not valid_hex(item['pack'],32) or not valid_hex(item['sha256'],64): raise ValueError('Invalid pack/digest')
        if any(type(item[k]) is not int for k in ('offset','stored_bytes','bytes')): raise ValueError('Invalid integer')
        if not 0<item['bytes']<=MAX_BLOCK or not 0<item['stored_bytes']<=MAX_BLOCK or not 0<=item['offset']<=2**64-1-item['stored_bytes']: raise ValueError('Invalid segment range')
        if item['codec'] not in ('raw','zlib') or (item['codec']=='raw' and item['bytes']!=item['stored_bytes']): raise ValueError('Invalid codec')
        total+=item['bytes']
    if total!=value['bytes']: raise ValueError('Segment total mismatch')

def chunks(root,path,limit=MAX_MODEL):
    value=read_descriptor(root,path,limit)
    if value is None:
        with Path(path).open('rb') as stream:
            before=os.fstat(stream.fileno()); total=0
            while block:=stream.read(BLOCK):
                total+=len(block)
                if total>limit: raise ValueError('Payload grew')
                yield block
            after=os.fstat(stream.fileno())
            if (before.st_size,before.st_mtime_ns)!=(after.st_size,after.st_mtime_ns) or total!=before.st_size: raise ValueError('Payload changed')
        return
    whole=hashlib.sha256()
    with contextlib.ExitStack() as stack:
        handles={}
        for item in value['segments']:
            if item['pack'] not in handles:
                if len(handles)>=16:
                    # Bound open handles without dropping byte-integrity checks.
                    for handle in handles.values():handle.close()
                    handles.clear()
                pack=confined(root,Path(root)/'.forge-storage/packs'/(item['pack']+'.pack'))
                handles[item['pack']]=stack.enter_context(pack.open('rb'))
            stream=handles[item['pack']]
            if item['offset']+item['stored_bytes']>os.fstat(stream.fileno()).st_size: raise ValueError('Pack range')
            stream.seek(item['offset']); stored=stream.read(item['stored_bytes'])
            if len(stored)!=item['stored_bytes']: raise ValueError('Truncated segment')
            if item['codec']=='zlib':
                decoder=zlib.decompressobj()
                block=decoder.decompress(stored,item['bytes']+1)
                if not decoder.eof or decoder.unused_data or decoder.unconsumed_tail: raise ValueError('Invalid/trailing zlib')
            else: block=stored
            if len(block)!=item['bytes'] or sha(block)!=item['sha256']: raise ValueError('Segment SHA-256 mismatch')
            whole.update(block)
            yield block
    if whole.hexdigest()!=value['sha256']: raise ValueError('Logical SHA-256 mismatch')

def digest(root,path,limit=MAX_MODEL):
    result=hashlib.sha256()
    for block in chunks(root,path,limit): result.update(block)
    return result.hexdigest()

def read_bytes(root,path,limit=MAX_MODEL):
    return b''.join(chunks(root,path,limit))

def spans(data):
    """Keep identical GLB views aligned; fallback preserves any other layout exactly."""
    total=len(data)
    result=[]
    try:
        if data[:4]!=b'glTF' or struct.unpack_from('<II',data,4)!=(2,total): raise ValueError('layout')
        length,kind=struct.unpack_from('<II',data,12)
        if kind!=0x4e4f534a or not 0<length<=16*1024**2: raise ValueError('JSON layout')
        document=json.loads(data[20:20+length]); header=20+length
        binary_length,binary_type=struct.unpack_from('<II',data,header)
        start=header+8
        if binary_type!=0x004e4942 or start+binary_length!=total: raise ValueError('BIN layout')
        intervals=sorted((start+item.get('byteOffset',0),item['byteLength']) for item in document.get('bufferViews',[]) if item.get('buffer',0)==0 and item.get('byteLength',0)>0)
        cursor=0
        for offset,size in intervals:
            if offset<cursor or offset+size>total: raise ValueError('Overlap/range')
            if offset>cursor: result.append((cursor,offset-cursor))
            result.append((offset,size));cursor=offset+size
        if cursor<total: result.append((cursor,total-cursor))
    except (ValueError,KeyError,TypeError,struct.error):
        result=[(0,total)]
    split=[(offset+i,min(BLOCK,size-i)) for offset,size in result for i in range(0,size,BLOCK)]
    if len(split)>MAX_SEGMENTS:
        split=[(i,min(BLOCK,total-i)) for i in range(0,total,BLOCK)]
    return split

def atomic_json(path,value):
    temp=path.with_name(path.name+'.tmp-'+uuid.uuid4().hex)
    with temp.open('xb') as stream:
        stream.write(json.dumps(value,ensure_ascii=True,sort_keys=True).encode());stream.flush();os.fsync(stream.fileno())
    # Windows readers/antivirus may briefly deny replacement of the status file.
    # Retry the same durable temporary file; never relax a model transaction.
    for attempt in range(9):
        try:
            os.replace(temp,path)
            break
        except PermissionError:
            if attempt==8:raise
            time.sleep(min(0.05*(2**attempt),1.0))

def ingest_database(root):
    root=Path(root);home=confined(root,root/'.forge-storage')
    path=home/'ingest.sqlite'
    if path.exists():confined(root,path)
    db=sqlite3.connect(path,timeout=10)
    db.execute('PRAGMA synchronous=FULL')
    db.execute('CREATE TABLE IF NOT EXISTS pending(path TEXT PRIMARY KEY,sha256 TEXT,bytes INTEGER,mtime_ns INTEGER)')
    db.commit()
    return db

def eligible_model_storage_path(root, path):
    """Restrict shared storage to converted models, including the Witch delivery."""
    try:
        parts=Path(path).relative_to(Path(root)).parts
    except ValueError:
        return False
    return (Path(path).suffix.lower()=='.glb' and bool(parts)
            and (parts[0] in ('normalized','candidates','animation')
                 or (len(parts)>=3 and parts[:2]==('witch-house-urp-20261108-r2','models'))))


def queue_models(root,models):
    """Minimal pending work, not conversion history. Caller supplies published pins."""
    root=Path(root);db=ingest_database(root);queued=0
    try:
        for model,expected in models:
            path=confined(root,Path(model))
            if not eligible_model_storage_path(root,path):raise ValueError('Published model outside storage scope')
            if not valid_hex(expected,64):raise ValueError('Invalid published model pin')
            info=os.stat(path)
            if not 1024**2<=info.st_size<=MAX_MODEL:continue
            with path.open('rb') as stream:encoded=stream.read(8)==MAGIC
            if encoded:continue
            db.execute('INSERT INTO pending VALUES (?,?,?,?) ON CONFLICT(path) DO UPDATE SET sha256=excluded.sha256,bytes=excluded.bytes,mtime_ns=excluded.mtime_ns',(str(path),expected,info.st_size,info.st_mtime_ns));queued+=1
        db.commit()
    finally:db.close()
    return queued

def replace_with_backup(path,replacement,backup):
    if os.name=='nt':
        kernel=ctypes.WinDLL('kernel32',use_last_error=True)
        call=kernel.ReplaceFileW
        call.argtypes=[ctypes.c_wchar_p]*3+[ctypes.c_uint32,ctypes.c_void_p,ctypes.c_void_p]
        call.restype=ctypes.c_int
        def raw(p): return '\\\\?\\'+str(p) if not str(p).startswith('\\\\?\\') else str(p)
        if not call(raw(path),raw(replacement),raw(backup),0,None,None):
            raise ctypes.WinError(ctypes.get_last_error())
    else:
        # Tests/portable tooling: keep a real transaction copy, never a mutable hardlink.
        shutil.copy2(path,backup)
        with backup.open('rb') as stream: os.fsync(stream.fileno())
        os.replace(replacement,path)

def locked_source(path,share_delete=True):
    """Deny concurrent byte writes on Windows while allowing atomic replacement.

    ReplaceFileW opens the replaced file for READ/DELETE, so it remains compatible
    with this READ|DELETE sharing mode. Existing writers cause acquisition to fail.
    """
    if os.name!='nt':return path.open('rb')
    import msvcrt
    kernel=ctypes.WinDLL('kernel32',use_last_error=True)
    create=kernel.CreateFileW
    create.argtypes=[ctypes.c_wchar_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_void_p,ctypes.c_uint32,ctypes.c_uint32,ctypes.c_void_p]
    create.restype=ctypes.c_void_p
    native_path=str(path) if str(path).startswith('\\\\?\\') else '\\\\?\\'+str(path)
    handle=create(native_path,0x80000000,0x5 if share_delete else 0x1,None,3,0x08000000|0x00200000,None)
    if handle==ctypes.c_void_p(-1).value:raise ctypes.WinError(ctypes.get_last_error())
    try:
        fd=msvcrt.open_osfhandle(handle,os.O_RDONLY|os.O_BINARY)
        return os.fdopen(fd,'rb')
    except BaseException:
        kernel.CloseHandle.argtypes=[ctypes.c_void_p];kernel.CloseHandle(handle);raise

class Packer:
    def __init__(self,root,pack_limit=256*1024**2):
        self.root=Path(root);confined(self.root,self.root)
        self.home=self.root/'.forge-storage';self.home.mkdir(exist_ok=True);confined(self.root,self.home)
        for part in ('packs','transactions'):
            (self.home/part).mkdir(exist_ok=True);confined(self.root,self.home/part)
        self.lock=self.home/'WRITER.lock';self.owner=uuid.uuid4().hex
        with self.lock.open('x',encoding='utf-8') as stream:
            json.dump(dict(owner=self.owner,pid=os.getpid(),utc=utc()),stream);stream.flush();os.fsync(stream.fileno())
        try:
            marker=self.home/'root.v1.json'
            if not marker.exists(): atomic_json(marker,dict(schema=1,vault_id=uuid.uuid4().hex))
            confined(self.root,marker);metadata=decode_json(marker.read_bytes())
            if not isinstance(metadata,dict) or set(metadata)!={'schema','vault_id'} or type(metadata['schema']) is not int or metadata['schema']!=1 or not valid_hex(metadata['vault_id'],32): raise ValueError('Unknown root marker')
            self.vault_id=metadata['vault_id']
            index=self.home/'index.sqlite'
            if index.exists(): confined(self.root,index)
            self.db=sqlite3.connect(index)
            self.db.execute('PRAGMA cache_size=-65536')
            self.db.execute('PRAGMA synchronous=FULL')
            self.db.execute('CREATE TABLE IF NOT EXISTS blobs(sha256 TEXT PRIMARY KEY,pack TEXT,offset INTEGER,stored_bytes INTEGER,bytes INTEGER,codec TEXT)')
            self.db.execute('CREATE TABLE IF NOT EXISTS models(path TEXT,sha256 TEXT,bytes INTEGER,mtime_ns INTEGER,descriptor BLOB,descriptor_sha256 TEXT,PRIMARY KEY(path,sha256))')
            self.db.commit();self.pack=None;self.pack_id=None;self.pack_size=0;self.pack_limit=pack_limit;self.new_pack_bytes=0;self.created_packs=[];self.sources={}
        except BaseException:
            self.lock.unlink();raise

    def close_pack(self):
        if self.pack is None:return
        self.pack.flush();os.fsync(self.pack.fileno());self.pack.close()
        pending=self.home/'packs'/(self.pack_id+'.pending')
        final=pending.with_suffix('.pack')
        if final.exists():raise ValueError('Unexpected immutable pack collision')
        os.rename(pending,final);self.new_pack_bytes+=self.pack_size;self.created_packs.append((self.pack_id,self.pack_size))
        self.db.commit();self.pack=None;self.pack_id=None;self.pack_size=0

    def discard_unpublished_batch(self,checkpoint):
        """Only packs created by this exclusive writer since its last publication."""
        self.close_pack()
        owned=self.created_packs[checkpoint:]
        for pack_id,_ in owned:self.db.execute('DELETE FROM blobs WHERE pack=?',(pack_id,))
        self.db.commit()
        for pack_id,size in owned:
            path=confined(self.root,self.home/'packs'/(pack_id+'.pack'))
            path.unlink();self.new_pack_bytes-=size
        self.created_packs=self.created_packs[:checkpoint]

    def blob(self,data):
        key=sha(data)
        row=self.db.execute('SELECT pack,offset,stored_bytes,bytes,codec FROM blobs WHERE sha256=?',(key,)).fetchone()
        if row:
            if row[3]!=len(data):raise ValueError('Digest length collision')
            return dict(zip(('pack','offset','stored_bytes','bytes','codec'),row),sha256=key)
        compressed=zlib.compress(data,6)
        codec,stored=('zlib',compressed) if len(compressed)+32<len(data) else ('raw',data)
        if self.pack is not None and self.pack_size+len(stored)>self.pack_limit:self.close_pack()
        if self.pack is None:
            self.pack_id=uuid.uuid4().hex;self.pack_size=0
            self.pack=(self.home/'packs'/(self.pack_id+'.pending')).open('xb')
        item=dict(pack=self.pack_id,offset=self.pack_size,stored_bytes=len(stored),bytes=len(data),sha256=key,codec=codec)
        self.pack.write(stored);self.pack_size+=len(stored)
        self.db.execute('INSERT INTO blobs VALUES (?,?,?,?,?,?)',(key,item['pack'],item['offset'],item['stored_bytes'],item['bytes'],codec))
        return item

    def prepare(self,path,expected_sha=None):
        path=confined(self.root,path)
        stream=locked_source(path)
        try:
            before=os.fstat(stream.fileno())
            if not stat.S_ISREG(before.st_mode) or not 0<before.st_size<=MAX_MODEL:raise ValueError('Model byte budget')
            if stream.read(8)==MAGIC:
                stream.close();return None
            with mmap.mmap(stream.fileno(),0,access=mmap.ACCESS_READ) as data:
                original_sha=sha(data)
                if expected_sha and original_sha!=expected_sha:raise ValueError('Existing catalog SHA-256 mismatch')
                value=dict(schema=1,vault_id=self.vault_id,bytes=len(data),sha256=original_sha,segments=[self.blob(data[offset:offset+size]) for offset,size in spans(data)])
            after=os.fstat(stream.fileno())
            if (before.st_size,before.st_mtime_ns)!=(after.st_size,after.st_mtime_ns):raise ValueError('Model changed during preparation')
            validate_descriptor(value,MAX_MODEL)
            encoded=MAGIC+json.dumps(value,separators=(',',':')).encode()
            if len(encoded)>MAX_DESCRIPTOR:raise ValueError('Descriptor byte budget')
        except BaseException:
            stream.close();raise
        self.sources[str(path)]=stream
        return dict(path=str(path),bytes=before.st_size,mtime_ns=before.st_mtime_ns,sha256=original_sha,descriptor=value)

    def release(self,item):
        stream=self.sources.pop(item['path'],None)
        if stream is not None:stream.close()

    def publish(self,item,verify_cli=None):
        self.close_pack()
        path=Path(item['path']);confined(self.root,path)
        current=os.stat(path)
        if (current.st_size,current.st_mtime_ns)!=(item['bytes'],item['mtime_ns']):raise ValueError('Source changed before publication')
        # Keep sidecar paths short even when the model name nearly fills MAX_PATH.
        token=uuid.uuid4().hex;temp=path.with_name('.orpv-pending-'+token)
        backup=path.with_name('.orpv-original-'+token)
        record=self.home/'transactions'/(token+'.json')
        encoded=MAGIC+json.dumps(item['descriptor'],separators=(',',':')).encode()
        with temp.open('xb') as stream:
            stream.write(encoded);stream.flush();os.fsync(stream.fileno())
        if digest(self.root,temp)!=item['sha256']:raise ValueError('Reconstruction SHA-256 mismatch')
        if verify_cli:
            check=subprocess.run([str(verify_cli),'verify',str(self.root),str(temp),str(MAX_MODEL)],capture_output=True,text=True,check=True)
            native=json.loads(check.stdout)
            if native.get('state')!='verified' or native['sha256']!=item['sha256'] or native['bytes']!=item['bytes']:raise ValueError('Native interoperability mismatch')
        transaction=dict(state='prepared',path=str(path),temporary=str(temp),backup=str(backup),sha256=item['sha256'],bytes=item['bytes'],mtime_ns=item['mtime_ns'],descriptor_sha256=sha(encoded),utc=utc())
        atomic_json(record,transaction)
        # A second functional manifest lives inside the same index file, not in
        # thousands of sidecars. It permits explicit recovery of a missing stub.
        self.db.execute('INSERT OR REPLACE INTO models VALUES (?,?,?,?,?,?)',(path.relative_to(self.root).as_posix(),item['sha256'],item['bytes'],item['mtime_ns'],encoded,sha(encoded)))
        self.db.commit()
        confined(self.root,path);confined(self.root,temp)
        if os.path.lexists(backup):raise ValueError('Unexpected backup collision')
        current=os.stat(path)
        if (current.st_size,current.st_mtime_ns)!=(item['bytes'],item['mtime_ns']):raise ValueError('Source changed immediately before replacement')
        # The original remains recoverable even if replacement or later verification is interrupted.
        replace_with_backup(path,temp,backup)
        transaction['state']='replaced';atomic_json(record,transaction)
        confined(self.root,backup)
        with backup.open('rb') as stream: backup_sha=hashlib.file_digest(stream,'sha256').hexdigest()
        info=os.stat(backup)
        if backup_sha!=item['sha256'] or info.st_size!=item['bytes']:
            if path.read_bytes()!=encoded:raise RuntimeError('Concurrent replacement: both files preserved; intervention required')
            os.replace(backup,path)
            transaction['state']='restored-concurrent-change';atomic_json(record,transaction)
            raise ValueError('Source changed; original restored without deletion')
        if path.read_bytes()!=encoded:raise RuntimeError('Descriptor changed; transaction backup retained')
        os.utime(path,ns=(info.st_atime_ns,item['mtime_ns']))
        confined(self.root,backup);backup.unlink()
        transaction['state']='complete';transaction['physical_descriptor_bytes']=len(encoded)
        # Keep active recovery records only, not one historical file per conversion.
        record.unlink()
        self.release(item)
        return transaction

    def close(self):
        for stream in self.sources.values():stream.close()
        self.sources.clear()
        self.close_pack();self.db.close()
        if json.loads(self.lock.read_bytes()).get('owner')!=self.owner:raise ValueError('Writer lock identity changed')
        self.lock.unlink()

def recover_transaction(root,record,verify_cli=None):
    root=Path(root);record=Path(record)
    confined(root,record)
    if record.parent!=root/'.forge-storage/transactions' or not valid_hex(record.stem,32):raise ValueError('Unknown recovery record')
    item=json.loads(record.read_bytes());path=Path(item['path']);temp=Path(item['temporary']);backup=Path(item['backup'])
    legacy=(path.with_name(path.name+'.orpv-pending-'+record.stem),path.with_name(path.name+'.orpv-original-'+record.stem))
    compact=(path.with_name('.orpv-pending-'+record.stem),path.with_name('.orpv-original-'+record.stem))
    if (temp,backup) not in (legacy,compact):raise ValueError('Invalid transaction paths')
    if not path.is_relative_to(root) or path.suffix.lower()!='.glb':raise ValueError('Unknown transaction model')
    def raw_hash(value):
        confined(root,value)
        with value.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
    def clean_temp():
        if temp.exists():
            if raw_hash(temp)!=item['descriptor_sha256']:raise ValueError('Changed temporary; preserved')
            temp.unlink()
    if path.exists() and raw_hash(path)==item['descriptor_sha256']:
        if digest(root,path)!=item['sha256']:raise ValueError('Invalid reconstruction; backup preserved')
        if verify_cli:
            result=subprocess.run([str(verify_cli),'verify',str(root),str(path),str(MAX_MODEL)],capture_output=True,text=True,check=True)
            if json.loads(result.stdout)['sha256']!=item['sha256']:raise ValueError('Native recovery verification failed')
        if backup.exists():
            backup_hash=raw_hash(backup)
            if backup_hash!=item['sha256']:
                # A concurrent source change was captured: restore it, never discard it.
                os.replace(backup,path);clean_temp();record.unlink()
                return dict(state='restored-concurrent-change',path=str(path))
            backup.unlink()
        os.utime(path,ns=(os.stat(path).st_atime_ns,item['mtime_ns']))
        clean_temp();record.unlink();return dict(state='completed',path=str(path))
    if path.exists() and raw_hash(path)==item['sha256'] and not backup.exists():
        clean_temp();record.unlink();return dict(state='rolled-back',path=str(path))
    if not path.exists() and backup.exists() and raw_hash(backup)==item['sha256']:
        confined(root,path.parent);os.rename(backup,path);clean_temp();record.unlink()
        return dict(state='restored-original',path=str(path))
    raise ValueError('Ambiguous transaction; all files preserved')

def restore_descriptor(root,path,expected,verify_cli):
    root=Path(root);path=Path(path)
    if not path.is_absolute() or not path.is_relative_to(root) or '..' in path.parts or not valid_hex(expected,64):raise ValueError('Invalid explicit recovery path/hash')
    if os.path.lexists(path):raise ValueError('Existing file preserved; restore only a missing descriptor')
    confined(root,path.parent)
    index=confined(root,root/'.forge-storage/index.sqlite')
    db=sqlite3.connect('file:'+index.as_posix()+'?mode=ro',uri=True)
    try:row=db.execute('SELECT descriptor,descriptor_sha256,mtime_ns FROM models WHERE path=? AND sha256=?',(path.relative_to(root).as_posix(),expected)).fetchone()
    finally:db.close()
    if row is None or len(row[0])>MAX_DESCRIPTOR or sha(row[0])!=row[1]:raise ValueError('No intact functional manifest for that exact file/hash')
    temp=path.with_name(path.name+'.recovery-'+uuid.uuid4().hex)
    with temp.open('xb') as stream:stream.write(row[0]);stream.flush();os.fsync(stream.fileno())
    if digest(root,temp)!=expected:raise ValueError('Recovered manifest fails full reconstruction')
    run=subprocess.run([str(verify_cli),'verify',str(root),str(temp),str(MAX_MODEL)],capture_output=True,text=True,check=True)
    if json.loads(run.stdout)['sha256']!=expected:raise ValueError('Native recovery digest differs')
    if os.path.lexists(path):raise ValueError('Concurrent file appeared; both preserved')
    os.rename(temp,path);os.utime(path,ns=(os.stat(path).st_atime_ns,row[2]))
    return dict(state='restored',path=str(path),sha256=expected)

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    commands=parser.add_subparsers(dest='command',required=True)
    check=commands.add_parser('verify');check.add_argument('root',type=Path);check.add_argument('path',type=Path)
    materialize=commands.add_parser('export');materialize.add_argument('root',type=Path);materialize.add_argument('path',type=Path);materialize.add_argument('output',type=Path)
    recover=commands.add_parser('recover');recover.add_argument('root',type=Path);recover.add_argument('record',type=Path);recover.add_argument('--verify-cli',type=Path)
    restore=commands.add_parser('restore-descriptor');restore.add_argument('root',type=Path);restore.add_argument('path',type=Path);restore.add_argument('sha256');restore.add_argument('--verify-cli',type=Path,required=True)
    plan=commands.add_parser('plan');plan.add_argument('--root',type=Path,required=True);plan.add_argument('--inventory',type=Path,required=True);plan.add_argument('--output',type=Path,required=True);plan.add_argument('--min-age-hours',type=float,default=48);plan.add_argument('--min-bytes',type=int,default=1024**2)
    apply=commands.add_parser('apply');apply.add_argument('--plan',type=Path,required=True);apply.add_argument('--verify-cli',type=Path,required=True);apply.add_argument('--readers-receipt',type=Path,required=True);apply.add_argument('--report',type=Path,required=True);apply.add_argument('--resume',action='store_true')
    args=parser.parse_args()
    if args.command=='restore-descriptor':print(json.dumps(restore_descriptor(args.root,args.path,args.sha256,args.verify_cli)));return
    if args.command=='recover':
        if (args.root/'.forge-storage/WRITER.lock').exists():raise ValueError('Writer lock present; verify its process before recovery')
        print(json.dumps(recover_transaction(args.root,args.record,args.verify_cli)));return
    if args.command=='verify':print(json.dumps(dict(sha256=digest(args.root,args.path))));return
    if args.command=='export':
        data=read_bytes(args.root,args.path)
        with args.output.open('xb') as stream:stream.write(data);stream.flush();os.fsync(stream.fileno())
        print(json.dumps(dict(bytes=len(data),sha256=sha(data))));return
    if args.command=='plan':
        db=sqlite3.connect('file:'+args.inventory.as_posix()+'?mode=ro',uri=True)
        cutoff=time.time_ns()-int(args.min_age_hours*3600*10**9)
        rows=db.execute("SELECT path,bytes,mtime_ns FROM entries WHERE root=? AND kind='file' AND extension='.glb' AND g1 IN ('normalized','candidates','animation') AND bytes>=? AND bytes<=? AND mtime_ns<? ORDER BY path",(str(args.root),args.min_bytes,MAX_MODEL,cutoff)).fetchall();db.close()
        value=dict(schema=1,root=str(args.root),utc=utc(),policy=dict(min_age_hours=args.min_age_hours,min_bytes=args.min_bytes,max_bytes=MAX_MODEL),files=[dict(path=p,bytes=b,mtime_ns=m) for p,b,m in rows],logical_bytes=sum(r[1] for r in rows))
        if args.output.exists():raise ValueError('Frozen plan already exists')
        atomic_json(args.output,value);print(json.dumps(dict(files=len(rows),logical_bytes=value['logical_bytes'])));return
    plan=json.loads(args.plan.read_bytes());root=Path(plan['root'])
    receipt=json.loads(args.readers_receipt.read_bytes())
    verify_deployed_readers(receipt,root,args.verify_cli)
    plan_sha=sha(args.plan.read_bytes());done_paths=set()
    if args.report.exists():
        if not args.resume:raise ValueError('Existing run: recover journal explicitly instead of blindly repeating')
        report=json.loads(args.report.read_bytes())
        if report.get('plan_sha256')!=plan_sha or report.get('vault')!=str(root) or report.get('state') not in ('running','interrupted'):raise ValueError('Resume plan/state mismatch')
        report['state']='running';report.pop('error',None);report['skipped']=report.get('skipped',[])
    else:
        if args.resume:raise ValueError('No run to resume')
        report=dict(state='running',utc=utc(),files=0,logical_bytes=0,skipped=[],free_before=shutil.disk_usage(root).free,plan_sha256=plan_sha,vault=str(root))
    report.update(pid=os.getpid(),process_started_utc=utc(),phase='preparing')
    journal=args.report.with_suffix('.jsonl')
    if journal.exists():
        if not args.resume:raise ValueError('Existing journal: explicit recovery required')
        journal_files=journal_bytes=0
        with journal.open(encoding='utf-8') as stream:
            for line in stream:
                entry=json.loads(line)
                if entry.get('state')=='complete':
                    done_paths.add(entry['path']);journal_files+=1;journal_bytes+=entry['bytes']
        report['files']=journal_files;report['logical_bytes']=journal_bytes
    transactions=root/'.forge-storage/transactions'
    if transactions.exists():
        confined(root,transactions)
        if any(transactions.iterdir()):raise ValueError('Unfinished transaction: recover it before resuming')
    writer=Packer(root);pending=[];pending_bytes=0;checkpoint=0;queue=None
    report.update(planned_files=len(plan['files']),planned_logical_bytes=plan['logical_bytes'])
    def progress():
        minimal={key:value for key,value in report.items() if key!='skipped'}
        minimal['skipped_count']=len(report['skipped']);minimal['updated_utc']=utc()
        atomic_json(args.report,minimal)
    def acknowledge(item):
        if queue is not None and item.get('_queue_pin'):
            queue.execute('DELETE FROM pending WHERE path=? AND sha256=?',(item['path'],item['_queue_pin']));queue.commit()
    def publish():
        nonlocal pending,pending_bytes,checkpoint
        if not pending:return
        writer.close_pack()
        cluster=131072
        rounded=lambda value:((value+cluster-1)//cluster)*cluster
        before_allocation=sum(rounded(item['bytes']) for item in pending)
        after_allocation=sum(rounded(8+len(json.dumps(item['descriptor'],separators=(',',':')).encode())) for item in pending)
        after_allocation+=sum(rounded(size) for _,size in writer.created_packs[checkpoint:])
        # Include the functional descriptor replica and conservative SQLite row
        # overhead. A smaller payload alone must not hide growing metadata.
        for item in pending:
            descriptor_bytes=8+len(json.dumps(item['descriptor'],separators=(',',':')).encode())
            key_bytes=len(Path(item['path']).relative_to(root).as_posix().encode('utf-8'))
            after_allocation+=((descriptor_bytes+key_bytes+1024+4095)//4096)*4096
        for pack_id,_ in writer.created_packs[checkpoint:]:
            rows=writer.db.execute('SELECT count(*) FROM blobs WHERE pack=?',(pack_id,)).fetchone()[0]
            after_allocation+=rows*512
        if not batch_saves_allocation(before_allocation,after_allocation):
            writer.discard_unpublished_batch(checkpoint)
            report['skipped'].extend(dict(path=item['path'],reason='batch has no positive conservative net allocation saving; original retained') for item in pending)
            for item in pending:writer.release(item)
            for item in pending:acknowledge(item)
            pending=[];pending_bytes=0;return
        for item in pending:
            report.update(phase='verifying-and-publishing',current_file=item['path']);progress()
            done=writer.publish(item,args.verify_cli)
            done_paths.add(item['path'])
            report['files']+=1;report['logical_bytes']+=item['bytes']
            with journal.open('a',encoding='utf-8') as stream:stream.write(json.dumps(done)+'\n');stream.flush();os.fsync(stream.fileno())
            acknowledge(item)
            report['new_pack_bytes']=writer.new_pack_bytes;report['free_now']=shutil.disk_usage(root).free;progress()
        pending=[];pending_bytes=0;checkpoint=len(writer.created_packs)
    try:
        progress()
        def process(row):
            nonlocal pending_bytes
            if row['path'] in done_paths:return
            path=Path(row['path'])
            try:
                confined(root,path);info=os.stat(path)
            except FileNotFoundError:
                report['skipped'].append(dict(path=str(path),reason='source removed since frozen inventory'));return
            if not eligible_model_storage_path(root,path):raise ValueError('Frozen plan outside converted-model scope')
            if windows_readonly(info):
                report['skipped'].append(dict(path=str(path),reason='Windows read-only attribute; original preserved'));return
            if (info.st_size,info.st_mtime_ns)!=(row['bytes'],row['mtime_ns']):
                report['skipped'].append(dict(path=str(path),reason='changed since frozen inventory'));return
            report.update(phase='preparing',current_file=str(path));progress()
            if shutil.disk_usage(root).free<16*1024**3:raise OSError('Storage free-space reserve below 16 GiB; originals preserved')
            try:item=writer.prepare(path,expected_sha=row.get('sha256'))
            except ValueError as error:
                if str(error)=='Existing catalog SHA-256 mismatch':
                    report['skipped'].append(dict(path=str(path),reason='published pin changed; original and queue preserved'));return
                raise
            except OSError as error:
                if getattr(error,'winerror',None) in (5,32,33):
                    report['skipped'].append(dict(path=str(path),reason='active writer or access refused; original preserved'));return
                raise
            if item is None:return
            item['_queue_pin']=row.get('sha256')
            pending.append(item);pending_bytes+=item['bytes']
            if len(pending)>=16 or pending_bytes>=2*1024**3 or writer.pack_size>=192*1024**2:publish()
        for row in plan['files']:process(row)
        publish()
        queue=ingest_database(root)
        queued=queue.execute('SELECT path,sha256,bytes,mtime_ns FROM pending ORDER BY path').fetchall()
        if len(queued)>100000:raise ValueError('Published queue exceeds bounded drain budget')
        for path,pin,size,mtime in queued:
            try:descriptor=read_descriptor(root,Path(path))
            except FileNotFoundError:
                report['skipped'].append(dict(path=path,reason='published source no longer present; queue preserved'));continue
            if descriptor is not None and descriptor['sha256']==pin and descriptor['bytes']==size:
                queue.execute('DELETE FROM pending WHERE path=? AND sha256=?',(path,pin));queue.commit();continue
            process(dict(path=path,sha256=pin,bytes=size,mtime_ns=mtime))
        publish();report['state']='complete';report['phase']='final'
        report['pending_publications']=queue.execute('SELECT count(*) FROM pending').fetchone()[0]
    except BaseException as error:
        report['state']='interrupted';report['error']=str(error);raise
    finally:
        if queue is not None:queue.close()
        writer.close();report['new_pack_bytes']=writer.new_pack_bytes;report['finished_utc']=utc();report['free_after']=shutil.disk_usage(root).free;atomic_json(args.report,report)
    print(json.dumps({key:value for key,value in report.items() if key!='skipped'}))

if __name__=='__main__':main()
