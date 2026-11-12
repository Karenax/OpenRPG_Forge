"""Retire only verified, relinked source copies; retain exact recovery mappings."""
import argparse
import contextlib
import hashlib
import json
import os
from pathlib import Path
import shutil

import vault_storage as storage
from exclusive_source_delete import ExclusiveSource
from publish_source_support_relinks import importer_module
from publish_variant_transition import exclusive_storage_lock
from stage_source_support_relinks import norm
from audit_active_archive_references import catalog_references


def strings(value):
    if isinstance(value, str):
        yield value.replace('\\', '/').casefold().removeprefix('//?/')
    elif isinstance(value, dict):
        for x in value.values():
            yield from strings(x)
    elif isinstance(value, list):
        for x in value:
            yield from strings(x)


def contains_reference(document, document_path, vault, targets):
    """Match absolute, Vault-relative and document-relative filesystem paths."""
    qualified=targets | {(vault.as_posix()+'/'+x).casefold() for x in targets}
    suffixes={Path(x).suffix.casefold() for x in targets if Path(x).suffix}
    def contextual_strings(value, context):
        if isinstance(value,dict):
            catalog=value.get('source_catalog')
            path=Path(catalog) if isinstance(catalog,str) else None
            if path is not None and path.is_absolute() and path.is_relative_to(vault):
                context=path
            for child in value.values():yield from contextual_strings(child,context)
        elif isinstance(value,list):
            for child in value:yield from contextual_strings(child,context)
        elif isinstance(value,str):
            yield value.replace('\\','/').casefold().removeprefix('//?/'),context
    for value,context in contextual_strings(document,document_path):
        if value in qualified:return True
        if not suffixes or not any(value.endswith(ext) for ext in suffixes):continue
        path=Path(value)
        if path.is_absolute():continue
        # Package manifests nested below their package root may still express
        # paths relative to that root, not relative to the manifest directory.
        # Treat any ancestor within the Vault as a possible base: false
        # positives block cleanup, whereas missing a live reference loses data.
        bases = [vault]
        bases.extend(parent for parent in context.parents
                     if parent != vault and parent.is_relative_to(vault))
        for base in bases:
            candidate=os.path.normpath(str(base/path)).replace('\\','/').casefold()
            if candidate in qualified:return True
    return False


def validate_scope(vault, plan, proof, binary):
    if plan.get('schema') != 'openrpg.source-support-relinks-stage.v1' or plan.get('state') != 'prepared_only':
        raise ValueError('invalid relink plan')
    rows = plan['paths']
    old = set()
    for row in rows:
        a, b = norm(row['old']), norm(row['retained'])
        hive = 'originals/unity-purchased/hivemind/'
        haunted = 'originals/hv427-v1/assets/hivemind/hauntedvillage/'
        dungeon_retained = 'normalized/hivemind/dungeon-variant-0a3b8a177b40ffbb-source-support-v1/source/assets/hivemind/mysticdungeon/urp/art/prefabs/'
        dungeon_quarantine = 'quarantine/hivemind-dungeon-builder-hdrp-crystal-exact12-blocked-v1/source/assets/hivemind/mysticdungeon/hdrp(default)/art/prefabs/'
        permitted = ((a.startswith(hive) and (b.startswith(hive) or b.startswith(haunted)))
                     or ((a.startswith(hive) or a.startswith(dungeon_quarantine)) and b.startswith(dungeon_retained))
                     or (a.startswith('normalized/hivemind/') and b.startswith('normalized/hivemind/')
                         and a.endswith('.png') and b.endswith('.png')))
        if not permitted:
            raise ValueError('source outside authorized Hivemind scope')
        # Extracted Unity Assets contain GUID-based references which are not
        # captured by exact-path catalogue checks. Do not remove their files or
        # .meta companions until a separately qualified migration preserves
        # resolution for every affected Unity tree and consumer.
        if '/source/' in a and '/assets/' in '/'+a.split('/source/', 1)[1]:
            raise ValueError('Unity asset tree requires qualified GUID-preserving migration')
        if a == b or a in old or not storage.valid_hex(row['sha256'], 64):
            raise ValueError('invalid or repeated source mapping')
        old.add(a)
    if not old or any(norm(x['retained']) in old for x in rows):
        raise ValueError('source retention mapping overlaps retired paths')
    if proof.get('state') != 'passed' or proof.get('old_paths_locked') != len(rows):
        raise ValueError('exclusive native reader proof missing')
    if storage.sha(binary.read_bytes()) != proof.get('binary_sha256'):
        raise ValueError('native reader binary changed')
    return old


def active_references(vault, targets):
    source = storage.confined(vault, vault/'metadata/asset-source-support-registry.v1.json')
    asset = storage.confined(vault, vault/'metadata/asset-import-registry.v1.json')
    sb, ab = source.read_bytes(), asset.read_bytes()
    sp, ap = json.loads(sb), json.loads(ab)
    raw = storage.confined(vault, vault/sp['path']).read_bytes()
    if storage.sha(raw) != sp['sha256']:
        raise ValueError('active source catalog changed')
    if contains_reference(json.loads(raw),vault/sp['path'],vault,targets):
        raise ValueError('active source catalog references a candidate')
    catalogs = {}
    for pack, entry in sorted(ap['packs'].items()):
        for name in ('details.v1.json', 'profiles.v1.json'):
            document_path=storage.confined(vault, vault/entry['path']/name)
            raw = document_path.read_bytes()
            if storage.sha(raw) != entry['files'][name]:
                raise ValueError('active model catalog changed: '+pack)
            doc = json.loads(raw)
            if contains_reference(doc,document_path,vault,targets):
                raise ValueError('active model catalog references a candidate')
            if name == 'details.v1.json':
                for reference in set(catalog_references(doc)):
                    catalog = storage.confined(vault, Path(reference))
                    if catalog not in catalogs:
                        content = catalog.read_bytes()
                        catalogs[catalog] = storage.sha(content)
                        if contains_reference(json.loads(content),catalog,vault,targets):
                            raise ValueError('active promotion catalog references a candidate')
    for catalog, pin in catalogs.items():
        if storage.sha(catalog.read_bytes()) != pin:
            raise ValueError('promotion catalog changed during active reference scan')
    if source.read_bytes() != sb or asset.read_bytes() != ab:
        raise ValueError('registry changed during active reference scan')
    return sb, ab


