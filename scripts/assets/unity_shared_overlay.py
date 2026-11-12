"""Prepare and materialize a byte-exact Unity source tree with shared files.

Preparation and export are non-destructive. A manifest maps each logical Unity
path to an independently SHA-pinned file in the Vault; Unity itself receives
normal files, including the original .meta files and GUIDs. This tool does not
authorize removal of existing files or integrate itself into other importers.
"""
import argparse
import contextlib
import hashlib
import json
import os
from pathlib import Path
import shutil
import stat
import vault_storage as storage


def inventory_tree(root):
    root=root.resolve(strict=True);files={};dirs=[];stack=[root]
    while stack:
        directory=stack.pop()
        with os.scandir(directory) as entries:
            for entry in entries:
                st=entry.stat(follow_symlinks=False)
                reparse=(bool(st.st_file_attributes & stat.FILE_ATTRIBUTE_REPARSE_POINT)
                         if hasattr(st,'st_file_attributes') else entry.is_symlink())
                if reparse:raise ValueError('Unity tree contains a link or reparse point')
                path=Path(entry.path);rel=path.relative_to(root).as_posix()
                if entry.is_dir(follow_symlinks=False):dirs.append(rel);stack.append(path)
                elif entry.is_file(follow_symlinks=False):
                    key=rel.casefold()
                    if key in files:raise ValueError('ambiguous logical path')
                    files[key]=(rel,path)
                else:raise ValueError('unsupported filesystem object')
    return files,sorted(dirs,key=str.casefold)


def file_pin(path):
    with storage.locked_source(path,share_delete=False) as f:
        size=os.fstat(f.fileno()).st_size;pin=hashlib.file_digest(f,'sha256').hexdigest()
        if os.fstat(f.fileno()).st_size!=size:raise ValueError('source changed while hashing')
    return pin,size


class UnitySourceResolver:
    """Resolve a pinned logical Unity path to its verified Vault payload.

    Consumers receive an ordinary physical path. This resolver does not change
    the Unity tree or authorize deleting a source; tools needing a complete
    on-disk project must still use materialize().
    """
    def __init__(self, vault, manifest_path, expected_manifest_sha256):
        self.vault = vault.resolve(strict=True)
        self.manifest_path = manifest_path.resolve(strict=True)
        self.manifest_raw = self.manifest_path.read_bytes()
        if not storage.valid_hex(expected_manifest_sha256, 64) or storage.sha(self.manifest_raw) != expected_manifest_sha256:
            raise ValueError('overlay manifest SHA differs')
        doc = json.loads(self.manifest_raw)
        if doc.get('schema') != 'openrpg.unity-shared-overlay.v1':
            raise ValueError('invalid overlay manifest')
        self.source_root = storage.confined(self.vault, self.vault / doc['source_root'])
        self.entries = {}
        for row in doc['files']:
            relative = self._relative(row['logical'])
            key = relative.as_posix().casefold()
            if key in self.entries or not storage.valid_hex(row['sha256'], 64) or not isinstance(row['bytes'], int) or row['bytes'] < 0:
                raise ValueError('invalid or duplicate overlay entry')
            physical = self._relative(row['vault_path'])
            self.entries[key] = (physical, row['sha256'], row['bytes'])

    @staticmethod
    def _relative(value):
        if not isinstance(value, str) or not value or '\\' in value or ':' in value:
            raise ValueError('invalid logical path')
        path = Path(value)
        if path.is_absolute() or '..' in path.parts or '.' in path.parts or not path.parts:
            raise ValueError('logical path escapes source')
        return path

    def resolve(self, logical):
        key = self._relative(logical).as_posix().casefold()
        if key not in self.entries:
            raise ValueError('logical Unity source absent from overlay')
        if self.manifest_path.read_bytes() != self.manifest_raw:
            raise ValueError('overlay manifest changed')
        physical, pin, size = self.entries[key]
        path = storage.confined(self.vault, self.vault / physical)
        if file_pin(path) != (pin, size):
            raise ValueError('overlay payload changed: ' + logical)
        return path

    def resolve_absolute(self, path):
        """Resolve paths under this overlay, leaving other source paths alone."""
        path = Path(path)
        if not path.is_absolute():
            raise ValueError('absolute source path required')
        if not path.is_relative_to(self.source_root):
            return path
        return self.resolve(path.relative_to(self.source_root).as_posix())

    def contains_absolute(self, path):
        path = Path(path)
        if not path.is_absolute():raise ValueError('absolute source path required')
        if not path.is_relative_to(self.source_root):return path.is_file()
        return path.relative_to(self.source_root).as_posix().casefold() in self.entries

    @contextlib.contextmanager
    def open_verified(self, logical):
        """Keep the payload write-locked for the duration of a consumer read."""
        key = self._relative(logical).as_posix().casefold()
        if key not in self.entries:
            raise ValueError('logical Unity source absent from overlay')
        if self.manifest_path.read_bytes() != self.manifest_raw:
            raise ValueError('overlay manifest changed')
        physical, pin, size = self.entries[key]
        path = storage.confined(self.vault, self.vault / physical)
        with storage.locked_source(path, share_delete=False) as stream:
            if os.fstat(stream.fileno()).st_size != size or hashlib.file_digest(stream, 'sha256').hexdigest() != pin:
                raise ValueError('overlay payload changed: ' + logical)
            stream.seek(0)
            yield stream
            if self.manifest_path.read_bytes() != self.manifest_raw:
                raise ValueError('overlay manifest changed during read')


