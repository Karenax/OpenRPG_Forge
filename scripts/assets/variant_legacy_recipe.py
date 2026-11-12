"""Create a checked recipe to reconstruct a retired GLB without retaining its payload."""
import hashlib
import struct

# Matches Studio's native model transport budget; this does not expand export,
# renderer, decoded-resource, or web budgets.
MAX_CONSOLIDATION_MODEL_BYTES = 1024 * 1024 * 1024
MAX_INLINE_MODEL_BYTES = 128 * 1024

def sha(data):
    return hashlib.sha256(data).hexdigest()


def split_glb(raw):
    if len(raw) < 28 or raw[:4] != b'glTF':
        raise ValueError('invalid GLB header')
    version, total, json_len, kind = struct.unpack_from('<IIII', raw, 4)
    if version != 2 or total != len(raw) or kind != 0x4e4f534a or json_len % 4:
        raise ValueError('invalid GLB metadata chunk')
    end = 20 + json_len
    if end + 8 > len(raw):
        raise ValueError('truncated GLB')
    tail_len, tail_kind = struct.unpack_from('<II', raw, end)
    if tail_kind != 0x004e4942 or end + 8 + tail_len != len(raw):
        raise ValueError('expected exactly one binary chunk')
    return raw[:end], memoryview(raw)[end:]


def build_recipe(retained, retired, retained_sha, retired_sha):
    if sha(retained) != retained_sha or sha(retired) != retired_sha:
        raise ValueError('model SHA differs from planned pin')
    _, retained_tail = split_glb(retained)
    retired_prefix, retired_tail = split_glb(retired)
    if retained_tail != retired_tail:
        raise ValueError('binary payloads differ')
    if len(retired_prefix) > 16 * 1024 * 1024:
        raise ValueError('legacy metadata exceeds budget')
    reconstructed = hashlib.sha256(retired_prefix)
    reconstructed.update(retained_tail)
    if reconstructed.hexdigest() != retired_sha:
        raise ValueError('reconstruction SHA mismatch')
    return {'schema': 'openrpg.legacy-glb-recipe.v1',
            'prefix_hex': retired_prefix.hex(),
            'shared_tail_sha256': sha(retained_tail),
            'logical_sha256': retired_sha, 'logical_bytes': len(retired)}


def build_descriptor_recipe(vault, retired_path, retired_sha):
    import vault_storage
    descriptor = vault_storage.read_descriptor(vault, retired_path, MAX_CONSOLIDATION_MODEL_BYTES)
    if descriptor is None or descriptor['sha256'] != retired_sha:
        raise ValueError('legacy model must be verified shared storage')
    if vault_storage.digest(vault, retired_path, MAX_CONSOLIDATION_MODEL_BYTES) != retired_sha:
        raise ValueError('legacy descriptor reconstruction SHA mismatch')
    return {'schema': 'openrpg.legacy-vault-descriptor.v1', 'descriptor': descriptor,
            'logical_sha256': retired_sha, 'logical_bytes': descriptor['bytes']}


def build_inline_recipe(retired, retired_sha):
    if not 0 < len(retired) <= MAX_INLINE_MODEL_BYTES or sha(retired) != retired_sha:
        raise ValueError('inline model exceeds budget or SHA differs')
    split_glb(retired)
    return {'schema': 'openrpg.legacy-inline-glb.v1', 'data_hex': retired.hex(),
            'logical_sha256': retired_sha, 'logical_bytes': len(retired)}


def verify_recipe(retained, recipe, retained_sha, retired_sha, vault=None, retired_path=None):
    if sha(retained) != retained_sha:
        raise ValueError('retained SHA mismatch')
    if recipe.get('schema') == 'openrpg.legacy-inline-glb.v1':
        import vault_storage
        if vault is None or retired_path is None:
            raise ValueError('vault and legacy model path required')
        text = recipe.get('data_hex')
        if (not isinstance(text, str) or len(text) % 2 or len(text) > 2 * MAX_INLINE_MODEL_BYTES
                or any(c not in '0123456789abcdef' for c in text)):
            raise ValueError('invalid inline model')
        raw = bytes.fromhex(text)
        if build_inline_recipe(raw, retired_sha) != recipe:
            raise ValueError('inline model differs from qualified recipe')
        if vault_storage.digest(vault, retired_path, MAX_INLINE_MODEL_BYTES) != retired_sha:
            raise ValueError('legacy inline source SHA mismatch')
        return
    if recipe.get('schema') == 'openrpg.legacy-vault-descriptor.v1':
        if vault is None or retired_path is None:
            raise ValueError('vault and legacy descriptor path required')
        if recipe != build_descriptor_recipe(vault, retired_path, retired_sha):
            raise ValueError('legacy descriptor differs from qualified file')
        return
    if recipe.get('schema') != 'openrpg.legacy-glb-recipe.v1':
        raise ValueError('retained SHA or recipe schema mismatch')
    if recipe.get('logical_sha256') != retired_sha:
        raise ValueError('retired SHA mismatch')
    text = recipe.get('prefix_hex')
    if (not isinstance(text, str) or len(text) % 2 or len(text) > 32 * 1024 * 1024
            or any(c not in '0123456789abcdef' for c in text)):
        raise ValueError('invalid prefix budget')
    try:
        prefix = bytes.fromhex(text)
    except ValueError as error:
        raise ValueError('invalid prefix encoding') from error
    if len(prefix) < 20 or prefix[:4] != b'glTF':
        raise ValueError('invalid prefix')
    version, total, json_len, kind = struct.unpack_from('<IIII', prefix, 4)
    if version != 2 or kind != 0x4e4f534a or json_len % 4 or len(prefix) != 20 + json_len:
        raise ValueError('invalid prefix header')
    _, tail = split_glb(retained)
    if sha(tail) != recipe.get('shared_tail_sha256'):
        raise ValueError('shared tail SHA mismatch')
    if total != len(prefix) + len(tail) or total != recipe.get('logical_bytes'):
        raise ValueError('logical size mismatch')
    digest = hashlib.sha256(prefix)
    digest.update(tail)
    if digest.hexdigest() != retired_sha:
        raise ValueError('reconstruction SHA mismatch')
