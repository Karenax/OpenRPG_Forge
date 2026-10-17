use super::{
    MediaFormat, MediaInfo, MediaLimits, MediaValidationError, bounded_add, error, number,
};
use std::io::Cursor;
use symphonia::core::{
    codecs::audio::{
        AudioCodecParameters, AudioDecoder, AudioDecoderOptions,
        well_known::{
            CODEC_ID_FLAC, CODEC_ID_PCM_F32LE, CODEC_ID_PCM_F64LE, CODEC_ID_PCM_S16LE,
            CODEC_ID_PCM_S24LE, CODEC_ID_PCM_S32LE, CODEC_ID_PCM_U8, CODEC_ID_VORBIS,
        },
    },
    common::Limit,
    formats::{FormatOptions, FormatReader, TrackType, probe::Hint},
    io::{MediaSourceStream, MediaSourceStreamOptions},
    meta::MetadataOptions,
};

pub(super) fn decode(
    bytes: &[u8],
    format: MediaFormat,
    limits: MediaLimits,
) -> Result<MediaInfo, MediaValidationError> {
    if limits.max_audio_packets == 0 {
        return Err(error(format, "audio packet budget exceeded"));
    }
    container(bytes, format, limits)?;
    if format == MediaFormat::OggVorbis {
        super::vorbis_setup::validate_ogg(bytes, limits)?;
    }
    let mut hint = Hint::new();
    hint.with_extension(match format {
        MediaFormat::Wav => "wav",
        MediaFormat::OggVorbis => "ogg",
        _ => "flac",
    });
    let source = MediaSourceStream::new(
        Box::new(Cursor::new(bytes.to_vec())),
        MediaSourceStreamOptions::default(),
    );
    let metadata = MetadataOptions::default()
        .limit_tag_bytes(Limit::Maximum(64 * 1024))
        .limit_visual_bytes(Limit::Maximum(0));
    let mut demux = symphonia::default::get_probe()
        .probe(&hint, source, FormatOptions::default(), metadata)
        .map_err(|e| error(format, format!("audio container decode: {e}")))?;
    if demux.tracks().len() != 1 {
        return Err(error(format, "one audio track is required"));
    }
    let track = demux
        .default_track(TrackType::Audio)
        .ok_or_else(|| error(format, "no decodable audio track"))?;
    let params = track
        .codec_params
        .as_ref()
        .and_then(|p| p.audio())
        .ok_or_else(|| error(format, "missing audio codec parameters"))?;
    codec(params, format)?;
    let rate = params
        .sample_rate
        .ok_or_else(|| error(format, "audio sample rate missing"))?;
    let channels = u32::try_from(
        params
            .channels
            .as_ref()
            .ok_or_else(|| error(format, "audio channels missing"))?
            .count(),
    )
    .map_err(|_| error(format, "audio channel budget overflow"))?;
    specification(channels, rate, limits, format)?;
    if track
        .num_frames
        .is_some_and(|n| n > limits.max_audio_frames)
    {
        return Err(error(format, "audio frame budget exceeded"));
    }
    let track_id = track.id;
    let expected_frames = track.num_frames;
    if params.max_frames_per_packet.is_some_and(|frames| {
        frames
            .checked_mul(u64::from(channels))
            .and_then(|n| n.checked_mul(8))
            .is_none_or(|n| n > limits.max_decoded_bytes)
    }) {
        return Err(error(
            format,
            "audio decoder packet allocation budget exceeded",
        ));
    }
    let mut decoder = symphonia::default::get_codecs()
        .make_audio_decoder(params, &AudioDecoderOptions::default().verify(true))
        .map_err(|e| error(format, format!("audio decoder admission: {e}")))?;
    decode_packets(
        demux.as_mut(),
        decoder.as_mut(),
        StreamSpec {
            track_id,
            expected_frames,
            channels,
            rate,
        },
        format,
        limits,
    )
}