def configured_resolver(vault, package_source):
    """Find a published overlay for a package, refusing ambiguity or tampering."""
    vault=vault.resolve(strict=True);package_source=package_source.resolve(strict=True)
    if not package_source.is_relative_to(vault):raise ValueError('source outside Vault')
    registry=vault/'metadata/unity-shared-overlay-registry.v1.json'
    if not registry.exists():return None
    registry=storage.confined(vault,registry);raw=registry.read_bytes();doc=json.loads(raw)
    if doc.get('schema')!='openrpg.unity-shared-overlay-registry.v1' or not isinstance(doc.get('overlays'),list):
        raise ValueError('invalid Unity overlay registry')
    matches=[]
    for item in doc['overlays']:
        root=UnitySourceResolver._relative(item['source_root'])
        absolute=storage.confined(vault,vault/root)
        if absolute.is_relative_to(package_source):matches.append((absolute,item))
    if not matches:return None
    if len(matches)!=1:raise ValueError('ambiguous Unity overlays for package')
    root,item=matches[0];relative=UnitySourceResolver._relative(item['manifest'])
    resolver=UnitySourceResolver(vault,storage.confined(vault,vault/relative),item['sha256'])
    if resolver.source_root!=root or registry.read_bytes()!=raw:raise ValueError('Unity overlay registry changed')
    return resolver


def changed_overlay_rows(current, previous, expected):
    """Identify newly shared entries while preserving all prior logical pins."""
    if current.get('source_root')!=previous.get('source_root'):raise ValueError('overlay source root changed')
    before={x['logical'].casefold():x for x in previous['files']}
    after={x['logical'].casefold():x for x in current['files']}
    if len(before)!=len(previous['files']) or len(after)!=len(current['files']) or set(before)!=set(after):
        raise ValueError('overlay entries changed')
    rows=[]
    for key,old in before.items():
        new=after[key]
        if {k:v for k,v in old.items() if k!='vault_path'}!={k:v for k,v in new.items() if k!='vault_path'}:
            raise ValueError('logical source pin changed')
        if old['vault_path']!=new['vault_path']:
            original=(current['source_root']+'/'+new['logical']).casefold()
            if old['vault_path'].casefold()!=original or new['vault_path'].casefold()==original:
                raise ValueError('not a newly shared original')
            rows.append(new)
    if len(rows)!=expected:raise ValueError('unexpected changed overlay count')
    return rows


def prepare(vault,root_rel,comparison_path):
    vault=vault.resolve(strict=True);root=storage.confined(vault,vault/root_rel)
    comparison_raw=comparison_path.read_bytes();comparison=json.loads(comparison_raw)
    ptr=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json')
    pointer=ptr.read_bytes()
    if storage.sha(pointer)!=comparison['source_registry_sha256']:raise ValueError('source registry changed')
    files,dirs=inventory_tree(root);aliases={}
    for row in comparison['results']:
        if row['state']!='identical_dependencies':continue
        for item in row['dependencies']:
            old=Path(item['hdrp']);keep=Path(item['urp'])
            if not (vault/old).is_relative_to(root):continue
            for suffix,pin_key in (('', 'sha256'),('.meta','meta_sha256')):
                a=Path(str(old)+suffix);b=Path(str(keep)+suffix)
                key=(vault/a).relative_to(root).as_posix().casefold();pin=item['hdrp_info'][pin_key]
                if key in aliases or key not in files or not storage.valid_hex(pin,64):raise ValueError('invalid overlay mapping')
                aliases[key]=(a.as_posix(),b.as_posix(),pin)
    if not aliases:raise ValueError('no shared files selected for tree')
    result=[];total=0
    for key,(rel,path) in sorted(files.items()):
        pin,size=file_pin(storage.confined(vault,path));total+=size
        if key in aliases:
            old,retained,expected=aliases[key]
            if pin!=expected or file_pin(storage.confined(vault,vault/retained))!=(pin,size):
                raise ValueError('shared source differs: '+rel)
            physical=retained
        else:physical=path.relative_to(vault).as_posix()
        result.append({'logical':rel,'vault_path':physical,'sha256':pin,'bytes':size})
    if ptr.read_bytes()!=pointer or comparison_path.read_bytes()!=comparison_raw:raise ValueError('inputs changed')
    return {'schema':'openrpg.unity-shared-overlay.v1','state':'prepared_only',
        'source_root':root.relative_to(vault).as_posix(),'source_registry_sha256':storage.sha(pointer),
        'comparison_sha256':storage.sha(comparison_raw),'directories':dirs,'files':result,
        'shared_files':len(aliases),'logical_bytes':total,
        'limitations':'Preparation only; no importer integration, Unity test or deletion authorization.'}


