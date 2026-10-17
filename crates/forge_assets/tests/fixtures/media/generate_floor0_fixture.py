"""Original CC0-1.0 adversarial Vorbis fixture; no external input or decoder.

One valid Floor0 setup advertises sixteen books. Its one-byte audio packet asks
for book31, exposing an unchecked lookup in Symphonia0.6.1 Floor0::read_channel.
Only this test directory's named fixture is written.
"""
import struct
from pathlib import Path


class Bits:
    def __init__(self):
        self.data = bytearray()
        self.count = 0

    def put(self, value, width):
        for bit in range(width):
            if self.count % 8 == 0:
                self.data.append(0)
            self.data[self.count // 8] |= ((value >> bit) & 1) << (self.count % 8)
            self.count += 1


def page(packet, sequence, flags, granule=0):
    assert len(packet) < 255
    data = bytearray(b"OggS\0" + bytes([flags]))
    data += struct.pack("<QIII", granule, 1, sequence, 0)
    data += bytes([1, len(packet)]) + packet
    crc = 0
    for value in data:
        crc ^= value << 24
        for _ in range(8):
            crc = ((crc << 1) ^ (0x04C11DB7 if crc & 0x80000000 else 0)) & 0xFFFFFFFF
    data[22:26] = struct.pack("<I", crc)
    return data


b = Bits()
b.put(0, 8)  # one codebook
b.put(0x564342, 24)
b.put(1, 16)  # dimensions
b.put(2, 24)  # entries
b.put(1, 1)  # ordered
b.put(0, 5)  # one-bit codewords
b.put(2, 2)  # both entries use that length
b.put(1, 4)  # VQ lookup type one
b.put(0, 32)  # minimum
b.put(0, 32)  # delta
b.put(0, 4)  # one-bit multiplicands
b.put(0, 1)  # sequence flag
b.put(0, 2)  # two multiplicands
b.put(0, 6)  # one time transform
b.put(0, 16)
b.put(0, 6)  # one floor
b.put(0, 16)  # Floor0
b.put(1, 8)  # order
b.put(8000, 16)  # rate
b.put(64, 16)  # Bark map
b.put(1, 6)  # amplitude bits
b.put(1, 8)  # amplitude offset
b.put(15, 4)  # sixteen floor books
for _ in range(16):
    b.put(0, 8)
b.put(0, 6)  # one residue
b.put(0, 16)  # residue type zero
b.put(0, 24)  # begin
b.put(0, 24)  # end
b.put(0, 24)  # partition size one
b.put(0, 6)  # one classification
b.put(0, 8)  # classbook zero
b.put(0, 3)  # unused cascade
b.put(0, 1)
b.put(0, 6)  # one mapping
b.put(0, 16)
b.put(0, 1)  # one submap
b.put(0, 1)  # no coupling
b.put(0, 2)
b.put(0, 8)  # time placeholder
b.put(0, 8)  # floor
b.put(0, 8)  # residue
b.put(0, 6)  # one mode
b.put(0, 1)  # short block
b.put(0, 16)  # window
b.put(0, 16)  # transform
b.put(0, 8)  # mapping
b.put(1, 1)  # framing
ident = b"\x01vorbis" + struct.pack("<IBI", 0, 1, 8000) + bytes(12) + b"\x66\x01"
comment = b"\x03vorbis" + bytes(8) + b"\x01"
setup = b"\x05vorbis" + bytes(b.data)
fixture = page(ident, 0, 2) + page(comment, 1, 0) + page(setup, 2, 0)
fixture += page(b"\x7e", 3, 4, 32)
Path(__file__).with_name("floor0_invalid_book.ogg").write_bytes(fixture)
