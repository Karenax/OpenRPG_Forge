//! Admission of codec setup allocations before Symphonia constructs its decoder.
//! This reads codebook declarations and admits the bounded Floor1 profile.
//! Symphonia remains the audio decoder; Floor0 is refused before probing.
use super::{MediaFormat, MediaLimits, MediaValidationError, error};
use symphonia::core::io::{BitReaderRtl, ReadBitsRtl};

const FORMAT: MediaFormat = MediaFormat::OggVorbis;
const MAX_SETUP_BYTES: usize = 16 * 1024 * 1024;

pub(super) fn validate_ogg(bytes: &[u8], limits: MediaLimits) -> Result<(), MediaValidationError> {
    // Preflight precedes probing too: demuxers can read/scan setup headers while
    // probing and can reject an empty stream before creating codec parameters.
    let mut offset = 0_usize;
    let mut packet_index = 0_usize;
    let mut packet = Vec::new();
    let mut serial = None;
    while offset < bytes.len() {
        let header_end = offset.checked_add(27).ok_or_else(budget)?;
        let header = bytes
            .get(offset..header_end)
            .ok_or_else(|| invalid("truncated page header"))?;
        let current_serial = u32::from_le_bytes(
            header[14..18]
                .try_into()
                .map_err(|_| invalid("page serial"))?,
        );
        if serial
            .replace(current_serial)
            .is_some_and(|value| value != current_serial)
        {
            return Err(invalid("interleaved or chained streams are not supported"));
        }
        let table_end = header_end
            .checked_add(usize::from(header[26]))
            .ok_or_else(budget)?;
        let lacing = bytes
            .get(header_end..table_end)
            .ok_or_else(|| invalid("truncated page lacing"))?;
        let mut data = table_end;
        for length in lacing {
            let end = data.checked_add(usize::from(*length)).ok_or_else(budget)?;
            let segment = bytes
                .get(data..end)
                .ok_or_else(|| invalid("truncated header packet"))?;
            let length = packet.len().checked_add(segment.len()).ok_or_else(budget)?;
            let max = match packet_index {
                0 => 30,
                1 => 65536,
                _ => MAX_SETUP_BYTES,
            };
            if length > max
                || (packet_index == 2
                    && u64::try_from(length).unwrap_or(u64::MAX) > limits.max_decoded_bytes)
            {
                return Err(budget());
            }
            packet.try_reserve(segment.len()).map_err(|_| budget())?;
            packet.extend_from_slice(segment);
            data = end;
            if segment.len() < 255 {
                match packet_index {
                    0 => identification(&packet, limits)?,
                    1 if packet.get(..7) != Some(b"\x03vorbis") => {
                        return Err(invalid("missing comment header"));
                    }
                    2 => return validate_setup(&packet, limits),
                    _ => {}
                }
                packet_index += 1;
                packet.clear();
            }
        }
        offset = data;
    }
    Err(invalid("missing or truncated setup header"))
}

fn identification(packet: &[u8], limits: MediaLimits) -> Result<(), MediaValidationError> {
    if packet.len() != 30 || packet.get(..7) != Some(b"\x01vorbis") {
        return Err(invalid("invalid identification header"));
    }
    let channels = u32::from(packet[11]);
    let rate = u32::from_le_bytes(
        packet[12..16]
            .try_into()
            .map_err(|_| invalid("sample rate"))?,
    );
    if channels == 0
        || channels > limits.max_audio_channels
        || rate == 0
        || rate > limits.max_audio_sample_rate
    {
        return Err(budget());
    }
    Ok(())
}

fn validate_setup(packet: &[u8], limits: MediaLimits) -> Result<(), MediaValidationError> {
    if packet.get(..7) != Some(b"\x05vorbis") {
        return Err(invalid("missing setup header"));
    }
    if packet.len() > MAX_SETUP_BYTES
        || u64::try_from(packet.len()).unwrap_or(u64::MAX) > limits.max_decoded_bytes
    {
        return Err(budget());
    }
    let mut bits = BitReaderRtl::new(&packet[7..]);
    let count = read(&mut bits, 8)? + 1;
    let mut allocation = 0_u64;
    for _ in 0..count {
        codebook(&mut bits, &mut allocation, limits.max_decoded_bytes)?;
    }
    let time_count = read(&mut bits, 6)? + 1;
    for _ in 0..time_count {
        if read(&mut bits, 16)? != 0 {
            return Err(invalid("invalid time transform"));
        }
    }
    let floor_count = read(&mut bits, 6)? + 1;
    for _ in 0..floor_count {
        let profile = read(&mut bits, 16)?;
        if profile != 1 {
            return Err(invalid(format!(
                "unsupported floor profile {profile}; only Floor1 is admitted"
            )));
        }
        floor1(&mut bits, count)?;
    }
    Ok(())
}

