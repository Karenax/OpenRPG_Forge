#![cfg(not(target_arch = "wasm32"))]
use forge_assets::media_validation::{MediaFormat, MediaLimits, validate_media};

#[test]
fn vorbis_setup_tables_are_admitted_before_decoding_samples() {
    let bytes = include_bytes!("fixtures/media/tone.ogg");
    let error = validate_media(
        bytes,
        MediaFormat::OggVorbis,
        MediaLimits {
            // The authored PCM fits exactly; its codec tables require additional memory.
            max_decoded_bytes: 1024,
            ..MediaLimits::default()
        },
    )
    .unwrap_err();
    assert!(error.reason.contains("Vorbis setup budget"), "{error}");
}

#[test]
fn vorbis_multiple_expanding_codebooks_share_one_setup_budget() {
    // The same budget admits one book's setup and refuses the aggregate of two.
    // The test never asks the decoder for a multi-gigabyte allocation. The final
    // empty stream is deliberate: admission must refuse the declared tables first.
    let limits = MediaLimits {
        max_decoded_bytes: 256 * 1024,
        ..MediaLimits::default()
    };
    let single =
        validate_media(&expanding_codebooks(1, 16), MediaFormat::OggVorbis, limits).unwrap_err();
    assert!(single.reason.contains("audio container decode"), "{single}");
    let bytes = expanding_codebooks(2, 16);
    let error = validate_media(&bytes, MediaFormat::OggVorbis, limits).unwrap_err();
    assert!(error.reason.contains("Vorbis setup budget"), "{error}");
}

#[test]
fn unsafe_vorbis_demux_lookup_arithmetic_is_refused_before_probe() {
    // Symphonia 0.6.1's Ogg mapper asserts (root+1)^dimensions in u32.
    // This declaration overflows that assertion even though each entry count
    // fits its advertised codec limits. User data must never reach that panic.
    let error = validate_media(
        &expanding_codebooks(1, 32),
        MediaFormat::OggVorbis,
        MediaLimits::default(),
    )
    .unwrap_err();
    assert!(
        error.reason.contains("unsafe codebook lookup arithmetic"),
        "{error}"
    );
}

#[derive(Default)]
struct Bits {
    bytes: Vec<u8>,
    count: usize,
}
impl Bits {
    fn put(&mut self, value: u32, width: u32) {
        for bit in 0..width {
            if self.count.is_multiple_of(8) {
                self.bytes.push(0);
            }
            self.bytes[self.count / 8] |= u8::from(value & (1 << bit) != 0) << (self.count % 8);
            self.count += 1;
        }
    }
}
fn expanding_codebooks(count: u32, dimensions: u32) -> Vec<u8> {
    let mut bits = Bits::default();
    bits.put(count - 1, 8);
    for _ in 0..count {
        bits.put(0x56_43_42, 24);
        bits.put(dimensions, 16);
        bits.put(256, 24); // entries
        bits.put(1, 1); // ordered
        bits.put(7, 5); // all codewords have length eight
        bits.put(256, 9);
        bits.put(1, 4); // lookup type one expands entries times dimensions
        bits.put(0, 32); // minimum
        bits.put(0, 32); // delta
        bits.put(0, 4); // one-bit multiplicand
        bits.put(0, 1); // no sequence accumulation
        bits.put(0, 1); // one lookup value
    }
    bits.put(0, 6); // one time transform
    bits.put(0, 16);
    bits.put(0, 6); // one floor
    bits.put(1, 16); // floor type one
    bits.put(0, 5); // zero partitions
    bits.put(0, 2); // multiplier one
    bits.put(1, 4); // range bits
    bits.put(0, 6); // one residue
    bits.put(0, 16);
    bits.put(0, 24); // begin
    bits.put(0, 24); // end
    bits.put(0, 24); // partition size one
    bits.put(0, 6); // one classification
    bits.put(0, 8); // classbook zero
    bits.put(0, 3); // no cascade
    bits.put(0, 1);
    bits.put(0, 6); // one mapping
    bits.put(0, 16);
    bits.put(0, 1); // one submap
    bits.put(0, 1); // no coupling
    bits.put(0, 2); // reserved
    bits.put(0, 8); // time placeholder
    bits.put(0, 8); // floor
    bits.put(0, 8); // residue
    bits.put(0, 6); // one mode
    bits.put(0, 1); // short block
    bits.put(0, 16); // window
    bits.put(0, 16); // transform
    bits.put(0, 8); // mapping
    bits.put(1, 1); // framing
    let mut setup = b"\x05vorbis".to_vec();
    setup.extend(bits.bytes);
    let mut ident = b"\x01vorbis".to_vec();
    ident.extend(0_u32.to_le_bytes());
    ident.push(1);
    ident.extend(8000_u32.to_le_bytes());
    ident.extend([0; 12]);
    ident.extend([0x66, 1]);
    let comment = b"\x03vorbis\0\0\0\0\0\0\0\0\x01";
    let mut bytes = page(&ident, 0, 2);
    bytes.extend(page(comment, 1, 0));
    bytes.extend(page(&setup, 2, 4));
    bytes
}
fn page(packet: &[u8], sequence: u32, flags: u8) -> Vec<u8> {
    assert!(packet.len() < 255);
    let mut bytes = b"OggS\0".to_vec();
    bytes.push(flags);
    bytes.extend(0_u64.to_le_bytes());
    bytes.extend(1_u32.to_le_bytes());
    bytes.extend(sequence.to_le_bytes());
    bytes.extend(0_u32.to_le_bytes());
    bytes.extend([1, u8::try_from(packet.len()).unwrap()]);
    bytes.extend(packet);
    let mut crc = 0_u32;
    for byte in &bytes {
        crc ^= u32::from(*byte) << 24;
        for _ in 0..8 {
            crc = (crc << 1)
                ^ if crc & 0x8000_0000 == 0 {
                    0
                } else {
                    0x04c1_1db7
                };
        }
    }
    bytes[22..26].copy_from_slice(&crc.to_le_bytes());
    bytes
}
