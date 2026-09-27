//! Bounded, read-only optical profile for candidate captures and live walks. No map mutation.
use crate::{Options, RuntimeDocument};
use forge_project::WaterMediumProfile;
use std::{fs::File, io::Read, path::Path};

pub(super) fn validate(options: &Options) -> Result<(), String> {
    let fixed_capture = options.fixed_proof_view.is_some() && options.capture_path.is_some();
    let live_walk = options.walk_proof.is_some()
        && ((options.walk_proof_measure && !options.walk_proof_measure_frozen)
            || options.capture_path.is_some());
    let recorded_bridge = options.bridge_crossing_probe && options.bridge_frames.is_some();
    let profile_cycle = options.global_cycle_proof.is_some();
    if options.water_medium_input.is_some()
        && (!options.water_depth_preview
            || !options.world_sketch_visual_valley
            || !(fixed_capture
                || live_walk
                || recorded_bridge
                || profile_cycle
                || options.adaptive_quality)
            || (options.exit_after_frames.is_none() && !recorded_bridge && !profile_cycle)
            || options.water_optics_isolation.is_some())
    {
        return Err("--water-medium-input requires a bounded optical WorldSketch fixed capture, recorded bridge or live walk capture/measurement, without isolation".into());
    }
    Ok(())
}

fn load(path: &Path) -> Result<WaterMediumProfile, String> {
    let file = File::open(path)
        .map_err(|e| format!("cannot open water medium {}: {e}", path.display()))?;
    let metadata = file
        .metadata()
        .map_err(|e| format!("cannot inspect water medium: {e}"))?;
    if !metadata.is_file() || metadata.len() > 1024 {
        return Err("water medium must be a regular file of at most 1024 bytes".into());
    }
    let mut bytes = Vec::new();
    file.take(1025)
        .read_to_end(&mut bytes)
        .map_err(|e| format!("cannot read water medium: {e}"))?;
    if bytes.len() > 1024 {
        return Err("water medium exceeds 1024 bytes".into());
    }
    let medium: WaterMediumProfile =
        serde_json::from_slice(&bytes).map_err(|e| format!("invalid water medium: {e}"))?;
    medium
        .validate()
        .map_err(|e| format!("invalid water medium: {e}"))?;
    Ok(medium)
}

