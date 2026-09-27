"""Convert sampled emission transport into straight material values.

The ORM red channel is an explicit unit-emission reference, not source AO.
All channels must share geometry, transparency, samples and sampling seed.
"""
import struct
import zlib
import numpy as np


def material_pixels(radiance, reference):
    if radiance.shape != reference.shape or radiance.ndim != 3 or radiance.shape[2] != 4:
        raise ValueError('matching RGBA buffers required')
    if not np.isfinite(radiance).all() or not np.isfinite(reference).all():
        raise ValueError('non-finite render data')
    if not np.allclose(radiance[:,:,3], reference[:,:,3], atol=1e-6, rtol=0):
        raise ValueError('render coverage differs across material channels')
    white = reference[:,:,0]
    covered = reference[:,:,3] > .001
    if np.any(white[covered] <= 0):
        raise ValueError('covered sample has no unit-emission reference')
    result = np.zeros_like(radiance)
    np.divide(radiance[:,:,:3], white[:,:,None], out=result[:,:,:3],
              where=white[:,:,None] > 0)
    result[:,:,3] = radiance[:,:,3]
    return result


def png_bytes(pixels, srgb=False):
    values = pixels.copy()
    if srgb:
        linear = values[:,:,:3]
        values[:,:,:3] = np.where(linear <= .0031308, linear*12.92,
                                  1.055*np.maximum(linear,0)**(1/2.4)-.055)
    # PNG is top-down; Blender pixel buffers are bottom-up. Quantization only.
    rgba = np.rint(np.clip(values[::-1],0,1)*255).astype(np.uint8)
    height, width = rgba.shape[:2]
    def chunk(kind, data):
        return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data))
    rows = b''.join(b'\0'+row.tobytes() for row in rgba)
    return (b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>2I5B',width,height,8,6,0,0,0))+
            chunk(b'IDAT',zlib.compress(rows,6))+chunk(b'IEND',b''))