def expand_source_root(vault, manifest_path, source_root_rel):
    """Build a complete source-root export, preserving overlay mappings.

    Shared logical files can already be absent. Other files must exist and are
    independently pinned. The result is an ordinary v1 materialize manifest.
    """
    vault=vault.resolve(strict=True)
    root=storage.confined(vault,vault/source_root_rel)
    raw=manifest_path.read_bytes();overlay=json.loads(raw)
    if overlay.get('schema')!='openrpg.unity-shared-overlay.v1':raise ValueError('invalid overlay manifest')
    overlay_root=storage.confined(vault,vault/overlay['source_root'])
    if overlay_root==root or not overlay_root.is_relative_to(root):raise ValueError('overlay root must be below source root')
    prefix=overlay_root.relative_to(root)
    files,dirs=inventory_tree(root);mapped={};out=[]
    for row in overlay['files']:
        logical=Path(row['logical'])
        if logical.is_absolute() or '..' in logical.parts or not logical.parts:raise ValueError('invalid overlay path')
        rel=(prefix/logical).as_posix();key=rel.casefold()
        if key in mapped or not storage.valid_hex(row['sha256'],64):raise ValueError('invalid overlay entry')
        shared=storage.confined(vault,vault/row['vault_path'])
        if file_pin(shared)!=(row['sha256'],row['bytes']):raise ValueError('overlay source changed: '+rel)
        if key in files and file_pin(storage.confined(vault,files[key][1]))!=(row['sha256'],row['bytes']):
            raise ValueError('existing logical source changed: '+rel)
        mapped[key]={'logical':rel,'vault_path':row['vault_path'],'sha256':row['sha256'],'bytes':row['bytes']}
    for key,(rel,path) in files.items():
        if key in mapped:continue
        pin,size=file_pin(storage.confined(vault,path))
        out.append({'logical':rel,'vault_path':path.relative_to(vault).as_posix(),'sha256':pin,'bytes':size})
    out.extend(mapped.values());out.sort(key=lambda x:x['logical'].casefold())
    all_dirs=set(dirs)
    for item in overlay['directories']:
        part=Path(item)
        if part.is_absolute() or '..' in part.parts or not part.parts:raise ValueError('invalid overlay directory')
        all_dirs.add((prefix/part).as_posix())
    if manifest_path.read_bytes()!=raw:raise ValueError('overlay manifest changed')
    return {'schema':'openrpg.unity-shared-overlay.v1','state':'prepared_only',
        'source_root':root.relative_to(vault).as_posix(),'overlay_manifest_sha256':storage.sha(raw),
        'comparison_sha256':overlay.get('comparison_sha256'),
        'directories':sorted(all_dirs,key=str.casefold),'files':out,
        'shared_files':overlay['shared_files'],'logical_bytes':sum(x['bytes'] for x in out),
        'limitations':'Source root export prepared; conversion consumers require individual qualification.'}


def materialize(vault,manifest_path,destination,work_root):
    vault=vault.resolve(strict=True);work_root=work_root.resolve(strict=True)
    if destination.parent.resolve(strict=True)!=work_root or destination.exists():
        raise ValueError('destination must be a new direct child of the work root')
    raw=manifest_path.read_bytes();doc=json.loads(raw)
    if doc.get('schema')!='openrpg.unity-shared-overlay.v1':raise ValueError('invalid overlay manifest')
    seen=set()
    for rel in doc['directories']+[x['logical'] for x in doc['files']]:
        p=Path(rel)
        if p.is_absolute() or '..' in p.parts or not p.parts:raise ValueError('invalid logical path')
        key=p.as_posix().casefold()
        if key in seen:raise ValueError('duplicate logical path')
        seen.add(key)
    destination.mkdir()
    for rel in doc['directories']:(destination/rel).mkdir(parents=True,exist_ok=True)
    count=0;total=0
    for row in doc['files']:
        source=storage.confined(vault,vault/row['vault_path']);target=destination/row['logical']
        target.parent.mkdir(parents=True,exist_ok=True);pin=hashlib.sha256();size=0
        with storage.locked_source(source,share_delete=False) as src,target.open('xb') as dst:
            while block:=src.read(4*1024*1024):
                pin.update(block);size+=len(block);dst.write(block)
            dst.flush();os.fsync(dst.fileno())
        if pin.hexdigest()!=row['sha256'] or size!=row['bytes'] or file_pin(target)!=(row['sha256'],size):
            raise ValueError('materialized file differs: '+row['logical'])
        count+=1;total+=size
    if manifest_path.read_bytes()!=raw or total!=doc['logical_bytes']:raise ValueError('manifest changed or total differs')
    return {'state':'materialized_verified','manifest_sha256':storage.sha(raw),'files':count,
            'logical_bytes':total,'shared_files':doc['shared_files'],'destination':str(destination),
            'limitations':'Byte-exact export only; Unity import and all application integrations remain unqualified.'}