pub(super) fn apply(path: &Path, document: &mut RuntimeDocument) -> Result<(), String> {
    let medium = load(path)?;
    let RuntimeDocument::VisualValley {
        water: Some(water), ..
    } = document
    else {
        return Err("water medium requires a loaded water document".into());
    };
    water.medium = Some(medium);
    water
        .validate()
        .map_err(|e| format!("invalid candidate water: {e}"))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn optical_profile_cycle_is_self_bounded_and_adaptation_requires_bounded_exit() {
        let base = [
            "--world-sketch-visual-valley",
            "--quality",
            "high",
            "--water-depth-preview",
            "--water-medium-input",
            "medium.json",
            "--water-reflection-preview",
        ];
        let parse = |extra: &[&str]| {
            Options::parse(
                base.into_iter()
                    .chain(extra.iter().copied())
                    .map(str::to_owned),
            )
        };
        assert!(parse(&["--quality-cycle-proof", "fresh"]).is_ok());
        assert!(parse(&["--adaptive-quality", "--exit-after-frames", "6000"]).is_ok());
        assert!(parse(&["--adaptive-quality"]).is_err());
        assert!(parse(&["--quality-cycle-proof", "fresh", "--adaptive-quality"]).is_err());
        assert!(
            parse(&[
                "--quality-cycle-proof",
                "fresh",
                "--water-optics-isolation",
                "normals-off"
            ])
            .is_err()
        );
    }

    #[test]
    fn medium_recorded_bridge_requires_capture_and_bounded_exit() {
        let base = [
            "--world-sketch-visual-valley",
            "--water-depth-preview",
            "--bridge-crossing-probe",
            "--bridge-frames",
            "frames",
            "--water-medium-input",
            "medium.json",
            "--water-reflection-preview",
        ];
        let args: Vec<String> = base.into_iter().map(str::to_owned).collect();
        assert!(Options::parse(args.clone().into_iter()).is_ok());
        for range in [1..2, 2..3, 3..5] {
            let mut invalid = args.clone();
            invalid.drain(range);
            assert!(Options::parse(invalid.into_iter()).is_err());
        }
        let mut isolation = args;
        isolation.extend(["--water-optics-isolation".into(), "normals-off".into()]);
        assert!(Options::parse(isolation.into_iter()).is_err());
    }

    #[test]
    fn medium_live_walk_preserves_measurement_exclusions_and_bounded_exit() {
        let base = [
            "--world-sketch-visual-valley",
            "--water-depth-preview",
            "--player-camera",
            "--walk-proof",
            "300:1:0.22",
            "--walk-proof-measure-moving",
            "--exit-after-frames",
            "5000",
            "--water-medium-input",
            "medium.json",
        ];
        let parse = |args: Vec<String>| Options::parse(args.into_iter());
        let args: Vec<String> = base.into_iter().map(str::to_owned).collect();
        assert!(parse(args.clone()).is_ok());
        for accepted in ["--walk-proof-measure", "--walk-proof-measure-moving"] {
            let mut changed = args.clone();
            changed[5] = accepted.into();
            assert!(parse(changed.clone()).is_ok());
            for extra in [
                vec!["--capture", "unused.png"],
                vec!["--metrics", "unused.json"],
                vec!["--water-optics-isolation", "normals-off"],
                vec!["--adaptive-quality"],
            ] {
                let mut invalid = changed.clone();
                invalid.extend(extra.into_iter().map(str::to_owned));
                assert!(parse(invalid).is_err());
            }
            changed.drain(6..8);
            assert!(parse(changed).is_err());
        }
        let mut frozen = args.clone();
        frozen[5] = "--walk-proof-measure-frozen".into();
        assert!(parse(frozen).is_err());
        for extra in [
            vec!["--capture", "unused.png"],
            vec!["--metrics", "unused.json"],
            vec!["--water-optics-isolation", "normals-off"],
            vec!["--adaptive-quality"],
        ] {
            let mut changed = args.clone();
            changed.extend(extra.into_iter().map(str::to_owned));
            assert!(parse(changed).is_err());
        }
        let mut unbounded = args.clone();
        unbounded.drain(6..8);
        assert!(parse(unbounded).is_err());
        let mut capture = args;
        capture.remove(5);
        capture.extend(["--capture".into(), "unused.png".into()]);
        assert!(parse(capture).is_ok());
    }
    #[test]
    fn medium_cli_requires_unique_bounded_capture() {
        let base = [
            "--world-sketch-visual-valley",
            "--water-depth-preview",
            "--fixed-proof-view",
            "human_bridge",
            "--capture",
            "unused.png",
            "--exit-after-frames",
            "420",
            "--water-medium-input",
            "medium.json",
        ];
        assert!(Options::parse(base.into_iter().map(str::to_owned)).is_ok());
        assert!(Options::parse(base[..9].iter().map(|v| (*v).to_owned())).is_err());
        let mut duplicate: Vec<_> = base.into_iter().map(str::to_owned).collect();
        duplicate.extend(["--water-medium-input".into(), "other.json".into()]);
        assert!(Options::parse(duplicate.into_iter()).is_err());
        for index in [1, 2, 4, 6] {
            let mut invalid: Vec<_> = base.into_iter().map(str::to_owned).collect();
            invalid.drain(index..index + if index == 1 { 1 } else { 2 });
            assert!(Options::parse(invalid.into_iter()).is_err());
        }
    }

    #[test]
    fn medium_input_is_strict_read_only_and_does_not_edit_other_water_fields() {
        let root = std::env::temp_dir().join(format!("forge-water-medium-{}", std::process::id()));
        std::fs::create_dir_all(&root).unwrap();
        let path = root.join("medium.json");
        let json = r#"{"absorption_per_m":[0.8,0.2,0.1],"scattering_per_m":[0.1,0.1,0.1]}"#;
        std::fs::write(&path, json).unwrap();
        let options = Options::parse(["--visual-valley".to_owned()].into_iter()).unwrap();
        let mut document = options.load_document().unwrap();
        apply(&path, &mut document).unwrap();
        let RuntimeDocument::VisualValley {
            water: Some(water), ..
        } = document
        else {
            panic!("missing water")
        };
        let mut restored = *water;
        assert_eq!(restored.medium, Some(load(&path).unwrap()));
        restored.medium = None;
        assert_eq!(
            restored,
            forge_world::embedded_visual_valley_water_document().unwrap()
        );
        assert_eq!(std::fs::read_to_string(&path).unwrap(), json);
        for invalid in [
            vec![b' '; 1025],
            vec![0xff],
            b"{}".to_vec(),
            json.replace("0.8", "-0.8").into_bytes(),
            json.replace("absorption_per_m", "unknown").into_bytes(),
        ] {
            std::fs::write(&path, invalid).unwrap();
            assert!(load(&path).is_err());
        }
        std::fs::remove_file(&path).unwrap();
        assert!(load(&path).is_err());
        assert!(load(&root).is_err());
        std::fs::remove_dir(&root).unwrap();
    }
}