fn floor1(bits: &mut BitReaderRtl<'_>, books: u32) -> Result<(), MediaValidationError> {
    let partitions = index(bits, 5)?;
    let mut classes = [0_usize; 32];
    let mut class_count = 0_usize;
    for class in classes.iter_mut().take(partitions) {
        *class = index(bits, 4)?;
        class_count = class_count.max(*class + 1);
    }
    let mut dimensions = [0_usize; 16];
    for dimension in dimensions.iter_mut().take(class_count) {
        *dimension = index(bits, 3)? + 1;
        let subclass_bits = read(bits, 2)?;
        if subclass_bits != 0 && read(bits, 8)? >= books {
            return Err(invalid("Floor1 master book is outside the codebook table"));
        }
        for _ in 0..(1 << subclass_bits) {
            let book = read(bits, 8)?;
            if book > books {
                return Err(invalid("Floor1 sub-book is outside the codebook table"));
            }
        }
    }
    read(bits, 2)?; // multiplier, encoded as zero through three
    let range_bits = read(bits, 4)?;
    // The decoder stores at most 65 Floor1 X coordinates. Repeated coordinates
    // cannot form the strictly bracketing neighbours used during synthesis.
    let mut points = [0_u32; 65];
    points[1] = 1 << range_bits;
    let mut point_count = 2_usize;
    for class in classes.iter().take(partitions) {
        for _ in 0..dimensions[*class] {
            if point_count == points.len() {
                return Err(invalid("Floor1 coordinate budget exceeded"));
            }
            let point = read(bits, range_bits)?;
            if points[..point_count].contains(&point) {
                return Err(invalid("Floor1 coordinates must be unique"));
            }
            points[point_count] = point;
            point_count += 1;
        }
    }
    Ok(())
}

fn index(bits: &mut BitReaderRtl<'_>, count: u32) -> Result<usize, MediaValidationError> {
    usize::try_from(read(bits, count)?).map_err(|_| budget())
}

fn codebook(
    bits: &mut BitReaderRtl<'_>,
    allocation: &mut u64,
    limit: u64,
) -> Result<(), MediaValidationError> {
    if read(bits, 24)? != 0x56_43_42 {
        return Err(invalid("invalid codebook sync"));
    }
    let dimensions = read(bits, 16)?;
    let entries = read(bits, 24)?;
    if !(1..=32).contains(&dimensions) || !(1..=131_072).contains(&entries) {
        return Err(budget());
    }
    // Conservative headroom for the codec's scalar vectors, intermediate blocks
    // and tree nodes. LUT expansion and VQ vectors are accounted separately.
    charge(allocation, u64::from(entries) * 512, limit)?;
    let mut lengths = [0_u64; 33];
    read_lengths(bits, entries, &mut lengths)?;
    charge(allocation, lookup_table_bytes(&mut lengths)?, limit)?;
    match read(bits, 4)? {
        0 => Ok(()),
        lookup @ (1 | 2) => {
            skip(bits, 64)?; // minimum and delta values
            let value_bits = read(bits, 4)? + 1;
            read(bits, 1)?; // sequence flag
            let values = if lookup == 1 {
                checked_lookup_root(entries, dimensions)?
            } else {
                entries * dimensions
            };
            charge(
                allocation,
                u64::from(entries) * u64::from(dimensions) * 4,
                limit,
            )?;
            charge(allocation, u64::from(values) * 2, limit)?;
            skip(bits, values.checked_mul(value_bits).ok_or_else(budget)?)
        }
        _ => Err(invalid("invalid codebook lookup type")),
    }
}

