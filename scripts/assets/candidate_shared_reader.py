"""Read candidate package files and bounded resource ranges through an overlay.

This reader does not publish overlays or modify candidate packages. Existing
consumers must explicitly adopt it before physical source paths can be removed.
"""
import argparse
import contextlib
import hashlib
import json
from pathlib import Path

import vault_storage as storage
from unity_shared_overlay import UnitySourceResolver, configured_resolver

# Material catalog includes individual image payloads larger than 16 MiB.
MAX_RANGE = 64 * 1024 * 1024


class CandidateSharedReader:
    def __init__(self, vault, manifest, expected_sha256):
        self.reader = UnitySourceResolver(vault, manifest, expected_sha256)

    @classmethod
    def from_package(cls, vault, package):
        """Use a published, SHA-pinned overlay covering the entire package."""
        vault = Path(vault).resolve(strict=True)
        package = Path(package).resolve(strict=True)
        resolver = configured_resolver(vault, package)
        if resolver is None or resolver.source_root != package:
            raise ValueError('complete candidate package overlay is not published')
        result = cls.__new__(cls)
        result.reader = resolver
        return result

    @contextlib.contextmanager
    def open(self, relative):
        with self.reader.open_verified(relative) as stream:
            yield stream

    def read_range(self, relative, offset, length, expected_sha256):
        if (type(offset) is not int or type(length) is not int or offset < 0 or
                length < 0 or length > MAX_RANGE or not storage.valid_hex(expected_sha256, 64)):
            raise ValueError('invalid candidate resource range')
        with self.open(relative) as stream:
            stream.seek(0, 2)
            if offset + length > stream.tell():
                raise ValueError('candidate resource range outside file')
            stream.seek(offset)
            payload = stream.read(length)
        if len(payload) != length or hashlib.sha256(payload).hexdigest() != expected_sha256:
            raise ValueError('candidate resource range SHA differs')
        return payload

    def verify_catalog_files(self, relative='catalog.v1.json'):
        with self.open(relative) as stream:
            catalog = json.load(stream)
        files = catalog.get('files')
        if not isinstance(files, list):
            raise ValueError('candidate catalog has no file list')
        seen = set()
        for entry in files:
            path = UnitySourceResolver._relative(entry.get('path')).as_posix()
            key = path.casefold()
            pin, size = entry.get('sha256'), entry.get('bytes')
            if key in seen or not storage.valid_hex(pin, 64) or type(size) is not int or size < 0:
                raise ValueError('invalid candidate catalog file')
            seen.add(key)
            actual = self.reader.entries.get(key)
            if actual is None:
                raise ValueError('candidate catalog file differs from overlay: ' + path)
            if Path(path).suffix.casefold() == '.glb':
                # The catalog pins logical GLB bytes, while the overlay pins
                # the physical Vault envelope. Verify both under the read lock.
                digest = hashlib.sha256()
                total = 0
                physical = self.reader.vault / actual[0]
                with self.open(path):
                    for block in storage.chunks(self.reader.vault, physical):
                        digest.update(block)
                        total += len(block)
                if (digest.hexdigest(), total) != (pin, size):
                    raise ValueError('candidate logical model differs: ' + path)
            else:
                if actual[1:] != (pin, size):
                    raise ValueError('candidate catalog file differs from overlay: ' + path)
                with self.open(path):
                    pass
        return {'state': 'candidate_catalog_files_verified', 'files': len(seen)}

    def verify_viking_catalog(self, relative='catalog.v1.json'):
        """Verify Viking asset and source-file references through the overlay."""
        with self.open(relative) as stream:
            catalog = json.load(stream)
        if catalog.get('schema_version') != 1 or not isinstance(catalog.get('assets'), list):
            raise ValueError('invalid Viking candidate catalog')
        references = {}
        for asset in catalog['assets']:
            sources = asset.get('source_files')
            if not isinstance(sources, list):
                raise ValueError('Viking source list missing')
            for entry in [asset, *sources]:
                path = UnitySourceResolver._relative(entry.get('file')).as_posix()
                pin = entry.get('sha256')
                if not storage.valid_hex(pin, 64):
                    raise ValueError('invalid Viking file SHA')
                key = path.casefold()
                if key in references and references[key][1] != pin:
                    raise ValueError('conflicting Viking file SHA')
                references[key] = (path, pin, entry.get('bytes'))
        for key, (path, pin, size) in references.items():
            actual = self.reader.entries.get(key)
            if actual is None:
                raise ValueError('Viking catalog file absent from overlay: ' + path)
            if Path(path).suffix.casefold() == '.glb':
                digest = hashlib.sha256()
                total = 0
                with self.open(path):
                    for block in storage.chunks(self.reader.vault, self.reader.vault / actual[0]):
                        digest.update(block)
                        total += len(block)
                if digest.hexdigest() != pin or (size is not None and total != size):
                    raise ValueError('Viking logical model differs: ' + path)
            else:
                if actual[1] != pin or (size is not None and actual[2] != size):
                    raise ValueError('Viking source differs from overlay: ' + path)
                with self.open(path):
                    pass
        return {'state': 'viking_catalog_files_verified', 'files': len(references),
                'assets': len(catalog['assets'])}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', type=Path, required=True)
    selection = parser.add_mutually_exclusive_group(required=True)
    selection.add_argument('--manifest', type=Path)
    selection.add_argument('--package', type=Path)
    parser.add_argument('--manifest-sha256')
    parser.add_argument('--catalog', default='catalog.v1.json')
    parser.add_argument('--kind', choices=('files', 'viking'), default='files')
    args = parser.parse_args()
    if args.package is not None:
        if args.manifest_sha256 is not None:
            parser.error('--manifest-sha256 is only valid with --manifest')
        package = args.package if args.package.is_absolute() else args.vault / args.package
        reader = CandidateSharedReader.from_package(args.vault, package)
    else:
        if args.manifest_sha256 is None:
            parser.error('--manifest requires --manifest-sha256')
        reader = CandidateSharedReader(args.vault, args.manifest, args.manifest_sha256)
    result = (reader.verify_viking_catalog(args.catalog) if args.kind == 'viking'
              else reader.verify_catalog_files(args.catalog))
    print(json.dumps(result))