def cleanup(manifest_path,proof_path,destination,work_root):
    """Remove only the complete, unchanged temporary export under exclusive handles."""
    from exclusive_source_delete import ExclusiveSource
    work_root=work_root.resolve(strict=True)
    if not destination.is_absolute():raise ValueError('absolute export path required')
    destination=storage.confined(work_root,destination)
    if destination.parent!=work_root:raise ValueError('export must be a direct child of the work root')
    raw=manifest_path.read_bytes();proof_raw=proof_path.read_bytes();doc=json.loads(raw);proof=json.loads(proof_raw)
    if (doc.get('schema')!='openrpg.unity-shared-overlay.v1' or proof.get('state')!='materialized_verified'
            or proof.get('manifest_sha256')!=storage.sha(raw)
            or Path(proof.get('destination',''))!=destination):
        raise ValueError('export proof does not match')
    files,dirs=inventory_tree(destination)
    expected={row['logical'].casefold():row for row in doc['files']}
    if (len(expected)!=len(doc['files']) or set(files)!=set(expected)
            or {x.casefold() for x in dirs}!={x.casefold() for x in doc['directories']}):
        raise ValueError('export contents changed')
    before=shutil.disk_usage(work_root).free
    with contextlib.ExitStack() as stack:
        handles=[]
        for key,(_,path) in sorted(files.items()):
            handle=stack.enter_context(ExclusiveSource(storage.confined(work_root,path)))
            if handle.sha256()!=expected[key]['sha256']:raise ValueError('export file changed: '+key)
            handles.append(handle)
        if manifest_path.read_bytes()!=raw or proof_path.read_bytes()!=proof_raw:raise ValueError('proof changed')
        for handle in handles:handle.mark_delete()
    for rel in sorted(dirs,key=lambda x:len(Path(x).parts),reverse=True):(destination/rel).rmdir()
    destination.rmdir()
    after=shutil.disk_usage(work_root).free
    return {'state':'temporary_export_removed','files':len(files),'logical_bytes':doc['logical_bytes'],
            'free_delta_observed':after-before,'manifest_sha256':storage.sha(raw)}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);sub=p.add_subparsers(dest='action',required=True)
    prep=sub.add_parser('prepare');prep.add_argument('--vault',type=Path,required=True)
    prep.add_argument('--root',required=True);prep.add_argument('--comparison',type=Path,required=True)
    prep.add_argument('--output',type=Path,required=True)
    expand=sub.add_parser('expand-source-root');expand.add_argument('--vault',type=Path,required=True)
    expand.add_argument('--manifest',type=Path,required=True);expand.add_argument('--root',required=True)
    expand.add_argument('--output',type=Path,required=True)
    run=sub.add_parser('materialize');run.add_argument('--vault',type=Path,required=True)
    run.add_argument('--manifest',type=Path,required=True);run.add_argument('--destination',type=Path,required=True)
    run.add_argument('--output',type=Path,required=True)
    clean=sub.add_parser('cleanup');clean.add_argument('--manifest',type=Path,required=True)
    clean.add_argument('--proof',type=Path,required=True);clean.add_argument('--destination',type=Path,required=True)
    clean.add_argument('--output',type=Path,required=True)
    a=p.parse_args();work=Path(r'E:\OpenRPG_Forge\.build\asset-conversion').resolve(strict=True)
    if a.output.resolve(strict=False).parent!=work or a.output.exists():p.error('output must be new and in the conversion workspace')
    if a.action=='prepare':result=prepare(a.vault,a.root,a.comparison)
    elif a.action=='expand-source-root':result=expand_source_root(a.vault,a.manifest,a.root)
    elif a.action=='materialize':result=materialize(a.vault,a.manifest,a.destination,work)
    else:result=cleanup(a.manifest,a.proof,a.destination,work)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'state':result['state'],'files':len(result['files']) if a.action in ('prepare','expand-source-root') else result['files'],
                      'shared_files':result.get('shared_files'),'logical_bytes':result['logical_bytes']}))