fn read_lengths(
    bits: &mut BitReaderRtl<'_>,
    entries: u32,
    lengths: &mut [u64; 33],
) -> Result<(), MediaValidationError> {
    if read(bits, 1)? == 0 {
        let sparse = read(bits, 1)? != 0;
        for _ in 0..entries {
            if !sparse || read(bits, 1)? != 0 {
                let length = usize::try_from(read(bits, 5)? + 1).map_err(|_| budget())?;
                lengths[length] += 1;
            }
        }
    } else {
        let mut length = usize::try_from(read(bits, 5)? + 1).map_err(|_| budget())?;
        let mut remaining = entries;
        while remaining > 0 {
            if length > 32 {
                return Err(invalid("ordered codeword length exceeds 32 bits"));
            }
            let count = read(bits, 32 - remaining.leading_zeros())?;
            remaining = remaining
                .checked_sub(count)
                .ok_or_else(|| invalid("ordered entry count overflow"))?;
            lengths[length] += u64::from(count);
            length += 1;
        }
    }
    Ok(())
}

fn lookup_table_bytes(lengths: &mut [u64; 33]) -> Result<u64, MediaValidationError> {
    let used: u64 = lengths.iter().sum();
    // Vorbis permits a special one-entry one-bit codebook. The decoder expands
    // this into both one-bit codewords before constructing its complete tree.
    if used == 1 && lengths[1] == 1 {
        lengths[1] = 2;
    }
    let max = lengths
        .iter()
        .rposition(|n| *n != 0)
        .ok_or_else(|| invalid("empty scalar codebook"))?;
    let block_bits = max.clamp(4, 8);
    let mut prefixes = 1_u64;
    let mut blocks = 1_u64;
    for (length, count) in lengths.iter().enumerate().take(max + 1).skip(1) {
        prefixes = (prefixes * 2)
            .checked_sub(*count)
            .ok_or_else(|| invalid("overspecified scalar codebook"))?;
        if length < max && length.is_multiple_of(block_bits) {
            blocks += prefixes;
        }
    }
    if prefixes != 0 {
        return Err(invalid("underspecified scalar codebook"));
    }
    // Each Symphonia block has 2^block_bits slots. Thirty-two bytes per slot
    // covers the tagged entry and Vec growth rather than relying on ABI layout.
    blocks
        .checked_mul(1 << block_bits)
        .and_then(|n| n.checked_mul(32))
        .ok_or_else(budget)
}

fn root(entries: u32, dimensions: u32) -> u32 {
    let mut low = 1_u32;
    let mut high = entries;
    while low < high {
        let mid = low + (high - low).div_ceil(2);
        if u64::from(mid)
            .checked_pow(dimensions)
            .is_some_and(|n| n <= u64::from(entries))
        {
            low = mid;
        } else {
            high = mid - 1;
        }
    }
    low
}

// Inputs are bounded above to 131072/32, both exactly representable in f32.
// This comparison admits only results for which Symphonia 0.6.1's own f32
// lookup helper and its unchecked u32 assertions are safe. It is not decoding.
#[allow(
    clippy::cast_precision_loss,
    clippy::cast_possible_truncation,
    clippy::cast_sign_loss
)]
fn checked_lookup_root(entries: u32, dimensions: u32) -> Result<u32, MediaValidationError> {
    let exact = root(entries, dimensions);
    let codec = (entries as f32).powf(1.0 / dimensions as f32).floor() as u32;
    if codec != exact
        || exact.checked_pow(dimensions).is_none()
        || (exact + 1).checked_pow(dimensions).is_none()
    {
        return Err(invalid("unsafe codebook lookup arithmetic in decoder"));
    }
    Ok(exact)
}
fn read(bits: &mut BitReaderRtl<'_>, count: u32) -> Result<u32, MediaValidationError> {
    bits.read_bits_leq32(count)
        .map_err(|e| invalid(format!("truncated setup: {e}")))
}
fn skip(bits: &mut BitReaderRtl<'_>, count: u32) -> Result<(), MediaValidationError> {
    bits.ignore_bits(count)
        .map_err(|e| invalid(format!("truncated setup values: {e}")))
}
fn charge(total: &mut u64, bytes: u64, limit: u64) -> Result<(), MediaValidationError> {
    *total = total.checked_add(bytes).ok_or_else(budget)?;
    if *total > limit {
        return Err(budget());
    }
    Ok(())
}
fn invalid(reason: impl std::fmt::Display) -> MediaValidationError {
    error(FORMAT, format!("Vorbis setup: {reason}"))
}
fn budget() -> MediaValidationError {
    error(FORMAT, "Vorbis setup budget exceeded")
}
