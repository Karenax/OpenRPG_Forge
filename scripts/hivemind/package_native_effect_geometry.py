"""Package captured Unity float32x16/u16 geometry as a private GLB candidate.

Preserves the original stream and indices. Canonical GLB fields explicitly
reflect X, reverse winding and flip texture V; no welding or recalculation.
This does not import a material or qualify the effect in Studio/runtime.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import struct


def digest(data):
    return hashlib.sha256(data).hexdigest()


def pinned(path, expected, maximum):
    if path.stat().st_size > maximum:
        raise ValueError(f"Input exceeds budget: {path}")
    raw = path.read_bytes()
    if digest(raw) != expected:
        raise ValueError(f"Input SHA differs: {path}")
    return raw


def member(root, pin, maximum):
    path = (root / pin['file']).resolve()
    if not path.is_relative_to(root):
        raise ValueError('Geometry input resolves outside manifest root')
    return pinned(path, pin['sha256'], maximum)


def prefab_transform(raw):
    text = raw.decode('utf-8')
    effects = re.findall(r'--- !u!2083052967 &(\d+)\nVisualEffect:\n(.*?)(?=\n---|\Z)', text, re.S)
    if len(effects) != 1:
        raise ValueError('Profile requires one explicit VisualEffect component')
    file_id, effect = effects[0]
    game_object = re.search(r'm_GameObject: \{fileID: (\d+)\}', effect).group(1)
    blocks = re.findall(r'--- !u!4 &\d+\nTransform:\n(.*?)(?=\n---|\Z)', text, re.S)
    blocks = [b for b in blocks if f'm_GameObject: {{fileID: {game_object}}}' in b]
    if len(blocks) != 1 or 'm_Father: {fileID: 0}' not in blocks[0]:
        raise ValueError('Profile requires an explicit root transform')
    def vector(name, keys):
        match = re.search(name + r': \{([^}]+)\}', blocks[0])
        fields = dict(re.findall(r'(\w+): ([^,]+)', match.group(1)))
        values = [float(fields[k]) for k in keys]
        if not all(math.isfinite(v) for v in values):
            raise ValueError('Non-finite author transform')
        return values
    p = vector('m_LocalPosition', 'xyz')
    q = vector('m_LocalRotation', 'xyzw')
    scale = vector('m_LocalScale', 'xyz')
    return file_id, dict(translation=[-p[0], p[1], p[2]],
                        rotation=[q[0], -q[1], -q[2], q[3]], scale=scale)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['manifest', 'prefab', 'output']:
        parser.add_argument('--' + name, type=Path, required=True)
    for name in ['manifest-sha256', 'prefab-sha256', 'meta-sha256']:
        parser.add_argument('--' + name, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise ValueError('Preserve prior outputs; choose a fresh directory')
    manifest = pinned(args.manifest, args.manifest_sha256, 1024 * 1024)
    document = json.loads(manifest)
    root = args.manifest.resolve().parent
    stream = member(root, document['mesh'], 64 * 1024 * 1024)
    indices = member(root, document['indices'], 64 * 1024 * 1024)
    prefab = pinned(args.prefab, args.prefab_sha256, 1024 * 1024)
    meta_path = Path(str(args.prefab) + '.meta')
    meta = pinned(meta_path, args.meta_sha256, 1024 * 1024)
    guid = re.search(r'^guid: ([0-9a-f]{32})$', meta.decode(), re.M).group(1)
    component, transform = prefab_transform(prefab)
    if not stream or len(stream) % 64 or len(indices) % 6:
        raise ValueError('Captured float32x16/u16 triangle profile differs')
    count = len(stream) // 64
    if count != document['vertex_projection']['vertex_count']:
        raise ValueError('Projection and native mesh counts differ')
    values = list(struct.iter_unpack('<16f', stream))
    if not all(math.isfinite(v) for row in values for v in row):
        raise ValueError('Non-finite native geometry')
    if any(row[9] not in (-1., 1.) for row in values):
        raise ValueError('Native tangent handedness is not explicit')
    native_indices = list(struct.unpack('<' + 'H' * (len(indices) // 2), indices))
    if any(i >= count for i in native_indices):
        raise ValueError('Native triangle index outside vertex stream')
    canonical = bytearray()
    for row in values:
        converted = list(row)
        for lane in [0, 3, 6, 9]:
            converted[lane] = -converted[lane]
        for lane in [11, 13, 15]:
            converted[lane] = 1. - converted[lane]
        canonical.extend(struct.pack('<16f', *converted))
    target_indices = []
    for i in range(0, len(native_indices), 3):
        a, b, c = native_indices[i:i + 3]
        target_indices.extend([a, c, b])
    target_indices = struct.pack('<' + 'H' * len(target_indices), *target_indices)
    binary = bytearray()
    views = []
    def view(data, **fields):
        binary.extend(b'\0' * (-len(binary) % 4))
        index = len(views)
        views.append(dict(buffer=0, byteOffset=len(binary), byteLength=len(data), **fields))
        binary.extend(data)
        return index
    geometry_view = view(canonical, byteStride=64, target=34962)
    index_view = view(target_indices, target=34963)
    raw_view = view(stream)
    raw_indices_view = view(indices)
    prefab_view = view(prefab)
    meta_view = view(meta)
    accessors = []
    attributes = {}
    for name, lane, kind in [('POSITION', 0, 'VEC3'), ('NORMAL', 3, 'VEC3'),
                             ('TANGENT', 6, 'VEC4'), ('TEXCOORD_0', 10, 'VEC2'),
                             ('TEXCOORD_1', 12, 'VEC2'), ('TEXCOORD_2', 14, 'VEC2')]:
        attributes[name] = len(accessors)
        accessor = dict(bufferView=geometry_view, byteOffset=lane * 4,
                        componentType=5126, count=count, type=kind)
        if name == 'POSITION':
            positions = [(-row[0], row[1], row[2]) for row in values]
            accessor.update(min=[min(v[k] for v in positions) for k in range(3)],
                            max=[max(v[k] for v in positions) for k in range(3)])
        accessors.append(accessor)
    index_accessor = len(accessors)
    accessors.append(dict(bufferView=index_view, componentType=5123,
                          count=len(native_indices), type='SCALAR'))
    gltf = dict(asset=dict(version='2.0', generator='Forge native effect geometry transport'),
                scene=0, scenes=[dict(nodes=[0])],
                nodes=[dict(name=args.prefab.stem, mesh=0, **transform)],
                meshes=[dict(primitives=[dict(attributes=attributes, indices=index_accessor, mode=4)])],
                accessors=accessors, bufferViews=views, buffers=[dict(byteLength=len(binary))],
                extras=dict(private_only=True, qualification='geometry_candidate_only',
                            source_component_guid=guid, source_component_file_id=component,
                            original_stream=dict(buffer_view=raw_view, sha256=digest(stream)),
                            original_indices=dict(buffer_view=raw_indices_view, sha256=digest(indices)),
                            source_prefab=dict(buffer_view=prefab_view, sha256=digest(prefab)),
                            source_meta=dict(buffer_view=meta_view, sha256=digest(meta)),
                            source_to_canonical_basis=[[-1, 0, 0], [0, 1, 0], [0, 0, 1]],
                            uv_v_transform='1-v', winding='reversed_once_for_reflection',
                            licence_ref='LicenseRef-Unity-Asset-Store-EULA',
                            material_required=True, effect_runtime_qualified=False))
    encoded = json.dumps(gltf, separators=(',', ':')).encode()
    encoded += b' ' * (-len(encoded) % 4)
    binary.extend(b'\0' * (-len(binary) % 4))
    result = (struct.pack('<III', 0x46546c67, 2, 28 + len(encoded) + len(binary))
              + struct.pack('<II', len(encoded), 0x4e4f534a) + encoded
              + struct.pack('<II', len(binary), 0x004e4942) + binary)
    args.output.mkdir(parents=True)
    output = args.output / 'native-effect-geometry.candidate.glb'
    output.write_bytes(result)
    receipt = dict(status='NATIVE_VERTEX_ORDER_GEOMETRY_CANDIDATE_PACKAGED',
                   input_manifest_sha256=digest(manifest), output=str(output), output_sha256=digest(result),
                   native_vertices=count, native_triangles=len(native_indices) // 3,
                   source_stream_sha256=digest(stream), source_indices_sha256=digest(indices),
                   source_prefab_sha256=digest(prefab), source_meta_sha256=digest(meta),
                   converter_sha256=digest(Path(__file__).read_bytes()),
                   source_component_guid=guid, source_component_file_id=component,
                   welding=False, recalculated_attributes=False, renderer_qualified=False,
                   material_imported=False, Studio_integrated=False, goal_complete=False)
    (args.output / 'PACKAGING.v1.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print(json.dumps(receipt))


if __name__ == '__main__':
    main()
