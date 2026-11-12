"""Share and retire byte-identical files in previously unregistered candidates."""
import argparse
import collections
import hashlib
import json
from pathlib import Path

from candidate_shared_reader import CandidateSharedReader
from prepare_candidate_shared_overlays import prepare
from publish_source_support_relinks import importer_module
from retire_exact_unity_overlays import run


def plan(vault, inventory, excluded, limit):
    registry = json.loads((vault/'metadata/unity-shared-overlay-registry.v1.json').read_bytes())
    roots = [x['source_root'].casefold().rstrip('/')+'/' for x in registry['overlays']]
    uses = collections.Counter()
    for record in registry['overlays']:
        raw = (vault/record['manifest']).read_bytes()
        if hashlib.sha256(raw).hexdigest() != record['sha256']:
            raise ValueError('registered overlay changed')
        for item in json.loads(raw)['files']:
            uses[item['vault_path'].casefold()] += 1
    packages = collections.defaultdict(list)
    for group in json.loads(inventory.read_bytes())['groups']:
        paths = sorted(group['paths'], key=str.casefold)
        retained = paths[0]
        if not (vault/retained).is_file():
            raise ValueError('inventory retained copy missing: '+retained)
        for old in paths[1:]:
            parts = old.split('/')
            if len(parts) < 4 or parts[:2] != ['candidates', 'hivemind']:
                raise ValueError('path outside candidate packages')
            package = '/'.join(parts[:3])
            key = package.casefold()+'/'
            if (package.casefold() in excluded or uses[old.casefold()] or
                    any(key.startswith(root) or root.startswith(key) for root in roots) or
                    not (vault/old).is_file()):
                continue
            packages[package].append({'old': old, 'retained': retained,
                                      'sha256': group['sha256'], 'bytes': group['bytes']})
    selected = sorted(packages, key=lambda p: (-len(packages[p]), p))[:limit]
    return [row for package in selected for row in packages[package]]


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', type=Path, required=True)
    p.add_argument('--inventory', type=Path, required=True)
    p.add_argument('--work', type=Path, required=True)
    p.add_argument('--importer-sha256', required=True)
    p.add_argument('--batches', type=int, default=1)
    p.add_argument('--start-batch', type=int, default=1)
    p.add_argument('--packages-per-batch', type=int, default=6)
    p.add_argument('--apply', action='store_true')
    a = p.parse_args()
    if not 1 <= a.batches <= 30 or not 1 <= a.packages_per_batch <= 6:
        raise ValueError('invalid batch size')
    vault = a.vault.resolve(strict=True)
    importer = importer_module(a.importer_sha256)
    excluded = set()
    for batch in range(a.start_batch, a.start_batch+a.batches):
        rows = plan(vault, a.inventory, excluded, a.packages_per_batch)
        if not rows:
            print(json.dumps({'state':'no_unregistered_candidates_remain'}), flush=True)
            break
        plan_path = a.work/('candidate-unregistered-batch-%03d-plan.json' % batch)
        stage = a.work/('candidate-unregistered-batch-%03d-stage' % batch)
        if plan_path.exists() or stage.exists():
            raise ValueError('plan or stage already exists')
        plan_path.write_text(json.dumps({'schema':'openrpg.candidate-dependency-sharing-plan.v1',
                                         'state':'prepared_only', 'rows':rows}, indent=2)+'\n', encoding='utf8')
        index = prepare(vault, plan_path, stage)
        manifests = [stage/x['manifest'] for x in index['packages']]
        journal = a.work/('candidate-unregistered-batch-%03d.jsonl' % batch)
        try:
            result = run(vault, manifests, importer, journal, a.apply)
        except ValueError as error:
            prefixes = ('active source catalog references', 'active model catalog references',
                        'active promotion catalog references')
            if not str(error).startswith(prefixes):
                raise
            safe = []
            for manifest in manifests:
                try:
                    run(vault, [manifest], importer, journal, False)
                    safe.append(manifest)
                except ValueError as single_error:
                    if not str(single_error).startswith(prefixes):
                        raise
                    package = json.loads(manifest.read_bytes())['source_root']
                    excluded.add(package.casefold())
                    print(json.dumps({'state':'skipped_active_catalog_reference', 'package':package}), flush=True)
            if not safe:
                continue
            manifests = safe
            result = run(vault, manifests, importer, journal, a.apply)
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
        result.update(batch=batch, packages=len(manifests))
        print(json.dumps(result, ensure_ascii=False), flush=True)
        if not a.apply:
            break


if __name__ == '__main__':
    main()
