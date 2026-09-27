//! Explicit transient optical comparison; never writes an authored document.
use crate::{Options, RuntimeDocument};
use forge_project::WaterRefractionProfile;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub(super) enum Isolation {
    NormalsOff,
    RefractionOff,
}

impl Isolation {
    pub(super) const fn label(self) -> &'static str {
        match self {
            Self::NormalsOff => "normals-off",
            Self::RefractionOff => "refraction-off",
        }
    }
}

pub(super) fn select(
    destination: &mut Option<Isolation>,
    arguments: &mut impl Iterator<Item = String>,
) -> Result<(), String> {
    if destination.is_some() {
        return Err("duplicate --water-optics-isolation".into());
    }
    *destination = Some(match arguments.next().as_deref() {
        Some("normals-off") => Isolation::NormalsOff,
        Some("refraction-off") => Isolation::RefractionOff,
        _ => return Err("--water-optics-isolation requires normals-off or refraction-off".into()),
    });
    Ok(())
}

pub(super) fn validate(options: &Options) -> Result<(), String> {
    let recorded_bridge = options.bridge_crossing_probe && options.bridge_frames.is_some();
    if options.water_reflection_preview
        && (!options.water_depth_preview
            || !options.world_sketch_visual_valley
            || (options.exit_after_frames.is_none()
                && !recorded_bridge
                && options.global_cycle_proof.is_none())
            || options.water_optics_isolation.is_some())
    {
        return Err("--water-reflection-preview requires the optical WorldSketch candidate, bounded exit and no isolation".into());
    }
    if options.water_optics_isolation.is_some()
        && (!options.water_depth_preview
            || !options.world_sketch_visual_valley
            || options.fixed_proof_view.is_none()
            || options.capture_path.is_none()
            || options.exit_after_frames.is_none())
    {
        return Err("--water-optics-isolation requires the optical WorldSketch candidate, fixed view, capture and bounded exit".into());
    }
    Ok(())
}

pub(super) fn apply(mode: Isolation, document: &mut RuntimeDocument) -> Result<(), String> {
    let RuntimeDocument::VisualValley {
        water: Some(water), ..
    } = document
    else {
        return Err("water optics isolation requires a loaded water document".into());
    };
    match mode {
        Isolation::NormalsOff => water.surface.normal_strength = 0.0,
        Isolation::RefractionOff => water.optics.refraction = WaterRefractionProfile::Disabled,
    }
    water
        .validate()
        .map_err(|error| format!("invalid isolated water: {error}"))
}

#[cfg(test)]
mod tests {
    use super::*;

    fn args(mode: &str) -> Vec<String> {
        [
            "--world-sketch-visual-valley",
            "--water-depth-preview",
            "--fixed-proof-view",
            "human_bridge",
            "--capture",
            "unused.png",
            "--exit-after-frames",
            "420",
            "--water-optics-isolation",
            mode,
        ]
        .map(str::to_owned)
        .to_vec()
    }

    #[test]
    fn reflection_preview_requires_optical_world_and_bounded_exit() {
        let accepted = [
            "--world-sketch-visual-valley",
            "--water-depth-preview",
            "--water-reflection-preview",
            "--exit-after-frames",
            "420",
        ];
        assert!(Options::parse(accepted.map(str::to_owned).into_iter()).is_ok());
        for removed in ["--water-depth-preview", "--world-sketch-visual-valley"] {
            assert!(
                Options::parse(
                    accepted
                        .into_iter()
                        .filter(|arg| *arg != removed)
                        .map(str::to_owned)
                )
                .is_err()
            );
        }
        assert!(Options::parse(accepted[..3].iter().map(|arg| (*arg).to_owned())).is_err());
        let mut isolated = args("normals-off");
        isolated.push("--water-reflection-preview".into());
        assert!(Options::parse(isolated.into_iter()).is_err());
    }

    #[test]
    fn strict_cli_requires_bounded_candidate_and_unique_mode() {
        for mode in ["normals-off", "refraction-off"] {
            assert!(Options::parse(args(mode).into_iter()).is_ok());
        }
        assert!(Options::parse(args("unknown").into_iter()).is_err());
        let mut missing = args("normals-off");
        missing.pop();
        assert!(Options::parse(missing.into_iter()).is_err());
        let mut duplicate = args("normals-off");
        duplicate.extend(["--water-optics-isolation".into(), "refraction-off".into()]);
        assert!(Options::parse(duplicate.into_iter()).is_err());
        let mut unbounded = args("normals-off");
        unbounded.drain(6..8);
        assert!(Options::parse(unbounded.into_iter()).is_err());
        let mut not_candidate = args("normals-off");
        not_candidate.remove(1);
        assert!(Options::parse(not_candidate.into_iter()).is_err());
    }

    #[test]
    fn isolation_changes_only_the_selected_loaded_field() {
        for mode in [Isolation::NormalsOff, Isolation::RefractionOff] {
            let options = Options::parse(args(mode.label()).into_iter()).unwrap();
            let baseline = options.load_document_source().unwrap();
            let isolated = options.load_document().unwrap();
            let RuntimeDocument::VisualValley {
                water: Some(original),
                ..
            } = baseline
            else {
                panic!()
            };
            let RuntimeDocument::VisualValley {
                water: Some(mut actual),
                ..
            } = isolated
            else {
                panic!()
            };
            match mode {
                Isolation::NormalsOff => {
                    assert_eq!(actual.surface.normal_strength.to_bits(), 0.0_f32.to_bits());
                    actual.surface.normal_strength = original.surface.normal_strength;
                }
                Isolation::RefractionOff => {
                    assert_eq!(actual.optics.refraction, WaterRefractionProfile::Disabled);
                    actual.optics.refraction = original.optics.refraction;
                }
            }
            assert_eq!(actual, original);
        }
    }
}
