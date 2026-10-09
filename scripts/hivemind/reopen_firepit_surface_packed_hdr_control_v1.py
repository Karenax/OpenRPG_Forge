"""Decode hardware packed HDR output; original masks retained separately."""
from pathlib import Path
import json
import struct
import math

W = Path('F:/OpenRPG_ImportWork')
CAP = W/'firepit-surface-packed-hdr-control-v1'
MASK = W/'firepit-surface-instance-fix-v1'
PLAN = W/'firepit-surface-packed-hdr-control-plan-v1'
BASE = Path(__file__).with_name('reopen_firepit_surface_native_v1.py')

def decode_unsigned_float(bits, mantissa):
    fraction = bits & ((1 << mantissa) - 1)
    exponent = bits >> mantissa
    assert exponent != 31, 'nonfinite packed HDR value'
    return math.ldexp(fraction, 1 - 15 - mantissa) if exponent == 0 else math.ldexp((1 << mantissa) + fraction, exponent - 15 - mantissa)

def packed_pixels(raw, view):
    meta = json.loads((CAP/f'gpu-{view}.metadata.json').read_text('utf-8-sig'))
    assert meta['format'] == 'Rg11b10Ufloat' and meta['bytes'] == 512*512*4
    current_camera = json.loads((CAP/f'CAMERA-STATE-{view}.json').read_text('utf-8-sig'))
    previous_camera = json.loads((MASK/f'CAMERA-STATE-{view}.json').read_text('utf-8-sig'))
    assert current_camera == previous_camera, 'external alpha masks require identical camera/transforms'
    original = list(struct.iter_unpack('<4e', (MASK/f'gpu-{view}.rgba16f').read_bytes()))
    values = list(struct.iter_unpack('<I', raw.read_bytes()))
    assert len(values) == len(original) == 512*512
    for (value,), prior in zip(values, original):
        yield (decode_unsigned_float(value & 2047, 6), decode_unsigned_float((value >> 11) & 2047, 6), decode_unsigned_float((value >> 22) & 1023, 5), prior[3])

source = BASE.read_text('utf-8')
source = source.replace("CAP=W/'firepit-surface-native-v1'", "CAP=W/'firepit-surface-packed-hdr-control-v1'")
source = source.replace("CASE=W/'firepit-surface-native-case-v1'", "CASE=W/'firepit-surface-instance-fix-case-v1'")
source = source.replace('firepit-surface-native-reopen-v1', 'firepit-surface-packed-hdr-control-reopen-v1')
source = source.replace("assert m['format']=='Rgba16Float' and m['bytes']==512*512*8", "assert m['format']=='Rg11b10Ufloat' and m['bytes']==512*512*4")
source = source.replace("struct.iter_unpack('<4e',raw.read_bytes())", 'packed_pixels(raw,view)')
source = source.replace("files.extend([raw,meta])", "files.extend([raw,meta,MASK/f'gpu-{view}.rgba16f',MASK/f'CAMERA-STATE-{view}.json',CAP/f'CAMERA-STATE-{view}.json',PLAN/'PROTOCOL.v1.json',W/'firepit-source-sampling-context-reopen-v1/RESULT.v1.json'])")
source = source.replace('source_debug_encoding_applied=True,', 'source_debug_encoding_applied=True,hardware_packed_hdr_output_control=True,internal_lighting_buffer_parity_qualified=False,external_alpha_masks_same_camera_verified=True,')
if __name__ == '__main__':
    protocol = json.loads((PLAN/'PROTOCOL.v1.json').read_text('utf-8-sig'))
    assert protocol['maximum_encoded_surface_mae'] == 2/255 and protocol['gpu_pixels_absent_at_definition']
    exec(compile(source, str(BASE), 'exec'), globals())
