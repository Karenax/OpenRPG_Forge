"""Resolve historical candidate model recipes and stream their Vault blocks."""
import contextlib
import hashlib
import json
from pathlib import Path
import zlib

import vault_storage as storage


def load_recipes(vault, wanted):
    """Return verified embedded-descriptor recipes for retired IDs and SHAs."""
    vault = vault.resolve(strict=True)
    registry = storage.confined(vault, vault / 'metadata/asset-import-registry.v1.json')
    before = registry.read_bytes()
    pointer = json.loads(before)
    found = {}
    for entry in pointer['packs'].values():
        path = storage.confined(vault, vault / entry['path'] / 'details.v1.json')
        raw = path.read_bytes()
        if storage.sha(raw) != entry['files']['details.v1.json']:
            raise ValueError('active model metadata changed')
        for owner in json.loads(raw)['assets']:
            for alias in owner.get('consolidated_variants', []):
                ident = alias.get('retired_id')
                if ident not in wanted:
                    continue
                pin = wanted[ident]
                recipe = alias.get('legacy_recipe', {})
                detail, profile = alias.get('original_detail', {}), alias.get('original_profile', {})
                if (ident in found or alias.get('retired_model_sha256') != pin or
                        detail.get('id') != ident or detail.get('model_sha256') != pin or
                        profile.get('external_id') != ident or
                        recipe.get('schema') != 'openrpg.legacy-vault-descriptor.v1' or
                        recipe.get('logical_sha256') != pin):
                    raise ValueError('historical candidate identity or recipe differs')
                descriptor = recipe.get('descriptor')
                storage.validate_descriptor(descriptor, storage.MAX_MODEL)
                if descriptor['sha256'] != pin or descriptor['bytes'] != recipe.get('logical_bytes'):
                    raise ValueError('historical candidate descriptor differs')
                found[ident] = recipe
    if registry.read_bytes() != before or set(found) != set(wanted):
        raise ValueError('historical candidates not fully resolved or registry changed')
    return found, storage.sha(before)


def chunks(vault, recipe, expected_sha256):
    """Verify and stream an embedded descriptor using the immutable Vault packs."""
    vault = vault.resolve(strict=True)
    if (recipe.get('schema') != 'openrpg.legacy-vault-descriptor.v1' or
            recipe.get('logical_sha256') != expected_sha256):
        raise ValueError('invalid historical candidate recipe')
    descriptor = recipe.get('descriptor')
    storage.validate_descriptor(descriptor, storage.MAX_MODEL)
    if descriptor['sha256'] != expected_sha256 or descriptor['bytes'] != recipe.get('logical_bytes'):
        raise ValueError('historical candidate descriptor mismatch')
    marker = json.loads(storage.confined(vault, vault / '.forge-storage/root.v1.json').read_bytes())
    if marker != {'schema': 1, 'vault_id': descriptor['vault_id']}:
        raise ValueError('historical candidate Vault identity differs')
    whole = hashlib.sha256()
    total = 0
    with contextlib.ExitStack() as stack:
        handles = {}
        for item in descriptor['segments']:
            key = item['pack']
            if key not in handles:
                if len(handles) >= 16:
                    for stream in handles.values():
                        stream.close()
                    handles.clear()
                path = storage.confined(vault, vault / '.forge-storage/packs' / (key + '.pack'))
                handles[key] = stack.enter_context(storage.locked_source(path, share_delete=False))
            stream = handles[key]
            stream.seek(item['offset'])
            stored = stream.read(item['stored_bytes'])
            if len(stored) != item['stored_bytes']:
                raise ValueError('historical candidate segment truncated')
            if item['codec'] == 'zlib':
                decoder = zlib.decompressobj()
                block = decoder.decompress(stored, item['bytes'] + 1)
                if not decoder.eof or decoder.unused_data or decoder.unconsumed_tail:
                    raise ValueError('invalid historical candidate compression')
            else:
                block = stored
            if len(block) != item['bytes'] or storage.sha(block) != item['sha256']:
                raise ValueError('historical candidate block SHA differs')
            whole.update(block)
            total += len(block)
            yield block
    if total != descriptor['bytes'] or whole.hexdigest() != expected_sha256:
        raise ValueError('historical candidate logical SHA differs')
