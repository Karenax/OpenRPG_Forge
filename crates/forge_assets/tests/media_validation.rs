#![cfg(not(target_arch = "wasm32"))]
use forge_assets::media_validation::*;
use std::io::Cursor;

fn png() -> Vec<u8> {
    let image = image::RgbaImage::from_pixel(1, 1, image::Rgba([30, 90, 160, 255]));
    let mut output = Cursor::new(Vec::new());
    image
        .write_to(&mut output, image::ImageFormat::Png)
        .unwrap();
    output.into_inner()
}

#[test]
fn jpeg_is_actually_decoded_and_truncated_images_are_refused() {
    let image = image::RgbImage::from_pixel(2, 1, image::Rgb([70, 120, 180]));
    let mut out = Cursor::new(Vec::new());
    image.write_to(&mut out, image::ImageFormat::Jpeg).unwrap();
    let bytes = out.into_inner();
    assert_eq!(
        validate_media(&bytes, MediaFormat::Jpeg, MediaLimits::default()).unwrap(),
        MediaInfo::Image {
            width: 2,
            height: 1,
            decoded_bytes: 6
        }
    );
    assert!(validate_media(&bytes[..20], MediaFormat::Jpeg, MediaLimits::default()).is_err());
}

#[test]
fn original_ogg_vorbis_and_flac_fixtures_decode_to_the_authored_frame_count() {
    for (format, bytes) in [
        (
            MediaFormat::OggVorbis,
            include_bytes!("fixtures/media/tone.ogg").as_slice(),
        ),
        (
            MediaFormat::Flac,
            include_bytes!("fixtures/media/tone.flac").as_slice(),
        ),
    ] {
        assert_eq!(
            validate_media(bytes, format, MediaLimits::default()).unwrap(),
            MediaInfo::Audio {
                channels: 1,
                sample_rate: 8000,
                frames: 256,
                samples: 256,
                decoded_bytes: 1024
            }
        );
        assert!(
            validate_media(&bytes[..bytes.len() - 10], format, MediaLimits::default()).is_err()
        );
    }
}

#[test]
fn ieee_pcm_nonfinite_audio_is_refused() {
    let mut bytes = wav(2);
    bytes[20..22].copy_from_slice(&3_u16.to_le_bytes());
    bytes[28..32].copy_from_slice(&32000_u32.to_le_bytes());
    bytes[32..34].copy_from_slice(&4_u16.to_le_bytes());
    bytes[34..36].copy_from_slice(&32_u16.to_le_bytes());
    bytes[44..48].copy_from_slice(&f32::NAN.to_le_bytes());
    assert!(validate_media(&bytes, MediaFormat::Wav, MediaLimits::default()).is_err());
}

