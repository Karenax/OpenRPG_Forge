#![cfg(not(target_arch = "wasm32"))]
use forge_assets::media_validation::{MediaFormat, MediaLimits, validate_media};

#[test]
fn floor0_profile_is_refused_before_a_hostile_packet_reaches_its_unchecked_book_lookup() {
    let bytes = include_bytes!("fixtures/media/floor0_invalid_book.ogg");
    let result = std::panic::catch_unwind(|| {
        validate_media(bytes, MediaFormat::OggVorbis, MediaLimits::default())
    });
    assert!(
        result.is_ok(),
        "user audio reached an unchecked Floor0 book lookup"
    );
    let error = result.unwrap().unwrap_err();
    assert!(error.reason.contains("floor profile"), "{error}");
}
