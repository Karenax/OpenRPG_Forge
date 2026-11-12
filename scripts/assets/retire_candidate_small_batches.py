"""Extend published candidate overlays for exact small-file duplicates.

The inventory itself is only a lead: the retirement tool rechecks live files,
active references, overlay dependencies and SHA before each transaction.
"""
import argparse
import collections
import hashlib
import json
from pathlib import Path

from candidate_shared_reader import CandidateSharedReader
from publish_source_support_relinks import importer_module
from retire_exact_unity_overlays import run


def prepare(vault, inventory, work, batch, package_limit, excluded):
    registry = json.loads((vault/'metadata/unity-shared-overlay-registry.v1.json').read_bytes())
    docs, uses = {}, collections.Counter()
    for record in registry['overlays']:
        raw = (vault/record['manifest']).read_bytes()
        if hashlib.sha256(raw).hexdigest() != record['sha256']:
            raise ValueError('registered manifest SHA changed')
        doc = json.loads(raw)
        docs[record['source_root'].casefold()] = doc
        for item in doc['files']:
            uses[item['vault_path'].casefold()] += 1
    groups = json.loads(inventory.read_bytes())['groups']
    choices = collections.defaultdict(list)
    for group in groups:
        paths = sorted(group['paths'], key=str.casefold)
        keeper = paths[0]
        for old in paths[1:]:
            parts = old.split('/')
            if len(parts) < 4 or parts[:2] != ['candidates', 'hivemind']:
                raise ValueError('path outside candidate packages')
            package = '/'.join(parts[:3])
            if package.casefold() in excluded:
                continue
            doc = docs.get(package.casefold())
            if not doc or uses[old.casefold()] != 1:
                continue
            logical = '/'.join(parts[3:]).casefold()
            item = next((e for e in doc['files'] if e['logical'].casefold() == logical), None)
            if (item and item['vault_path'].casefold() == old.casefold() and
                    item['sha256'] == group['sha256'] and item['bytes'] == group['bytes']):
                choices[package].append((logical, keeper))
    packages = sorted(choices, key=lambda p: (-len(choices[p]), p))[:package_limit]
    manifests = []
    for number, package in enumerate(packages):
        doc = docs[package.casefold()]
        changes = dict(choices[package])
        if len(changes) != len(choices[package]):
            raise ValueError('duplicate logical selection')
        for item in doc['files']:
            if item['logical'].casefold() in changes:
                item['vault_path'] = changes[item['logical'].casefold()]
        doc['shared_files'] += len(changes)
        path = work / ('candidate-small-extension-%03d-%02d.json' % (batch, number))
        if path.exists():
            raise ValueError('stage already exists')
        path.write_text(json.dumps(doc, ensure_ascii=False, sort_keys=True, indent=2)+'\n', encoding='utf8')
        manifests.append(path)
    return manifests, sum(len(choices[p]) for p in packages)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vault', type=Path, required=True)
    parser.add_argument('--inventory', type=Path, required=True)
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--importer-sha256', required=True)
    parser.add_argument('--start-batch', type=int, default=1)
    parser.add_argument('--batches', type=int, default=1)
    parser.add_argument('--packages-per-batch', type=int, default=6)
    parser.add_argument('--apply', action='store_true')
    a = parser.parse_args()
    if not 1 <= a.packages_per_batch <= 6 or not 1 <= a.batches <= 30:
        raise ValueError('invalid batch size')
    vault = a.vault.resolve(strict=True)
    importer = importer_module(a.importer_sha256)
    excluded = set()
    for batch in range(a.start_batch, a.start_batch+a.batches):
        manifests, count = prepare(vault, a.inventory, a.work, batch, a.packages_per_batch, excluded)
        if not manifests:
            print(json.dumps({'state': 'no_registered_candidates_remain'}), flush=True)
            return
        journal = a.work / ('candidate-small-batch-%03d.jsonl' % batch)
        try:
            result = run(vault, manifests, importer, journal, a.apply, extend=True)
        except ValueError as error:
            if not str(error).startswith(('active source catalog references', 'active model catalog references',
                                          'active promotion catalog references')):
                raise
            safe = []
            for manifest in manifests:
                try:
                    run(vault, [manifest], importer, journal, False, extend=True)
                    safe.append(manifest)
                except ValueError as single_error:
                    if not str(single_error).startswith(('active source catalog references', 'active model catalog references',
                                                         'active promotion catalog references')):
                        raise
                    package = json.loads(manifest.read_bytes())['source_root']
                    excluded.add(package.casefold())
                    print(json.dumps({'state': 'skipped_active_catalog_reference', 'package': package}), flush=True)
            if not safe:
                continue
            count = None
            result = run(vault, safe, importer, journal, a.apply, extend=True)
            manifests = safe
        if count is not None and result['files'] != count:
            raise ValueError('batch count differs from prepared manifests')
        if a.apply:
            recovery = json.loads(Path(result['recovery']).read_bytes())
            readers = {}
            for row in recovery['rows']:
                parts = row['old'].split('/')
                package = '/'.join(parts[:3])
                if package not in readers:
                    readers[package] = CandidateSharedReader.from_package(vault, vault/package)
                with readers[package].open('/'.join(parts[3:])) as stream:
                    if hashlib.file_digest(stream, 'sha256').hexdigest() != row['sha256']:
                        raise ValueError('shared readback failed')
            result['shared_readback_files'] = len(recovery['rows'])
        result['batch'] = batch
        result['packages'] = len(manifests)
        print(json.dumps(result, ensure_ascii=False), flush=True)
        if not a.apply:
            return


if __name__ == '__main__':
    main()
