"""Assemble verified, disjoint prepared pack stages without rereading models.

This only copies metadata to the authorized conversion workspace. The normal
stage verifier and transactional publisher still recheck the result and pins.
"""
import argparse
import hashlib
import json
from pathlib import Path

import vault_storage
from prepare_variant_metadata_transition import encoded


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def compose(stages, plan, output, allowed_root=None):
    allowed = (allowed_root or Path(r'E:\OpenRPG_Forge\.build\asset-conversion')).resolve(strict=True)
    root = output.parent.resolve(strict=True)
    if not root.is_relative_to(allowed) or output.exists():
        raise ValueError('invalid or existing output stage')
    vault_storage.confined(allowed, root)
    aliases, packs, contents, source_pins = [], {}, {}, []
    for source in stages:
        source = source.resolve(strict=True)
        if not source.is_relative_to(allowed):
            raise ValueError('stage outside work root')
        vault_storage.confined(allowed, source)
        raw_manifest = vault_storage.confined(allowed, source / 'transition.json').read_bytes()
        manifest = json.loads(raw_manifest)
        if (manifest.get('state') != 'prepared_only' or not manifest.get('includes_legacy_recipes')
                or manifest.get('registry_sha256') != plan['registry_sha256']):
            raise ValueError('source stage is stale or incomplete')
        aliases.extend(manifest['aliases_requiring_project_migration'])
        source_pins.append({'path': str(source), 'manifest_sha256': sha(raw_manifest)})
        for pack, pins in manifest['packs'].items():
            if pack in packs or not pack or any(c not in 'abcdefghijklmnopqrstuvwxyz0123456789.-_' for c in pack):
                raise ValueError('overlapping or invalid pack')
            packs[pack] = pins
            for name in ('details.v1.json', 'profiles.v1.json'):
                raw = vault_storage.confined(allowed, source / pack / name).read_bytes()
                if sha(raw) != pins[name]:
                    raise ValueError('source staged file changed')
                contents[(pack, name)] = raw
    keys = ('retained_id', 'retired_id', 'retained_uuid', 'retired_uuid', 'retained_sha256', 'retired_sha256')
    expected = [{key: row[key] for key in keys} for row in plan['candidates']]
    if aliases != expected or plan.get('candidate_count') != len(expected):
        raise ValueError('stage aliases do not match composed plan')
    output.mkdir()
    for (pack, name), raw in contents.items():
        dest = output / pack
        dest.mkdir(exist_ok=True)
        with (dest / name).open('xb') as stream:
            stream.write(raw)
    manifest = {'schema': 'openrpg.variant-metadata-transition.v1', 'state': 'prepared_only',
                'registry_sha256': plan['registry_sha256'], 'includes_legacy_recipes': True,
                'aliases_requiring_project_migration': aliases, 'packs': packs,
                'not_ready_reasons': ['publication and runtime qualification remain pending'],
                'composed_sources': source_pins}
    with (output / 'transition.json').open('xb') as stream:
        stream.write(encoded(manifest))
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--stage', type=Path, action='append', required=True)
    parser.add_argument('--plan', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    manifest = compose(args.stage, json.loads(args.plan.read_bytes()), args.output)
    print(json.dumps({'state': manifest['state'], 'packs': len(manifest['packs']),
                      'aliases': len(manifest['aliases_requiring_project_migration'])}))