#[derive(Clone, Copy)]
struct StreamSpec {
    track_id: u32,
    expected_frames: Option<u64>,
    channels: u32,
    rate: u32,
}
fn decode_packets(
    demux: &mut dyn FormatReader,
    decoder: &mut dyn AudioDecoder,
    spec: StreamSpec,
    format: MediaFormat,
    limits: MediaLimits,
) -> Result<MediaInfo, MediaValidationError> {
    let mut frames = 0_u64;
    let mut packets = 0_u64;
    let mut samples = 0_u64;
    let mut output = Vec::<f32>::new();
    while let Some(packet) = demux
        .next_packet()
        .map_err(|e| error(format, format!("audio packet decode: {e}")))?
    {
        packets = bounded_add(packets, 1, limits.max_audio_packets, format, "audio packet")?;
        if packet.track_id != spec.track_id {
            return Err(error(format, "audio track changed during decode"));
        }
        let audio = decoder
            .decode(&packet)
            .map_err(|e| error(format, format!("audio sample decode: {e}")))?;
        let current_channels = u32::try_from(audio.spec().channels().count())
            .map_err(|_| error(format, "audio channel budget overflow"))?;
        specification(current_channels, audio.spec().rate(), limits, format)?;
        if current_channels != spec.channels || audio.spec().rate() != spec.rate {
            return Err(error(format, "audio specification changed during decode"));
        }
        frames = bounded_add(
            frames,
            number(audio.frames(), format)?,
            limits.max_audio_frames,
            format,
            "audio frame",
        )?;
        samples = samples
            .checked_add(number(audio.samples_interleaved(), format)?)
            .ok_or_else(|| error(format, "audio sample budget overflow"))?;
        if samples
            .checked_mul(4)
            .is_none_or(|n| n > limits.max_decoded_bytes)
        {
            return Err(error(format, "audio decoded byte budget exceeded"));
        }
        output.clear();
        output
            .try_reserve_exact(audio.samples_interleaved())
            .map_err(|_| error(format, "audio allocation budget unavailable"))?;
        output.resize(audio.samples_interleaved(), 0.0);
        audio.copy_to_slice_interleaved(&mut output);
        if output.iter().any(|sample| !sample.is_finite()) {
            return Err(error(format, "nonfinite decoded audio sample"));
        }
    }
    if frames == 0 || spec.expected_frames.is_some_and(|n| n != frames) {
        return Err(error(format, "empty or truncated audio frame sequence"));
    }
    if decoder.finalize().verify_ok == Some(false) {
        return Err(error(format, "decoded audio verification failed"));
    }
    Ok(MediaInfo::Audio {
        channels: spec.channels,
        sample_rate: spec.rate,
        frames,
        samples,
        decoded_bytes: samples * 4,
    })
}

fn specification(
    channels: u32,
    rate: u32,
    limits: MediaLimits,
    format: MediaFormat,
) -> Result<(), MediaValidationError> {
    if channels == 0
        || channels > limits.max_audio_channels
        || rate == 0
        || rate > limits.max_audio_sample_rate
    {
        return Err(error(format, "audio channel/sample rate budget exceeded"));
    }
    Ok(())
}
fn codec(params: &AudioCodecParameters, format: MediaFormat) -> Result<(), MediaValidationError> {
    let accepted = match format {
        MediaFormat::OggVorbis => params.codec == CODEC_ID_VORBIS,
        MediaFormat::Flac => params.codec == CODEC_ID_FLAC,
        MediaFormat::Wav => [
            CODEC_ID_PCM_S32LE,
            CODEC_ID_PCM_S24LE,
            CODEC_ID_PCM_S16LE,
            CODEC_ID_PCM_U8,
            CODEC_ID_PCM_F32LE,
            CODEC_ID_PCM_F64LE,
        ]
        .contains(&params.codec),
        _ => false,
    };
    if !accepted {
        return Err(error(
            format,
            "container/codec differs from the declared format",
        ));
    }
    Ok(())
}
fn container(
    bytes: &[u8],
    format: MediaFormat,
    limits: MediaLimits,
) -> Result<(), MediaValidationError> {
    if format == MediaFormat::OggVorbis {
        return ogg_pages(bytes, limits);
    }
    let valid = match format {
        MediaFormat::Wav => {
            bytes.len() >= 44
                && bytes.starts_with(b"RIFF")
                && bytes.get(8..12) == Some(b"WAVE")
                && u64::from(u32::from_le_bytes(
                    bytes[4..8]
                        .try_into()
                        .map_err(|_| error(format, "WAV header"))?,
                )) + 8
                    == number(bytes.len(), format)?
        }
        MediaFormat::Flac => bytes.starts_with(b"fLaC"),
        _ => false,
    };
    if !valid {
        return Err(error(
            format,
            "invalid or truncated declared audio container",
        ));
    }
    Ok(())
}

fn ogg_pages(bytes: &[u8], limits: MediaLimits) -> Result<(), MediaValidationError> {
    let mut offset = 0_usize;
    let mut pages = 0;
    let mut eos = false;
    while offset < bytes.len() {
        pages = bounded_add(
            pages,
            1,
            limits.max_audio_packets,
            MediaFormat::OggVorbis,
            "Ogg page",
        )?;
        let header = bytes
            .get(
                offset
                    ..offset
                        .checked_add(27)
                        .ok_or_else(|| error(MediaFormat::OggVorbis, "Ogg offset overflow"))?,
            )
            .ok_or_else(|| error(MediaFormat::OggVorbis, "truncated Ogg page"))?;
        if !header.starts_with(b"OggS\0") {
            return Err(error(MediaFormat::OggVorbis, "invalid Ogg page signature"));
        }
        let table = offset + 27;
        let table_end = table + usize::from(header[26]);
        let lacing = bytes
            .get(table..table_end)
            .ok_or_else(|| error(MediaFormat::OggVorbis, "truncated Ogg lacing table"))?;
        let body = lacing.iter().map(|n| usize::from(*n)).sum::<usize>();
        let end = table_end
            .checked_add(body)
            .ok_or_else(|| error(MediaFormat::OggVorbis, "Ogg page range overflow"))?;
        if end > bytes.len() {
            return Err(error(MediaFormat::OggVorbis, "truncated Ogg page body"));
        }
        eos = header[5] & 4 != 0;
        offset = end;
    }
    if pages == 0 || !eos {
        return Err(error(
            MediaFormat::OggVorbis,
            "Ogg stream lacks its final end-of-stream page",
        ));
    }
    Ok(())
}