#[test]
fn shared_glb_decode_keeps_uri_and_node_hierarchy_guards() {
    for json in [br#"{"asset":{"version":"2.0"},"extensions":{"custom":{"uri":"https://example.invalid/a"}}}"#.as_slice(),br#"{"asset":{"version":"2.0"},"nodes":[{"children":[1]},{"children":[0]}]}"#.as_slice()]{
        assert!(validate_media(&glb(json,None),MediaFormat::Glb,MediaLimits::default()).is_err());
    }
}
fn wav(frames: u32) -> Vec<u8> {
    let data_bytes = frames * 2;
    let mut b = b"RIFF".to_vec();
    b.extend((36 + data_bytes).to_le_bytes());
    b.extend(b"WAVEfmt ");
    b.extend(16_u32.to_le_bytes());
    b.extend(1_u16.to_le_bytes());
    b.extend(1_u16.to_le_bytes());
    b.extend(8000_u32.to_le_bytes());
    b.extend(16000_u32.to_le_bytes());
    b.extend(2_u16.to_le_bytes());
    b.extend(16_u16.to_le_bytes());
    b.extend(b"data");
    b.extend(data_bytes.to_le_bytes());
    for i in 0..frames {
        b.extend(i16::try_from(i % 100).unwrap().to_le_bytes());
    }
    b
}
fn glb(json: &[u8], bin: Option<&[u8]>) -> Vec<u8> {
    let mut json = json.to_vec();
    while !json.len().is_multiple_of(4) {
        json.push(b' ');
    }
    let mut bin = bin.map(<[u8]>::to_vec);
    if let Some(bin) = &mut bin {
        while !bin.len().is_multiple_of(4) {
            bin.push(0);
        }
    }
    let length = 20 + json.len() + bin.as_ref().map_or(0, |b| 8 + b.len());
    let mut b = b"glTF".to_vec();
    b.extend(2_u32.to_le_bytes());
    b.extend(u32::try_from(length).unwrap().to_le_bytes());
    b.extend(u32::try_from(json.len()).unwrap().to_le_bytes());
    b.extend(b"JSON");
    b.extend(json);
    if let Some(bin) = bin {
        b.extend(u32::try_from(bin.len()).unwrap().to_le_bytes());
        b.extend(b"BIN\0");
        b.extend(bin);
    }
    b
}
fn triangle() -> Vec<u8> {
    let mut bin = Vec::new();
    for v in [0_f32, 0., 0., 1., 0., 0., 0., 1., 0.] {
        bin.extend(v.to_le_bytes());
    }
    glb(br#"{"asset":{"version":"2.0"},"buffers":[{"byteLength":36}],"bufferViews":[{"buffer":0,"byteOffset":0,"byteLength":36}],"accessors":[{"bufferView":0,"componentType":5126,"count":3,"type":"VEC3","min":[0,0,0],"max":[1,1,0]}],"meshes":[{"primitives":[{"attributes":{"POSITION":0}}]}],"nodes":[{"mesh":0}],"scenes":[{"nodes":[0]}],"scene":0}"#,Some(&bin))
}

#[test]
fn png_is_fully_decoded_and_crc_or_truncation_damage_is_rejected() {
    let b = png();
    assert_eq!(
        validate_media(&b, MediaFormat::Png, MediaLimits::default()).unwrap(),
        MediaInfo::Image {
            width: 1,
            height: 1,
            decoded_bytes: 4
        }
    );
    let mut corrupt = b.clone();
    corrupt[29] ^= 1;
    assert!(validate_media(&corrupt, MediaFormat::Png, MediaLimits::default()).is_err());
    assert!(validate_media(&b[..33], MediaFormat::Png, MediaLimits::default()).is_err());
}

#[test]
fn image_pixel_and_allocation_budgets_are_checked() {
    for limits in [
        MediaLimits {
            max_image_pixels: 0,
            ..MediaLimits::default()
        },
        MediaLimits {
            max_decoded_bytes: 3,
            ..MediaLimits::default()
        },
    ] {
        let error = validate_media(&png(), MediaFormat::Png, limits).unwrap_err();
        assert!(error.reason.contains("budget"));
    }
}

#[test]
fn pcm_wav_is_decoded_with_exact_frames_and_samples() {
    assert_eq!(
        validate_media(&wav(4), MediaFormat::Wav, MediaLimits::default()).unwrap(),
        MediaInfo::Audio {
            channels: 1,
            sample_rate: 8000,
            frames: 4,
            samples: 4,
            decoded_bytes: 16
        }
    );
}

#[test]
fn audio_frame_channel_rate_packet_and_decoded_byte_budgets_are_enforced() {
    for limits in [
        MediaLimits {
            max_audio_frames: 3,
            ..MediaLimits::default()
        },
        MediaLimits {
            max_audio_channels: 0,
            ..MediaLimits::default()
        },
        MediaLimits {
            max_audio_sample_rate: 7999,
            ..MediaLimits::default()
        },
        MediaLimits {
            max_audio_packets: 0,
            ..MediaLimits::default()
        },
        MediaLimits {
            max_decoded_bytes: 15,
            ..MediaLimits::default()
        },
    ] {
        assert!(
            validate_media(&wav(4), MediaFormat::Wav, limits)
                .unwrap_err()
                .reason
                .contains("budget")
        );
    }
}

#[test]
fn malformed_or_truncated_audio_is_rejected_after_container_signature() {
    let b = wav(4);
    assert!(validate_media(&b[..b.len() - 2], MediaFormat::Wav, MediaLimits::default()).is_err());
    let mut invalid = b;
    invalid[22..24].copy_from_slice(&0_u16.to_le_bytes());
    assert!(validate_media(&invalid, MediaFormat::Wav, MediaLimits::default()).is_err());
    for (format, b) in [
        (MediaFormat::OggVorbis, b"OggS\0malformed".as_slice()),
        (MediaFormat::Flac, b"fLaCmalformed".as_slice()),
    ] {
        assert!(validate_media(b, format, MediaLimits::default()).is_err());
    }
}

#[test]
fn declared_format_cannot_be_substituted_and_input_budgets_precede_decoding() {
    let b = png();
    assert!(validate_media(&b, MediaFormat::Jpeg, MediaLimits::default()).is_err());
    assert!(
        validate_media(
            &b,
            MediaFormat::Png,
            MediaLimits {
                max_input_bytes: 1,
                ..MediaLimits::default()
            }
        )
        .unwrap_err()
        .reason
        .contains("budget")
    );
    assert!(validate_media(&[], MediaFormat::Png, MediaLimits::default()).is_err());
}

#[test]
fn glb_structure_and_buffer_accessors_are_verified_without_claiming_a_render() {
    assert_eq!(
        validate_media(&triangle(), MediaFormat::Glb, MediaLimits::default()).unwrap(),
        MediaInfo::Glb {
            meshes: 1,
            primitives: 1,
            vertices: 3,
            indices: 0,
            has_geometry: true
        }
    );
    let empty = glb(br#"{"asset":{"version":"2.0"}}"#, None);
    assert_eq!(
        validate_media(&empty, MediaFormat::Glb, MediaLimits::default()).unwrap(),
        MediaInfo::Glb {
            meshes: 0,
            primitives: 0,
            vertices: 0,
            indices: 0,
            has_geometry: false
        }
    );
}

#[test]
fn glb_rejects_missing_out_of_bounds_and_nonfinite_vertex_data() {
    let mut b = triangle();
    let offset = b.windows(4).position(|bytes| bytes == b"BIN\0").unwrap() + 4;
    b[offset..offset + 4].copy_from_slice(&f32::NAN.to_le_bytes());
    assert!(validate_media(&b, MediaFormat::Glb, MediaLimits::default()).is_err());
    let outside=glb(br#"{"asset":{"version":"2.0"},"buffers":[{"byteLength":36}],"bufferViews":[{"buffer":0,"byteOffset":0,"byteLength":48}],"accessors":[{"bufferView":0,"componentType":5126,"count":3,"type":"VEC3","min":[0,0,0],"max":[1,1,0]}],"meshes":[{"primitives":[{"attributes":{"POSITION":0}}]}]}"#,Some(&[0;36]));
    assert!(validate_media(&outside, MediaFormat::Glb, MediaLimits::default()).is_err());
}

#[test]
fn glb_mesh_budgets_precede_vertex_iteration() {
    for limits in [
        MediaLimits {
            max_mesh_vertices: 2,
            ..MediaLimits::default()
        },
        MediaLimits {
            max_mesh_primitives: 0,
            ..MediaLimits::default()
        },
    ] {
        assert!(
            validate_media(&triangle(), MediaFormat::Glb, limits)
                .unwrap_err()
                .reason
                .contains("budget")
        );
    }
}