def run(vault, plan_path, proof_path, binary, importer, journal, remove):
    vault = vault.resolve(strict=True)
    plan_raw, proof_raw = plan_path.read_bytes(), proof_path.read_bytes()
    plan, proof = json.loads(plan_raw), json.loads(proof_raw)
    targets = validate_scope(vault, plan, proof, binary)
    if storage.sha(plan_raw) != proof.get('plan_sha256'):
        raise ValueError('exclusive reader proof is stale')
    if journal.exists():
        raise ValueError('journal already exists')
    before = shutil.disk_usage(vault).free
    with importer.shared_import_lock(vault), exclusive_storage_lock(vault), contextlib.ExitStack() as stack:
        source_raw, asset_raw = active_references(vault, targets)
        if (storage.sha(source_raw) != proof.get('source_registry_sha256')
                or json.loads(source_raw).get('sha256') != plan['staged_catalog_sha256']
                or storage.sha(asset_raw) != plan['asset_registry_sha256']):
            raise ValueError('active catalog differs from qualified transition')
        old_handles, kept_handles = {}, {}
        for row in plan['paths']:
            old_path = storage.confined(vault, vault/row['old'])
            kept_path = storage.confined(vault, vault/row['retained'])
            if old_path.name.casefold().endswith('.meta'):
                companion = old_path.with_name(old_path.name[:-5])
                companion_relative = norm(companion.relative_to(vault).as_posix())
                if companion.exists() and companion_relative not in targets:
                    raise ValueError('Unity metadata companion remains; qualify reconstruction before retirement')
            if storage.windows_readonly(old_path.stat()):
                raise ValueError('read-only candidate requires separate inspection')
            old_handles[row['old']] = stack.enter_context(ExclusiveSource(old_path))
            if kept_path not in kept_handles:
                kept_handles[kept_path] = stack.enter_context(storage.locked_source(kept_path, share_delete=False))
        for row in plan['paths']:
            if old_handles[row['old']].sha256() != row['sha256']:
                raise ValueError('old source SHA changed: '+row['old'])
            stream = kept_handles[vault/row['retained']]
            stream.seek(0)
            if hashlib.file_digest(stream, 'sha256').hexdigest() != row['sha256']:
                raise ValueError('retained source SHA changed: '+row['retained'])
        if (vault/'metadata/asset-source-support-registry.v1.json').read_bytes() != source_raw or (vault/'metadata/asset-import-registry.v1.json').read_bytes() != asset_raw:
            raise ValueError('registry changed before retirement')
        if plan_path.read_bytes() != plan_raw or proof_path.read_bytes() != proof_raw:
            raise ValueError('plan or proof changed')
        if not remove:
            return {'state': 'dry_run_passed', 'candidates': len(targets), 'removed': 0}
        # Persist only the compact recovery mapping, not the large stage catalog.
        txdir = storage.confined(vault, vault/'metadata/asset-source-support-transactions')
        recovery = txdir/('relinks-'+storage.sha(plan_raw)+'.json')
        if recovery.exists():
            if storage.confined(vault, recovery).read_bytes() != plan_raw:
                raise ValueError('recovery mapping conflict')
        else:
            with recovery.open('xb') as out:
                out.write(plan_raw)
                out.flush()
                os.fsync(out.fileno())
            if recovery.read_bytes() != plan_raw:
                raise ValueError('recovery mapping readback failed')
        with journal.open('x', encoding='utf8', newline='\n') as log:
            def record(item):
                log.write(json.dumps(item, ensure_ascii=False)+'\n')
                log.flush()
                os.fsync(log.fileno())
            record({'state':'prepared','count':len(targets),'recovery':str(recovery),'free_before':before})
            for row in plan['paths']:
                record({'state':'verified','old':row['old'],'retained':row['retained'],'sha256':row['sha256']})
                old_handles[row['old']].mark_delete()
                record({'state':'marked_delete','old':row['old']})
            record({'state':'marked_pending_handle_close','count':len(targets)})
    after = shutil.disk_usage(vault).free
    with journal.open('a', encoding='utf8', newline='\n') as log:
        log.write(json.dumps({'state':'handles_closed','free_after':after})+'\n')
        log.flush()
        os.fsync(log.fileno())
    return {'state':'removed_pending_verification', 'removed':len(targets), 'recovery':str(recovery),
            'free_before':before, 'free_after':after, 'free_delta_observed':after-before}


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    for n in ('vault', 'plan', 'proof', 'binary', 'journal', 'output'):
        p.add_argument('--'+n, type=Path, required=True)
    p.add_argument('--importer-sha256', required=True)
    p.add_argument('--remove', action='store_true')
    a = p.parse_args()
    if a.output.exists(): p.error('output already exists')
    importer = importer_module(a.importer_sha256)
    result = run(a.vault, a.plan, a.proof, a.binary, importer, a.journal, a.remove)
    with a.output.open('x', encoding='utf8', newline='\n') as f:
        json.dump(result, f, ensure_ascii=False, indent=2)
        f.write('\n')
    print(json.dumps(result, ensure_ascii=False))
