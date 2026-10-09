"""Extract checksum-pinned public crates onto exFAT without legacy mtimes.

Only the owned probe cache is written. Archive bytes/checksums and every member
are retained; no links or escaped paths are accepted and no files are deleted.
"""
from pathlib import Path, PurePosixPath
import datetime
import hashlib
import io
import json
import shutil
import tarfile
import tomllib
import urllib.request

ROOT = Path('F:/OpenRPG_ImportWork/model-large-wasm-carrier-v1')
CACHE = Path('F:/OpenRPG_ImportWork/public-wasm-probe-cargo-cache')
INDEX = 'index.crates.io-1949cf8c6b5b557f'


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    lock = ROOT / 'Cargo.lock'
    packages = tomllib.loads(lock.read_text('utf-8'))['package']
    cache = CACHE / 'registry/cache' / INDEX
    source = CACHE / 'registry/src' / INDEX
    cache.mkdir(parents=True, exist_ok=True)
    source.mkdir(parents=True, exist_ok=True)
    rows = []
    for package in packages:
        if not package.get('source', '').startswith('registry+'):
            continue
        name, version, checksum = package['name'], package['version'], package['checksum']
        assert all(c.isalnum() or c in '-_.' for c in name + version)
        key = name + '-' + version
        archive = cache / (key + '.crate')
        if not archive.exists():
            existing = Path('C:/Users/karen/.cargo/registry/cache') / INDEX / archive.name
            if existing.exists() and sha(existing) == checksum:
                shutil.copyfile(existing, archive)
            else:
                url = f'https://static.crates.io/crates/{name}/{key}.crate'
                temporary = archive.with_suffix('.download.tmp')
                with urllib.request.urlopen(url, timeout=30) as response, temporary.open('wb') as stream:
                    written = 0
                    while chunk := response.read(1024 * 1024):
                        written += len(chunk)
                        assert written <= 32 * 1024 * 1024, 'Public crate archive size ceiling'
                        stream.write(chunk)
                assert sha(temporary) == checksum, key
                temporary.replace(archive)
        assert sha(archive) == checksum, key
        files = {}
        total = 0
        with tarfile.open(archive, 'r:gz') as contents:
            for member in contents:
                relative = PurePosixPath(member.name)
                assert not relative.is_absolute() and relative.parts[0] == key
                assert all(part not in ('..', '') and ':' not in part and '\\' not in part for part in relative.parts)
                target = source.joinpath(*relative.parts)
                assert target.resolve().is_relative_to((source / key).resolve())
                if member.isdir():
                    target.mkdir(parents=True, exist_ok=True)
                    continue
                assert member.isfile(), ('Link or unsupported crate member', member.name)
                total += member.size
                assert member.size <= 32 * 1024 * 1024 and total <= 256 * 1024 * 1024
                payload = contents.extractfile(member)
                assert payload is not None
                data = payload.read()
                assert len(data) == member.size
                target.parent.mkdir(parents=True, exist_ok=True)
                # Applying archive mtime 0 fails on exFAT; writing source bytes
                # uses the current valid filesystem time and preserves content.
                target.write_bytes(data)
                digest = hashlib.sha256(data).hexdigest()
                assert sha(target) == digest
                files[str(target)] = digest
        marker = source / key / '.cargo-ok'
        marker.write_text('{"v":1}', 'utf-8')
        rows.append(dict(package=name, version=version, locked_archive_sha256=checksum,
                         archive=str(archive), source_files=len(files), source_bytes=total,
                         extracted_file_sha256=files, content_bytes_preserved=True,
                         legacy_archive_mtime_not_applied=True))
        print(json.dumps(dict(package=name, version=version, files=len(files))), flush=True)
    result = dict(status='public_locked_crates_extracted_byte_exact_without_legacy_mtime',
                  cargo_lock_sha256=sha(lock), packages=len(rows), rows=rows,
                  assets_changed=False, finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    path = ROOT / 'EXFAT-REGISTRY-REPAIR.v1.json'
    assert not path.exists(), 'Immutable extraction receipt already exists'
    path.write_text(json.dumps(result, indent=2), 'utf-8')
    print(json.dumps(dict(status=result['status'], packages=len(rows))), flush=True)


if __name__ == '__main__':
    main()
