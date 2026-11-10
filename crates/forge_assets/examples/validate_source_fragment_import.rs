//! Validate a source-fragment carrier before private asset import.
//! This does not qualify its visual appearance, placement or runtime.
use forge_assets::media_validation::{MediaFormat, MediaLimits, validate_media};
use std::{env, fs, process};

fn main() {
    let Some(path) = env::args_os().nth(1) else {
        eprintln!("usage: validate_source_fragment_import MODEL.glb");
        process::exit(2);
    };
    let result = fs::read(path)
        .map_err(|error| error.to_string())
        .and_then(|bytes| {
            let limits = MediaLimits {
                max_decoded_bytes: 1024 * 1024 * 1024,
                ..MediaLimits::default()
            };
            validate_media(&bytes, MediaFormat::Glb, limits).map_err(|error| error.to_string())
        });
    match result {
        Ok(info) => println!("{info:?}"),
        Err(error) => {
            eprintln!("{error}");
            process::exit(1);
        }
    }
}
