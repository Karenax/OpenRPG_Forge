//! Generic native module arguments; no workspace or Vault fallback.
use forge_core::{AreaId, ContentHash};
use forge_gameplay::native_storage::StartIntent;
use forge_project::VisualQualityTier;
use forge_project::workspace::launcher::DisplayMode;
use std::{
    collections::BTreeSet,
    ffi::{OsStr, OsString},
    path::PathBuf,
};

#[derive(Debug, PartialEq, Eq)]
pub struct ModuleGameOptions {
    pub package: PathBuf,
    pub area_id: AreaId,
    pub cache_root: PathBuf,
    pub save_root: PathBuf,
    pub capture: Option<PathBuf>,
    pub exit_after_frames: Option<u32>,
    pub expected_zone: Option<ExpectedZonePins>,
    pub expected_package_manifest: Option<ContentHash>,
    pub start_intent: StartIntent,
    pub default_quality: Option<VisualQualityTier>,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct ExpectedZonePins {
    pub head_hash: ContentHash,
    pub archive_sha256: ContentHash,
}

/// Called after immutable package admission and before constructing a game scene.
pub fn verify_expected_zone(
    expected: Option<ExpectedZonePins>,
    actual_head: ContentHash,
    archive: &[u8],
) -> Result<(), String> {
    let Some(expected) = expected else {
        return Ok(());
    };
    if actual_head != expected.head_hash {
        return Err("Admitted module head differs from the reviewed Studio zone".into());
    }
    if forge_core::bytes_sha256(archive) != expected.archive_sha256 {
        return Err("Admitted module archive differs from the reviewed Studio archive".into());
    }
    Ok(())
}

impl ModuleGameOptions {
    pub fn parse(arguments: impl IntoIterator<Item = OsString>) -> Result<Self, String> {
        let mut arguments = arguments.into_iter();
        let (mut package, mut area_id, mut cache_root, mut save_root) = (None, None, None, None);
        let (mut capture, mut exit_after_frames) = (None, None);
        let (mut expected_head, mut expected_archive) = (None, None);
        let mut expected_package_manifest = None;
        let mut start_intent = StartIntent::Legacy;
        let mut default_quality = None;
        let mut seen = BTreeSet::new();
        while let Some(flag) = arguments.next() {
            let flag = flag.to_str().ok_or("Module option names must be Unicode")?;
            if !seen.insert(flag.to_owned()) {
                return Err(format!("Duplicate module option: {flag}"));
            }
            let value = arguments
                .next()
                .ok_or_else(|| format!("Missing value for {flag}"))?;
            match flag {
                "--module" => package = Some(PathBuf::from(value)),
                "--zone" => {
                    area_id = Some(
                        value
                            .to_str()
                            .ok_or("Invalid zone ID")?
                            .parse::<AreaId>()
                            .map_err(|error| error.to_string())?,
                    );
                }
                "--cache" => cache_root = Some(PathBuf::from(value)),
                "--save-root" => save_root = Some(PathBuf::from(value)),
                "--capture" => capture = Some(PathBuf::from(value)),
                "--expected-head" => {
                    expected_head = Some(parse_hash(&value, "expected head")?);
                }
                "--expected-archive-sha256" => {
                    expected_archive = Some(parse_hash(&value, "expected archive SHA-256")?);
                }
                "--expected-package-manifest-sha256" => {
                    expected_package_manifest =
                        Some(parse_hash(&value, "package manifest SHA-256")?);
                }
                "--start-intent" => start_intent = parse_start_intent(&value)?,
                "--default-quality" => default_quality = Some(parse_quality(&value)?),
                "--frames" => {
                    let frames = value
                        .to_str()
                        .ok_or("Invalid module frame limit")?
                        .parse::<u32>()
                        .map_err(|error| error.to_string())?;
                    if !(1..=60_000).contains(&frames) {
                        return Err("Module frame limit must be between 1 and 60000".into());
                    }
                    exit_after_frames = Some(frames);
                }
                _ => return Err(format!("Unknown module option: {flag}")),
            }
        }
        let options = Self {
            package: package.ok_or("--module is required")?,
            area_id: area_id.ok_or("--zone is required")?,
            cache_root: cache_root.ok_or("--cache is required")?,
            save_root: save_root.ok_or("--save-root is required")?,
            capture,
            exit_after_frames,
            expected_zone: expected_zone(expected_head, expected_archive)?,
            expected_package_manifest,
            start_intent,
            default_quality,
        };
        for path in [&options.cache_root, &options.save_root]
            .into_iter()
            .chain(options.capture.iter())
        {
            if !path.is_absolute()
                || path.components().any(|part| {
                    matches!(
                        part,
                        std::path::Component::CurDir | std::path::Component::ParentDir
                    )
                })
            {
                return Err(
                    "Module cache, save and capture paths must be absolute and normalized".into(),
                );
            }
        }
        if options.capture.is_some() && options.exit_after_frames.is_none() {
            return Err("--capture requires an explicit --frames limit".into());
        }
        if options.expected_package_manifest.is_some() && options.expected_zone.is_none() {
            return Err("A package manifest pin requires the reviewed zone pin pair".into());
        }
        Ok(options)
    }
}

fn expected_zone(
    head: Option<ContentHash>,
    archive: Option<ContentHash>,
) -> Result<Option<ExpectedZonePins>, String> {
    match (head, archive) {
        (None, None) => Ok(None),
        (Some(head_hash), Some(archive_sha256)) => Ok(Some(ExpectedZonePins {
            head_hash,
            archive_sha256,
        })),
        _ => Err("--expected-head and --expected-archive-sha256 must be supplied together".into()),
    }
}

fn parse_start_intent(value: &OsString) -> Result<StartIntent, String> {
    let value = value.to_str().ok_or("Invalid game start intent")?;
    if value == "fresh" {
        return Ok(StartIntent::Fresh);
    }
    if let Some(hash) = value.strip_prefix("resume:") {
        return hash
            .parse::<ContentHash>()
            .map(StartIntent::Resume)
            .map_err(|error| format!("Invalid resume hash: {error}"));
    }
    Err("Invalid game start intent".into())
}

fn parse_quality(value: &OsString) -> Result<VisualQualityTier, String> {
    let tier = value.to_str().ok_or("Invalid default graphics quality")?;
    serde_json::from_value(serde_json::Value::String(tier.to_owned()))
        .map_err(|error| format!("Invalid default graphics quality: {error}"))
}

/// An absent default keeps legacy launches unchanged; malformed values are refused.
pub fn parse_default_display_mode(value: Option<&OsStr>) -> Result<Option<DisplayMode>, String> {
    let Some(value) = value else {
        return Ok(None);
    };
    let mode = value.to_str().ok_or("Invalid default display mode")?;
    serde_json::from_value(serde_json::Value::String(mode.to_owned()))
        .map(Some)
        .map_err(|error| format!("Invalid default display mode: {error}"))
}

fn parse_hash(value: &OsString, label: &str) -> Result<ContentHash, String> {
    value
        .to_str()
        .ok_or_else(|| format!("Invalid {label}"))?
        .parse::<ContentHash>()
        .map_err(|error| format!("Invalid {label}: {error}"))
}
