"""Verify selected extracted Unity sources against their preserved parent archive."""
import argparse
import collections
import gzip
import hashlib
import json
import re
import tarfile
from pathlib import Path

import vault_storage as storage
from stage_source_support_relinks import norm


def audit(vault, blocks_report, archive_suffix, include_all=False, infer_direct=False):
    vault = vault.resolve(strict=True)
    report_raw = blocks_report.read_bytes()
    report = json.loads(report_raw)
    pointer = storage.confined(vault, vault/'metadata/asset-source-support-registry.v1.json')
    pointer_raw = pointer.read_bytes()
    if storage.sha(pointer_raw) != report['source_registry_sha256']:
        raise ValueError('source registry changed')
    pointer_doc = json.loads(pointer_raw)
    catalog_raw = storage.confined(vault, vault/pointer_doc['path']).read_bytes()
    if storage.sha(catalog_raw) != pointer_doc['sha256']:
        raise ValueError('catalogue hash mismatch')
    active_cards = json.loads(catalog_raw)['assets']
    cards = {c['id']: c for c in active_cards}
    for owner in active_cards:
        for alias in owner.get('consolidated_sources', []):
            original = alias['original_detail']
            ident = alias['retired_id']
            if ident != original.get('id') or ident in cards:
                raise ValueError('duplicate or invalid source alias')
            cards[ident] = original
    wanted = {}
    excluded = []
    archive_path = None
    archive_sha = None
    for item in report['results']:
        if item['state'] != 'compared':
            raise ValueError('block audit incomplete')
        changed = [k for k, v in item['blocks_by_class'].items() if any(s != 'equal' for s in v)]
        if not include_all and changed != ['114']:
            continue
        for hdrp, _ in item['card_pairs']:
            card = cards[hdrp]
            if not card['source_archive'].endswith(archive_suffix):
                continue
            if card.get('source_direct_archive_is_nested') is not False and not (infer_direct and card.get('source_direct_archive_is_nested') is None):
                excluded.append({'id':hdrp,'reason':'direct archive provenance not explicitly established'})
                continue
            guid = card.get('source_guid')
            if infer_direct:
                norm(card['source_meta_file'])
                meta = storage.confined(vault, vault/card['source_meta_file'])
                with storage.locked_source(meta, share_delete=False) as f:
                    meta_raw = f.read(2*1024*1024+1)
                if len(meta_raw)>2*1024*1024 or storage.sha(meta_raw)!=card['source_meta_sha256']:
                    raise ValueError('source metadata hash differs')
                matches = re.findall(rb'(?m)^guid:\s*([0-9a-f]{32})\s*$',meta_raw)
                if len(matches)!=1:
                    raise ValueError('source metadata GUID missing or ambiguous')
                inferred = matches[0].decode('ascii')
                if guid is not None and guid != inferred:
                    raise ValueError('declared source GUID differs from metadata')
                guid = inferred
            if not isinstance(guid, str) or not re.fullmatch('[0-9a-f]{32}', guid):
                raise ValueError('invalid source GUID')
            row = {k: card[k] for k in ('id','source_file','source_sha256','source_meta_file',
                                         'source_meta_sha256','source_asset_path')}
            if guid in wanted and any({k:v for k,v in old.items() if k!='id'} != {k:v for k,v in row.items() if k!='id'} for old in wanted[guid]):
                raise ValueError('ambiguous repeated GUID')
            wanted.setdefault(guid, []).append(row)
            if archive_path is None:
                archive_path, archive_sha = card['source_archive'], card['source_archive_sha256']
            if (archive_path, archive_sha) != (card['source_archive'], card['source_archive_sha256']):
                raise ValueError('ambiguous archive selection')
    if not wanted:
        raise ValueError('no matching source assets')
    norm(archive_path)
    archive = storage.confined(vault, vault/archive_path)
    results = {guid: {} for guid in wanted}
    with storage.locked_source(archive, share_delete=False) as source:
        if hashlib.file_digest(source, 'sha256').hexdigest() != archive_sha:
            raise ValueError('archive SHA mismatch')
        source.seek(0)
        # gzip.GzipFile handles optional gzip header fields that tarfile's
        # streaming gzip autodetection does not consistently parse.
        with gzip.GzipFile(fileobj=source, mode='rb') as uncompressed, tarfile.open(fileobj=uncompressed, mode='r|') as tar:
            for member in tar:
                parts = member.name.replace('\\','/').split('/')
                if len(parts) != 2 or parts[0] not in wanted or parts[1] not in ('asset', 'asset.meta', 'pathname'):
                    continue
                if not member.isfile() or member.size > 512*1024*1024:
                    raise ValueError('invalid archive member')
                guid, kind = parts
                if kind in results[guid]:
                    raise ValueError('duplicate archive member')
                payload = tar.extractfile(member)
                if payload is None:
                    raise ValueError('archive member unreadable')
                if kind == 'pathname':
                    if member.size > 8192:
                        raise ValueError('oversized Unity pathname')
                    text = payload.read().decode('utf8')
                    # Some Unity packages append a separate literal `00` line.
                    # Accept only that observed trailer, never arbitrary text.
                    lines = text.splitlines()
                    if len(lines) == 2 and lines[1] == '00':
                        lines.pop()
                    if len(lines) != 1 or not lines[0]:
                        raise ValueError('invalid Unity pathname contents')
                    results[guid][kind] = lines[0]
                else:
                    results[guid][kind] = hashlib.file_digest(payload, 'sha256').hexdigest()
        source.seek(0)
        if hashlib.file_digest(source, 'sha256').hexdigest() != archive_sha:
            raise ValueError('archive changed during scan')
    for guid, rows in wanted.items():
        row = rows[0]
        found = results[guid]
        if set(found) != {'asset','asset.meta','pathname'}:
            raise ValueError('archive member missing for '+guid)
        if found['asset'] != row['source_sha256'] or found['asset.meta'] != row['source_meta_sha256']:
            raise ValueError('archive source hash mismatch for '+guid)
        if found['pathname'].replace('\\','/').casefold() != row['source_asset_path'].replace('\\','/').casefold():
            raise ValueError('archive pathname mismatch for '+guid)
        for key, pin in (('source_file','source_sha256'),('source_meta_file','source_meta_sha256')):
            norm(row[key])
            path = storage.confined(vault, vault/row[key])
            with storage.locked_source(path, share_delete=False) as stream:
                if hashlib.file_digest(stream, 'sha256').hexdigest() != row[pin]:
                    raise ValueError('extracted source changed for '+guid)
    if pointer.read_bytes() != pointer_raw or blocks_report.read_bytes() != report_raw:
        raise ValueError('inputs changed')
    return {'state': 'passed', 'archive': archive_path, 'archive_sha256': archive_sha,
            'source_registry_sha256': storage.sha(pointer_raw), 'count': sum(len(rows) for rows in wanted.values()),
            'assets': [{'guid': guid, **row} for guid, rows in wanted.items() for row in rows], 'excluded': excluded,
            'limitations': 'Archive reconstructibility only; no references changed or files removed.'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('vault','blocks-report','output'):
        parser.add_argument('--'+name, type=Path, required=True)
    parser.add_argument('--archive-suffix', required=True)
    parser.add_argument('--include-all', action='store_true', help='Include other compared Unity source classes')
    parser.add_argument('--infer-direct', action='store_true', help='Verify GUID from pinned metadata when direct provenance is absent')
    args = parser.parse_args()
    result = audit(args.vault, args.blocks_report, args.archive_suffix, args.include_all, args.infer_direct)
    with args.output.open('x', encoding='utf8') as f:
        json.dump(result, f, ensure_ascii=False, indent=2)
        f.write('\n')
    print(json.dumps({'state': result['state'], 'count': result['count']}))
