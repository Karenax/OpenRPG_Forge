"""Package original-frame texture captures with immutable source binding proof.

Requested slots must be authored PNG textures, never scene render targets.
The existing Rust texture validator decides admission after packaging.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def pinned(path, expected, limit=64 * 1024 * 1024):
    if path.stat().st_size > limit:
        raise ValueError(f'Input exceeds budget: {path}')
    raw = path.read_bytes()
    if sha(raw) != expected:
        raise ValueError(f'Input SHA differs: {path}')
    return raw


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['draw', 'binding', 'capture', 'output']:
        parser.add_argument('--' + name, required=True, type=Path)
    for name in ['draw-sha256', 'binding-sha256', 'slots']:
        parser.add_argument('--' + name, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise ValueError('Use a fresh output; preserve previous packages')
    draw_raw = pinned(args.draw, args.draw_sha256, 1024 * 1024)
    binding_raw = pinned(args.binding, args.binding_sha256, 16 * 1024 * 1024)
    draw = json.loads(draw_raw)
    binding = json.loads(binding_raw)
    capture_raw = pinned(args.capture, binding['pins'][str(args.capture)], 16 * 1024 * 1024)
    capture = json.loads(capture_raw)
    frame_pin = binding['pins'].get(capture['capture'])
    if not isinstance(frame_pin, str) or not re.fullmatch('[0-9a-f]{64}', frame_pin):
        raise ValueError('Original frame is not pinned by the source binding proof')
    slots = [int(s) for s in args.slots.split(',')]
    if not slots or len(slots) > 10 or len(set(slots)) != len(slots):
        raise ValueError('Requested authored slots are invalid')
    payloads = []
    resources = []
    textures = []
    def blob(raw, label):
        index = len(resources)
        path = f'{index:03d}-{label}.bin'
        resources.append(dict(path=path, sha256=sha(raw), byte_length=len(raw)))
        payloads.append(raw)
        return dict(buffer_view=index, sha256=sha(raw))
    capture_blob = blob(capture_raw, 'original-capture')
    binding_blob = blob(binding_raw, 'source-binding-proof')
    for slot in slots:
        spec = draw['textures'][slot]
        storage_path = (args.draw.parent / spec['storage']['file']).resolve()
        if not storage_path.is_relative_to(args.draw.parent.resolve()):
            raise ValueError('Storage outside draw root')
        storage = pinned(storage_path, spec['storage']['sha256'])
        rows = [row for row in binding['textures'] if row['storage']['sha256'] == sha(storage)]
        if len(rows) != 1:
            raise ValueError(f'Slot {slot} is not uniquely bound to an original source PNG')
        row = rows[0]
        source = Path(row['source_png'])
        png = pinned(source, row['source_sha256'])
        meta = pinned(Path(str(source) + '.meta'), row['source_meta_sha256'], 1024 * 1024)
        native_rows = [(i, native) for i, native in enumerate(capture['textures'])
                       if native['resource_name'] == row['native_resource_name'] and native['exported']]
        if len(native_rows) != 1:
            raise ValueError('Native resource name is ambiguous')
        native_index, native = native_rows[0]
        if native['view_format'] != row['view_format']:
            raise ValueError('Capture and binding formats differ')
        def field(name, cast=int):
            matches = re.findall(r'^\s*' + name + r': ([^\r\n]+)$', meta.decode('utf-8'), re.M)
            if len(matches) != 1:
                raise ValueError(f'Original importer field absent or ambiguous: {name}')
            return cast(matches[0])
        texture = dict(png=blob(png, 'original-PNG'), source_meta=blob(meta, 'original-meta'),
                       source_meta_sha256=sha(meta), is_srgb=bool(field('sRGBTexture')),
                       source_mipmaps=bool(field('enableMipMap')), filter_mode=field('filterMode'),
                       wrap=[field('wrapU'), field('wrapV'), field('wrapW')],
                       anisotropy=field('aniso'), mip_bias=field('mipBias', float),
                       imported_storage=dict(schema_version=3,
                           native_capture=dict(protocol='renderdoc_original_frame_mips.v1',
                                               source_guid=row['source_guid']),
                           source_binding=binding_blob, storage=blob(storage, 'native-mips'),
                           capture=capture_blob, capture_case_index=native_index,
                           width=spec['width'], height=spec['height'], format=spec['format'],
                           mips=spec['mips'], sampler=spec['sampler']))
        textures.append(texture)
    if sum(len(raw) for raw in payloads) > 128 * 1024 * 1024:
        raise ValueError('Package resource budget exceeded')
    manifest = dict(schema_version=1, licence_ref='LicenseRef-Unity-Asset-Store-EULA',
                    resources=resources, textures=textures)
    args.output.mkdir(parents=True)
    for resource, raw in zip(resources, payloads, strict=True):
        (args.output / resource['path']).write_bytes(raw)
    manifest_path = args.output / 'manifest.json'
    manifest_path.write_text(json.dumps(manifest, indent=2) + '\n')
    receipt = dict(status='ORIGINAL_SOURCE_TEXTURE_PACKAGE_PREPARED_NOT_YET_ADMITTED',
                   manifest_sha256=sha(manifest_path.read_bytes()), authored_slots=slots,
                   requested_slots_preserved=True, textures=len(textures),
                   native_mips=sum(len(t['imported_storage']['mips']) for t in textures),
                   input_draw_sha256=sha(draw_raw), input_binding_sha256=sha(binding_raw),
                   input_capture_sha256=sha(capture_raw), converter_sha256=sha(Path(__file__).read_bytes()),
                   scene_targets_not_packaged_as_authored_images=True,
                   material_assembled=False, renderer_qualified=False, Studio_integrated=False)
    (args.output / 'PACKAGING.v1.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print(json.dumps(receipt))


if __name__ == '__main__':
    main()
