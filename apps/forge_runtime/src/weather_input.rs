//! Read-only candidate weather input. Identity is checked against the selected terrain by the caller.

use std::{fs::File, io::Read, path::Path};

use forge_core::Revision;
use forge_project::WeatherProfileDocument;

const MAX_BYTES: u64 = 64 * 1024;

pub(super) fn load(
    path: Option<&Path>,
    embedded_revision: Option<Revision>,
) -> Result<WeatherProfileDocument, String> {
    let Some(path) = path else {
        let mut document = forge_world::embedded_visual_valley_weather_profile()
            .map_err(|error| format!("invalid embedded Visual Valley weather: {error}"))?;
        if let Some(revision) = embedded_revision {
            document.revision = revision;
        }
        return Ok(document);
    };
    let file = File::open(path)
        .map_err(|error| format!("cannot open weather {}: {error}", path.display()))?;
    let metadata = file
        .metadata()
        .map_err(|error| format!("cannot inspect weather {}: {error}", path.display()))?;
    if !metadata.is_file() || metadata.len() > MAX_BYTES {
        return Err(format!(
            "weather {} must be a regular file of at most {MAX_BYTES} bytes",
            path.display()
        ));
    }
    // Bound the read itself too: the file can grow after metadata was inspected.
    let mut bytes = Vec::new();
    file.take(MAX_BYTES + 1)
        .read_to_end(&mut bytes)
        .map_err(|error| format!("cannot read weather {}: {error}", path.display()))?;
    if bytes.len() as u64 > MAX_BYTES {
        return Err(format!(
            "weather {} exceeds {MAX_BYTES} bytes",
            path.display()
        ));
    }
    let json = std::str::from_utf8(&bytes)
        .map_err(|error| format!("invalid weather UTF-8 {}: {error}", path.display()))?;
    // Never rebind an external candidate to the selected scene's revision.
    WeatherProfileDocument::from_json(json)
        .map_err(|error| format!("invalid weather {}: {error}", path.display()))
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{Options, RuntimeDocument};
    use std::fs;

    #[test]
    fn weather_input_cli_is_explicit_unique_and_valley_only() {
        for arguments in [
            vec!["--weather-input", "candidate.json"],
            vec!["--visual-valley", "--weather-input"],
            vec![
                "--visual-valley",
                "--weather-input",
                "a",
                "--weather-input",
                "b",
            ],
            vec!["--terrain", "terrain.json", "--weather-input", "a"],
        ] {
            assert!(Options::parse(arguments.into_iter().map(str::to_owned)).is_err());
        }
        for scene in ["--visual-valley", "--world-sketch-visual-valley"] {
            assert!(
                Options::parse(
                    [
                        scene,
                        "--weather-input",
                        "candidate.json",
                        "--weather",
                        "rain"
                    ]
                    .into_iter()
                    .map(str::to_owned)
                )
                .is_ok()
            );
        }
    }

    #[test]
    fn weather_input_is_bounded_strict_and_never_rebound_or_written() {
        let root = std::env::temp_dir().join(format!("forge-weather-input-{}", std::process::id()));
        fs::create_dir_all(&root).unwrap();
        let path = root.join("candidate.json");
        let mut candidate = forge_world::embedded_visual_valley_weather_profile().unwrap();
        candidate.sunlight = Some(forge_project::WeatherSunlightProfile {
            rain_illuminance_multiplier: 0.25,
            transition_seconds: 4.0,
        });
        let options = Options::parse(
            [
                "--visual-valley".to_owned(),
                "--weather-input".to_owned(),
                path.display().to_string(),
            ]
            .into_iter(),
        )
        .unwrap();
        let original = candidate.to_pretty_json().unwrap();
        fs::write(&path, &original).unwrap();
        let RuntimeDocument::VisualValley {
            weather: Some(actual),
            ..
        } = options.load_document().unwrap()
        else {
            panic!("expected candidate weather");
        };
        assert_eq!(*actual, candidate);
        let sketch = forge_world::embedded_visual_valley_world_sketch().unwrap();
        let world_options = Options {
            visual_valley: false,
            world_sketch_visual_valley: true,
            ..options
        };
        assert!(
            world_options
                .load_document()
                .unwrap_err()
                .contains("weather")
        );
        assert_eq!(fs::read_to_string(&path).unwrap(), original);
        candidate.revision = sketch.revision;
        fs::write(&path, candidate.to_pretty_json().unwrap()).unwrap();
        let RuntimeDocument::VisualValley {
            weather: Some(actual),
            ..
        } = world_options.load_document().unwrap()
        else {
            panic!("expected candidate weather");
        };
        assert_eq!(*actual, candidate);
        candidate.area_id = forge_core::AreaId::deterministic("wrong-weather-area");
        fs::write(&path, candidate.to_pretty_json().unwrap()).unwrap();
        assert!(
            world_options
                .load_document()
                .unwrap_err()
                .contains("weather")
        );
        for invalid in [
            b"{\"unknown\":true}".to_vec(),
            vec![0xff],
            vec![b' '; usize::try_from(MAX_BYTES).unwrap() + 1],
        ] {
            fs::write(&path, invalid).unwrap();
            assert!(load(Some(&path), None).is_err());
        }
        fs::remove_file(&path).unwrap();
        assert!(load(Some(&path), None).is_err());
        assert!(load(Some(&root), None).is_err());
        fs::remove_dir(root).unwrap();
    }
}
